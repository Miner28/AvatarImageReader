using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Components;
using Logger = VRC.Core.Logger;
using Object = UnityEngine.Object;

namespace BocuD.VRChatApiTools.Editor
{
    public static class VRChatApiTools
    {
        public static Dictionary<string, Texture2D> ImageCache = new Dictionary<string, Texture2D>();

        public static List<ApiWorld> uploadedWorlds = null;
        public static List<ApiAvatar> uploadedAvatars = null;

        public static EditorCoroutine fetchingWorlds = null;
        public static EditorCoroutine fetchingAvatars = null;

        [NonSerialized] public static List<string> currentlyFetching = new List<string>();
        [NonSerialized] public static List<string> currentlyFetchingAvatars = new List<string>();

        [NonSerialized] public static List<string> invalidWorlds = new List<string>();
        [NonSerialized] public static List<string> invalidAvatars = new List<string>();

        [NonSerialized] public static Dictionary<string, ApiWorld> worldCache = new Dictionary<string, ApiWorld>();
        [NonSerialized] public static Dictionary<string, ApiAvatar> avatarCache = new Dictionary<string, ApiAvatar>();

        public static void RefreshData()
        {
            uploadedWorlds = null;
            uploadedAvatars = null;

            ImageCache.Clear();

            currentlyFetching.Clear();
            currentlyFetchingAvatars.Clear();

            invalidWorlds.Clear();
            invalidAvatars.Clear();

            worldCache.Clear();
            avatarCache.Clear();
        }

        public static IEnumerator FetchUploadedData()
        {
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
                            var list = worlds.ToList();
                            int count = list.Count;
                            SetupWorldData(list);
                            FetchWorlds(offset + count);
                        });
                    else
                    {
                        fetchingWorlds = null;

                        foreach (ApiWorld w in uploadedWorlds)
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
                            var list = avatars.ToList();
                            int count = list.Count;
                            SetupAvatarData(list);
                            FetchAvatars(offset + count);
                        });
                    else
                    {
                        fetchingAvatars = null;

                        foreach (ApiAvatar a in uploadedAvatars)
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

        public static PipelineManager FindPipelineManager()
        {
            Scene currentScene = SceneManager.GetActiveScene();

            VRCSceneDescriptor[] sceneDescriptors = Object.FindObjectsOfType<VRCSceneDescriptor>()
                .Where(x => x.gameObject.scene == currentScene).ToArray();

            if (sceneDescriptors.Length == 0) return null;
            if (sceneDescriptors.Length == 1)
                return sceneDescriptors[0].GetComponent<PipelineManager>();
            if (sceneDescriptors.Length > 1)
            {
                Logger.LogError("Multiple scene descriptors found. Make sure you only have one scene descriptor.");
                return sceneDescriptors[0].GetComponent<PipelineManager>();
            }

            return null;
        }

        public static void SetupWorldData(List<ApiWorld> worlds)
        {
            if (worlds == null || uploadedWorlds == null)
                return;

            worlds.RemoveAll(w => w == null || w.name == null || uploadedWorlds.Any(w2 => w2.id == w.id));

            if (worlds.Count > 0)
            {
                uploadedWorlds.AddRange(worlds);
                foreach (ApiWorld world in uploadedWorlds)
                {
                    if (!worldCache.TryGetValue(world.id, out ApiWorld test))
                    {
                        worldCache.Add(world.id, world);
                    }
                }
            }
        }

        public static void SetupAvatarData(List<ApiAvatar> avatars)
        {
            if (avatars == null || uploadedAvatars == null)
                return;

            avatars.RemoveAll(a => a == null || a.name == null || uploadedAvatars.Any(a2 => a2.id == a.id));

            if (avatars.Count > 0)
            {
                uploadedAvatars.AddRange(avatars);

                foreach (ApiAvatar avatar in uploadedAvatars)
                {
                    if (!avatarCache.TryGetValue(avatar.id, out ApiAvatar test))
                    {
                        avatarCache.Add(avatar.id, avatar);
                    }
                }
            }
        }

        public static void DownloadImage(string blueprintID, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (ImageCache.ContainsKey(blueprintID) && ImageCache[blueprintID] != null) return;

            EditorCoroutine.Start(VRCCachedWebRequest.Get(url, succes));

            void succes(Texture2D texture)
            {
                if (texture != null)
                {
                    ImageCache[blueprintID] = texture;
                }
                else if (ImageCache.ContainsKey(blueprintID))
                {
                    ImageCache.Remove(blueprintID);
                }
            }
        }

        public static void FetchApiWorld(string blueprintID)
        {
            if (currentlyFetching.Contains(blueprintID)) return;

            currentlyFetching.Add(blueprintID);
            ApiWorld world = API.FromCacheOrNew<ApiWorld>(blueprintID);
            world.Fetch(null,
                (c) => AddWorldToCache(blueprintID, c.Model as ApiWorld),
                (c) =>
                {
                    if (c.Code == 404)
                    {
                        currentlyFetching.Remove(world.id);
                        invalidWorlds.Add(world.id);
                        VRC.Core.Logger.Log($"Could not load world {blueprintID} because it didn't exist.",
                            DebugLevel.All);
                        ApiCache.Invalidate<ApiWorld>(blueprintID);
                    }
                    else
                        currentlyFetching.Remove(world.id);
                });
        }

        public static void FetchApiAvatar(string blueprintID)
        {
            if (currentlyFetchingAvatars.Contains(blueprintID)) return;

            currentlyFetchingAvatars.Add(blueprintID);

            ApiAvatar avatar = API.FromCacheOrNew<ApiAvatar>(blueprintID);

            avatar.Fetch((c) => AddAvatarToCache(blueprintID, c.Model as ApiAvatar),
                (c) =>
                {
                    if (c.Code == 404)
                    {
                        currentlyFetchingAvatars.Remove(avatar.id);
                        invalidAvatars.Add(avatar.id);
                        VRC.Core.Logger.Log($"Could not load avatar {blueprintID} because it didn't exist.",
                            DebugLevel.All);
                        ApiCache.Invalidate<ApiAvatar>(blueprintID);
                    }
                    else
                        currentlyFetchingAvatars.Remove(avatar.id);
                });
        }

        private static void AddWorldToCache(string blueprintID, ApiWorld world)
        {
            currentlyFetching.Remove(world.id);
            worldCache.Add(blueprintID, world);
            DownloadImage(blueprintID, world.thumbnailImageUrl);
        }

        private static void AddAvatarToCache(string blueprintID, ApiAvatar avatar)
        {
            currentlyFetchingAvatars.Remove(avatar.id);
            avatarCache.Add(blueprintID, avatar);
            DownloadImage(blueprintID, avatar.thumbnailImageUrl);
        }

        public static async void TryAutoLogin(EditorWindow repaintOnSucces = null)
        {
            VRCSdkControlPanel controlPanel = EditorWindow.GetWindow<VRCSdkControlPanel>();
            for (int i = 0; i < 50; i++)
            {
                if (APIUser.IsLoggedIn)
                {
                    controlPanel.Close();
                    if (repaintOnSucces != null) repaintOnSucces.Repaint();
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(0.1f));
            }

            Logger.Log("Timed out waiting for automatic login");
        }
    }
}