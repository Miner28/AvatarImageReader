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
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BocuD.BuildHelper.Editor;
using UnityEditor;
using UnityEngine.Networking;
using VRC.Core;
using VRC.SDK3.Editor.Builder;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

namespace BocuD.BuildHelper
{
    using VRChatApiTools;
    
    public static class BuildHelperBuilder
    {
        public static string ExportAssetBundle()
        {
            bool buildTestBlocked = !VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene);
            
            if (!buildTestBlocked)
            {
                EnvConfig.ConfigurePlayerSettings();
                VRC_SdkBuilder.shouldBuildUnityPackage = false;
                AssetExporter.CleanupUnityPackageExport();
                VRC_SdkBuilder.PreBuildBehaviourPackaging();

                VRC_SdkBuilder.ExportSceneResource();
                string output = EditorPrefs.GetString("currentBuildingAssetBundlePath");
                
                //save last build information
                PlatformBuildInfo data = BuildHelperData.GetDataObject()?.CurrentBranch?.buildData?.CurrentPlatformBuildData();
                if (data != null)
                {
                    data.buildPath = output;
                    data.buildHash = VRChatApiTools.ComputeFileMD5(output);
                    
                    BuildHelperData.RunLastBuildChecks();
                }

                return output;
            }

            return "";
        }
        
        public static void ReloadExistingBuild(string path)
        {
            if (File.Exists(path))
            {
                File.SetLastWriteTimeUtc(path, DateTime.Now);
            }
            else
            {
                Logger.LogWarning($"Cannot find last built scene, please Rebuild.");
            }
        }

        public static void ReloadNewBuild(Action onSuccess = null)
        {
            ExportAssetBundle();
            onSuccess?.Invoke();
        }

        public static void TestExistingBuild(string path)
        {
            string actualLastBuild = EditorPrefs.GetString("lastVRCPath");
            
            EditorPrefs.SetString("lastVRCPath", path);
            //EditorPrefs.SetString("currentBuildingAssetBundlePath", UnityWebRequest.UnEscapeURL(deploymentUnit.buildPath));
            VRC_SdkBuilder.shouldBuildUnityPackage = false;
            VRC_SdkBuilder.RunLastExportedSceneResource();
            
            EditorPrefs.SetString("lastVRCPath", actualLastBuild);
        }

        public static void TestNewBuild()
        {
            ExportAssetBundle();
            VRC_SdkBuilder.RunLastExportedSceneResource();
        }

        public static void PublishLastBuild()
        {
            if (APIUser.CurrentUser.canPublishWorlds)
            {
                VRC_SdkBuilder.UploadLastExportedSceneBlueprint();
            }
            else
            {
                Logger.LogError("You need to be logged in to publish a world");
            }
        }

        public static void PublishNewBuild()
        {
            ExportAssetBundle();

            VRC_SdkBuilder.RunUploadLastExportedSceneBlueprint();
        }

        public static void PublishNewBuildAsync(VRChatApiTools.WorldInfo worldInfo = null, Action<VRChatApiTools.WorldInfo> onSucces = null)
        {
            string assetBundlePath = ExportAssetBundle();

            PublishWorldAsync(assetBundlePath, "", worldInfo, onSucces);
        }

        public static void PublishExistingBuild(DeploymentUnit deploymentUnit)
        {
            if (VRChatApiTools.FindPipelineManager().blueprintId != deploymentUnit.pipelineID)
            {
                if (EditorUtility.DisplayDialog("Deployment Manager",
                    "The blueprint ID for the selected build doesn't match the one on the scene descriptor. This can happen if the blueprint ID on the selected branch was changed after this build was published. While this build can still be uploaded, you will have to switch the blueprint ID on your scene descriptor to match that of the selected build. Are you sure you want to continue?",
                    "Yes", "No"))
                    BuildHelperWindow.ApplyPipelineID(deploymentUnit.pipelineID);
                else return;
            }

            EditorPrefs.SetString("lastVRCPath", deploymentUnit.filePath);
            EditorPrefs.SetString("currentBuildingAssetBundlePath", UnityWebRequest.UnEscapeURL(deploymentUnit.filePath));
            EditorPrefs.SetString("lastBuiltAssetBundleBlueprintID", deploymentUnit.pipelineID);
            AssetExporter.CleanupUnityPackageExport();
            VRCWorldAssetExporter.LaunchSceneBlueprintUploader();
        }
        
        public static void PublishWorldAsync(string assetbundlePath, string unityPackagePath, VRChatApiTools.WorldInfo worldInfo = null, Action<VRChatApiTools.WorldInfo> onSucces = null)
        {
            if (APIUser.CurrentUser.canPublishWorlds)
            {
                VRChatApiUploaderAsync uploaderAsync = new VRChatApiUploaderAsync();
                uploaderAsync.UseStatusWindow();

                uploaderAsync.uploadStatus.ConfirmButton("Ready for upload", "Start Upload", UploadTask, () => Logger.LogError("Upload was aborted"),
                    () =>
                    {
                        EditorGUILayout.LabelField("Target world: ");
                        if (worldInfo != null)
                        {
                            VRChatApiToolsGUI.DrawBlueprintInspector(worldInfo.blueprintID);
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Couldn't load world information");
                        }
                    });
                
                async void UploadTask()
                {
                    try
                    {
                        EditorApplication.LockReloadAssemblies();

                        await uploaderAsync.UploadWorld(assetbundlePath, unityPackagePath, worldInfo);

                        onSucces?.Invoke(worldInfo);

                        BranchStorageObject data = BuildHelperData.GetDataObject();
                        if (data != null) DeploymentManager.TrySaveBuild(data.CurrentBranch, assetbundlePath);
                    }
                    catch (Exception e)
                    {
                        uploaderAsync.OnError(e.Message, e.InnerException == null ? "" : e.InnerException.ToString());
                        Logger.LogError($"Upload Exception: {e.Message} {(e.InnerException == null ? "" : $"({e.InnerException.Message})")}");
                    }
                    finally
                    {
                        EditorApplication.UnlockReloadAssemblies();
                    }
                }
            }
            else
            {
                Logger.LogError("You need to be logged in to publish a world");
            }
        }
    }
}