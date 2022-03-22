#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using librsync.net;
using UnityEngine;
using VRC.Core;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

using static VRC.Core.ApiFileHelper;
using Tools = VRC.Tools;

namespace BocuD.VRChatApiTools
{
    using static Constants;
    
    public class ApiFileHelperAsync
    {
        //status strings
        private const string prepareFileMessage = "Preparing file for upload...";
        private const string prepareRemoteMessage = "Preparing server for upload...";
        private const string postUploadMessage = "Processing upload...";
        
        //global flow control
        private ApiFile apiFile;
        private static string errorStr;

        public delegate void UploadStatus(string status, string subStatus);
        public delegate void UploadProgress(long done, long total);

        public async Task<string> UploadFile(string filepath, string existingFileId, string fileType, string friendlyName, UploadStatus onStatus, UploadProgress onProgress, Func<bool> cancelQuery)
        {
            //Init remote config
            await InitRemoteConfig();
            
            bool deltaCompression = ConfigManager.RemoteConfig.GetBool("sdkEnableDeltaCompression");

            //Check filename
            CheckFile(filepath);

            //Fetch Api File Record
            apiFile = await FetchRecord(filepath, existingFileId, friendlyName, onStatus, cancelQuery);
            if (apiFile == null)
                throw new Exception("Fetching or creating record failed");

            Logger.Log($"Fetched record succesfully: {apiFile.name}");

            if (apiFile.HasQueuedOperation(deltaCompression))
            {
                //delete last version
                onStatus?.Invoke(prepareRemoteMessage, "Cleaning up previous version");

                await ApiFileAsyncExtensions.DeleteLatestVersion(apiFile);
            }

            // check for server side errors from last upload
            await HandleFileErrorState(onStatus);

            // verify previous file op is complete
            if (apiFile.HasQueuedOperation(deltaCompression))
                throw new Exception("Can't initiate upload: A previous upload is still being processed. Please try again later.");

            //gemerate file md5
            string fileMD5 = await GenerateMD5Base64(filepath, onStatus);
            
            // check if file has been changed
            bool isPreviousUploadRetry = await CheckForExistingVersion(onStatus, fileMD5);
            
            //generate file signature
            string signatureFilename = await GenerateSignatureFile(filepath, onStatus);

            //generate signature md5 and file size
            string signatureMD5 = await GenerateMD5Base64(signatureFilename, onStatus);
            
            if (!Tools.GetFileSize(signatureFilename, out long sigFileSize, out errorStr))
            {
                CleanupTempFiles(apiFile.id);
                throw new Exception($"Failed to generate file signature: Couldn't get filesize", new Exception(errorStr));
            }
            
            // download previous version signature (if exists)
            string existingFileSignaturePath = "";
            if (deltaCompression && apiFile.HasExistingVersion())
            {
                existingFileSignaturePath = await GetExistingFileSignature(onStatus, onProgress, cancelQuery);
            }
            
            // create delta if needed
            string deltaFilepath = "";

            if (deltaCompression && !string.IsNullOrEmpty(existingFileSignaturePath))
            {
                deltaFilepath = await CreateFileDelta(filepath, onStatus, existingFileSignaturePath);
            }

            // upload smaller of delta and new file
            long deltaFileSize = 0;
            
            //get filesize
            if (!Tools.GetFileSize(filepath, out long fullFileSize, out errorStr) ||
                !string.IsNullOrEmpty(deltaFilepath) && !Tools.GetFileSize(deltaFilepath, out deltaFileSize, out errorStr))
            {
                CleanupTempFiles(apiFile.id);
                throw new Exception("Failed to create file delta for upload", new Exception(errorStr));
            }

            bool uploadDeltaFile = deltaCompression && deltaFileSize > 0 && deltaFileSize < fullFileSize;
            Logger.Log(deltaCompression
                    ? $"Delta size {deltaFileSize} ({(deltaFileSize / (float)fullFileSize)} %), full file size {fullFileSize}, uploading {(uploadDeltaFile ? " DELTA" : " FULL FILE")}"
                    : $"Delta compression disabled, uploading FULL FILE, size {fullFileSize}");
            
            //generate MD5 for delta file
            string deltaMD5 = "";
            if (uploadDeltaFile)
            {
                deltaMD5 = await GenerateMD5Base64(deltaFilepath, onStatus);
            }

            bool versionAlreadyExists = false;
            
            // validate existing pending version info, if we're retrying an older upload
            if (isPreviousUploadRetry)
            {
                bool isValid;

                ApiFile.Version version = apiFile.GetVersion(apiFile.GetLatestVersionNumber());
                if (version == null)
                {
                    isValid = false;
                }
                else
                {
                    //make sure fileSize for file and signature need to match their respective remote versions
                    //make sure MD5 for file and signature match their respective remote versions
                    if (uploadDeltaFile)
                    {
                        isValid = deltaFileSize == version.delta.sizeInBytes &&
                                  string.Compare(deltaMD5, version.delta.md5, StringComparison.Ordinal) == 0 &&
                                  sigFileSize == version.signature.sizeInBytes &&
                                  string.Compare(signatureMD5, version.signature.md5, StringComparison.Ordinal) == 0;
                    }
                    else
                    {
                        isValid = fullFileSize == version.file.sizeInBytes &&
                                  string.Compare(fileMD5, version.file.md5, StringComparison.Ordinal) == 0 &&
                                  sigFileSize == version.signature.sizeInBytes &&
                                  string.Compare(signatureMD5, version.signature.md5, StringComparison.Ordinal) == 0;
                    }
                }

                if (isValid)
                {
                    versionAlreadyExists = true;
                    Logger.Log("Using existing version record");
                }
                else
                {
                    // delete previous invalid version
                    onStatus?.Invoke(prepareRemoteMessage, "Cleaning up previous version");
                    await ApiFileAsyncExtensions.DeleteLatestVersion(apiFile);
                }
            }
            
            //create new file record
            if (!versionAlreadyExists)
            {
                if (uploadDeltaFile)
                {
                    await CreateFileRecord(deltaMD5, deltaFileSize, signatureMD5, sigFileSize, onStatus, cancelQuery);
                }
                else
                {
                    await CreateFileRecord(fileMD5, fullFileSize, signatureMD5, sigFileSize, onStatus, cancelQuery);
                }
            }

            // upload components
            string uploadFilepath = uploadDeltaFile ? deltaFilepath : filepath;
            string uploadMD5 = uploadDeltaFile ? deltaMD5 : fileMD5;
            long uploadFileSize = uploadDeltaFile ? deltaFileSize : fullFileSize;
            
            Logger.Log($"ApiFile name: {apiFile.name}");

            switch (uploadDeltaFile)
            {
                case true when apiFile.GetLatestVersion().delta.status == ApiFile.Status.Waiting:
                case false when apiFile.GetLatestVersion().status == ApiFile.Status.Waiting:
                    onStatus?.Invoke(prepareFileMessage, $"Uploading file{(uploadDeltaFile ? " delta..." : "...")}");
                    onStatus?.Invoke($"Uploading {fileType}...", $"Uploading file{(uploadDeltaFile ? " delta..." : "...")}");

                    await UploadFileComponentInternal(apiFile, uploadDeltaFile ? ApiFile.Version.FileDescriptor.Type.delta : ApiFile.Version.FileDescriptor.Type.file, 
                        uploadFilepath, uploadMD5, uploadFileSize,
                        file =>
                        {
                            Logger.Log($"Successfully uploaded file{(uploadDeltaFile ? " delta" : "")}");
                            apiFile = file;
                        }, (done, total) => onProgress?.Invoke(done, total), cancelQuery);
                    break;
            }
            
            // upload signature
            if (apiFile.GetLatestVersion().signature.status == ApiFile.Status.Waiting)
            {
                onStatus?.Invoke(prepareFileMessage, $"Uploading file signature...");
                onStatus?.Invoke($"Uploading file signature...", "Uploading file signature...");

                await UploadFileComponentInternal(apiFile,
                    ApiFile.Version.FileDescriptor.Type.signature, signatureFilename, signatureMD5, sigFileSize,
                    file =>
                    {
                        Logger.Log("Successfully uploaded file signature.");
                        apiFile = file;
                    }, (done, total) => onProgress?.Invoke(done, total), cancelQuery);
            }
            
            ValidateRecord(fileType, onStatus, uploadDeltaFile);

            await CheckFileStatus(onStatus, cancelQuery, uploadDeltaFile);
            
            // cleanup and wait for it to finish
            await CleanupTempFilesInternal(apiFile.id);

            return apiFile.GetFileURL();
        }

        private void ValidateRecord(string fileType, UploadStatus onStatus, bool uploadDeltaFile)
        {
            // Validate file records queued or complete
            onStatus?.Invoke($"Uploading {fileType}...", "Validating upload...");

            bool isUploadComplete = uploadDeltaFile
                ? apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.delta)
                    .status == ApiFile.Status.Complete
                : apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.file)
                    .status == ApiFile.Status.Complete;
            
            isUploadComplete = isUploadComplete && apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(),
                ApiFile.Version.FileDescriptor.Type.signature).status == ApiFile.Status.Complete;

            if (!isUploadComplete)
            {
                CleanupTempFiles(apiFile.id);
                throw new Exception("Upload validation failed");
            }

            bool isServerOpQueuedOrComplete = uploadDeltaFile
                ? apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.file)
                    .status != ApiFile.Status.Waiting
                : apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), ApiFile.Version.FileDescriptor.Type.delta)
                    .status != ApiFile.Status.Waiting;

            if (!isServerOpQueuedOrComplete)
            {
                CleanupTempFiles(apiFile.id);
                throw new Exception("Failed to upload file", new Exception("Previous version is still in waiting status"));
            }
        }

        private async Task CheckFileStatus(UploadStatus onStatus, Func<bool> cancelQuery,
            bool uploadDeltaFile)
        {
            // wait for server processing to complete
            onStatus?.Invoke(postUploadMessage, "Checking file status");

            float checkDelay = SERVER_PROCESSING_INITIAL_RETRY_TIME;
            float timeout = GetServerProcessingWaitTimeoutForDataSize(apiFile.GetLatestVersion().file.sizeInBytes);
            double initialStartTime = Time.realtimeSinceStartup;
            double startTime = initialStartTime;

            while (apiFile.HasQueuedOperation(uploadDeltaFile))
            {
                // wait before polling again
                onStatus?.Invoke(postUploadMessage, $"Checking status in {Mathf.CeilToInt(checkDelay)} seconds");

                while (Time.realtimeSinceStartup - startTime < checkDelay)
                {
                    if (Time.realtimeSinceStartup - initialStartTime > timeout)
                    {                       
                        CleanupTempFiles(apiFile.id);
                        throw new TimeoutException("Couldn't verify upload: Timed out waiting for upload processing to complete");
                    }

                    await Task.Delay(33);
                }

                while (true)
                {
                    // check status
                    onStatus?.Invoke(postUploadMessage, "Checking status...");

                    bool wait = true;
                    errorStr = "";
                    API.Fetch<ApiFile>(apiFile.id, c =>
                    {
                        apiFile = c.Model as ApiFile;
                        wait = false;
                    }, c =>
                    {
                        CleanupTempFiles(apiFile.id);
                        throw new Exception(c.Error);
                    });

                    while (wait)
                    {
                        if (cancelQuery())
                        {
                            CleanupTempFiles(apiFile.id);
                            throw new OperationCanceledException();
                        }

                        await Task.Delay(33);
                    }

                    break;
                }

                checkDelay = Mathf.Min(checkDelay * 2, SERVER_PROCESSING_MAX_RETRY_TIME);
                startTime = Time.realtimeSinceStartup;
            }
        }

        private async Task<string> CreateFileDelta(string filename, UploadStatus onStatus, string existingFileSignaturePath)
        {
            onStatus?.Invoke(prepareFileMessage, "Creating file delta");
            
            string deltaFilename = Tools.GetTempFileName(".delta", out errorStr, apiFile.id);
            
            if (string.IsNullOrEmpty(deltaFilename))
            {
                CleanupTempFiles(apiFile.id);
                throw new Exception("Failed to create file delta for upload", new Exception($"Failed to create temp file: {errorStr}"));
            }
            
            await CreateFileDeltaInternal(filename, existingFileSignaturePath, deltaFilename);

            return deltaFilename;
        }

        private async Task CreateFileRecord(string fileMD5Base64, long fileSize, string sigMD5Base64,
            long sigFileSize, UploadStatus onStatus, Func<bool> cancelQuery)
        {
            while (true)
            {
                onStatus?.Invoke(prepareRemoteMessage, "Creating file version record...");

                bool wait = true;

                apiFile.CreateNewVersion(ApiFile.Version.FileType.Full, fileMD5Base64, fileSize,
                    sigMD5Base64, sigFileSize, c =>
                    {
                        apiFile = c.Model as ApiFile;
                        wait = false;
                    }, c =>
                    {
                        CleanupTempFiles(apiFile.id);
                        throw new Exception("Creating file version record failed", new Exception(c.Error));
                    });

                while (wait)
                {
                    if (cancelQuery())
                    {
                        CleanupTempFiles(apiFile.id);
                        throw new OperationCanceledException();
                    }

                    await Task.Delay(33);
                }

                // delay to let write get through servers
                await Task.Delay(postWriteDelay);

                break;
            }
        }

        private async Task<string> GetExistingFileSignature(UploadStatus onStatus, UploadProgress onProgress, Func<bool> cancelQuery)
        {
            string existingFileSignaturePath = "";
            onStatus?.Invoke(prepareRemoteMessage, "Downloading previous version signature");

            bool wait = true;
            errorStr = "";
            apiFile.DownloadSignature(
                data =>
                {
                    // save to temp file
                    existingFileSignaturePath = Tools.GetTempFileName(".sig", out errorStr, apiFile.id);
                    if (string.IsNullOrEmpty(existingFileSignaturePath))
                    {
                        throw new Exception($"Couldn't create temp file: {errorStr}");
                    }
                    
                    File.WriteAllBytes(existingFileSignaturePath, data);

                    wait = false;
                },
                error => throw new Exception(error),
                (downloaded, length) => onProgress?.Invoke(downloaded, length)
            );

            while (wait)
            {
                if (cancelQuery())
                {
                    throw new OperationCanceledException();
                }

                await Task.Delay(33);
            }

            if (string.IsNullOrEmpty(errorStr)) return existingFileSignaturePath;

            CleanupTempFiles(apiFile.id);
            throw new Exception($"Failed to download previous file version signature: {errorStr}");
        }

        private async Task<string> GenerateMD5Base64(string filename, UploadStatus onStatus)
        {
            onStatus?.Invoke(prepareFileMessage, "Generating file hash");
            
            string result = await Task.Run(() =>
            {
                try
                {
                    return Convert.ToBase64String(MD5.Create().ComputeHash(File.OpenRead(filename)));
                }
                catch (Exception)
                {
                    CleanupTempFiles(apiFile.id);
                    throw;
                }
            });

            if (result.IsNullOrWhitespace()) throw new Exception("File MD5 generation failed");
            
            return result;
        }

        private static void CheckFile(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new FileNotFoundException();
            }

            if (!Path.HasExtension(filename))
            {
                throw new Exception("Specified file doesn't have an extension");
            }

            FileStream fileStream = null;
            try
            {
                fileStream = File.OpenRead(filename);
                fileStream.Close();
            }
            catch (Exception)
            {
                fileStream?.Close();
                throw;
            }
        }
        
        private async Task<bool> CheckForExistingVersion(UploadStatus onStatus, string fileMD5Base64)
        {
            onStatus?.Invoke(prepareFileMessage, "Checking for changes");

            //if this is a new file, we can be sure that this is not a retry
            if (!apiFile.HasExistingOrPendingVersion()) return false;
            
            //if the file we are about to upload matches the existing file on the remote server
            if (string.CompareOrdinal(fileMD5Base64, apiFile.GetFileMD5(apiFile.GetLatestVersionNumber())) == 0)
            {
                Logger.Log("New file hash matches remote file hash, aborting upload");
                //the MD5 of the new file matches MD5 of existing file

                //if the last upload was successful, we can terminate the upload process early
                if (!apiFile.IsWaitingForUpload())
                {
                    CleanupTempFiles(apiFile.id);
                    throw new Exception("The file to upload matches the remote file already.");
                }

                //the previous upload wasn't succesful
                Logger.Log("Retrying previous upload");
                return true;
            }

            //if the newest version doesn't have pending changes, return
            if (!apiFile.IsWaitingForUpload()) return false;
            
            //if it does, clean up
            Logger.Log("Latest version of remote file has pending changes, cleaning up...");
            await ApiFileAsyncExtensions.DeleteLatestVersion(apiFile);

            return false;
        }

        private static async Task InitRemoteConfig()
        {
            //If remoteconfig is already initialised, return true
            if (ConfigManager.RemoteConfig.IsInitialized()) return;
            
            bool done = false;
            ConfigManager.RemoteConfig.Init(() => done = true, () => done = true);

            //god i hate these.. why can't vrc use proper async programming
            while (!done)
                await Task.Delay(33);

            if (ConfigManager.RemoteConfig.IsInitialized()) return;

            throw new Exception("Failed to fetch remote configuration");
        }

        private async Task HandleFileErrorState(UploadStatus onStatus)
        {
            if (!apiFile.IsInErrorState()) return;

            Logger.LogWarning($"ApiFile: {apiFile.id}: server failed to process last uploaded, deleting failed version");

            // delete previous failed version
            onStatus?.Invoke(prepareRemoteMessage, "Cleaning up previous version");

            await ApiFileAsyncExtensions.DeleteLatestVersion(apiFile);
        }

        public static async Task<ApiFile> FetchRecord(string filePath, string existingFileId, string friendlyName,
            UploadStatus onStatus, Func<bool> cancelQuery)
        {
            ApiFile apiFile;
            
            Logger.Log(string.IsNullOrEmpty(existingFileId) ? "Creating new file record for asset bundle..." : "Fetching file record for asset bundle...");
            
            string extension = Path.GetExtension(filePath);
            string mimeType = GetMimeTypeFromExtension(extension);

            onStatus?.Invoke(prepareRemoteMessage, string.IsNullOrEmpty(existingFileId) ? "Creating file record..." : "Getting file record...");
            
            if (string.IsNullOrEmpty(friendlyName))
                friendlyName = filePath;

            //Get file record
            while (true)
            {
                apiFile = null;
                bool wait = true;
                errorStr = "";

                if (string.IsNullOrEmpty(existingFileId))
                    ApiFile.Create(friendlyName, mimeType, extension, (c) =>
                    {
                        apiFile = c.Model as ApiFile;
                        wait = false;
                    }, (c) => throw new Exception(c.Error));
                else
                    API.Fetch<ApiFile>(existingFileId, (c) =>
                    {
                        apiFile = c.Model as ApiFile;
                        wait = false;
                    }, (c) => throw new Exception(c.Error), true);

                while (wait)
                {
                    if (apiFile != null && cancelQuery())
                        throw new OperationCanceledException();

                    await Task.Delay(33);
                }

                if (!string.IsNullOrEmpty(errorStr))
                {
                    if (errorStr.Contains("File not found"))
                    {
                        Logger.LogWarning($"Couldn't find file record: {existingFileId}, creating new file record");

                        existingFileId = "";
                        continue;
                    }

                    throw new Exception(string.IsNullOrEmpty(existingFileId)
                        ? "Failed to create file record"
                        : $"Failed to get file record: {errorStr}");
                }

                break;
            }

            return apiFile;
        }

        private async Task<string> GenerateSignatureFile(string filename, UploadStatus onStatus)
        {
            //generate signature file for new upload
            onStatus?.Invoke(prepareFileMessage, "Generating signature file");

            string signatureFilename = Tools.GetTempFileName(".sig", out errorStr, apiFile.id);

            if (string.IsNullOrEmpty(signatureFilename))
            {
                CleanupTempFiles(apiFile.id);
                throw new Exception($"Failed to generate file signature: Failed to create temp file", new Exception(errorStr));
            }

            // create file signature
            try
            {
                Logger.Log($"Generating signature for {filename.GetFileName()}");

                await Task.Delay(33);

                byte[] buf = new byte[512 * 1024];
            
                Stream inStream = Librsync.ComputeSignature(File.OpenRead(filename));
                FileStream outStream = File.Open(signatureFilename, FileMode.Create, FileAccess.Write);

                while (true)
                {
                    IAsyncResult asyncRead = inStream.BeginRead(buf, 0, buf.Length, null, null);

                    while (!asyncRead.IsCompleted)
                    {
                    
                    }

                    int read = inStream.EndRead(asyncRead);

                    if (read <= 0)
                    {
                        break;
                    }

                    IAsyncResult asyncWrite = outStream.BeginWrite(buf, 0, read, null, null);

                    while (!asyncWrite.IsCompleted)
                    {
                    
                    }

                    outStream.EndWrite(asyncWrite);
                }

                inStream.Close();
                outStream.Close();
            }
            catch (Exception)
            {
                CleanupTempFiles(apiFile.id);
                throw;
            }

            return signatureFilename;
        }

        private static async Task CreateFileDeltaInternal(string newFilename, string existingFileSignaturePath, string outputDeltaFilename)
        {
            Logger.Log($"CreateFileDelta: {newFilename} (delta) {existingFileSignaturePath} => {outputDeltaFilename}");

            await Task.Delay(33);

            byte[] buf = new byte[64 * 1024];
            Stream inStream = Librsync.ComputeDelta(File.OpenRead(existingFileSignaturePath), File.OpenRead(newFilename));
            FileStream outStream = File.Open(outputDeltaFilename, FileMode.Create, FileAccess.Write);

            while (true)
            {
                IAsyncResult asyncRead = inStream.BeginRead(buf, 0, buf.Length, null, null);

                while (!asyncRead.IsCompleted)
                    await Task.Delay(33);

                int read = inStream.EndRead(asyncRead);

                if (read <= 0)
                    break;

                IAsyncResult asyncWrite = outStream.BeginWrite(buf, 0, read, null, null);

                while (!asyncWrite.IsCompleted)
                    await Task.Delay(33);
                
                outStream.EndWrite(asyncWrite);
            }

            inStream.Close();
            outStream.Close();

            await Task.Delay(33);
        }

        private static void CleanupTempFiles(string subFolderName)
        {
            Task unused = CleanupTempFilesInternal(subFolderName);
        }

        private static async Task CleanupTempFilesInternal(string subFolderName)
        {
            if (string.IsNullOrEmpty(subFolderName)) return;
            
            string folder = Tools.GetTempFolderPath(subFolderName);

            while (Directory.Exists(folder))
            {
                try
                {
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, true);
                }
                catch (Exception)
                {
                    //ignored as removing temp files can be supressed
                }

                await Task.Delay(33);
            }
        }

        private static async Task UploadFileComponentInternal(ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            string filepath, string md5Base64, long fileSize, Action<ApiFile> onSuccess, Action<long, long> onProgress,
            Func<bool> cancelQuery)
        {
            Logger.Log($"UploadFileComponent: {fileDescriptorType} ({apiFile.id}): {filepath.GetFileName()}");

            ApiFile.Version.FileDescriptor fileDesc =
                apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), fileDescriptorType);

            //validate file descriptor
            if (fileDesc.status != ApiFile.Status.Waiting)
            {
                // nothing to do (might be a retry)
                Logger.Log("UploadFileComponent: (file record not in waiting status, done)");
                onSuccess?.Invoke(apiFile);
                return;
            }

            if (fileSize != fileDesc.sizeInBytes)
            {
                throw new Exception("File size does not match version descriptor");
            }

            if (string.CompareOrdinal(md5Base64, fileDesc.md5) != 0)
            {
                throw new Exception("File MD5 does not match version descriptor");
            }

            // make sure file is right size
            if (!Tools.GetFileSize(filepath, out long tempSize, out string errorStr))
            {
                throw new Exception($"Couldn't get file size : {errorStr}");
            }

            if (tempSize != fileSize)
            {
                throw new Exception("File size does not match input size");
            }

            switch (fileDesc.category)
            {
                case ApiFile.Category.Simple:
                    await apiFile.UploadFileComponentDoSimpleUpload(fileDescriptorType, filepath, md5Base64,
                        onProgress, cancelQuery);
                    break;
                case ApiFile.Category.Multipart:
                    await apiFile.UploadFileComponentDoMultipartUpload(fileDescriptorType, filepath,
                        fileSize, onProgress, cancelQuery);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported file category type: {fileDesc.category}");
            }

            await apiFile.UploadFileComponentVerifyRecord(fileDescriptorType, fileDesc);
        }
    }
}
#endif