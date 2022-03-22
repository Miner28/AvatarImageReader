using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using VRC.Core;

namespace BocuD.VRChatApiTools
{
    [InitializeOnLoad]
    public static class VRChatApiToolsEditor
    {
        public static EditorCoroutine fetchingWorlds;
        public static EditorCoroutine fetchingAvatars;
        
        static VRChatApiToolsEditor()
        {
            //gotta love this
            VRChatApiTools.DownloadImage = DownloadImage;
            
            //get access to AsyncProgressBar methods through reflection https://answers.unity.com/questions/1104823/using-editors-built-in-progress-bar.html
            Type type = typeof(Editor).Assembly.GetTypes().FirstOrDefault(t => t.Name == "AsyncProgressBar");
            if (type == null) return;
            
            _display = type.GetMethod("Display");
            _clear = type.GetMethod("Clear");
        }
        
        private static MethodInfo _display = null;
        private static MethodInfo _clear = null;

        public static void ShowGIProgressBar(string aText, float aProgress)
        {
            _display?.Invoke(null, new object[] { aText, aProgress });
        }
        public static void ClearGIProgressBar()
        {
            _clear?.Invoke(null, null);
        }
        
        [MenuItem("Tools/VRChatApiTools/Refresh data")]
        public static void RefreshData()
        {
            Logger.Log("Refreshing data...");
            VRChatApiTools.ClearCaches();
            EditorCoroutine.Start(FetchUploadedData());
        }
        
        #region Fetch worlds and avatars owned by user
        //Almost 1:1 reimplementation of SDK methods
        public static IEnumerator FetchUploadedData()
        {
            VRChatApiTools.uploadedWorlds = new List<ApiWorld>();
            VRChatApiTools.uploadedAvatars = new List<ApiAvatar>();
            
            if (!ConfigManager.RemoteConfig.IsInitialized())
                ConfigManager.RemoteConfig.Init();

            if (!APIUser.IsLoggedIn)
                yield break;

            ApiCache.ClearResponseCache();
            VRCCachedWebRequest.ClearOld();

            if (fetchingAvatars == null)
                fetchingAvatars = EditorCoroutine.Start(() => FetchAvatars());
            
            if (fetchingWorlds == null)
                fetchingWorlds = EditorCoroutine.Start(() => FetchWorlds());
        }
        
        public static void FetchWorlds(int offset = 0)
        {
            ApiWorld.FetchList(
                delegate(IEnumerable<ApiWorld> worlds)
                {
                    if (worlds.FirstOrDefault() != null)
                        fetchingWorlds = EditorCoroutine.Start(() =>
                        {
                            List<ApiWorld> list = worlds.ToList();
                            int count = list.Count;
                            VRChatApiTools.SetupWorldData(list);
                            FetchWorlds(offset + count);
                        });
                    else
                    {
                        fetchingWorlds = null;

                        foreach (ApiWorld w in VRChatApiTools.uploadedWorlds)
                            DownloadImage(w.id, w.thumbnailImageUrl);
                    }
                },
                delegate(string obj)
                {
                    Logger.LogError("Couldn't fetch world list:\n" + obj);
                    fetchingWorlds = null;
                },
                ApiWorld.SortHeading.Updated,
                ApiWorld.SortOwnership.Mine,
                ApiWorld.SortOrder.Descending,
                offset,
                20,
                "",
                null,
                null,
                null,
                null,
                "",
                ApiWorld.ReleaseStatus.All,
                null,
                null,
                true,
                false);
        }

        public static void FetchAvatars(int offset = 0)
        {
            ApiAvatar.FetchList(
                delegate(IEnumerable<ApiAvatar> avatars)
                {
                    if (avatars.FirstOrDefault() != null)
                        fetchingAvatars = EditorCoroutine.Start(() =>
                        {
                            List<ApiAvatar> list = avatars.ToList();
                            int count = list.Count;
                            VRChatApiTools.SetupAvatarData(list);
                            FetchAvatars(offset + count);
                        });
                    else
                    {
                        fetchingAvatars = null;

                        foreach (ApiAvatar a in VRChatApiTools.uploadedAvatars)
                            DownloadImage(a.id, a.thumbnailImageUrl);
                    }
                },
                delegate(string obj)
                {
                    Logger.LogError("Couldn't fetch avatar list:\n" + obj);
                    fetchingAvatars = null;
                },
                ApiAvatar.Owner.Mine,
                ApiAvatar.ReleaseStatus.All,
                null,
                20,
                offset,
                ApiAvatar.SortHeading.None,
                ApiAvatar.SortOrder.Descending,
                null,
                null,
                true,
                false,
                null,
                false
            );
        }
        #endregion
        
        public static void DownloadImage(string blueprintID, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (VRChatApiTools.ImageCache.ContainsKey(blueprintID) && VRChatApiTools.ImageCache[blueprintID] != null) return;

            EditorCoroutine.Start(VRCCachedWebRequest.Get(url, texture =>
            {
                if (texture != null)
                {
                    VRChatApiTools.ImageCache[blueprintID] = texture;
                }
                else if (VRChatApiTools.ImageCache.ContainsKey(blueprintID))
                {
                    VRChatApiTools.ImageCache.Remove(blueprintID);
                }
            }));
        }
    }
}