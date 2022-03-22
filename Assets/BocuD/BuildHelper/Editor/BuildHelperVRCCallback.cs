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
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.BuildPipeline;
using static BocuD.VRChatApiTools.VRChatApiTools;
using static BocuD.BuildHelper.AutonomousBuilder;

namespace BocuD.BuildHelper.Editor
{
    /*TODO: use [PostProcessBuildAttribute(0)] public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject) {}
     in combination with IVRCSDKBuildRequestedCallback instead of only relying on one; using PostProcessBuildAttribute lets us verify
     if the build was actually succesful
    */
    public class BuildHelperVRCCallback : IVRCSDKBuildRequestedCallback
    {
        public int callbackOrder => 10;

        public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
        {
            if (requestedBuildType == VRCSDKRequestedBuildType.Avatar) return true;

            BuildHelperData buildHelperData = BuildHelperData.GetDataBehaviour();
            BuildData buildData;
            
            if (buildHelperData)
            {
                buildData = buildHelperData.dataObject.CurrentBranch.buildData;
            }
            else
            {
                return true;
            }

            buildHelperData.PrepareExcludedGameObjects();
            buildHelperData.overrideContainers[buildHelperData.dataObject.currentBranch].ApplyStateChanges();
            
            //handle autonomous build version assignments
            AutonomousBuildData autonomousBuild = GetAutonomousBuildData();

            //make the build numbers match for autonomous builds
            if (autonomousBuild != null && autonomousBuild.activeBuild)
            {
                switch(autonomousBuild.progress)
                {
                    case AutonomousBuildData.Progress.PreInitialBuild:
                        buildData.SaveBuildTime();
                        buildData.CurrentPlatformBuildData().buildVersion = buildData.GetLatestBuild().buildVersion + 1;
                        break;
                    
                    case AutonomousBuildData.Progress.PreSecondaryBuild:
                        buildData.SaveBuildTime();
                        buildData.CurrentPlatformBuildData().buildVersion = buildData.GetLatestBuild().buildVersion;
                        break;
                }
            }
            else //autonomous builder is not active: determine what the build number should be
            {
                //todo: implement the new ask every time option
                switch (BuildHelperEditorPrefs.BuildNumberMode)
                {
                    //On Build
                    case 0:
                        //if our current platform is the one with the newest build, just increment the build number again
                        if (buildData.CurrentPlatformBuildData().buildVersion >=
                            buildData.GetLatestBuild().buildVersion)
                        {
                            buildData.SaveBuildTime();
                            buildData.CurrentPlatformBuildData().buildVersion++;
                        }
                        else
                        {
                            PlatformBuildInfo latestBuild = buildData.GetLatestBuild();
                            double minutesSinceLastBuild = (DateTime.Now - latestBuild.BuildTime).TotalMinutes;

                            switch (BuildHelperEditorPrefs.PlatformSwitchMode)
                            {
                                //ask
                                case 0:
                                    int newBuild = EditorUtility.DisplayDialogComplex("Build Helper",
                                        $"The last build on this branch was for a different platform. " +
                                        $"If you made any changes to the scene since the last build, (build {latestBuild.buildVersion}, {(long)minutesSinceLastBuild} minutes ago for {latestBuild.platform}) " +
                                        $"you should probably mark this build as a new build.\n\n" +
                                        $"Increment build number: This build will be marked as build {latestBuild.buildVersion + 1}. Keep in mind that this will make this build count as a new version for World Update Detection, so you should reupload for {latestBuild.platform} as well.\n\n" +
                                        $"Match build number: This build will be marked as the {CurrentPlatform()} version of build {latestBuild.buildVersion}.",
                                        "Increment", "Cancel", "Match");

                                    switch (newBuild)
                                    {
                                        //new build
                                        case 0:
                                            buildData.CurrentPlatformBuildData().buildVersion =
                                                buildData.GetLatestBuild().buildVersion + 1;
                                            break;
                                        //cancel
                                        case 1:
                                            return false;
                                        //equivalent build
                                        case 2:
                                            buildData.CurrentPlatformBuildData().buildVersion =
                                                buildData.GetLatestBuild().buildVersion;
                                            break;
                                    }

                                    break;

                                //always increment
                                case 1:
                                    buildData.CurrentPlatformBuildData().buildVersion =
                                        buildData.GetLatestBuild().buildVersion + 1;
                                    break;
                            }
                        }
                        break;
                    
                    //On upload
                    //if we didn't just upload, keep the same build number unless we have a platform switch (in which case it should be matched to the highest build number)
                    case 1:
                        if (buildData.justUploaded)
                        {
                            buildData.CurrentPlatformBuildData().buildVersion =
                                buildData.GetLatestBuild().buildVersion + 1;
                        }
                        else
                        {
                            if (buildData.CurrentPlatformBuildData().buildVersion <
                                buildData.GetLatestBuild().buildVersion)
                            {
                                buildData.CurrentPlatformBuildData().buildVersion =
                                    buildData.GetLatestBuild().buildVersion;
                            }
                        }

                        break;
                }
                
                buildData.SaveBuildTime();
                buildData.justUploaded = false;
            }

            buildHelperData.SaveToJSON();

            if (buildHelperData.dataObject.CurrentBranch.hasUdonLink && buildHelperData.linkedBehaviourGameObject != null)
            {
                BuildHelperUdon linkedUdon = buildHelperData.linkedBehaviourGameObject.GetUdonSharpComponent<BuildHelperUdon>();
                linkedUdon.UpdateProxy();
                linkedUdon.branchName = buildHelperData.dataObject.CurrentBranch.name;
                linkedUdon.buildNumber = buildData.CurrentPlatformBuildData().buildVersion;
                linkedUdon.buildDate = buildData.CurrentPlatformBuildData().BuildTime;
                linkedUdon.ApplyProxyModifications();
            } 
            else if (!buildHelperData.dataObject.CurrentBranch.hasUdonLink)
            {
                Scene currentScene = SceneManager.GetActiveScene();
                List<BuildHelperUdon> foundBehaviours = new List<BuildHelperUdon>();

                foreach (GameObject obj in currentScene.GetRootGameObjects())
                {
                    BuildHelperUdon[] behaviours = obj.GetUdonSharpComponentsInChildren<BuildHelperUdon>();
                    foundBehaviours.AddRange(behaviours);
                }
                    
                if (foundBehaviours.Count > 0)
                {
                    foreach (BuildHelperUdon behaviour in foundBehaviours)
                    {
                        behaviour.enabled = false;
                        behaviour.gameObject.SetActive(false);
                    }
                }
            }

            return true;
        }
    }
    
    //todo: add option that verifies builds before updating build numbers in JSON file
    public static class BuildHelperBuildPostProcessor {
        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject) {
            Logger.Log("Detected successful build");
            Debug.Log( pathToBuiltProject );
        }
    }
}