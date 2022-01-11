#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using Object = UnityEngine.Object;

namespace BocuD.VRChatApiTools
{
    public class VRChatApiUploader : MonoBehaviour
    {
        private bool cancelRequested = false;
        
        private string uploadTitle = "";
        private string uploadMessage = "";
        private float uploadProgress = 0f;
        
        private string cloudFrontImageUrl;

        private ApiAvatar apiAvatar;

        [Header("Setup")]
        public bool ready = false;
        public string imagePath;
        public string avatarID;

        [MenuItem("Tools/Clear Progress Bar")]
        private static void ClearStatus()
        {
            EditorUtility.ClearProgressBar();
            
            GameObject ImageUploader = GameObject.Find("ImageUploader");
            if (ImageUploader != null)
                DestroyImmediate(ImageUploader);
        }

        public void SetupAvatarImageUpdate(ApiAvatar avatar, Texture2D newImage)
        {
            imagePath = SaveImageTemp(newImage);
            avatarID = avatar.id;
            ready = true;

            EditorApplication.isPlaying = true;
        }

        private void Start()
        {
            API.SetOnlineMode(true, "vrchat");
            ConfigManager.RemoteConfig.Init(Setup);
        }

        private void Setup()
        {
            if (ready)
            {
                Login();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Login()
        {
            if (!ApiCredentials.Load())
                Debug.LogError("[<color=lime>VRChatApiTools</color>] Not logged in - Please log in before trying to upload images");
            else
                APIUser.InitialFetchCurrentUser(delegate
                {
                    ApiAvatar avater = new ApiAvatar() {id = avatarID};
                    avater.Get(false,
                        (c2) =>
                        {
                            apiAvatar = c2.Model as ApiAvatar;
                        },
                        (c2) => { Debug.LogError("[<color=lime>VRChatApiTools</color>] Error while trying to load ApiAvatar"); });
                }, (c) => { Debug.LogError(c.Error); });
        }

        private bool active = false;
        private void Update()
        {
            if (!active)
            {
                if (APIUser.IsLoggedIn)
                {
                    if (apiAvatar != null)
                    {
                        //this is just some fluff to make it look nicer to the user, not doing these checks would still prevent uploading
                        if (apiAvatar.authorId == APIUser.CurrentUser.id)
                        {
                            StartCoroutine(UpdateAvatarImage(apiAvatar, imagePath));
                            active = true;
                        }
                        else
                        {
                            Debug.LogError("[<color=lime>VRChatApiTools</color>] Error while updating avatar: Logged in user doesn't own the target avatar");
                            EditorApplication.isPlaying = false;
                            EditorUtility.ClearProgressBar();
                        }
                    }
                }
            }
            else
            {
                bool cancelled = EditorUtility.DisplayCancelableProgressBar(uploadTitle, uploadMessage, uploadProgress);
                if (cancelled)
                {
                    cancelRequested = true;
                }
            }
        }

        public IEnumerator UpdateAvatarImage(ApiAvatar avatar, string newImagePath)
        {
            yield return UpdateImage(avatar.imageUrl, GetFriendlyAvatarFileName("Image", avatar.id), newImagePath);
            
            avatar.imageUrl = cloudFrontImageUrl;
            
            yield return ApplyAvatarChanges(avatar);
            
            EditorApplication.isPlaying = false;
            EditorUtility.ClearProgressBar();
        }

        public IEnumerator ApplyAvatarChanges(ApiAvatar avatar)
        {
            bool doneUploading = false;

            SetUploadProgress("Saving Avatar", "Almost finished!!", 0.8f);
            avatar.Save(
                (c) => { AnalyticsSDK.AvatarUploaded(avatar, true); doneUploading = true; },
                (c) => {
                    Debug.LogError(c.Error);
                    SetUploadProgress("Saving Avatar", "Error saving blueprint.", 0.0f);
                    doneUploading = true;
                });

            while (!doneUploading)
                yield return null;
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

        public IEnumerator UpdateImage(string existingFileUrl, string friendlyFileName, string newImagePath)
        {
            if (!string.IsNullOrEmpty(newImagePath))
            {
                yield return UploadFile(newImagePath, existingFileUrl, friendlyFileName, "Image",
                    delegate (string fileUrl)
                    {
                        cloudFrontImageUrl = fileUrl;
                    }
                );
            }
        }

        public IEnumerator UploadFile(string filename, string existingFileUrl, string friendlyFileName, string fileType, Action<string> onSuccess)
        {
            if (string.IsNullOrEmpty(filename))
            {
                Debug.LogError("[<color=lime>VRChatApiTools</color>] Null file passed to UploadFile");
                yield break;
            }

            Debug.Log("[<color=lime>VRChatApiTools</color>] Uploading " + fileType + "(" + filename + ") ...");
            
            SetUploadProgress("Uploading " + fileType + "...", "", 0.0f);

            string fileId = ApiFile.ParseFileIdFromFileAPIUrl(existingFileUrl);
            
            string errorStr = "";
            string newFileUrl = "";

            yield return ApiFileHelper.Instance.UploadFile(filename, fileId, friendlyFileName,
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
                    SetUploadProgress($"Uploading {fileType}...", status + (!string.IsNullOrEmpty(subStatus) ? " (" + subStatus + ")" : ""), progress);
                },
                WasCancelRequested
            );

            if (!string.IsNullOrEmpty(errorStr))
            {
                Debug.LogError($"[<color=lime>VRChatApiTools</color>] {fileType} upload failed.\n{errorStr}");
                yield break;
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
            uploadTitle = title;
            uploadMessage = message;
            uploadProgress = progress;
        }

        private bool WasCancelRequested(ApiFile apiFile)
        {
            return cancelRequested;
        }
    }
    
    [InitializeOnLoad]
    public static class ApiUploaderPlayModeStateWatcher
    {
        static ApiUploaderPlayModeStateWatcher()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;
        }

        private static void LogPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                GameObject ImageUploader = GameObject.Find("ImageUploader");
                if (ImageUploader == null) return;
                
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(ImageUploader);
            }
        }
    }
}

#endif
