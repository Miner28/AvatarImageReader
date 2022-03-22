#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VRC;
using VRC.Core;

namespace BocuD.VRChatApiTools
{
    using static Constants;
    
    public static class ApiFileAsyncExtensions
    {
        //todo: merge StartSimpleUploadAsync and StartMultiPartUploadAsync into one function, then use proxy functions to do the actual calls
        public static async Task<string> StartSimpleUploadAsync(this ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, Func<bool> cancelQuery)
        {
            string uploadUrl = "";
            
            if (!apiFile.IsInitialized)
            {
                throw new Exception("Unable to upload file: file not initialized");
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();

            if (apiFile.GetFileDescriptor(latestVersionNumber, fileDescriptorType) == null)
            {
                throw new Exception("Version record doesn't exist");
            }

            ApiFile.UploadStatus uploadStatus = new ApiFile.UploadStatus(apiFile.id, latestVersionNumber, fileDescriptorType, "start");

            bool wait = true;

            ApiDictContainer apiDictContainer = new ApiDictContainer("url")
            {
                OnSuccess = c =>
                {
                    wait = false;
                    uploadUrl = (c as ApiDictContainer)?.ResponseDictionary["url"] as string;
                },
                OnError = c => throw new Exception(c.Error)
            };

            API.SendPutRequest(uploadStatus.Endpoint, apiDictContainer);

            while (wait)
            {
                if (cancelQuery())
                {
                    throw new OperationCanceledException();
                }
                await Task.Delay(33);
            }

            if (string.IsNullOrEmpty(uploadUrl))
            {
                throw new Exception("Invalid URL provided by API");
            }

            return uploadUrl;
        }
        
        public static async Task<string> StartMultiPartUploadAsync(this ApiFile apiFile, int partNumber,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, Func<bool> cancelQuery)
        {
            string uploadUrl = "";
            
            if (!apiFile.IsInitialized)
            {
                throw new Exception("Unable to upload file: file not initialized.");
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();

            if (apiFile.GetFileDescriptor(latestVersionNumber, fileDescriptorType) == null)
            {
                throw new Exception("Version record doesn't exist");
            }
            
            ApiFile.UploadStatus uploadStatus = new ApiFile.UploadStatus(apiFile.id, latestVersionNumber, fileDescriptorType, "start");

            bool wait = true;

            ApiDictContainer apiDictContainer = new ApiDictContainer("url")
            {
                OnSuccess = c =>
                {
                    wait = false;
                    uploadUrl = (c as ApiDictContainer)?.ResponseDictionary["url"] as string;
                },
                OnError = c => throw new Exception("Failed to start multipart upload", new Exception(c.Error))
            };
            
            API.SendPutRequest($"{uploadStatus.Endpoint}?partNumber={partNumber}", apiDictContainer);

            while (wait)
            {
                if (cancelQuery())
                {
                    throw new OperationCanceledException();
                }
                await Task.Delay(33);
            }

            if (string.IsNullOrEmpty(uploadUrl))
            {
                throw new Exception("Invalid URL provided by API while uploading multipart file");
            }
            
            await Task.Delay(postWriteDelay);

            return uploadUrl;
        }

        public static async Task PutSimpleFileAsync(this ApiFile apiFile, string filename, string md5Base64, ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            string uploadUrl = await apiFile.StartSimpleUploadAsync(fileDescriptorType, cancelQuery);
            
            bool wait = true;

            HttpRequest req = ApiFile.PutSimpleFileToURL(uploadUrl, filename,
                ApiFileHelper.GetMimeTypeFromExtension(Path.GetExtension(filename)), md5Base64, true,
                () => wait = false,
                error => throw new Exception($"Failed to upload file: {error}"),
                (uploaded, length) => onProgress?.Invoke(uploaded, length)
            );

            while (wait)
            {
                if (cancelQuery())
                {
                    req?.Abort();
                    throw new OperationCanceledException();
                }

                await Task.Delay(33);
            }
        }

        public static async Task FinishUploadAsync(this ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            List<string> multipartEtags, Func<bool> cancelQuery)
        {
            if (!apiFile.IsInitialized)
            {
                throw new Exception("Unable to finish upload of file: file not initialized.");
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();

            if (apiFile.GetFileDescriptor(latestVersionNumber, fileDescriptorType) == null)
            {
                throw new Exception("Version record doesn't exist");
            }

            bool wait = true;

            new ApiFile.UploadStatus(apiFile.id, latestVersionNumber, fileDescriptorType, "finish")
            {
                etags = multipartEtags
            }.Put(c => wait = false,
                c => 
                    throw new Exception("Unable to finish upload of file", new Exception(c.Error)));

            while (wait)
            {
                if (cancelQuery())
                {
                    throw new OperationCanceledException();
                }
                await Task.Delay(33);
            }
            
            await Task.Delay(postWriteDelay);
        }

        public static async Task<ApiFile.UploadStatus> GetUploadStatus(this ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, Func<bool> cancelQuery)
        {
            bool wait = true;
            ApiFile.UploadStatus result = null;
            
            apiFile.GetUploadStatus(apiFile.GetLatestVersionNumber(), fileDescriptorType,
                c =>
                {
                    wait = false;
                    result = (ApiFile.UploadStatus)c.Model;
                },
                c => throw new Exception("Failed to query multipart upload status", new Exception(c.Error)));

            while (wait)
            {
                if (cancelQuery())
                {
                    throw new OperationCanceledException();
                }
                await Task.Delay(33);
            }

            if (result == null)
            {
                throw new Exception("Failed to query multipart upload status", new Exception("Got null status from api"));
            }
            
            return result;
        }
        
        public static async Task<string> PutMultipartDataAsync(this ApiFile apiFile, int partNumber, ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            byte[] buffer, string mimeType, int bytesRead, Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            string uploadUrl = await apiFile.StartMultiPartUploadAsync(partNumber, fileDescriptorType, cancelQuery);
            
            bool wait = true;
            string resultTag = "";
            
            HttpRequest req = ApiFile.PutMultipartDataToURL(uploadUrl, buffer, bytesRead, mimeType, true,
                etag =>
                {
                    if (!string.IsNullOrEmpty(etag))
                        resultTag = etag;
                    wait = false;
                },
                error => throw new Exception("Failed to upload data part", new Exception(error)),
                (uploaded, length) => onProgress?.Invoke(uploaded, length)
            );

            while (wait)
            {
                if (cancelQuery())
                {
                    req?.Abort();
                    throw new OperationCanceledException();
                }

                await Task.Delay(33);
            }

            return resultTag;
        }

        public static async Task DeleteLatestVersion(this ApiFile apiFile)
        {
            bool wait = true;

            if (!apiFile.IsInitialized)
            {
                throw new Exception("Unable to delete file: file not initialized.");
            }

            int latestVersionNumber = apiFile.GetLatestVersionNumber();
            if (latestVersionNumber <= 0 || latestVersionNumber >= apiFile.versions.Count)
                throw new Exception($"ApiFile ({apiFile.id}): version to delete is invalid: {latestVersionNumber}");

            if (latestVersionNumber == 1)
                throw new Exception("There is only one version. Deleting version that would delete the file. Please use another method.");

            apiFile.DeleteVersion(latestVersionNumber,
                c => wait = false,
                c => throw new Exception(c.Error));

            while (wait)
            {
                await Task.Delay(33);
            }
            
            await Task.Delay(postWriteDelay);
        }

        public static async Task UploadFileComponentDoSimpleUpload(this ApiFile apiFile, ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            string filename, string md5Base64, Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            Logger.Log($"Starting simple upload for {apiFile.name}...");
            
            // delay to let write get through servers
            await Task.Delay(postWriteDelay);

            //PUT file to url
            await apiFile.PutSimpleFileAsync(filename, md5Base64, fileDescriptorType, onProgress, cancelQuery);

            //finish upload
            await apiFile.FinishUploadAsync(fileDescriptorType, null, cancelQuery);
        }

        public static async Task UploadFileComponentDoMultipartUpload(this ApiFile apiFile, ApiFile.Version.FileDescriptor.Type fileDescriptorType,
            string filename, long fileSize, Action<long, long> onProgress, Func<bool> cancelQuery)
        {
            //get existing multipart upload status in case there is one
            ApiFile.UploadStatus uploadStatus = await apiFile.GetUploadStatus(fileDescriptorType, cancelQuery);
            
            FileStream fs = File.OpenRead(filename);

            byte[] buffer = new byte[kMultipartUploadChunkSize * 2];

            long totalBytesUploaded = 0;
            List<string> etags = new List<string>();
            if (uploadStatus != null)
                etags = uploadStatus.etags.ToList();
            
            //why is this a FloorToInt? what the fuck? so 100MB parts on a 250MB world gives you... a 100MB and a 150MB part? 
            int numParts = Mathf.Max(1, Mathf.FloorToInt(fs.Length / (float)kMultipartUploadChunkSize));
            
            try
            {
                for (int partNumber = 1; partNumber <= numParts; partNumber++)
                {
                    Logger.Log($"Uploading part {partNumber}/{numParts}...");

                    // read chunk
                    int bytesToRead = partNumber < numParts
                        ? kMultipartUploadChunkSize
                        : (int)(fs.Length - fs.Position);
                    int bytesRead = fs.Read(buffer, 0, bytesToRead);

                    if (bytesRead != bytesToRead)
                    {
                        throw new Exception($"Uploading part {partNumber} failed", new Exception("Couldn't read file: read incorrect number of bytes from stream"));
                    }

                    // check if this part has been upload already
                    // NOTE: uploadStatus.nextPartNumber == number of parts already uploaded
                    if (uploadStatus != null && partNumber <= uploadStatus.nextPartNumber)
                    {
                        totalBytesUploaded += bytesRead;
                        continue;
                    }

                    void OnMultiPartUploadProgress(long uploadedBytes, long totalBytes)
                    {
                        onProgress(totalBytesUploaded + uploadedBytes, fileSize);
                    }

                    //PUT file
                    string etag = await apiFile.PutMultipartDataAsync(partNumber, fileDescriptorType, buffer,
                        ApiFileHelper.GetMimeTypeFromExtension(Path.GetExtension(filename)), bytesRead, OnMultiPartUploadProgress,
                        cancelQuery);

                    etags.Add(etag);
                    totalBytesUploaded += bytesRead;
                }
            }
            catch (Exception)
            {
                fs.Close();
                throw;
            }
            
            await Task.Delay(postWriteDelay);

            //finish upload
            try
            {
                await apiFile.FinishUploadAsync(fileDescriptorType, etags, cancelQuery);
            }
            catch (Exception)
            {
                fs.Close();
                throw;
            }
            
            await Task.Delay(postWriteDelay);
        }

        public static async Task UploadFileComponentVerifyRecord(this ApiFile apiFile,
            ApiFile.Version.FileDescriptor.Type fileDescriptorType, ApiFile.Version.FileDescriptor fileDesc)
        {
            float initialStartTime = Time.realtimeSinceStartup;
            float startTime = initialStartTime;
            float timeout = GetServerProcessingWaitTimeoutForDataSize(fileDesc.sizeInBytes);
            float waitDelay = SERVER_PROCESSING_INITIAL_RETRY_TIME;

            while(true)
            {
                if (apiFile == null)
                {
                    throw new Exception("ApiFile is null");
                }

                ApiFile.Version.FileDescriptor desc = apiFile.GetFileDescriptor(apiFile.GetLatestVersionNumber(), fileDescriptorType);
                if (desc == null)
                {
                    throw new Exception($"File descriptor is null ('{fileDescriptorType}')");
                }

                if (desc.status != ApiFile.Status.Waiting)
                {
                    // upload completed or is processing
                    break;
                }
                
                // wait for next poll
                while (Time.realtimeSinceStartup - startTime < waitDelay)
                {
                    if (Time.realtimeSinceStartup - initialStartTime > timeout)
                    {
                        throw new TimeoutException("Couldn't verify upload status: Timed out wait for server processing");
                    }

                    await Task.Delay(33);
                }
                
                while (true)
                {
                    bool wait = true;
                    bool worthRetry = false;

                    apiFile.Refresh(
                        (c) =>
                        {
                            wait = false;
                        },
                        (c) =>
                        {
                            if (c.Code == 400)
                            {
                                worthRetry = true;
                                wait = false;
                            }
                            else
                            {
                                throw new Exception($"Couldn't verify upload status", new Exception(c.Error));
                            }
                        });

                    while (wait)
                    {
                        await Task.Delay(33);
                    }

                    if (!worthRetry)
                        break;
                }

                waitDelay = Mathf.Min(waitDelay * 2, SERVER_PROCESSING_MAX_RETRY_TIME);
                startTime = Time.realtimeSinceStartup;
            }
        }
    }
}
#endif