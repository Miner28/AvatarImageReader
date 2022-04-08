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
            string modelName = "";
            string releaseStatus = "";
            string authorName = "";
            bool isWorld = false;
            
            switch (model)
            {
                case ApiWorld world:
                    modelName = world.name;
                    releaseStatus = world.releaseStatus;
                    authorName = world.authorName;
                    isWorld = true;
                    break;
                
                case ApiAvatar avatar:
                    modelName = avatar.name;
                    releaseStatus = avatar.releaseStatus;
                    authorName = avatar.authorName;
                    break;
                
                default:
                    if (model == null)
                        EditorGUILayout.HelpBox("Null ApiModel passed to DrawBlueprintInspector", MessageType.Warning);
                    else
                        EditorGUILayout.HelpBox("Non world or avatar ApiModel passed to DrawBlueprintInspector", MessageType.Warning);
                    
                    return;
            }
            
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(108));
            EditorGUILayout.BeginHorizontal();

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

            EditorGUILayout.LabelField("Release Status: " + releaseStatus);
            EditorGUILayout.LabelField("Author: " + authorName);

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
    
    public class BlueprintPicker : EditorWindow
    {
        private ApiModel selection;
        private Action<ApiModel> onComplete;
        private Type targetType;
        private string targetName;
        private bool confirm;

        public static void BlueprintSelector<T>(Action<T> onComplete, bool confirm = false, string initialSelection = null) where T : ApiModel
        {
            Type type = typeof(T);
            BlueprintPicker blueprintPicker;
            
            if (type == typeof(ApiWorld))
            {
                blueprintPicker = GetWindow<BlueprintPicker>();
                blueprintPicker.titleContent = new GUIContent("World Picker");
                blueprintPicker.targetName = "World";
            } 
            else if (type == typeof(ApiAvatar))
            {
                blueprintPicker = GetWindow<BlueprintPicker>();
                blueprintPicker.titleContent = new GUIContent("Avatar Picker");
                blueprintPicker.targetName = "Avatar";
            }
            else
            {
                throw new ArgumentException("The specified blueprint type is not supported");
            }

            blueprintPicker.onComplete = m => onComplete((T)m);
            blueprintPicker.targetType = type;
            blueprintPicker.minSize = new Vector2(640, 400);
            blueprintPicker.confirm = confirm;
            if (initialSelection != null &&
                VRChatApiTools.blueprintCache.TryGetValue(initialSelection, out ApiModel model))
            {
                blueprintPicker.selection = model;
            }
            blueprintPicker.UpdateListContent();
        }

        private void OnGUI()
        {
            //close on domain reload as type is not serialized
            if (targetType == null) Close();
            
            if (!VRChatApiToolsGUI.HandleLogin(this, false)) return;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{APIUser.CurrentUser.displayName}'s Uploaded {targetName}s", EditorStyles.boldLabel);

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
                    if (targetName == "Avatar")
                    {
                        ManualBlueprintSelector.BlueprintSelector<ApiAvatar>(OnSelected);
                    } 
                    else if (targetName == "World")
                    {
                        ManualBlueprintSelector.BlueprintSelector<ApiWorld>(OnSelected);
                    }
                }
                
                if (GUILayout.Button("Refresh", GUILayout.Width(120)))
                {
                    VRChatApiTools.ClearCaches();
                }
            }

            EditorGUILayout.EndHorizontal();

            RenderListContents();
            
            if (confirm)
            {
                EditorGUILayout.LabelField("Current selection:");
                if (selection == null)
                {
                    EditorGUILayout.HelpBox($"No {targetName} is currently selected", MessageType.Info);
                }
                else
                {
                    VRChatApiToolsGUI.DrawBlueprintInspector(selection, true, () =>
                    {
                        if (GUILayout.Button($"Select this {targetName}"))
                        {
                            onComplete(selection);
                            Close();
                        }
                    });
                }
            }
        }

        private Vector2 listScroll;
        private string searchString = "";
        private List<ApiModel> displayedBlueprints = new List<ApiModel>();

        private void RenderListContents()
        {
            if (VRChatApiTools.uploadedWorlds == null || VRChatApiTools.uploadedAvatars == null) return;
            
            GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
            EditorGUILayout.LabelField("Search ", GUILayout.Width(75));

            searchString = GUILayout.TextField(searchString, GUI.skin.FindStyle("ToolbarSeachTextField"));
            UpdateListContent();

            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
            {
                // Remove focus if cleared
                searchString = "";
                GUI.FocusControl(null);
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            if (displayedBlueprints.Count > 0)
            {
                foreach (ApiModel m in displayedBlueprints)
                {
                    VRChatApiToolsGUI.DrawBlueprintInspector(m, false, () =>
                    {
                        if (GUILayout.Button($"Select {targetName}", GUILayout.Width(140)))
                        {
                            OnSelected(m);
                        }
                    });
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void UpdateListContent()
        {
            if (targetType == typeof(ApiWorld) && VRChatApiTools.uploadedWorlds != null)
            {
                displayedBlueprints = VRChatApiTools.uploadedWorlds.OrderByDescending(x => x.updated_at)
                    .Where(w => w.name.IndexOf(searchString, StringComparison.InvariantCultureIgnoreCase) >= 0).Cast<ApiModel>().ToList();
            }
            else if (targetType == typeof(ApiAvatar) && VRChatApiTools.uploadedAvatars != null)
            {
                displayedBlueprints = VRChatApiTools.uploadedAvatars.OrderByDescending(x => x.updated_at)
                    .Where(a => a.name.IndexOf(searchString, StringComparison.InvariantCultureIgnoreCase) >= 0).Cast<ApiModel>().ToList();
            }
            else
            {
                displayedBlueprints = new List<ApiModel>();
            }
        }

        private void OnSelected(ApiModel model)
        {
            if (confirm)
            {
                selection = model;
            }
            else
            {
                onComplete(model);
                Close();
            }
        }
    }

    public class ManualBlueprintSelector : EditorWindow
    {
        private Action<ApiModel> OnSelected;
        private Type type;
        public static void BlueprintSelector<T>(Action<T> onSelected) where T : ApiModel
        {
            ManualBlueprintSelector blueprintPicker;
            Type t = typeof(T);

            if (t == typeof(ApiWorld))
            {
                blueprintPicker = GetWindow<ManualBlueprintSelector>();
                blueprintPicker.titleContent = new GUIContent("Manual World Selector");
            } 
            else if (t == typeof(ApiAvatar))
            {
                blueprintPicker = GetWindow<ManualBlueprintSelector>();
                blueprintPicker.titleContent = new GUIContent("Manual Avatar Selector");
            }
            else
            {
                throw new ArgumentException("The specified blueprint type is not supported");
            }

            blueprintPicker.type = t;
            blueprintPicker.OnSelected = model => onSelected((T)model);
            blueprintPicker.minSize = new Vector2(500, 150);
            blueprintPicker.maxSize = blueprintPicker.minSize;
        }

        private string blueprintID = "";
        private bool ranFetch = false;
        
        private void OnGUI()
        {
            //close on domain reload as type is not serialized
            if(type == null) Close();
            
            if (!VRChatApiToolsGUI.HandleLogin(this, false)) return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Blueprint ID", GUILayout.Width(100));
            string oldID = blueprintID;
            blueprintID = EditorGUILayout.TextField(blueprintID);
            if (oldID != blueprintID) ranFetch = false;

            bool valid = Regex.IsMatch(blueprintID, VRChatApiTools.world_regex) && type == typeof(ApiWorld) ||
                         Regex.IsMatch(blueprintID, VRChatApiTools.avatar_regex) && type == typeof(ApiAvatar);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Load", GUILayout.Width(60)))
                {
                    if (!VRChatApiTools.blueprintCache.ContainsKey(blueprintID))
                    {
                        if (blueprintID.StartsWith("wrld"))
                            VRChatApiTools.FetchApiWorld(blueprintID);
                        else
                            VRChatApiTools.FetchApiAvatar(blueprintID);
                    }

                    ranFetch = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (ranFetch)
            {
                if (VRChatApiTools.blueprintCache.TryGetValue(blueprintID, out ApiModel model))
                {
                    VRChatApiToolsGUI.DrawBlueprintInspector(model, true, () =>
                    {
                        if (GUILayout.Button("Select this blueprint"))
                        {
                            OnSelected(model);
                            Close();
                        }
                    });
                }
            }
        }
    }
}