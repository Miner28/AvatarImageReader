using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase.Editor;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace BocuD.VRChatApiTools
{
    public static class VRChatApiToolsGUI
    {
        /// <summary>
        /// Draws inspector for VRChat avatar or world from blueprint ID
        /// </summary>
        /// <param name="blueprintID">The blueprint ID to be displayed</param>
        /// <param name="small">When disabled, adds copy id and open in website buttons</param>
        /// <param name="secondaryButtons">Action params that can be implemented to add extra GUI functionality to inspector</param>
        public static void DrawBlueprintInspector(string blueprintID, bool small = true, params Action[] secondaryButtons)
        {
            if (blueprintID.IsNullOrWhitespace())
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(108));
                EditorGUILayout.BeginHorizontal();
                GUILayout.Box("Can't load image: empty blueprint", GUILayout.Width(128), GUILayout.Height(99));

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("No selected blueprint", EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();

                GUILayout.FlexibleSpace();

                foreach (Action b in secondaryButtons)
                {
                    b.Invoke();
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                return;
            }

            if (!HandleLogin(null, false)) return;

            //already cached
            if (VRChatApiTools.blueprintCache.TryGetValue(blueprintID, out ApiModel model)) 
            {
                DrawBlueprintInspector(model, small, secondaryButtons);
            }
            else
            {
                //loading
                if (VRChatApiTools.currentlyFetching.Contains(blueprintID))
                {
                    EditorGUILayout.LabelField($"Loading blueprint information...");
                }
                else
                {
                    //its not invalidated yet, so try loading
                    if (!VRChatApiTools.invalidBlueprints.Contains(blueprintID))
                    {
                        if (Regex.IsMatch(blueprintID, VRChatApiTools.world_regex))
                        {
                            VRChatApiTools.FetchApiWorld(blueprintID);
                        }
                        else if (Regex.IsMatch(blueprintID, VRChatApiTools.avatar_regex))
                        {
                            VRChatApiTools.FetchApiAvatar(blueprintID);
                        }
                        else
                        {
                            VRChatApiTools.invalidBlueprints.Add(blueprintID);
                        }
                    }
                    //invalid avatar
                    else
                    {
                        EditorGUILayout.HelpBox("Couldn't load specified blueprint ID.", MessageType.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Draws inspector for VRChat blueprint
        /// </summary>
        /// <param name="model">Valid ApiWorld or ApiAvatar to be drawn</param>
        /// <param name="small">When disabled, adds copy id and open in website buttons</param>
        /// <param name="secondaryButtons">Action params that can be implemented to add extra GUI functionality to inspector</param>
        public static void DrawBlueprintInspector(ApiModel model, bool small = true, params Action[] secondaryButtons)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(108));
            EditorGUILayout.BeginHorizontal();

            string modelName = "";
            bool isWorld = false;
            
            switch (model)
            {
                case ApiWorld world:
                    modelName = world.name;
                    isWorld = true;
                    break;
                
                case ApiAvatar avatar:
                    modelName = avatar.name;
                    break;
                
                default:
                    Logger.LogWarning("Non world or avatar ApiModel passed to DrawBlueprintInspector");
                    return;
            }

            if (VRChatApiTools.ImageCache.ContainsKey(model.id))
            {
                GUILayout.Box(VRChatApiTools.ImageCache[model.id], GUILayout.Width(128), GUILayout.Height(99));
            }
            else
            {
                GUILayout.Box("Loading image...", GUILayout.Width(128), GUILayout.Height(99));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(modelName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(model.id);

            string releaseStatus = model is ApiWorld ? ((ApiWorld)model).releaseStatus : ((ApiAvatar)model).releaseStatus;
            EditorGUILayout.LabelField("Release Status: " + releaseStatus);

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (!small)
            {
                if (GUILayout.Button("Open in browser", GUILayout.Width(140)))
                {
                    Application.OpenURL(isWorld
                        ? $"https://vrchat.com/home/world/{model.id}"
                        : $"https://vrchat.com/home/avatar/{model.id}");
                }

                if (GUILayout.Button("Copy ID to clipboard", GUILayout.Width(160)))
                {
                    GUIUtility.systemCopyBuffer = model.id;
                }
            }

            foreach (Action b in secondaryButtons)
            {
                b.Invoke();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        public static bool HandleLogin(EditorWindow repaintOnSucces = null, bool displayLoginStatus = true)
        {
            if (!APIUser.IsLoggedIn)
            {
                if(VRChatApiTools.autoLoginFailed)
                {
                    EditorGUILayout.HelpBox(
                        "You need to be logged in to access VRChat data. Automatically logging in failed, probably because the SDK control panel isn't logged in. Try logging in in the SDK control panel.",
                        MessageType.Error);

                    if (GUILayout.Button("Open VRCSDK Control Panel"))
                    {
                        VRCSettings.ActiveWindowPanel = 0;
                        EditorWindow.GetWindow<VRCSdkControlPanel>();
                    }
                }
                else
                {
                    if (repaintOnSucces != null) VRChatApiTools.TryAutoLogin(repaintOnSucces.Repaint);
                    else VRChatApiTools.TryAutoLogin();

                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    EditorGUILayout.LabelField("Logging in...");
                    
                    EditorGUILayout.EndVertical();
                }
            }
            else if(displayLoginStatus)
            {
                EditorGUILayout.HelpBox($"Currently logged in as {APIUser.CurrentUser.displayName}", MessageType.Info);
            }

            return APIUser.IsLoggedIn;
        }
    }
    
    public class AvatarPicker : EditorWindow
    {
        private string targetString;
        private Action<ApiAvatar> onComplete;

        public static void ApiAvatarSelector(Action<ApiAvatar> OnComplete)
        {
            AvatarPicker avatarPicker = GetWindow<AvatarPicker>();
            avatarPicker.onComplete = OnComplete;
        }

        private void OnGUI()
        {
            if (!VRChatApiToolsGUI.HandleLogin(this)) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("VRChat Avatar List", EditorStyles.boldLabel);

            if (VRChatApiTools.uploadedAvatars == null)
            {
                VRChatApiTools.uploadedAvatars = new List<ApiAvatar>();

                EditorCoroutine.Start(VRChatApiToolsEditor.FetchUploadedData());
            }

            if (VRChatApiToolsEditor.fetchingAvatars != null)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Fetching data from VRChat Api");
            }
            else
            {
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("Enter ID", GUILayout.Width(120)))
                {
                    ManualAvatarSelector.AvatarSelector(ManuallySelected);
                }
                
                if (GUILayout.Button("Refresh", GUILayout.Width(120)))
                {
                    VRChatApiTools.ClearCaches();
                }
            }

            EditorGUILayout.EndHorizontal();

            RenderListContents();
        }

        private Vector2 listScroll;
        private string searchString = "";

        private void RenderListContents()
        {
            if (VRChatApiTools.uploadedAvatars == null) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Uploaded Avatars", EditorStyles.boldLabel, GUILayout.Width(110));

            float searchFieldShrinkOffset = searchString == "" ? 0 : 20f;

            GUILayoutOption layoutOption = (GUILayout.Width(position.width - searchFieldShrinkOffset));
            searchString = EditorGUILayout.TextField(searchString, GUI.skin.FindStyle("SearchTextField"), layoutOption);

            GUIStyle searchButtonStyle = searchString == string.Empty
                ? GUI.skin.FindStyle("SearchCancelButtonEmpty")
                : GUI.skin.FindStyle("SearchCancelButton");

            if (GUILayout.Button("", searchButtonStyle))
            {
                searchString = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(position.width));

            List<ApiAvatar> displayedAvatars = VRChatApiTools.uploadedAvatars.OrderByDescending(x => x.updated_at).ToList();

            if (displayedAvatars.Count > 0)
            {
                foreach (ApiAvatar avatar in displayedAvatars)
                {
                    if (!avatar.name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                    {
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(108));
                    EditorGUILayout.BeginHorizontal();

                    if (VRChatApiTools.ImageCache.ContainsKey(avatar.id))
                    {
                        GUILayout.Box(VRChatApiTools.ImageCache[avatar.id], GUILayout.Width(128), GUILayout.Height(99));
                    }
                    else
                    {
                        GUILayout.Box("Loading image...", GUILayout.Width(128), GUILayout.Height(99));
                    }

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(avatar.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(avatar.id);

                    EditorGUILayout.LabelField("Release Status: " + avatar.releaseStatus);

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.BeginHorizontal();

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Copy ID to clipboard", GUILayout.Width(160)))
                    {
                        GUIUtility.systemCopyBuffer = avatar.id;
                    }

                    if (GUILayout.Button("Select Avatar", GUILayout.Width(140)))
                    {
                        onComplete(avatar);
                        Close();
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                }
            }

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void ManuallySelected(ApiAvatar avatar)
        {
            onComplete(avatar);
            Close();
        }
    }

    public class ManualAvatarSelector : EditorWindow
    {
        private Action<ApiAvatar> OnSelected;
        
        public static void AvatarSelector(Action<ApiAvatar> onSelected)
        {
            ManualAvatarSelector avatarPicker = GetWindow<ManualAvatarSelector>();
            avatarPicker.OnSelected = onSelected;
        }

        private string avatarID = "";
        
        private void OnGUI()
        {
            if (!VRChatApiToolsGUI.HandleLogin(this)) return;

            EditorGUILayout.BeginHorizontal();
            avatarID = EditorGUILayout.TextField("Avatar blueprint ID", avatarID);
            if (GUILayout.Button("Load Avatar"))
            {
                if (!VRChatApiTools.blueprintCache.ContainsKey(avatarID))
                    VRChatApiTools.FetchApiAvatar(avatarID);
            }
            
            EditorGUILayout.EndHorizontal();

            if (VRChatApiTools.blueprintCache.TryGetValue(avatarID, out ApiModel model))
            {
                ApiAvatar avatar = (ApiAvatar) model;
                EditorGUILayout.BeginHorizontal();
                if (VRChatApiTools.ImageCache.ContainsKey(avatarID))
                {
                    GUILayout.Box(VRChatApiTools.ImageCache[avatarID]);
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Name: ", avatar.name);
                EditorGUILayout.LabelField("Status: ", avatar.releaseStatus);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Select this avatar"))
                {
                    OnSelected(avatar);
                    Close();
                }
            }
        }
    }
}