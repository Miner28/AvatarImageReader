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

#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRCSDK2;
using static BocuD.VRChatApiTools.VRChatApiTools;
using Debug = UnityEngine.Debug;

namespace BocuD.BuildHelper
{
    public class BuildHelperRuntime : MonoBehaviour
    {
        [SerializeField]private bool vrcSceneReady;
        [SerializeField]private RuntimeWorldCreation runtimeWorldCreation;

        [SerializeField]private BuildHelperData buildHelperBehaviour;
        [SerializeField]private BranchStorageObject buildHelperData;
        
        [SerializeField]private BuildHelperToolsMenu buildHelperToolsMenu;

        private void Start()
        {
            buildHelperBehaviour = BuildHelperData.GetDataBehaviour();
            
            if (buildHelperBehaviour)
                buildHelperData = buildHelperBehaviour.dataObject;
        }

        private int timeout = 10;
        private bool appliedChanges = false;
        private bool appliedImageChanges = false;
        
        private void Update()
        {
            if (!vrcSceneReady)
            {
                if (timeout > 0)
                {
                    if (GameObject.Find("VRCSDK"))
                    {
                        runtimeWorldCreation = GameObject.Find("VRCSDK").GetComponent<RuntimeWorldCreation>();
                    
                        Logger.Log("Found RuntimeWorldCreation component, initialising BuildHelperRuntime");

                        //apply saved camera position
                        GameObject.Find("VRCCam").transform.SetPositionAndRotation(buildHelperData.CurrentBranch.camPos, buildHelperData.CurrentBranch.camRot);

                        //modify sdk upload panel to add world helper menu
                        Transform worldPanel = runtimeWorldCreation.transform.GetChild(0).GetChild(0).GetChild(1);
                        RectTransform worldPanelRect = worldPanel.GetComponent<RectTransform>();
                        worldPanelRect.offsetMin = new Vector2(-250, 0);

                        GameObject RuntimeTools = (GameObject) Instantiate(Resources.Load("RuntimeTools"),
                            runtimeWorldCreation.transform.GetChild(0).GetChild(0));
                        RuntimeTools.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 335.5f);

                        buildHelperToolsMenu = RuntimeTools.GetComponent<BuildHelperToolsMenu>();

                        buildHelperToolsMenu.saveCamPosition.isOn = buildHelperData.CurrentBranch.saveCamPos;
                        buildHelperToolsMenu.uniqueCamPosition.isOn = buildHelperData.CurrentBranch.uniqueCamPos;

                        vrcSceneReady = true;
                    }

                    timeout--;
                }
                else
                {
                    Application.logMessageReceived -= Log;
                    Destroy(gameObject);
                }
            }

            if (vrcSceneReady)
            {
                if (runtimeWorldCreation.titleText.text != "Configure World") return;
                
                if (!appliedChanges)
                {
                    if (buildHelperData.CurrentBranch.nameChanged)
                        runtimeWorldCreation.blueprintName.text = buildHelperData.CurrentBranch.editedName;
                    if (buildHelperData.CurrentBranch.descriptionChanged)
                        runtimeWorldCreation.blueprintDescription.text =
                            buildHelperData.CurrentBranch.editedDescription;
                    if (buildHelperData.CurrentBranch.capacityChanged)
                        runtimeWorldCreation.worldCapacity.text = buildHelperData.CurrentBranch.editedCap.ToString();
                    if (buildHelperData.CurrentBranch.tagsChanged)
                        runtimeWorldCreation.userTags.text =
                            TagListToTagString(buildHelperData.CurrentBranch.editedTags);
                    
                    appliedChanges = true;
                    return;
                }

                if (!appliedChanges && buildHelperData.CurrentBranch.HasVRCDataChanges())
                {
                    runtimeWorldCreation.blueprintName.text = buildHelperData.CurrentBranch.editedName;
                    runtimeWorldCreation.blueprintDescription.text = buildHelperData.CurrentBranch.editedDescription;
                    runtimeWorldCreation.worldCapacity.text = buildHelperData.CurrentBranch.editedCap.ToString();
                    runtimeWorldCreation.userTags.text = DisplayTags(buildHelperData.CurrentBranch.editedTags);
                }

                if (!appliedImageChanges && buildHelperData.CurrentBranch.vrcImageHasChanges)
                {
                    runtimeWorldCreation.shouldUpdateImageToggle.isOn = true;
                    buildHelperToolsMenu.imageSourceDropdown.value = 1;
                    buildHelperToolsMenu.imageSourceDropdown.onValueChanged.Invoke(1);
                    buildHelperToolsMenu.OnFileSelected(Application.dataPath + "/Resources/BuildHelper/" + buildHelperBehaviour.sceneID + '_' + buildHelperData.CurrentBranch.branchID + "-edit.png");
                    appliedImageChanges = true;
                }
            }
        }
        
        private void Log(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Log && logString.Contains("Starting upload"))
            {
                buildHelperData.CurrentBranch.saveCamPos = buildHelperToolsMenu.saveCamPosition.isOn;
                buildHelperData.CurrentBranch.uniqueCamPos = buildHelperToolsMenu.uniqueCamPosition.isOn;
            
                if (buildHelperData.CurrentBranch.saveCamPos)
                {
                    GameObject vrcCam = GameObject.Find("VRCCam");
                    if (vrcCam)
                    {
                        if (buildHelperData.CurrentBranch.uniqueCamPos)
                        {
                            buildHelperData.CurrentBranch.camPos = vrcCam.transform.position;
                            buildHelperData.CurrentBranch.camRot = vrcCam.transform.rotation;
                        }
                        else
                        {
                            foreach (Branch b in buildHelperData.branches)
                            {
                                if (b.uniqueCamPos) continue;

                                b.camPos = vrcCam.transform.position;
                                b.camRot = vrcCam.transform.rotation;
                            }
                            
                            buildHelperData.CurrentBranch.camPos = vrcCam.transform.position;
                            buildHelperData.CurrentBranch.camRot = vrcCam.transform.rotation;
                        }
                    }
                }
            }
        
            if (type == LogType.Log && logString.Contains("Asset bundle upload succeeded"))
            {
                if (buildHelperData.CurrentBranch == null)
                {
                    Logger.LogError("Build Helper data object doesn't exist, skipping build data update");
                    return;
                }

                //ExtractWorldImage();
                ExtractBuildInfo();
                DeploymentManager.TrySaveBuild(buildHelperData.CurrentBranch, EditorPrefs.GetString("lastVRCPath"));
            }

            if (type == LogType.Log && logString.Contains("Image upload succeeded"))
            {
                buildHelperData.CurrentBranch.vrcImageHasChanges = false;
                buildHelperData.CurrentBranch.vrcImageWarning = "";
                buildHelperData.branches[buildHelperData.currentBranch] = buildHelperData.CurrentBranch;
                buildHelperBehaviour.SaveToJSON();
            }
        }

        private void ExtractBuildInfo()
        {
            Logger.Log("Detected succesful upload");

            RuntimeEditorPrefs.SaveUploadData(buildHelperData.CurrentBranch);
        }
        
        private static string TagListToTagString(IEnumerable<string> input)
        {
            return input.Aggregate("", (current, s) => current + s.ReplaceFirst("author_tag_", "") + " ");
        }

        private void OnEnable()
        {
            Application.logMessageReceived += Log;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= Log;
        }
    }

    public static class RuntimeEditorPrefs
    {
        public const string branchIDPath = "BuildHelperRuntimeBranchID";
        public const string uploadVersionPath = "BuildHelperRuntimeUploadVersion";
        public const string uploadTimePath = "BuildHelperRuntimeUploadTime";
        public const string blueprintIDPath = "BuildHelperRuntimeBlueprintID";
        
        public static void SaveUploadData(Branch branch)
        {
            EditorPrefs.SetString(branchIDPath, branch.branchID);
            EditorPrefs.SetInt(uploadVersionPath, branch.buildData.CurrentPlatformBuildData().buildVersion);
            EditorPrefs.SetString(uploadTimePath, DateTime.Now.ToString(CultureInfo.InvariantCulture));
            EditorPrefs.SetString(blueprintIDPath, FindPipelineManager().blueprintId);
        }

        public static async void ExtractUploadData()
        {
            string branchID = EditorPrefs.GetString(branchIDPath);
            int uploadVersion = EditorPrefs.GetInt(uploadVersionPath);
            DateTime.TryParse(EditorPrefs.GetString(uploadTimePath), out DateTime uploadTime);
            string blueprintID = EditorPrefs.GetString(blueprintIDPath);

            if (string.IsNullOrEmpty(branchID)) return;
            
            BuildHelperData data = BuildHelperData.GetDataBehaviour();

            if (data == null) return;
            
            Branch targetBranch = data.dataObject?.branches.FirstOrDefault(b => b.branchID == branchID);

            if (targetBranch == null) return;

            BlueprintInfo info = targetBranch.ToWorldInfo();
            info.blueprintID = blueprintID;
            
            await data.OnSuccesfulPublish(targetBranch, info, uploadTime, uploadVersion);

            //clear everything so this doesn't get loaded again
            EditorPrefs.SetString(branchIDPath, "");
            EditorPrefs.SetInt(uploadVersionPath, -1);
            EditorPrefs.SetString(uploadTimePath, "");
            EditorPrefs.SetString(branchIDPath, "");
        }
    }

    //when exiting playmode, read and clear editorprefs that are set in playmode
    [InitializeOnLoadAttribute]
    public static class RuntimePlaymodeStateWatcher
    {
        static RuntimePlaymodeStateWatcher()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;
        }

        private static void LogPlayModeState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RuntimeEditorPrefs.ExtractUploadData();
            }
        }
    }
}
#endif