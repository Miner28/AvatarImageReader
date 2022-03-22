#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BocuD.BuildHelper
{
    public class DeploymentManager
    {
        public static void TrySaveBuild(Branch branch, string buildPath, bool autonomousBuild = false)
        {
            if (!branch.hasDeploymentData) return;
            
            if (branch.deploymentData.deploymentPath == "") {
                Logger.LogWarning($"Deployment folder location for {branch.name} is not set, no published builds will be saved.");
                return;
            }
            
            if (!File.Exists(buildPath)) return; // Defensive check, normally the file should exist there given that a publish was just completed

            string deploymentFolder = Path.GetFullPath(Application.dataPath + branch.deploymentData.deploymentPath);
            
            if (Path.GetDirectoryName(buildPath).StartsWith(deploymentFolder))
            {
                Logger.Log("Not saving build as the published build was already located within the deployments folder. This probably means the published build was an existing (older) build.");
                return;
            }

            string backupFileName = ComposeBackupFileName(branch, buildPath, autonomousBuild);
            string backupPath = Path.Combine(new []{deploymentFolder, backupFileName});

            File.Copy(buildPath, backupPath);
            Logger.Log("Completed a backup: " + backupFileName);
        }
        
        private static string ComposeBackupFileName(Branch branch, string justPublishedFilePath, bool autonomousBuilder = false)
        {
            string buildDate = File.GetLastWriteTime(justPublishedFilePath).ToString("yyyy'-'MM'-'dd HH'-'mm'-'ss");
            string autoUploader = "";
            string buildNumber = "build" + branch.buildData.CurrentPlatformBuildData().buildVersion;
            string platform = VRChatApiTools.VRChatApiTools.CurrentPlatform().ToString();
            string gitHash = TryGetGitHashDiscardErrorsSilently();
            return $"[{buildDate}]_{autoUploader}{branch.branchID}_{buildNumber}_{branch.blueprintID}_{platform}_{gitHash}.vrcw";
        }
        
        private static string TryGetGitHashDiscardErrorsSilently()
        {
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "git",
                        WorkingDirectory = Application.dataPath,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        Arguments = "rev-parse --short HEAD"
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                string trimmedOutput = output.Trim().ToLowerInvariant();
                
                if (trimmedOutput.Length != 8)
                {
                    Logger.Log("Could not retrieve git hash: " + trimmedOutput);
                    return "@nohash";
                }

                return trimmedOutput;
            }
            catch (Exception e)
            {
                Logger.Log("Could not retrieve git hash: " + e.Message);
                return "@nohash";
            }
        }
    }
}

#endif