#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using Object = UnityEngine.Object;

namespace BocuD.VRChatApiTools
{
    public class VRChatApiUploaderAsync
    {
        private bool cancelRequested = false;

        private string uploadVrcPath;

        private string cloudFrontAssetUrl;
        private string cloudFrontImageUrl;
        private string cloudFrontUnityPackageUrl;

        private VRChatApiToolsUploadStatus statusWindow;

        #region Avatar Image Update
        public void SetupAvatarImageUpdate(ApiAvatar apiAvatar, Texture2D newImage)
        {
            statusWindow = VRChatApiToolsUploadStatus.ShowStatus();
            string imagePath = SaveImageTemp(newImage);
            UpdateAvatarImage(apiAvatar, imagePath);
        }

        public async void UpdateAvatarImage(ApiAvatar avatar, string newImagePath)
        {
            await UpdateImage(avatar.imageUrl, GetFriendlyAvatarFileName("Image", avatar.id), newImagePath);
            
            avatar.imageUrl = cloudFrontImageUrl;
            
            await ApplyAvatarChanges(avatar);
            
            statusWindow.SetStatus("Finished!", 1);
        }

        public async Task ApplyAvatarChanges(ApiAvatar avatar)
        {
            bool doneUploading = false;

            statusWindow.SetStatus("Applying Avatar Changes", 0);
            
            avatar.Save(
                (c) => { AnalyticsSDK.AvatarUploaded(avatar, true); doneUploading = true; },
                (c) => {
                    Debug.LogError(c.Error);
                    SetUploadProgress("Saving Avatar", "Error saving blueprint.", 0.0f);
                    doneUploading = true;
                });

            while (!doneUploading)
                await Task.Delay(33);
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

        public async Task UpdateImage(string existingFileUrl, string friendlyFileName, string newImagePath)
        {
            if (!string.IsNullOrEmpty(newImagePath))
            {
                await UploadFile(newImagePath, existingFileUrl, friendlyFileName, "Image",
                    delegate (string fileUrl)
                    {
                        cloudFrontImageUrl = fileUrl;
                    }
                );
            }
        }
        #endregion
        #region World Uploader
        public void SetupWorldUpload(ApiWorld target, string unityPackagePath)
        {
            statusWindow = VRChatApiToolsUploadStatus.ShowStatus();

            UploadWorld(target, true, unityPackagePath);
        }

        public async void UploadWorld(ApiWorld apiWorld, bool isUpdate, string unityPackagePath)
        {
            // upload unity package
            if (!string.IsNullOrEmpty(unityPackagePath))
            {
                await UploadFile(unityPackagePath, isUpdate ? apiWorld.unityPackageUrl : "",
                    GetFriendlyWorldFileName("Unity package", apiWorld), "Unity package",
                    delegate(string fileUrl) { cloudFrontUnityPackageUrl = fileUrl; }
                );
            }

            // upload asset bundle
            if (!string.IsNullOrEmpty(uploadVrcPath))
            {
                await UploadFile(uploadVrcPath, isUpdate ? apiWorld.assetUrl : "",
                    GetFriendlyWorldFileName("Asset bundle", apiWorld), "Asset bundle",
                    delegate(string fileUrl) { cloudFrontAssetUrl = fileUrl; }
                );
            }

            if (isUpdate)
                await UpdateBlueprint(apiWorld);
            // else
            //     await CreateBlueprint();
            
            // if (publishingToCommunityLabs)
            // {
            //     ApiWorld.PublishWorldToCommunityLabs(pipelineManager.blueprintId,
            //         (world) => OnUploadedWorld(),
            //         (err) =>
            //         {
            //             Debug.LogError("PublishWorldToCommunityLabs error:" + err);
            //             OnUploadedWorld();
            //         }
            //     );
            // }
            // else
            // {
            //}
            
            statusWindow.SetStatus("Finished!", 1);
        }
        
        private async Task UpdateBlueprint(ApiWorld apiWorld)
        {
            bool doneUploading = false;

            // apiWorld.name = blueprintName.text;
            // apiWorld.description = blueprintDescription.text;
            // apiWorld.capacity = System.Convert.ToInt16(worldCapacity.text);
            // apiWorld.assetUrl = cloudFrontAssetUrl;
            // apiWorld.tags = BuildTags();
            // apiWorld.releaseStatus = (releasePublic.isOn) ? ("public") : ("private");
            // apiWorld.unityPackageUrl = cloudFrontUnityPackageUrl;
            // apiWorld.isCurated = contentFeatured.isOn || contentSDKExample.isOn;

            // if (shouldUpdateImageToggle.isOn)
            // {
            //     yield return StartCoroutine(UpdateImage(isUpdate ? worldRecord.imageUrl : "", GetFriendlyWorldFileName("Image")));
            //
            //     worldRecord.imageUrl = cloudFrontImageUrl;
            // }

            statusWindow.SetStatus("Applying Blueprint Changes", 0);
            apiWorld.Save((c) => doneUploading = true, (c) => { doneUploading = true; Debug.LogError(c.Error); });

            while (!doneUploading)
                await Task.Delay(33);
        }

        private string GetFriendlyWorldFileName(string type, ApiWorld apiWorld)
        {
            return "World - " + apiWorld.name + " - " + type + " - " + Application.unityVersion + "_" + ApiWorld.VERSION.ApiVersion +
                   "_" + VRC.Tools.Platform + "_" + API.GetServerEnvironmentForApiUrl();
        }


        #endregion

        public async Task UploadFile(string filename, string existingFileUrl, string friendlyFileName, string fileType, Action<string> onSuccess)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Debug.LogError("[<color=lime>VRChatApiTools</color>] Null file passed to UploadFile");
                return;
            }

            Debug.Log("[<color=lime>VRChatApiTools</color>] Uploading " + fileType + "(" + filename + ") ...");
            
            statusWindow.SetStatus($"Uploading {fileType}...", 0);

            string fileId = ApiFile.ParseFileIdFromFileAPIUrl(existingFileUrl);
            
            string errorStr = "";
            string newFileUrl = "";

            ApiFileHelperAsync fileHelperAsync = new ApiFileHelperAsync();
            
            await fileHelperAsync.UploadFile(filename, fileId, friendlyFileName,
                delegate (ApiFile apiFile, string message)
                {
                    newFileUrl = apiFile.GetFileURL();
                    Debug.Log($"[<color=lime>VRChatApiTools</color>] {fileType} upload succeeded");
                },
                delegate (ApiFile apiFile, string error)
                {
                    errorStr = error;
                    Debug.LogError($"[<color=lime>VRChatApiTools</color>] {fileType} upload failed: {error} ({filename}) => {apiFile}");
                },
                delegate (ApiFile apiFile, string status, string subStatus, float progress)
                {
                    statusWindow.SetStatus($"Uploading {fileType}...", progress, status);
                },
                WasCancelRequested
            );

            if (!string.IsNullOrEmpty(errorStr))
            {
                Debug.LogError($"[<color=lime>VRChatApiTools</color>] {fileType} upload failed.\n{errorStr}");
                return;
            }

            if (onSuccess != null)
                onSuccess(newFileUrl);
        }
        
        public static string GetFriendlyAvatarFileName(string type, string blueprintID)
        {
            return "Avatar - " + blueprintID + " - " + type + " - " + Application.unityVersion + "_" + ApiWorld.VERSION.ApiVersion +
                   "_" + VRC.Tools.Platform + "_" + API.GetServerEnvironmentForApiUrl();
        }

        private void SetUploadProgress(string title, string message, float progress)
        {
            Debug.Log($"[<color=lime>VRChatApiTools</color>] {title} {message} {progress}");
        }

        private bool WasCancelRequested(ApiFile apiFile)
        {
            return cancelRequested;
        }
    }
}

#endif
