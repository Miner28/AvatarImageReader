/* MIT License
 Copyright (c) 2021 BocuD (github.com/BocuD)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BocuD.VRChatApiTools;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase.Editor;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI;
using static BocuD.VRChatApiTools.VRChatApiTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace BocuD.BuildHelper.Editor
{
    public class BuildHelperWindow : EditorWindow
    {
        private GUIStyle styleHelpBox;
        private GUIStyle styleBox;

        private Texture2D _iconGitHub;
        private Texture2D _iconVRChat;
        private Texture2D _iconCloud;
        private Texture2D _iconBuild;
        private Texture2D _iconSettings;

        private Dictionary<string, Texture2D> modifiedWorldImages = new Dictionary<string, Texture2D>();
        
        public const string version = "v1.0.0";

        private Vector2 scrollPosition;
        private bool settings;
        private bool dirty;

        private PipelineManager pipelineManager;

        [MenuItem("Window/VR Build Helper")]
        public static void ShowWindow()
        {
            BuildHelperWindow window = GetWindow<BuildHelperWindow>();
            window.titleContent = new GUIContent("VR Build Helper");
            window.minSize = new Vector2(550, 650);
            window.Show();
        }

        private void OnEnable()
        {
            buildHelperBehaviour = BuildHelperData.GetDataBehaviour();
            pipelineManager = FindObjectOfType<PipelineManager>();
            BuildHelperData.RunLastBuildChecks();

            if (buildHelperBehaviour)
            {
                buildHelperData = buildHelperBehaviour.dataObject;

                InitBranchList();
            }
        }

        private void OnGUI()
        {
            if (styleBox == null) InitializeStyles();
            if (_iconVRChat == null) GetUIAssets();

            if (BuildPipeline.isBuildingPlayer) return;

            DrawBanner();

            if (DrawSettings()) return;

            if (buildHelperBehaviour == null)
            {
                OnEnable();

                if (buildHelperBehaviour == null)
                {
                    EditorGUILayout.HelpBox("Build Helper has not been set up in this scene.", MessageType.Info);

                    if (GUILayout.Button("Set up Build Helper in this scene"))
                    {
                        if(FindObjectOfType<VRCSceneDescriptor>())
                            ResetData();
                        else
                        {
                            if (EditorUtility.DisplayDialog("Build Helper",
                                "The scene currently does not contain a scene descriptor. For VR Build Helper to work, a scene descriptor needs to be present. Should VR Build Helper create one automatically?",
                                "Yes", "No"))
                            {
                                CreateSceneDescriptor();
                                ResetData();
                            }
                            else
                            {
                                ResetData();
                            }
                        }
                    }
                    else return;
                }
            }

            if (buildHelperData == null)
            {
                OnEnable();
            }
            
            DrawUpgradeUI();
            
            buildHelperDataSO.Update();
            branchList.DoLayoutList();
            buildHelperDataSO.ApplyModifiedProperties();

            if (buildHelperData.branches.Length == 0)
            {
                GUIStyle welcomeLabel = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fontSize = 20 };
                GUIStyle textArea = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
                
                EditorGUILayout.BeginVertical("Helpbox");
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Welcome to VR Build Helper", welcomeLabel, GUILayout.Height(23));
                EditorGUILayout.LabelField("To get started, click the '+' button to create a new branch. For documentation, please visit the wiki on GitHub.", textArea);
                EditorGUILayout.Space(2);
                if (GUILayout.Button("Open Wiki"))
                {
                    Application.OpenURL("https://github.com/BocuD/VRBuildHelper/wiki/Getting-Started");
                }
                EditorGUILayout.EndVertical();
            }

            if (buildHelperData.currentBranch >= buildHelperData.branches.Length)
                buildHelperData.currentBranch = 0;

            DrawSwitchBranchButton();

            DrawBranchUpgradeUI();

            PipelineChecks();

            if (SceneChecks()) return;

            if (branchList.index != -1 && buildHelperData.branches.Length > 0)
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none,
                    GUI.skin.verticalScrollbar);

                DrawBranchEditor();

                GUILayout.EndScrollView();

                DisplayBuildButtons();
            }
        }

        private bool DrawSettings()
        {
            if (!settings) return false;

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { richText = true };
            EditorGUILayout.LabelField("<b>VR Build Helper Options</b>", labelStyle);

            if (buildHelperBehaviour != null)
            {
                EditorGUILayout.BeginVertical("Helpbox");
                EditorGUILayout.LabelField("<b>Scene Options</b>", labelStyle);
                
                if (buildHelperBehaviour.gameObject.hideFlags == HideFlags.None)
                {
                    EditorGUILayout.HelpBox("The VRBuildHelper Data object is currently not hidden.",
                        MessageType.Warning);
                    if (GUILayout.Button("Hide VRBuildHelper Data object"))
                    {
                        buildHelperBehaviour.gameObject.hideFlags = HideFlags.HideInHierarchy;
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }
                else
                {
                    if (GUILayout.Button("Show VRBuildHelper Data object (Not recommended)"))
                    {
                        buildHelperBehaviour.gameObject.hideFlags = HideFlags.None;
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }
                
                if (GUILayout.Button("Remove VRBuildHelper from this scene"))
                {
                    bool confirm = EditorUtility.DisplayDialog("Build Helper",
                        "Are you sure you want to remove Build Helper from this scene? All stored information will be lost permanently.",
                        "Yes",
                        "Cancel");

                    if (confirm)
                    {
                        buildHelperBehaviour = BuildHelperData.GetDataBehaviour();

                        if (buildHelperBehaviour != null)
                        {
                            buildHelperBehaviour.DeleteJSON();
                            DestroyImmediate(buildHelperBehaviour.gameObject);
                        }
                    }
                }
                EditorGUILayout.EndVertical();
                GUILayout.Space(10);
            }

            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUILayout.LabelField("<b>Global VR Build Helper Options</b>", labelStyle);

            EditorGUILayout.BeginHorizontal();
            BuildHelperEditorPrefs.AutoSave = EditorGUILayout.Toggle(BuildHelperEditorPrefs.AutoSave, GUILayout.Width(15));
            EditorGUILayout.LabelField("Auto save");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            bool asyncPublishTemp = BuildHelperEditorPrefs.UseAsyncPublish;
            BuildHelperEditorPrefs.UseAsyncPublish = EditorGUILayout.Toggle(BuildHelperEditorPrefs.UseAsyncPublish, GUILayout.Width(15));
            if (asyncPublishTemp != BuildHelperEditorPrefs.UseAsyncPublish && BuildHelperEditorPrefs.UseAsyncPublish)
            {
                BuildHelperEditorPrefs.UseAsyncPublish = EditorUtility.DisplayDialog("Build Helper",
                    "Async publishing is a new feature of VRChat Api Tools that lets you build and publish your world without entering playmode. This may speed up your workflow significantly depending on how large your project is. It should already fully work as expected, but has not undergone as much testing as the rest of VR Build Helper. Do you want to use Async Publishing?",
                    "Enable", "Keep disabled");
            }
            
            EditorGUILayout.LabelField("Use Async Publisher (beta)");
            EditorGUILayout.EndHorizontal();

            GUIContent[] buildNumberModes =
            {
                new GUIContent("On build", "The build number will be incremented on every new build"),
                new GUIContent("On upload", "The build number will only be incremented after every upload")
            };
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Increment build number", GUILayout.Width(150));
            GUILayout.FlexibleSpace();
            BuildHelperEditorPrefs.BuildNumberMode = GUILayout.Toolbar(BuildHelperEditorPrefs.BuildNumberMode, buildNumberModes, GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();
            
            GUIContent[] platformSwitchModes =
            {
                new GUIContent("For every build", "Build Helper will always ask you if it should match build numbers between PC and Android right after switching between them."),
                new GUIContent("Only after switching", "Build Helper will always increment the build number when doing a new build. The Autonomous publisher should be used if you want version detection to work.")
            }; 
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("<i>When building for Android</i>", labelStyle);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ask to match build number", GUILayout.Width(250));
            GUILayout.FlexibleSpace();
            BuildHelperEditorPrefs.PlatformSwitchMode = GUILayout.Toolbar(BuildHelperEditorPrefs.PlatformSwitchMode, platformSwitchModes);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            if (GUILayout.Button("Close"))
            {
                settings = false;
            }

            EditorGUILayout.LabelField($"VR Build Helper {version}");
            EditorGUILayout.LabelField($"<i>Made with ♡ by BocuD</i>", new GUIStyle(EditorStyles.label) {richText = true});
            return true;
        }

        private void DrawSwitchBranchButton()
        {
            if (pipelineManager == null) return;
            if (buildHelperData.branches.Length <= 0 || branchList.index == -1) return;
            
            Rect buttonRectBase = GUILayoutUtility.GetLastRect();

            Rect buttonRect = new Rect(5, buttonRectBase.y, 250, EditorGUIUtility.singleLineHeight);

            bool buttonDisabled = true;
            if (buildHelperData.currentBranch != branchList.index)
            {
                buttonDisabled = false;
            }
            else if(buildHelperData.CurrentBranch.blueprintID != pipelineManager.blueprintId)
            {
                buttonDisabled = false;
            }

            EditorGUI.BeginDisabledGroup(buttonDisabled);
                
            if (GUI.Button(buttonRect, $"Switch to {buildHelperData.branches[branchList.index].name}"))
            {
                SwitchBranch(buildHelperBehaviour, branchList.index);
            }
            
            EditorGUI.EndDisabledGroup();
        }

        private void DrawUpgradeUI()
        {
            if (buildHelperBehaviour.sceneID != "") return;

            EditorGUILayout.HelpBox(
                "This scene still uses the non GUID based identifier for scene identification. You should consider upgrading using the button below.",
                MessageType.Warning);
            if (GUILayout.Button("Upgrade to GUID system"))
            {
                try
                {
                    buildHelperBehaviour.DeleteJSON();
                    buildHelperBehaviour.sceneID = BuildHelperData.GetUniqueID();
                    buildHelperBehaviour.SaveToJSON();
                    EditorSceneManager.SaveScene(buildHelperBehaviour.gameObject.scene);
                    Logger.Log($"Succesfully converted Build Helper data for scene {SceneManager.GetActiveScene().name} to GUID {buildHelperBehaviour.sceneID}");
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error occured while trying to convert data to GUID system: {e}");
                }
            }
        }

        private void DrawBranchUpgradeUI()
        {
            if (buildHelperData.CurrentBranch == null || buildHelperData.CurrentBranch.branchID != "") return;
            
            Branch currentBranch = buildHelperData.CurrentBranch;

            EditorGUILayout.HelpBox(
                "This branch still uses the non GUID based identifier for branches and deployment data identification. You should consider upgrading using the button below. Please keep in mind that this process may not transfer over all deployment data (if any is present) perfectly.", MessageType.Warning);
            if (GUILayout.Button("Upgrade to GUID system"))
            {
                try
                {
                    DeploymentManager.RefreshDeploymentDataLegacy(currentBranch);

                    currentBranch.branchID = BuildHelperData.GetUniqueID();

                    if (string.IsNullOrEmpty(currentBranch.deploymentData.initialBranchName)) return;

                    foreach (DeploymentUnit unit in currentBranch.deploymentData.units)
                    {
                        //lol idk how this is part of udongraphextensions but suuure i'll use it here :P
                        string newFilePath = unit.filePath.ReplaceLast(
                            currentBranch.deploymentData.initialBranchName,
                            currentBranch.branchID);
                        Debug.Log($"Renaming {unit.filePath} to {newFilePath}");

                        File.Move(unit.filePath, newFilePath);
                    }

                    currentBranch.deploymentData.initialBranchName = "unused";
                    TrySave();
                    Logger.Log($"Succesfully converted branch '{currentBranch}' to GUID system");
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error occured while trying to convert deployment data to GUID system: {e}");
                }
            }
        }
        
        private void PipelineChecks()
        {
            pipelineManager = FindObjectOfType<PipelineManager>();

            if (buildHelperData.CurrentBranch == null) return;

            if (pipelineManager != null)
            {
                //dumb check to prevent buildhelper from throwing an error when it doesn't need to
                if (buildHelperData.CurrentBranch.blueprintID.Length > 1)
                {
                    if (pipelineManager.blueprintId != buildHelperData.CurrentBranch.blueprintID)
                    {
                        EditorGUILayout.HelpBox(
                            "The scene descriptor blueprint ID currently doesn't match the branch blueprint ID. VR Build Helper will not function properly.",
                            MessageType.Error);
                        if (GUILayout.Button("Auto fix"))
                        {
                            ApplyPipelineID(buildHelperData.CurrentBranch.blueprintID);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "To use VR Build Helper you need a Scene Decriptor in the scene. Please add a VRC Scene Descriptor.",
                    MessageType.Error);
                
                GUIContent autoFix = new GUIContent("Auto fix", "Create a new GameObject containing Scene Descriptor and Pipeline Manager components");
                
                if (GUILayout.Button(autoFix))
                {
                    CreateSceneDescriptor();
                    ApplyPipelineID(buildHelperData.CurrentBranch.blueprintID);
                }
            }
        }
        
        private void CreateSceneDescriptor()
        {
            GameObject sceneDescriptorObject = new GameObject("Scene Descriptor");
            sceneDescriptorObject.AddComponent<VRCSceneDescriptor>();
            sceneDescriptorObject.AddComponent<PipelineManager>();
            pipelineManager = sceneDescriptorObject.GetComponent<PipelineManager>();
        }

        private bool SceneChecks()
        {
            bool sceneIssues = !UpdateLayers.AreLayersSetup() || !UpdateLayers.IsCollisionLayerMatrixSetup();
            
            // if (!UpdateLayers.AreLayersSetup())
            // {
            //     if (GUILayout.Button("Setup Layers for VRChat", GUILayout.Width(172)))
            //     {
            //         if (EditorUtility.DisplayDialog("Build Helper",
            //             "This will set up all VRChat reserved layers. Custom layers will be moved down the layer list. Any GameObjects currently using custom layers will have to be reassigned. Are you sure you want to continue?",
            //             "Yes", "No"))
            //             UpdateLayers.SetupEditorLayers();
            //     }
            // }
            //
            // if (!UpdateLayers.IsCollisionLayerMatrixSetup())
            // {
            // if (GUILayout.Button("Set Collision Matrix", GUILayout.Width(172)))
            // {
            //     if(EditorUtility.DisplayDialog("Build Helper",
            //         "This will set up the Collision Matrix according to VRChats requirements. Are you sure you want to continue?",
            //         "Yes", "No"))
            //     {
            //         UpdateLayers.SetupCollisionLayerMatrix();
            //     }
            // }
            // }

            if (sceneIssues)
            {
                EditorGUILayout.HelpBox(
                    "The current project either has Layer or Collision Matrix issues. You should open the VRChat SDK Control Panel to fix these issues, or have Build Helper fix them automatically.",
                    MessageType.Warning);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Open VRChat Control Panel"))
                {
                    VRCSettings.ActiveWindowPanel = 1;
                    GetWindow<VRCSdkControlPanel>();
                }
                
                if (!UpdateLayers.AreLayersSetup())
                {
                    if (GUILayout.Button("Setup Layers for VRChat", GUILayout.Width(172)))
                    {
                        UpdateLayers.SetupEditorLayers();
                    }
                }

                EditorGUI.BeginDisabledGroup(!UpdateLayers.AreLayersSetup());
                if (!UpdateLayers.IsCollisionLayerMatrixSetup())
                {
                    if (GUILayout.Button("Set Collision Matrix", GUILayout.Width(172)))
                    {
                        UpdateLayers.SetupCollisionLayerMatrix();
                    }
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();
            }

            return sceneIssues;
        }
        
        public static void SwitchBranch(BuildHelperData data, int targetBranch)
        {
            BranchStorageObject storageObject = data.dataObject;
            
            //prevent indexoutofrangeexception
            if (storageObject.currentBranch < storageObject.branches.Length && storageObject.currentBranch > -1)
            {
                //reverse override container state
                if (storageObject.branches[storageObject.currentBranch].hasOverrides)
                    data.overrideContainers[storageObject.currentBranch].ResetStateChanges();
            }

            storageObject.currentBranch = targetBranch;
            data.PrepareExcludedGameObjects();

            data.SaveToJSON();

            if (storageObject.branches.Length > targetBranch)
            {
                if (storageObject.branches[storageObject.currentBranch].hasOverrides)
                    data.overrideContainers[storageObject.currentBranch].ApplyStateChanges();

                ApplyPipelineID(storageObject.CurrentBranch.blueprintID);
            }
            else if(storageObject.branches.Length == 0)
            {
                ApplyPipelineID("");
            }
        }

        public static void ApplyPipelineID(string blueprintID)
        {
            if (FindObjectOfType<VRCSceneDescriptor>())
            {
                VRCSceneDescriptor sceneDescriptor = FindObjectOfType<VRCSceneDescriptor>();
                PipelineManager pipelineManager = sceneDescriptor.GetComponent<PipelineManager>();

                pipelineManager.blueprintId = "";
                pipelineManager.completedSDKPipeline = false;

                EditorUtility.SetDirty(pipelineManager);
                EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                sceneDescriptor.apiWorld = null;
                
                pipelineManager.blueprintId = blueprintID;
                pipelineManager.completedSDKPipeline = true;

                EditorUtility.SetDirty(pipelineManager);
                EditorSceneManager.MarkSceneDirty(pipelineManager.gameObject.scene);
                EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);

                if (pipelineManager.blueprintId == "") return;
                
                ApiWorld world = API.FromCacheOrNew<ApiWorld>(pipelineManager.blueprintId);
                world.Fetch(null,
                    c => sceneDescriptor.apiWorld = c.Model as ApiWorld,
                    c =>
                    {
                        if (c.Code == 404)
                        {
                            Logger.LogError($"[<color=blue>API</color>] Could not load world {pipelineManager.blueprintId} because it didn't exist.");
                            ApiCache.Invalidate<ApiWorld>(pipelineManager.blueprintId);
                        }
                        else
                            Logger.LogError($"[<color=blue>API</color>] Could not load world {pipelineManager.blueprintId} because {c.Error}");
                    });
                sceneDescriptor.apiWorld = world;
            }
        }

        private bool deploymentEditor, gameObjectOverrides;

        private void DrawBranchEditor()
        {
            Branch selectedBranch = buildHelperData.CurrentBranch;

            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>Branch Editor</b>", styleRichTextLabel);

            EditorGUI.BeginChangeCheck();

            selectedBranch.name = EditorGUILayout.TextField("Branch name:", selectedBranch.name);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(true);
            selectedBranch.blueprintID = EditorGUILayout.TextField("Blueprint ID:", selectedBranch.blueprintID);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Change", GUILayout.Width(55)))
            {
                //spawn editor window
                BlueprintIDEditor.SpawnEditor(buildHelperBehaviour, selectedBranch);
            }

            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) TrySave();

            DrawVRCWorldEditor(selectedBranch);

            DrawGameObjectEditor(selectedBranch);
            DrawDeploymentEditorPreview(selectedBranch);
            DrawUdonLinkEditor(selectedBranch);

            GUILayout.FlexibleSpace();

            DisplayBuildInformation(selectedBranch);

            DrawBuildVersionWarnings(selectedBranch);

            EditorGUILayout.Space();
        }

        private void DrawGameObjectEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");
            selectedBranch.hasOverrides = EditorGUILayout.Toggle("GameObject Overrides", selectedBranch.hasOverrides);
            if (selectedBranch.hasOverrides) gameObjectOverrides = EditorGUILayout.Foldout(gameObjectOverrides, "");
            if (EditorGUI.EndChangeCheck())
            {
                TrySave();
            }

            if (gameObjectOverrides && selectedBranch.hasOverrides)
            {
                EditorGUILayout.HelpBox(
                    "GameObject overrides are rules that can be set up for a branch to exclude GameObjects from builds for that or other branches. Exclusive GameObjects are only included on branches which have them added to the exclusive list. Excluded GameObjects are excluded for branches that have them added.",
                    MessageType.Info);

                _overrideContainer = buildHelperBehaviour.overrideContainers[buildHelperData.currentBranch];

                if (currentGameObjectContainerIndex != buildHelperData.currentBranch) InitGameObjectContainerLists();
                if (exclusiveGameObjectsList == null) InitGameObjectContainerLists();
                if (excludedGameObjectsList == null) InitGameObjectContainerLists();

                buildHelperDataSO.Update();

                exclusiveGameObjectsList.DoLayoutList();
                excludedGameObjectsList.DoLayoutList();

                buildHelperDataSO.ApplyModifiedProperties();
            }

            GUILayout.EndVertical();
        }


        private Vector2 deploymentScrollArea;
        private bool doneScan = false;

        private void DrawDeploymentEditorPreview(Branch selectedBranch)
        {
            GUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            selectedBranch.hasDeploymentData = EditorGUILayout.Toggle("Deployment Manager", selectedBranch.hasDeploymentData);
            if (EditorGUI.EndChangeCheck())
            {
                TrySave();
            }

            EditorGUI.BeginDisabledGroup(!selectedBranch.hasDeploymentData);
            if (GUILayout.Button("Open Deployment Manager", GUILayout.Width(200)))
            {
                DeploymentManagerEditor.OpenDeploymentManager(buildHelperBehaviour, buildHelperData.currentBranch);
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selectedBranch.hasDeploymentData)
            {
                EditorGUILayout.BeginHorizontal();
                deploymentEditor = EditorGUILayout.Foldout(deploymentEditor, "");
                if (deploymentEditor)
                {
                    if (GUILayout.Button("Force Refresh", GUILayout.Width(100)))
                    {
                        DeploymentManager.RefreshDeploymentData(selectedBranch);
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (deploymentEditor)
                {
                    if (selectedBranch.deploymentData.deploymentPath == "")
                    {
                        EditorGUILayout.HelpBox(
                            "The Deployment Manager automatically saves uploaded builds so you can revisit or reupload them later.\nTo start using the Deployment Manager, please set a location to store uploaded builds.",
                            MessageType.Info);
                        if (GUILayout.Button("Set deployment path..."))
                        {
                            string selectedFolder = EditorUtility.OpenFolderPanel("Set deployment folder location...",
                                Application.dataPath, "Deployments");
                            if (!string.IsNullOrEmpty(selectedFolder))
                            {
                                if (selectedFolder.StartsWith(Application.dataPath))
                                {
                                    selectedBranch.deploymentData.deploymentPath =
                                        selectedFolder.Substring(Application.dataPath.Length);
                                }
                                else
                                {
                                    Logger.LogError("Please choose a location within the Assets folder");
                                }
                            }
                        }
                        GUILayout.EndVertical();

                        return;
                    }

                    if (!doneScan)
                    {
                        DeploymentManager.RefreshDeploymentData(selectedBranch);
                        doneScan = true;
                    }
                    
                    deploymentScrollArea = EditorGUILayout.BeginScrollView(deploymentScrollArea);

                    if (selectedBranch.deploymentData.units.Length < 1)
                    {
                        EditorGUILayout.HelpBox(
                            "No builds have been saved yet. To save a build for this branch, upload your world.",
                            MessageType.Info);
                    }

                    bool pcUploadKnown = false, androidUploadKnown = false;

                    foreach (DeploymentUnit deploymentUnit in selectedBranch.deploymentData.units)
                    {
                        Color backgroundColor = GUI.backgroundColor;

                        bool isLive = false;

                        if (deploymentUnit.platform == Platform.Android)
                        {
                            if (selectedBranch.buildData.androidData.uploadVersion != -1)
                            {
                                DateTime androidUploadTime = selectedBranch.buildData.androidData.UploadTime;
                                if (Mathf.Abs((float) (androidUploadTime - deploymentUnit.buildDate).TotalSeconds) <
                                    300 &&
                                    !androidUploadKnown)
                                {
                                    androidUploadKnown = true;
                                    isLive = true;
                                }
                            }
                        }
                        else
                        {
                            if (selectedBranch.buildData.pcData.uploadVersion != -1)
                            {
                                DateTime pcUploadTime = selectedBranch.buildData.pcData.UploadTime;
                                if (Mathf.Abs((float) (pcUploadTime - deploymentUnit.buildDate).TotalSeconds) < 300 &&
                                    !pcUploadKnown)
                                {
                                    pcUploadKnown = true;
                                    isLive = true;
                                }
                            }
                        }

                        if (isLive) GUI.backgroundColor = new Color(0.2f, 0.92f, 0.2f);

                        GUILayout.BeginVertical("GroupBox");

                        GUI.backgroundColor = backgroundColor;

                        EditorGUILayout.BeginHorizontal();
                        GUIContent icon = EditorGUIUtility.IconContent(deploymentUnit.platform == Platform.Windows
                            ? "BuildSettings.Metro On"
                            : "BuildSettings.Android On");
                        EditorGUILayout.LabelField(icon, GUILayout.Width(20));
                        GUILayout.Label("Build " + deploymentUnit.buildNumber, GUILayout.Width(60));

                        EditorGUI.BeginDisabledGroup(true);
                        Rect fieldRect = EditorGUILayout.GetControlRect();
                        GUI.TextField(fieldRect, deploymentUnit.fileName);
                        EditorGUI.EndDisabledGroup();

                        GUIStyle selectButtonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 60};
                        if (GUILayout.Button("Select", selectButtonStyle))
                        {
                            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(
                                $"Assets/{selectedBranch.deploymentData.deploymentPath}/" + deploymentUnit.fileName);
                        }

                        EditorGUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }

                    EditorGUILayout.EndScrollView();
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawUdonLinkEditor(Branch selectedBranch)
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginVertical("Helpbox");

            EditorGUILayout.BeginHorizontal();
            selectedBranch.hasUdonLink = EditorGUILayout.Toggle("Udon Link", selectedBranch.hasUdonLink);
            EditorGUI.BeginDisabledGroup(!selectedBranch.hasUdonLink || buildHelperBehaviour.linkedBehaviour == null);
            if (GUILayout.Button("Open inspector", GUILayout.Width(200)))
            {
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                Selection.objects = new Object[] {buildHelperBehaviour.linkedBehaviourGameObject};
            }

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selectedBranch.hasUdonLink)
            {
                if (buildHelperBehaviour.linkedBehaviourGameObject != null)
                {
                    buildHelperBehaviour.linkedBehaviour = buildHelperBehaviour.linkedBehaviourGameObject
                        .GetUdonSharpComponent<BuildHelperUdon>();
                }

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                buildHelperDataSO.Update();
                EditorGUILayout.PropertyField(buildHelperDataSO.FindProperty("linkedBehaviour"));
                buildHelperDataSO.ApplyModifiedProperties();

                if (EditorGUI.EndChangeCheck())
                {
                    if (buildHelperBehaviour.linkedBehaviour == null)
                    {
                        buildHelperBehaviour.linkedBehaviourGameObject = null;
                    }
                    else buildHelperBehaviour.linkedBehaviourGameObject = buildHelperBehaviour.linkedBehaviour.gameObject;
                }

                if (buildHelperBehaviour.linkedBehaviourGameObject == null)
                {
                    if (GUILayout.Button("Create new", GUILayout.Width(100)))
                    {
                        GameObject buildHelperUdonGameObject = new GameObject("BuildHelperUdon");
                        buildHelperUdonGameObject.AddUdonSharpComponent<BuildHelperUdon>();
                        buildHelperBehaviour.linkedBehaviourGameObject = buildHelperUdonGameObject;
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.HelpBox(
                        "There is no BuildHelperUdon behaviour selected for this scene right now.\nSelect an existing behaviour or create a new one.",
                        MessageType.Info);
                }
                else EditorGUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                if (selectedBranch.hasUdonLink)
                {

                }

                TrySave();
            }
        }

        private bool editMode;
        private bool editModeChanges;
        private string tempName;
        private string tempDesc;
        private int tempCap;
        private bool applying;
        private Branch applyBranch;
        private List<string> tempTags;

        private void DrawVRCWorldEditor(Branch branch)
        {
            GUILayout.BeginVertical("Helpbox");

            GUILayout.BeginHorizontal();
            GUILayout.Label("VRChat World Editor");
            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            if (GUILayout.Button("Force Refresh", GUILayout.Width(100)))
            {
                VRChatApiToolsEditor.RefreshData();
            }

            EditorGUI.EndDisabledGroup();
            GUILayout.EndHorizontal();

            ApiWorld apiWorld = null;
            
            bool apiWorldLoaded = true;
            bool isNewWorld = false;
            bool loadError = false;

            if (branch.blueprintID != "")
            {
                if (!blueprintCache.TryGetValue(branch.blueprintID, out ApiModel model))
                {
                    if (!invalidBlueprints.Contains(branch.blueprintID))
                        FetchApiWorld(branch.blueprintID);
                    else isNewWorld = true;
                }
                else
                {
                    apiWorld = (ApiWorld)model;
                }

                if (invalidBlueprints.Contains(branch.blueprintID))
                {
                    loadError = true;
                    EditorGUILayout.HelpBox(
                        "Couldn't load world information. This can happen if the blueprint ID is invalid, or if the world was deleted.",
                        MessageType.Error);
                    apiWorldLoaded = false;
                }
                else if (!isNewWorld && model == null && !Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Loading world information...");
                    apiWorldLoaded = false;
                }
            }
            else
            {
                isNewWorld = true;
                apiWorldLoaded = false;
            }

            if (isNewWorld) branch.remoteExists = false;
            else if (apiWorldLoaded) branch.remoteExists = true;

            GUIStyle styleRichTextLabelBig = new GUIStyle(GUI.skin.label)
                { richText = true, fontSize = 20, wordWrap = true };

            if (loadError)
            {
                GUILayout.Label("Unknown VRChat World", styleRichTextLabelBig);
            }
            else if (isNewWorld)
            {
                GUILayout.Label("Unpublished VRChat World", styleRichTextLabelBig);
            }
            else
            {
                string headerText = branch.cachedName;

                if (apiWorldLoaded)
                {
                    CacheWorldInfo(branch, apiWorld);
                    headerText = $"<b>{apiWorld.name}</b> by {apiWorld.authorName}";
                }

                GUILayout.Label(headerText, styleRichTextLabelBig);
            }

            float imgWidth = 170;
            float width = position.width - imgWidth - 20;

            GUIStyle worldInfoStyle = new GUIStyle(GUI.skin.label)
                { wordWrap = true, fixedWidth = width, richText = true };

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(width));

            string displayName = "", displayDesc = "", displayCap = "", displayTags = "", displayRelease = "";

            //draw world editor
            if (!editMode)
            {
                if (isNewWorld)
                {
                    displayName = $"<color=gray>{branch.editedName}</color>";
                    displayDesc = $"<color=gray>{branch.editedDescription}</color>";
                    displayCap = $"<color=gray>{branch.editedCap}</color>";
                    displayTags = $"<color=gray>{DisplayTags(branch.editedTags)}</color>";
                    displayRelease = "<b>New world</b>";
                }
                else if (apiWorld != null)
                {
                    displayName = branch.nameChanged
                        ? $"<color=yellow>{branch.editedName}</color>"
                        : apiWorld.name;
                    displayDesc = branch.descriptionChanged
                        ? $"<color=yellow>{branch.editedDescription}</color>"
                        : apiWorld.description;
                    displayCap = branch.capacityChanged
                        ? $"<color=yellow>{branch.editedCap}</color>"
                        : apiWorld.capacity.ToString();
                    displayTags = branch.tagsChanged
                        ? $"<color=yellow>{DisplayTags(branch.editedTags)}</color>"
                        : DisplayTags(apiWorld.publicTags);
                    displayRelease = apiWorld.releaseStatus;
                }

                EditorGUILayout.LabelField("Name: " + displayName, worldInfoStyle);
                EditorGUILayout.LabelField("Description: " + displayDesc, worldInfoStyle);
                EditorGUILayout.LabelField("Capacity: " + displayCap, worldInfoStyle);
                EditorGUILayout.LabelField("Tags: " + displayTags, worldInfoStyle);

                EditorGUILayout.LabelField("Release: " + displayRelease, worldInfoStyle);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                tempName = EditorGUILayout.TextField("Name:", tempName);
                EditorGUILayout.LabelField("Description:");
                tempDesc = EditorGUILayout.TextArea(tempDesc,
                    new GUIStyle(EditorStyles.textArea) { wordWrap = true });
                tempCap = EditorGUILayout.IntField("Capacity:", tempCap);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tags:");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add tag", GUILayout.Width(70)))
                {
                    tempTags.Add("author_tag_new tag");
                }

                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < tempTags.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    //don't expose the user to author_tag_
                    tempTags[i] = "author_tag_" + EditorGUILayout.TextField(tempTags[i].Substring(11));

                    if (GUILayout.Button("Delete", GUILayout.Width(70)))
                    {
                        tempTags.RemoveAt(i);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (EditorGUI.EndChangeCheck()) editModeChanges = true;

                EditorGUILayout.LabelField(
                    apiWorldLoaded ? "Release: " + apiWorld.releaseStatus : "Release: " + branch.cachedRelease,
                    worldInfoStyle);
            }

            if (branch.nameChanged || branch.descriptionChanged || branch.capacityChanged || branch.tagsChanged ||
                branch.vrcImageHasChanges)
            {
                GUIStyle infoStyle = new GUIStyle(EditorStyles.helpBox) { fixedWidth = width, richText = true };
                string changesWarning = branch.vrcImageWarning +
                                        "<color=yellow>Your changes will be applied automatically with the next upload. You can also apply changes directly by clicking [Apply Changes to World].</color>";
                EditorGUILayout.LabelField(changesWarning, infoStyle);
            }

            GUILayout.EndVertical();

            //draw image
            if (branch.vrcImageHasChanges)
            {
                if (!modifiedWorldImages.ContainsKey(branch.branchID))
                {
                    modifiedWorldImages.Add(branch.branchID,
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            ImageTools.GetImageAssetPath(buildHelperBehaviour.sceneID, branch.branchID)));
                }

                if (modifiedWorldImages.ContainsKey(branch.branchID))
                {
                    GUILayout.BeginVertical();
                    GUILayout.Box(modifiedWorldImages[branch.branchID], GUILayout.Width(imgWidth),
                        GUILayout.Height(imgWidth / 4 * 3));
                    GUILayout.Space(8);
                    GUILayout.EndVertical();
                }
            }
            else
            {
                if (apiWorldLoaded && !loadError)
                {
                    if (ImageCache.ContainsKey(apiWorld.id))
                    {
                        GUILayout.BeginVertical();
                        GUILayout.Box(ImageCache[apiWorld.id], GUILayout.Width(imgWidth),
                            GUILayout.Height(imgWidth / 4 * 3));
                        GUILayout.Space(8);
                        GUILayout.EndVertical();
                    }
                }
            }

            GUILayout.EndHorizontal();

            //draw buttons
            EditorGUILayout.BeginHorizontal();
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fixedWidth = 100, richText = true };

            if (!editMode)
            {
                if (GUILayout.Button("Edit", buttonStyle))
                {
                    editMode = true;
                    editModeChanges = false;

                    if (!isNewWorld)
                    {
                        tempName = branch.nameChanged ? branch.editedName : apiWorld.name;
                        tempDesc = branch.descriptionChanged ? branch.editedDescription : apiWorld.description;
                        tempCap = branch.capacityChanged ? branch.editedCap : apiWorld.capacity;
                        tempTags = branch.tagsChanged ? branch.editedTags.ToList() : apiWorld.publicTags.ToList();
                    }
                    else
                    {
                        tempName = branch.editedName;
                        tempDesc = branch.editedDescription;
                        tempCap = branch.editedCap;
                        tempTags = branch.editedTags.ToList();
                    }
                }

                buttonStyle.fixedWidth = 170;
                if (GUILayout.Button("View on VRChat website", buttonStyle))
                {
                    Application.OpenURL($"https://vrchat.com/home/world/{branch.blueprintID}");
                }

                if (!isNewWorld && apiWorldLoaded && branch.HasVRCDataChanges())
                {
                    EditorGUI.BeginDisabledGroup(applying);

                    if (GUILayout.Button(
                            applying ? "Applying changes..." : "<color=yellow>Apply Changes to World</color>",
                            buttonStyle))
                    {
                        if (EditorUtility.DisplayDialog("Applying Changes to VRChat World",
                                "Applying changes will immediately apply any changes you made here without reuploading the world. Are you sure you want to continue?",
                                "Yes", "No"))
                        {
                            ApplyBranchChanges(branch, apiWorld);
                        }
                    }

                    EditorGUI.EndDisabledGroup();
                }
            }
            else
            {
                if (GUILayout.Button(editModeChanges ? "Save" : "Cancel", buttonStyle))
                {
                    editMode = false;

                    if (editModeChanges)
                    {
                        branch.editedName = tempName;
                        branch.editedDescription = tempDesc;
                        branch.editedCap = tempCap;
                        branch.editedTags = tempTags.ToList();

                        if (isNewWorld)
                        {
                            branch.nameChanged = branch.editedName != "New VRChat World";
                            branch.descriptionChanged = branch.editedDescription != "Fancy description for your world";
                            branch.capacityChanged = branch.editedCap != 16;
                            branch.tagsChanged = !branch.editedTags.SequenceEqual(new List<string>());
                        }
                        else
                        {
                            branch.nameChanged = branch.editedName != apiWorld.name;
                            branch.descriptionChanged = branch.editedDescription != apiWorld.description;
                            branch.capacityChanged = branch.editedCap != apiWorld.capacity;
                            branch.tagsChanged = !branch.editedTags.SequenceEqual(apiWorld.publicTags);
                        }

                        TrySave();
                    }
                }

                EditorGUI.BeginDisabledGroup(!editModeChanges && isNewWorld);
                if (GUILayout.Button(
                        editModeChanges
                            ? new GUIContent("Revert")
                            : new GUIContent("Revert All",
                                isNewWorld ? "There is nothing to revert to on new worlds." : ""), buttonStyle))
                {
                    if (!editModeChanges)
                    {
                        if (EditorUtility.DisplayDialog("Reverting all changes",
                                "You don't seem to have made any text changes while in edit mode. Clicking revert will reset all previously edited text to what is currently stored on VRChat's servers. Do you want to proceed?",
                                "Proceed", "Cancel"))
                        {
                            branch.editedName = branch.cachedName;
                            branch.editedDescription = branch.cachedDescription;
                            branch.editedCap = branch.cachedCap;
                            branch.editedTags = branch.cachedTags.ToList();

                            branch.nameChanged = false;
                            branch.descriptionChanged = false;
                            branch.capacityChanged = false;
                            branch.tagsChanged = false;

                            editMode = false;

                            TrySave();
                        }
                    }
                    else
                    {
                        editMode = false;

                        TrySave();
                    }
                }

                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Replace image", buttonStyle))
                {
                    string[] allowedFileTypes = { "png" };
                    imageBranch = branch;
                    NativeFilePicker.PickFile(OnImageSelected, allowedFileTypes);
                }

                if (branch.vrcImageHasChanges)
                {
                    if (GUILayout.Button("Revert image", buttonStyle))
                    {
                        branch.vrcImageHasChanges = false;
                        branch.vrcImageWarning = "";

                        modifiedWorldImages.Remove(branch.branchID);

                        string oldImagePath = ImageTools.GetImageAssetPath(buildHelperBehaviour.sceneID, branch.branchID);

                        Texture2D oldImage =
                            AssetDatabase.LoadAssetAtPath(oldImagePath, typeof(Texture2D)) as Texture2D;

                        if (oldImage != null)
                        {
                            AssetDatabase.DeleteAsset(oldImagePath);
                        }
                    }
                }

                if (apiWorldLoaded)
                {
                    Color temp = GUI.backgroundColor;
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Delete world", GUILayout.ExpandWidth(false)))
                    {
                        if (EditorUtility.DisplayDialog("Delete " + apiWorld.name + "?",
                                $"Are you sure you want to delete the world '{apiWorld.name}'? This cannot be undone.", "Delete",
                                "Cancel"))
                        {
                            branch.blueprintID = "";
                
                            branch.cachedName = "Unpublished VRChat world";
                            branch.cachedDescription = "";
                            branch.cachedCap = 16;
                            branch.cachedRelease = "private";
                            branch.cachedTags = new List<string>();

                            branch.nameChanged = false;
                            branch.descriptionChanged = false;
                            branch.capacityChanged = false;
                            branch.tagsChanged = false;

                            SwitchBranch(buildHelperBehaviour, Array.IndexOf(buildHelperData.branches, branch));

                            API.Delete<ApiWorld>(apiWorld.id);
                            
                            ClearCaches();
                        }
                        
                        editMode = false;
                        TrySave();
                    }

                    GUI.backgroundColor = temp;
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private async void ApplyBranchChanges(Branch branch, ApiWorld apiWorld)
        {
            applyBranch = branch;
            applying = true;

            apiWorld.name = branch.editedName;
            apiWorld.description = branch.editedDescription;
            apiWorld.capacity = branch.editedCap;
            apiWorld.tags = branch.editedTags.ToList();

            if (branch.vrcImageHasChanges)
            {
                VRChatApiUploaderAsync uploader = new VRChatApiUploaderAsync();
                uploader.UseStatusWindow();

                apiWorld.imageUrl = await uploader.UploadImage(apiWorld.imageUrl, GetFriendlyWorldFileName("Image", apiWorld, CurrentPlatform()), branch.overrideImagePath);
                branch.vrcImageHasChanges = false;
                
                uploader.OnUploadState(VRChatApiToolsUploadStatus.UploadState.finished);
            }

            apiWorld.Save(c =>
            {
                applyBranch.nameChanged = false;
                applyBranch.descriptionChanged = false;
                applyBranch.capacityChanged = false;
                applyBranch.tagsChanged = false;

                VRChatApiToolsEditor.RefreshData();
                applying = false;
                Logger.Log($"Succesfully applied changed to {branch.name}");
            }, c =>
            {
                EditorUtility.DisplayDialog("Build Helper",
                    $"Couldn't apply changes to target branch: {c.Error}", "Ok");

                VRChatApiToolsEditor.RefreshData();
                applying = false;
            });

            await Task.Delay(3000);
            
            ClearCaches();

            FetchApiWorld(branch.blueprintID);
            
            await Task.Delay(200);
            
            Repaint();
        }

        private void CacheWorldInfo(Branch branch, ApiWorld apiWorld)
        {
            bool localDataOutdated = false;

            if (branch.cachedName != apiWorld.name)
            {
                branch.cachedName = apiWorld.name;
                localDataOutdated = true;
            }

            if (branch.cachedDescription != apiWorld.description)
            {
                branch.cachedDescription = apiWorld.description;
                localDataOutdated = true;
            }

            if (branch.cachedCap != apiWorld.capacity)
            {
                branch.cachedCap = apiWorld.capacity;
                localDataOutdated = true;
            }

            if (!branch.cachedTags.SequenceEqual(apiWorld.publicTags))
            {
                branch.cachedTags = apiWorld.publicTags.ToList();
                localDataOutdated = true;
            }

            if (branch.cachedRelease != apiWorld.releaseStatus)
            {
                branch.cachedRelease = apiWorld.releaseStatus;
                localDataOutdated = true;
            }

            if (localDataOutdated)
            {
                if (branch.editedName == "notInitialised") branch.editedName = branch.cachedName;
                if (branch.editedDescription == "notInitialised") branch.editedDescription = branch.cachedDescription;
                if (branch.editedCap == -1) branch.editedCap = branch.cachedCap;
                if (branch.editedTags.Count == 0) branch.editedTags = branch.cachedTags.ToList();
            }

            if (localDataOutdated) TrySave();
        }

        private Branch imageBranch;

        private void OnImageSelected(string filePath)
        {
            if (File.Exists(filePath))
            {
                if (imageBranch != null)
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    Texture2D overrideImage = new Texture2D(2, 2);
                    
                    overrideImage.LoadImage(fileData); //..this will auto-resize the texture dimensions.
                    overrideImage.Apply();
                    
                    //check aspect ratio and resolution
                    imageBranch.vrcImageWarning = ImageTools.GenerateImageFeedback(overrideImage.width, overrideImage.height);

                    //resize image if needed (for some reason i can just upload 8k images to 
                    if (overrideImage.width != 1200 || overrideImage.height != 900)
                    {
                        overrideImage = ImageTools.Resize(overrideImage, 1200, 900);
                    }

                    //encode image as PNG
                    byte[] worldImagePNG = overrideImage.EncodeToPNG();

                    string dirPath = Application.dataPath + "/Resources/BuildHelper/";
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }
                    
                    //write image
                    File.WriteAllBytes(ImageTools.GetImagePath(buildHelperBehaviour.sceneID, imageBranch.branchID), worldImagePNG);

                    string savePath = ImageTools.GetImageAssetPath(buildHelperBehaviour.sceneID, imageBranch.branchID);

                    AssetDatabase.WriteImportSettingsIfDirty(savePath);
                    AssetDatabase.ImportAsset(savePath);

                    TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(savePath);
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = 2048;
                    EditorUtility.SetDirty(importer);
                    AssetDatabase.WriteImportSettingsIfDirty(savePath);

                    AssetDatabase.ImportAsset(savePath);

                    imageBranch.vrcImageHasChanges = true;
                    imageBranch.overrideImagePath = savePath;

                    editModeChanges = true;

                    TrySave();
                }
                else
                {
                    Logger.LogError("Target branch for image processor doesn't exist anymore, was it deleted?");
                }
            }
            else
            {
                Logger.LogError("Null filepath was passed to image processor, skipping process steps");
            }
        }
        
        private void DisplayBuildInformation(Branch branch)
        {
            BuildData buildData = branch.buildData;
            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };

            GUILayout.Label("<b>Build Information</b>", styleRichTextLabel);
            
            PlatformBuildInfo pcBuild = buildData.pcData;
            PlatformBuildInfo androidBuild = buildData.androidData;

            GUIContent build = new GUIContent(_iconBuild);
            GUIContent cloud = new GUIContent(_iconCloud);
            
            buildStatusStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                fixedWidth = 400,
                contentOffset = new Vector2(-12, 5)
            };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(build, GUILayout.Width(48), GUILayout.Height(48));
            EditorGUILayout.BeginVertical();
            DrawBuildInfoLine(Platform.Windows, pcBuild, false);
            DrawBuildInfoLine(Platform.Android, androidBuild, false); 
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(cloud, GUILayout.Width(48), GUILayout.Height(48));
            EditorGUILayout.BeginVertical();
            DrawBuildInfoLine(Platform.Windows, pcBuild, true);
            DrawBuildInfoLine(Platform.Android, androidBuild, true);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private GUIStyle buildStatusStyle;
        
        private void DrawBuildInfoLine(Platform platform, PlatformBuildInfo info, bool isUpload)
        {
            int ver = isUpload ? info.uploadVersion : info.buildVersion;
            bool hasTime = ver != -1;
            string time = isUpload
                ? hasTime ? info.UploadTime.ToString() : "Unknown"
                : hasTime ? info.BuildTime.ToString() : "Unknown";

            string tooltip = hasTime
                ? $"Build {ver}\n" +
                  (isUpload ? $"Uploaded at {time}" : $"Built at {time}\nBuild path: {info.buildPath}\nBuild hash: {info.buildHash}\n{(info.buildValid ? "Verified build" : "Couldn't verify build")}")
                : $"Couldn't find a last {(isUpload ? "upload" : "build")} for this platform";
            GUIContent content = new GUIContent(
                $"Last {platform} {(isUpload ? "upload" : "build")}: {(hasTime ? $"build {ver} ({time})" : "Unknown")}",
                tooltip);
            
            EditorGUILayout.LabelField(content, buildStatusStyle);
        }

        private static void DrawBuildVersionWarnings(Branch selectedBranch)
        {
            BuildData buildData = selectedBranch.buildData;

            if (buildData.pcData.uploadVersion != buildData.androidData.uploadVersion)
            {
                if (buildData.pcData.uploadVersion > buildData.androidData.uploadVersion)
                {
                    if (buildData.androidData.uploadVersion != -1)
                    {
                        EditorGUILayout.HelpBox(
                            "Your uploaded PC and Android builds currently don't match. The last uploaded PC build is newer than the last uploaded Android build. You should consider reuploading for Android to make them match.",
                            MessageType.Warning);
                    }
                }
                else
                {
                    if (buildData.pcData.uploadVersion != -1)
                    {
                        EditorGUILayout.HelpBox(
                            "Your uploaded PC and Android builds currently don't match. The last uploaded Android build is newer than the last uploaded PC build. You should consider reuploading for PC to make them match.",
                            MessageType.Warning);
                    }
                }
            }
            else
            {
                if (buildData.pcData.uploadVersion != -1 && buildData.androidData.uploadVersion != -1)
                {
                    EditorGUILayout.HelpBox(
                        "Your uploaded PC and Android builds match. Awesome!",
                        MessageType.Info);
                }
            }
        }

        private void DisplayBuildButtons()
        {
            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>Build Options</b>", styleRichTextLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Build options are unavailable in play mode.", MessageType.Error);
                return;
            }
            
            if (!VRChatApiToolsGUI.HandleLogin(this, false)) return;

            DrawBuildTargetSwitcher();

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            
            EditorGUILayout.BeginVertical("Helpbox");
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("<b>Local Testing</b>", styleRichTextLabel);
            EditorGUILayout.LabelField("Number of Clients", GUILayout.Width(140));
            VRCSettings.NumClients = EditorGUILayout.IntField(VRCSettings.NumClients, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("Force no VR", GUILayout.Width(140));
            VRCSettings.ForceNoVR = EditorGUILayout.Toggle(VRCSettings.ForceNoVR, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField(new GUIContent("Watch for changes",
                    "When enabled, launched VRChat clients will watch for new builds and reload the world when a new build is detected."),
                GUILayout.Width(140));
            VRCSettings.WatchWorlds = EditorGUILayout.Toggle(VRCSettings.WatchWorlds, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 140};

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Local test in VRChat");
            
            //Prevent local testing on Android

            bool localTestBlocked = CurrentPlatform() == Platform.Android;
            bool lastBuildBlocked = !CheckLastBuiltBranch();
            string lastBuildBlockedTooltip = $"Your last build for the current platform couldn't be found or the hash doesn't match the last {CurrentPlatform()} build for this branch.";
            
            EditorGUI.BeginDisabledGroup(localTestBlocked);

            GUIContent lastBuildTextTest = new GUIContent("Last Build",
                localTestBlocked
                    ? "Local testing is not supported for Android" : lastBuildBlocked ? lastBuildBlockedTooltip
                    : "Equivalent to the (Last build) Build & Test option in the VRChat SDK");
            
            EditorGUI.BeginDisabledGroup(lastBuildBlocked);
            if (GUILayout.Button(lastBuildTextTest, buttonStyle))
            {
                BuildHelperBuilder.TestExistingBuild(buildHelperData.CurrentBranch.buildData.CurrentPlatformBuildData().buildPath);
            }
            EditorGUI.EndDisabledGroup();

            GUIContent newBuildTextTest = new GUIContent("New Build",
                localTestBlocked ? "Local testing is not supported for Android"
                        : "Equivalent to the Build & Test option in the VRChat SDK");

            if (GUILayout.Button(newBuildTextTest, buttonStyle))
            {
                BuildHelperBuilder.TestNewBuild();
            }

            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Reload existing test clients");
            
            EditorGUI.BeginDisabledGroup(localTestBlocked);
            
            GUIContent lastBuildTextReload = new GUIContent("Last Build",
                localTestBlocked
                    ? "Local testing is not supported for Android" : lastBuildBlocked ? lastBuildBlockedTooltip
                    : "Equivalent to using the (Last build) Enable World Reload option in the VRChat SDK with number of clients set to 0");
            
            EditorGUI.BeginDisabledGroup(lastBuildBlocked);
            if (GUILayout.Button(lastBuildTextReload, buttonStyle))
            {
                BuildHelperBuilder.ReloadExistingBuild(buildHelperData.CurrentBranch.buildData.CurrentPlatformBuildData().buildPath);
            }
            EditorGUI.EndDisabledGroup();

            GUIContent newBuildTextReload = new GUIContent("New Build",
                localTestBlocked
                    ? "Local testing is not supported for Android"
                    : "Equivalent to using the Enable World Reload option in the VRChat SDK with number of clients set to 0");
            if (GUILayout.Button(newBuildTextReload, buttonStyle))
            {
                BuildHelperBuilder.ReloadNewBuild();
            }
            
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            

            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("<b>Publishing Options</b>", styleRichTextLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"<i>{(APIUser.IsLoggedIn ? "Currently logged in as " + APIUser.CurrentUser.displayName : "")}</i>", styleRichTextLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Publish to VRChat");

            EditorGUI.BeginDisabledGroup(lastBuildBlocked);
            
            GUIContent lastBuildPublish = new GUIContent("Last Build",
                lastBuildBlocked
                    ? lastBuildBlockedTooltip
                    : "Equivalent to (Last build) Build & Publish in the VRChat SDK");

            if (GUILayout.Button(lastBuildPublish, buttonStyle))
            {
                if (CheckLastBuiltBranch())
                {               
                    Branch targetBranch = buildHelperData.CurrentBranch;

                    bool canPublish = true;
                    if (!targetBranch.remoteExists && !targetBranch.HasVRCDataChanges() && BuildHelperEditorPrefs.UseAsyncPublish)
                    {
                        canPublish = EditorUtility.DisplayDialog("Build Helper",
                            $"You are about to publish a new world, but you haven't edited any world details. The async publisher doesn't enter playmode to let you edit world details, so your world will be uploaded as '{targetBranch.editedName}'. Do you want to continue?",
                            "Continue", "Cancel");
                    }

                    if (canPublish && CheckAccount(targetBranch))
                    {
                        if (BuildHelperEditorPrefs.UseAsyncPublish)
                        {
                            Branch b = buildHelperData.CurrentBranch;
                            BuildHelperBuilder.PublishWorldAsync(b.buildData.CurrentPlatformBuildData().buildPath, "", b.ToWorldInfo(), info =>
                            {
                                Task verify = buildHelperBehaviour.OnSuccesfulPublish(buildHelperData.CurrentBranch, info, DateTime.Now);
                            });
                        }
                        else BuildHelperBuilder.PublishLastBuild();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            GUIContent newBuildPublish = new GUIContent("New Build", "Equivalent to Build & Publish in the VRChat SDK");

            if (GUILayout.Button(newBuildPublish, buttonStyle))
            {
                Branch targetBranch = buildHelperData.CurrentBranch;
                
                bool canPublish = true;
                if (!targetBranch.remoteExists && !targetBranch.HasVRCDataChanges() && BuildHelperEditorPrefs.UseAsyncPublish)
                {
                    canPublish = EditorUtility.DisplayDialog("Build Helper",
                        $"You are about to publish a new world, but you haven't edited any world details. The async publisher doesn't enter playmode to let you edit world details, so your world will be uploaded as '{targetBranch.editedName}'. Do you want to continue?",
                        "Continue", "Cancel");
                }

                if (canPublish && CheckAccount(targetBranch))
                {
                    if (BuildHelperEditorPrefs.UseAsyncPublish)
                    {
                        BuildHelperBuilder.PublishNewBuildAsync(buildHelperData.CurrentBranch.ToWorldInfo(), info =>
                        {
                            Task verify = buildHelperBehaviour.OnSuccesfulPublish(buildHelperData.CurrentBranch, info, DateTime.Now);
                        });
                    }
                    else BuildHelperBuilder.PublishNewBuild();
                }
            }

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Autonomous Builder");

            GUIStyle autoButtonStyle = new GUIStyle(GUI.skin.button) {fixedWidth = 283};
            
            EditorGUI.BeginDisabledGroup(!BuildHelperEditorPrefs.UseAsyncPublish);
            GUIContent autonomousBuilderButton = new GUIContent("Build and publish for PC and Android",  BuildHelperEditorPrefs.UseAsyncPublish ? "Publish your world for both platforms simultaneously" : "To use the autonomous builder, please enable Async Publishing in settings");
            
            if (GUILayout.Button(autonomousBuilderButton, autoButtonStyle))
            {
                Branch targetBranch = buildHelperData.CurrentBranch;
                
                bool canPublish = true;
                if (!targetBranch.remoteExists && !targetBranch.HasVRCDataChanges())
                {
                    canPublish = EditorUtility.DisplayDialog("Build Helper",
                        $"You are about to publish a new world using the autonomous builder, but you haven't edited any world details. The autonomous builder doesn't enter playmode to let you edit world details, so your world will be uploaded as '{targetBranch.editedName}'. Do you want to continue?",
                        "Continue", "Cancel");
                }
                
                if (canPublish && CheckAccount(targetBranch))
                {
                    InitAutonomousBuild();
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        private static void DrawBuildTargetSwitcher()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Active Build Target: " + EditorUserBuildSettings.activeBuildTarget);
            
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 &&
                GUILayout.Button("Switch Build Target to Android", GUILayout.Width(200)))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher",
                    "Are you sure you want to switch your build target to Android? This could take a while.", "Confirm",
                    "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android &&
                GUILayout.Button("Switch Build Target to Windows", GUILayout.Width(200)))
            {
                if (EditorUtility.DisplayDialog("Build Target Switcher",
                    "Are you sure you want to switch your build target to Windows? This could take a while.", "Confirm",
                    "Cancel"))
                {
                    EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,
                        BuildTarget.StandaloneWindows64);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private bool CheckLastBuiltBranch()
        {
            return buildHelperData.CurrentBranch.buildData.CurrentPlatformBuildData().buildValid;
        }

        private static bool CheckAccount(Branch target)
        {
            if (target.blueprintID == "")
            {
                return true;
            }

            if (blueprintCache.TryGetValue(target.blueprintID, out ApiModel model))
            {
                if (APIUser.CurrentUser.id != ((ApiWorld)model).authorId)
                {
                    if (EditorUtility.DisplayDialog("Build Helper",
                        "The world author for the selected branch doesn't match the currently logged in user. Publishing will result in an error. Do you still want to continue?",
                        "Yes", "No"))
                    {
                        return true;
                    }

                    return false;
                }
                return true;
            }

            if (EditorUtility.DisplayDialog("Build Helper",
                "Couldn't verify the world author for the selected branch. Do you want to try publishing anyways?",
                "Yes", "No"))
            {
                return true;
            }

            return false;
        }

        private void InitAutonomousBuild()
        {
            if (buildHelperData.CurrentBranch.blueprintID == "")
            {
                if (EditorUtility.DisplayDialog("Build Helper",
                        "You are trying to use the autonomous builder with an unpublished build. Are you sure you want to continue?",
                        "Yes", "No"))
                {

                }
                else return;
            }
            else
            {
                if (!EditorUtility.DisplayDialog("Build Helper",
                        "Build Helper will initiate a build and publish cycle for both PC and mobile in succesion.",
                        "Proceed", "Cancel"))
                {
                    return;
                }
            }

            AutonomousBuilder.AutonomousBuildData buildInfo = new AutonomousBuilder.AutonomousBuildData
                {
                    initialTarget = CurrentPlatform(),
                    secondaryTarget = CurrentPlatform() == Platform.Windows ? Platform.Android : Platform.Windows,
                    progress = AutonomousBuilder.AutonomousBuildData.Progress.PreInitialBuild
                };
            
            WorldInfo worldInfo = buildHelperData.CurrentBranch.ToWorldInfo();

            buildInfo.worldInfo = worldInfo;
            
            AutonomousBuilder.StartAutonomousPublish(buildInfo);
        }

        private void ResetData()
        {
            BuildHelperData data = BuildHelperData.GetDataBehaviour();
            if (data != null)
            {
                DestroyImmediate(data.gameObject);
            }

            GameObject dataObj = new GameObject("BuildHelperData");

            buildHelperBehaviour = dataObj.AddComponent<BuildHelperData>();
            buildHelperData.branches = new Branch[0];
            buildHelperBehaviour.overrideContainers = new OverrideContainer[0];

            dataObj.AddComponent<BuildHelperRuntime>();
            dataObj.hideFlags = HideFlags.HideInHierarchy;
            dataObj.tag = "EditorOnly";
            buildHelperBehaviour.SaveToJSON();
            EditorSceneManager.SaveScene(buildHelperBehaviour.gameObject.scene);
            OnEnable();
        }

        private void OnDestroy()
        {
            Save();
        }

        private void TrySave()
        {
            if (BuildHelperEditorPrefs.AutoSave)
            {
                Save();
            }
            else
            {
                dirty = true;
            }
        }

        private void Save()
        {
            if (buildHelperData != null)
                buildHelperBehaviour.SaveToJSON();
            else Logger.LogError("Error while saving, Data Object not found");
        }

        #region Reorderable list initialisation

        private BuildHelperData buildHelperBehaviour;
        private BranchStorageObject buildHelperData;

        private SerializedObject buildHelperDataSO;
        private ReorderableList branchList;

        private void InitBranchList()
        {
            buildHelperDataSO = new SerializedObject(buildHelperBehaviour);

            branchList = new ReorderableList(buildHelperDataSO, buildHelperDataSO.FindProperty("dataObject").FindPropertyRelative("branches"), true,
                true, true, true)
            {
                drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "World branches"); },

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty property = branchList.serializedProperty.GetArrayElementAtIndex(index);

                    SerializedProperty branchName = property.FindPropertyRelative("name");
                    SerializedProperty worldID = property.FindPropertyRelative("blueprintID");
                    
                    Rect nameRect = new Rect(rect)
                        { y = rect.y + 1.5f, width = 110, height = EditorGUIUtility.singleLineHeight };
                    Rect blueprintIDRect = new Rect(rect)
                    {
                        x = 115, y = rect.y + 1.5f, width = EditorGUIUtility.currentViewWidth - 115,
                        height = EditorGUIUtility.singleLineHeight
                    };
                    Rect selectedRect = new Rect(rect)
                    {
                        x = EditorGUIUtility.currentViewWidth - 95, y = rect.y + 1.5f, width = 90,
                        height = EditorGUIUtility.singleLineHeight
                    };

                    EditorGUI.LabelField(nameRect, branchName.stringValue);
                    EditorGUI.LabelField(blueprintIDRect, worldID.stringValue);

                    if (buildHelperData.currentBranch == index)
                        EditorGUI.LabelField(selectedRect, "current branch");
                },
                onAddCallback = list =>
                {
                    Undo.RecordObject(buildHelperBehaviour, "Create new branch");
                    Branch newBranch = new Branch
                        { name = "new branch", buildData = new BuildData(), branchID = BuildHelperData.GetUniqueID() };
                    ArrayUtility.Add(ref buildHelperData.branches, newBranch);

                    OverrideContainer newContainer = new OverrideContainer
                        { ExclusiveGameObjects = new GameObject[0], ExcludedGameObjects = new GameObject[0] };
                    ArrayUtility.Add(ref buildHelperBehaviour.overrideContainers, newContainer);

                    list.index = Array.IndexOf(buildHelperData.branches, newBranch);
                    TrySave();
                },
                onRemoveCallback = list =>
                {
                    string branchName = buildHelperData.branches[list.index].name;
                    if (EditorUtility.DisplayDialog("Build Helper",
                            $"Are you sure you want to delete the branch '{branchName}'? This can not be undone.", "Yes",
                            "No"))
                    {
                        ArrayUtility.RemoveAt(ref buildHelperData.branches, list.index);
                    }

                    SwitchBranch(buildHelperBehaviour, 0);
                    list.index = 0;
                    TrySave();
                },
                
                index = buildHelperData.currentBranch
            };
        }

        private OverrideContainer _overrideContainer;
        private int currentGameObjectContainerIndex;
        private ReorderableList excludedGameObjectsList;
        private ReorderableList exclusiveGameObjectsList;

        private void InitGameObjectContainerLists()
        {
            if (!buildHelperBehaviour) return;
            
            //setup exclusive list
            exclusiveGameObjectsList = new ReorderableList(buildHelperDataSO,
                buildHelperDataSO.FindProperty("overrideContainers")
                    .GetArrayElementAtIndex(buildHelperData.currentBranch)
                    .FindPropertyRelative("ExclusiveGameObjects"), true,
                true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Exclusive GameObjects"),

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty property =
                        exclusiveGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(rect, property);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(buildHelperBehaviour, "Modify GameObject list");
                        TrySave();
                    }
                },
                onAddCallback = list =>
                {
                    Undo.RecordObject(buildHelperBehaviour, "Add GameObject to list");
                    ArrayUtility.Add(ref _overrideContainer.ExclusiveGameObjects, null);
                    TrySave();
                },
                onRemoveCallback = list =>
                {
                    Undo.RecordObject(buildHelperBehaviour, "Remove GameObject from list");

                    GameObject toRemove = _overrideContainer.ExclusiveGameObjects[exclusiveGameObjectsList.index];

                    bool existsInOtherList = false;

                    foreach (OverrideContainer container in buildHelperBehaviour.overrideContainers)
                    {
                        if (container == _overrideContainer) continue;
                        if (container.ExclusiveGameObjects.Contains(toRemove)) existsInOtherList = true;
                    }

                    if (!existsInOtherList) OverrideContainer.EnableGameObject(toRemove);

                    ArrayUtility.RemoveAt(ref _overrideContainer.ExclusiveGameObjects,
                        exclusiveGameObjectsList.index);
                    TrySave();
                }
            };

            //setup exclude list
            excludedGameObjectsList = new ReorderableList(buildHelperDataSO,
                buildHelperDataSO.FindProperty("overrideContainers")
                    .GetArrayElementAtIndex(buildHelperData.currentBranch)
                    .FindPropertyRelative("ExcludedGameObjects"), true,
                true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Excluded GameObjects"),

                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty property =
                        excludedGameObjectsList.serializedProperty.GetArrayElementAtIndex(index);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(rect, property);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(buildHelperBehaviour, "Modify GameObject list");
                        TrySave();
                    }
                },
                onAddCallback = list =>
                {
                    Undo.RecordObject(buildHelperBehaviour, "Add GameObject to list");
                    ArrayUtility.Add(ref _overrideContainer.ExcludedGameObjects, null);
                    TrySave();
                },
                onRemoveCallback = list =>
                {
                    GameObject toRemove = _overrideContainer.ExcludedGameObjects[excludedGameObjectsList.index];

                    Undo.RecordObject(buildHelperBehaviour, "Remove GameObject from list");

                    OverrideContainer.EnableGameObject(toRemove);

                    ArrayUtility.RemoveAt(ref _overrideContainer.ExcludedGameObjects, excludedGameObjectsList.index);
                    TrySave();
                }
            };

            currentGameObjectContainerIndex = buildHelperData.currentBranch;
        }

        #endregion
        #region Editor GUI Helper Functions

        private void InitializeStyles()
        {
            // EditorGUI
            styleHelpBox = new GUIStyle(EditorStyles.helpBox);
            styleHelpBox.padding = new RectOffset(0, 0, styleHelpBox.padding.top, styleHelpBox.padding.bottom + 3);

            // GUI
            styleBox = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(GUI.skin.box.padding.left * 2, GUI.skin.box.padding.right * 2,
                    GUI.skin.box.padding.top * 2, GUI.skin.box.padding.bottom * 2),
                margin = new RectOffset(0, 0, 4, 4)
            };
        }

        private void GetUIAssets()
        {
            _iconVRChat = Resources.Load<Texture2D>("Icons/VRChat-Emblem-32px");
            _iconGitHub = Resources.Load<Texture2D>("Icons/GitHub-Mark-32px");
            _iconCloud = Resources.Load<Texture2D>("Icons/Cloud-32px");
            _iconBuild = Resources.Load<Texture2D>("Icons/Build-32px");
            _iconSettings = Resources.Load<Texture2D>("Icons/Settings-32px");
        }

        private void DrawBanner()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUIStyle styleRichTextLabel = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>VR Build Helper</b>", styleRichTextLabel);

            GUILayout.FlexibleSpace();

            float iconSize = EditorGUIUtility.singleLineHeight;

            if (dirty && !BuildHelperEditorPrefs.AutoSave)
            {
                if (GUILayout.Button("Save Changes"))
                {
                    Save();
                }
            }

            GUIContent buttonVRChat = new GUIContent("", "VRChat");
            GUIStyle styleVRChat = new GUIStyle(GUI.skin.box);
            if (_iconVRChat != null)
            {
                buttonVRChat = new GUIContent(_iconVRChat, "VRChat");
                styleVRChat = GUIStyle.none;
            }

            if (GUILayout.Button(buttonVRChat, styleVRChat, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://vrchat.com/home/user/usr_3a5bf7e4-e569-41d5-b70a-31304fd8e0e8");
            }

            GUILayout.Space(iconSize / 4);

            GUIContent buttonGitHub = new GUIContent("", "Github");
            GUIStyle styleGitHub = new GUIStyle(GUI.skin.box);
            if (_iconGitHub != null)
            {
                buttonGitHub = new GUIContent(_iconGitHub, "Github");
                styleGitHub = GUIStyle.none;
            }

            if (GUILayout.Button(buttonGitHub, styleGitHub, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                Application.OpenURL("https://github.com/BocuD/VRBuildHelper");
            }

            GUILayout.Space(iconSize / 4);

            GUIContent buttonSettings = new GUIContent("", "Settings");
            GUIStyle styleSettings = new GUIStyle(GUI.skin.box);
            if (_iconSettings != null)
            {
                buttonSettings = new GUIContent(_iconSettings, "Settings");
                styleSettings = GUIStyle.none;
            }

            if (GUILayout.Button(buttonSettings, styleSettings, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
            {
                settings = true;
            }

            GUILayout.EndHorizontal();
        }

        #endregion
    }
}