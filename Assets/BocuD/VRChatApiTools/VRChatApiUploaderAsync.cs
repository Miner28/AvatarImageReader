#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using Debug = UnityEngine.Debug;

namespace BocuD.VRChatApiTools
{
    public class VRChatApiUploaderAsync
    { 
        public delegate void SetStatusFunc(string header, string status = null, string subStatus = null);
        public delegate void SetUploadProgressFunc(long done, long total);
        public delegate void SetUploadStateFunc(VRChatApiToolsUploadStatus.UploadState state);
        public delegate void SetErrorStateFunc(string header, string details);
        public delegate void LoggerFunc(string contents);

        public VRChatApiToolsUploadStatus uploadStatus;
        
        public SetStatusFunc OnStatus = (header, status, subStatus) => { };
        public SetUploadProgressFunc OnUploadProgress = (done, total) => { };
        public SetUploadStateFunc OnUploadState = state => { };
        public SetErrorStateFunc OnError = (header, details) => { };
        public LoggerFunc Log = contents => Logger.Log(contents);
        public LoggerFunc LogWarning = contents => Logger.LogWarning(contents);
        public LoggerFunc LogError = contents => Logger.LogError(contents);

        public Func<bool> cancelQuery = () => false;

        public void UseStatusWindow()
        {
            uploadStatus = VRChatApiToolsUploadStatus.GetNew();
            
            OnStatus = uploadStatus.SetStatus;
            OnUploadProgress = uploadStatus.SetUploadProgress;
            OnUploadState = uploadStatus.SetUploadState;
            OnError = uploadStatus.SetErrorState;
            cancelQuery = () => uploadStatus.cancelRequested;
        }

        public async void SetupAvatarImageUpdate(ApiAvatar apiAvatar, Texture2D newImage)
        {
            string imagePath = SaveImageTemp(newImage);
            
            await UpdateAvatarImage(apiAvatar, imagePath);
        }

        public async Task UpdateAvatarImage(ApiAvatar avatar, string newImagePath)
        {
            avatar.imageUrl = await UploadImage(avatar.imageUrl, VRChatApiTools.GetFriendlyAvatarFileName("Image", avatar.id, VRChatApiTools.CurrentPlatform()), newImagePath);

            await ApplyAvatarChanges(avatar);

            OnUploadState(VRChatApiToolsUploadStatus.UploadState.finished);
        }

        public async Task ApplyAvatarChanges(ApiAvatar avatar)
        {
            bool doneUploading = false;

            OnStatus("Applying Avatar Changes");
            
            avatar.Save(
                c => { AnalyticsSDK.AvatarUploaded(avatar, true); doneUploading = true; },
                c => {
                    LogError(c.Error);
                    doneUploading = true;
                });

            while (!doneUploading)
                await Task.Delay(33);
        }

        public async Task UploadWorld(string assetbundlePath, string unityPackagePath, VRChatApiTools.WorldInfo worldInfo = null)
        {
            VRChatApiTools.ClearCaches();
            await Task.Delay(100);
            if (!await VRChatApiTools.TryAutoLoginAsync()) return;
            
            PipelineManager pipelineManager = VRChatApiTools.FindPipelineManager();
            if (pipelineManager == null)
            {
                LogError("Couldn't find Pipeline Manager");
                return;
            }

            pipelineManager.user = APIUser.CurrentUser;

            bool isUpdate = true;
            bool wait = true;
            
            ApiWorld apiWorld = new ApiWorld
            {
                id = pipelineManager.blueprintId
            };
            
            apiWorld.Fetch(null,
                (c) =>
                {
                    Log("Updating an existing world.");
                    apiWorld = c.Model as ApiWorld;
                    pipelineManager.completedSDKPipeline = !string.IsNullOrEmpty(apiWorld.authorId);
                    isUpdate = true;
                    wait = false;
                },
                (c) =>
                {
                    Log("World record not found, creating a new world.");
                    apiWorld = new ApiWorld { capacity = 16 };
                    pipelineManager.completedSDKPipeline = false;
                    apiWorld.id = pipelineManager.blueprintId;
                    isUpdate = false;
                    wait = false;
                });

            while (wait) await Task.Delay(100);

            if (apiWorld == null)
            {
                LogError("Couldn't get world record");
                return;
            }

            //Prepare asset bundle
            string blueprintId = apiWorld.id;
            int version = Mathf.Max(1, apiWorld.version + 1);
            string uploadVrcPath = PrepareVRCPathForS3(assetbundlePath, blueprintId, version, VRChatApiTools.CurrentPlatform(), ApiWorld.VERSION);
            
            //Prepare unity package if it exists
            bool shouldUploadUnityPackage = !string.IsNullOrEmpty(unityPackagePath) && File.Exists(unityPackagePath);
            string uploadUnityPackagePath = shouldUploadUnityPackage ? PrepareUnityPackageForS3(unityPackagePath, blueprintId, version, VRChatApiTools.CurrentPlatform(), ApiWorld.VERSION) : "";
            if (shouldUploadUnityPackage) Logger.LogWarning("Found UnityPackage. Why are you building with future proof publish enabled?");

            //Assign a new blueprint ID if this is a new world
            if (string.IsNullOrEmpty(apiWorld.id))
            {
                pipelineManager.AssignId();
                apiWorld.id = pipelineManager.blueprintId;
            }

            await UploadWorldData(apiWorld, uploadUnityPackagePath, uploadVrcPath, isUpdate, VRChatApiTools.CurrentPlatform(), worldInfo);
        }

        public async Task UploadWorldData(ApiWorld apiWorld, string uploadUnityPackagePath, string uploadVrcPath, bool isUpdate, VRChatApiTools.Platform platform, VRChatApiTools.WorldInfo worldInfo = null)
        {
            string unityPackageUrl = "";
            string assetBundleUrl = "";

            // upload unity package
            if (!string.IsNullOrEmpty(uploadUnityPackagePath))
            {
                unityPackageUrl = await PrepareFileUpload(uploadUnityPackagePath,
                    isUpdate ? apiWorld.unityPackageUrl : "",
                    VRChatApiTools.GetFriendlyWorldFileName("Unity package", apiWorld, platform), "Unity package");
            }
            
            // upload asset bundle
            if (!string.IsNullOrEmpty(uploadVrcPath))
            {
                assetBundleUrl = await PrepareFileUpload(uploadVrcPath, isUpdate ? apiWorld.assetUrl : "",
                    VRChatApiTools.GetFriendlyWorldFileName("Asset bundle", apiWorld, platform), "Asset bundle");
            }
            
            if (assetBundleUrl.IsNullOrWhitespace()) 
            {
                OnStatus("Failed", "Asset bundle upload failed");
                return;
            }

            bool appliedSucces = false;

            if (isUpdate)
                appliedSucces = await UpdateWorldBlueprint(apiWorld, assetBundleUrl, unityPackageUrl, platform, worldInfo);
            else
                appliedSucces = await CreateWorldBlueprint(apiWorld, assetBundleUrl, unityPackageUrl, worldInfo);

            if (appliedSucces)
            {
                OnUploadState(VRChatApiToolsUploadStatus.UploadState.finished);
            }
            else
            {
                OnUploadState(VRChatApiToolsUploadStatus.UploadState.failed);
            }
        }

        public async Task<bool> UpdateWorldBlueprint(ApiWorld apiWorld, string newAssetUrl, string newPackageUrl, VRChatApiTools.Platform platform, VRChatApiTools.WorldInfo worldInfo = null)
        {
            bool applied = false;

            if (worldInfo != null)
            {
                apiWorld.name = worldInfo.name;
                apiWorld.description = worldInfo.name;
                apiWorld.tags = worldInfo.tags.ToList();
                apiWorld.capacity = worldInfo.capacity;

                if (worldInfo.newImagePath != "")
                {
                    string newImageUrl = await UploadImage(apiWorld.imageUrl, VRChatApiTools.GetFriendlyWorldFileName("Image", apiWorld, platform), worldInfo.newImagePath);
                    apiWorld.imageUrl = newImageUrl;
                }
            }
            
            apiWorld.assetUrl = newAssetUrl.IsNullOrWhitespace() ? apiWorld.assetUrl : newAssetUrl;
            apiWorld.unityPackageUrl = newPackageUrl.IsNullOrWhitespace() ? apiWorld.unityPackageUrl : newPackageUrl;
            
            OnStatus("Applying Blueprint Changes");
            
            bool success = false;
            apiWorld.Save(c =>
            {
                applied = true;
                success = true;
            }, c =>
            {
                applied = true; 
                LogError(c.Error);
                OnError("Applying blueprint changes failed", c.Error);
                success = false;
            });

            while (!applied)
                await Task.Delay(33);

            return success;
        }

        private async Task<bool> CreateWorldBlueprint(ApiWorld apiWorld, string newAssetUrl, string newPackageUrl, VRChatApiTools.WorldInfo worldInfo = null)
        {
            bool success = false;
            
            PipelineManager pipelineManager = VRChatApiTools.FindPipelineManager();
            if (pipelineManager == null)
            {
                LogError("Couldn't find Pipeline Manager");
                OnError("Creating blueprint failed", "Couldn't find Pipeline Manager");
                return false;
            }
            
            ApiWorld newWorld = new ApiWorld
            {
                id = apiWorld.id,
                authorName = pipelineManager.user.displayName,
                authorId = pipelineManager.user.id,
                name = "New VRChat world", //temp
                imageUrl = "",
                assetUrl = newAssetUrl,
                unityPackageUrl = newPackageUrl,
                description = "A description", //temp
                tags = new List<string>(), //temp
                releaseStatus = (false) ? ("public") : ("private"), //temp
                capacity = Convert.ToInt16(16), //temp
                occupants = 0,
                shouldAddToAuthor = true,
                isCurated = false
            };
            
            if (worldInfo != null)
            {
                newWorld.name = worldInfo.name;
                newWorld.description = worldInfo.name;
                newWorld.tags = worldInfo.tags.ToList();
                newWorld.capacity = worldInfo.capacity;

                if (worldInfo.newImagePath != "")
                {
                    newWorld.imageUrl = await UploadImage(newWorld.imageUrl, VRChatApiTools.GetFriendlyWorldFileName("Image", newWorld, VRChatApiTools.CurrentPlatform()), worldInfo.newImagePath);;
                }
            }

            if (newWorld.imageUrl.IsNullOrWhitespace())
            {
                newWorld.imageUrl = await UploadImage("", VRChatApiTools.GetFriendlyWorldFileName("Image", newWorld, VRChatApiTools.CurrentPlatform()),
                    SaveImageTemp(new Texture2D(1200, 900)));
            }

            bool applied = false;
            
            newWorld.Post(
                (c) =>
                {
                    ApiWorld savedBlueprint = (ApiWorld)c.Model;
                    pipelineManager.blueprintId = savedBlueprint.id;
                    EditorUtility.SetDirty(pipelineManager);
                    applied = true;
                    if (worldInfo != null) worldInfo.blueprintID = savedBlueprint.id;
                    success = true;
                },
                (c) =>
                {
                    applied = true;
                    Debug.LogError(c.Error);
                    success = false;
                    OnError("Creating blueprint failed", c.Error);
                });

            while (!applied)
                await Task.Delay(100);

            return success;
        }
        
        public async Task<string> UploadImage(string existingFileUrl, string friendlyFileName, string newImagePath)
        {
            Log($"Preparing image upload for {newImagePath}...");
            
            string newUrl = null;

            if (!string.IsNullOrEmpty(newImagePath))
            {
                newUrl = await PrepareFileUpload(newImagePath, existingFileUrl, friendlyFileName, "Image");
            }
            
            return newUrl;
        }

        public static string SaveImageTemp(Texture2D input)
        {
            byte[] png = input.EncodeToPNG();
            string path = ImageName(input.width, input.height, "image", Application.temporaryCachePath);
            File.WriteAllBytes(path, png);
            return path;
        }

        private static string ImageName(int width, int height, string name, string savePath) =>
            $"{savePath}/{name}_{width}x{height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

        public async Task<string> PrepareFileUpload(string filePath, string existingFileUrl, string friendlyFileName,
            string fileType)
        {
            string newFileUrl = "";

            if (string.IsNullOrEmpty(filePath))
            {
                LogError("Null file passed to UploadFileAsync");
                return newFileUrl;
            }

            Log($"Uploading {fileType} ({filePath.GetFileName()}) ...");

            OnStatus($"Uploading {fileType}...");

            string fileId = ApiFile.ParseFileIdFromFileAPIUrl(existingFileUrl);

            ApiFileHelperAsync fileHelperAsync = new ApiFileHelperAsync();

            Stopwatch stopwatch = Stopwatch.StartNew();
            newFileUrl = await fileHelperAsync.UploadFile(filePath, fileId, fileType, friendlyFileName,
                (status, subStatus) => OnStatus(status, subStatus), (done, total) => OnUploadProgress(done, total),
                cancelQuery);

            Log($"<color=green>{fileType} upload succeeded</color>");
            stopwatch.Stop();
            OnStatus("Upload Succesful", $"Finished upload in {stopwatch.Elapsed:mm\\:ss}");

            return newFileUrl;
        }

        private static string PrepareUnityPackageForS3(string packagePath, string blueprintId, int version, VRChatApiTools.Platform platform, AssetVersion assetVersion)
        {
            string uploadUnityPackagePath =
                $"{Application.temporaryCachePath}/{blueprintId}_{version}_{Application.unityVersion}_{assetVersion.ApiVersion}_{platform.ToApiString()}_{API.GetServerEnvironmentForApiUrl()}.unitypackage";

            if (File.Exists(uploadUnityPackagePath))
                File.Delete(uploadUnityPackagePath);

            File.Copy(packagePath, uploadUnityPackagePath);

            return uploadUnityPackagePath;
        }

        public static string PrepareVRCPathForS3(string assetBundlePath, string blueprintId, int version, VRChatApiTools.Platform platform,
            AssetVersion assetVersion)
        {
            string uploadVrcPath =
                $"{Application.temporaryCachePath}/{blueprintId}_{version}_{Application.unityVersion}_{assetVersion.ApiVersion}_{platform.ToApiString()}_{API.GetServerEnvironmentForApiUrl()}{Path.GetExtension(assetBundlePath)}";

            if (File.Exists(uploadVrcPath))
                File.Delete(uploadVrcPath);

            File.Copy(assetBundlePath, uploadVrcPath);

            return uploadVrcPath;
        }
    }
}

#endif
