using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDKBase.Editor;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace BocuD.VRChatApiTools
{
    public static class VRChatApiToolsEditor
    {
        /// <summary>
        /// Draws inspector for VRChat ApiAvatar from blueprint ID
        /// </summary>
        /// <param name="blueprintID">The blueprint ID for the avatar to be displayed</param>
        public static void DrawAvatarInspector(string blueprintID)
        {
            if (blueprintID.IsNullOrWhitespace()) return;
            if (!APIUser.IsLoggedIn)
            {
                VRChatApiToolsEditor.HandleLogin(null, false);
                return;
            }
            
            //already cached
            if (VRChatApiTools.avatarCache.TryGetValue(blueprintID, out ApiAvatar avatar))
            {
                DrawApiAvatarInspector(avatar);
            }
            else
            {
                //loading
                if (VRChatApiTools.currentlyFetchingAvatars.Contains(blueprintID))
                {
                    EditorGUILayout.LabelField("Loading avatar information...");
                }
                else
                {
                    //its not invalidated yet, so try loading
                    if (!VRChatApiTools.invalidAvatars.Contains(blueprintID))
                    {
                        VRChatApiTools.FetchApiAvatar(blueprintID);
                    }
                    //invalid avatar
                    else
                    {
                        EditorGUILayout.HelpBox("Couldn't load specified avatar.", MessageType.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Draws inspector for VRChat ApiAvatar
        /// </summary>
        /// <param name="avatar">Valid ApiAvatar to be drawn</param>
        public static void DrawApiAvatarInspector(ApiAvatar avatar)
        {
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
                        VRCSdkControlPanel controlPanel = EditorWindow.GetWindow<VRCSdkControlPanel>();
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
            if (!VRChatApiToolsEditor.HandleLogin(this)) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("VRChat Avatar List", EditorStyles.boldLabel);

            if (VRChatApiTools.uploadedAvatars == null)
            {
                VRChatApiTools.uploadedAvatars = new List<ApiAvatar>();

                EditorCoroutine.Start(VRChatApiTools.FetchUploadedData());
            }

            if (VRChatApiTools.fetchingAvatars != null)
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

            List<ApiAvatar> displayedAvatars =
                VRChatApiTools.uploadedAvatars.OrderByDescending(x => x.updated_at).ToList();

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
            if (!VRChatApiToolsEditor.HandleLogin(this)) return;

            EditorGUILayout.BeginHorizontal();
            avatarID = EditorGUILayout.TextField("Avatar blueprint ID", avatarID);
            if (GUILayout.Button("Load Avatar"))
            {
                if (!VRChatApiTools.avatarCache.ContainsKey(avatarID))
                    VRChatApiTools.FetchApiAvatar(avatarID);
            }
            
            EditorGUILayout.EndHorizontal();

            if (VRChatApiTools.avatarCache.TryGetValue(avatarID, out ApiAvatar avatar))
            {
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