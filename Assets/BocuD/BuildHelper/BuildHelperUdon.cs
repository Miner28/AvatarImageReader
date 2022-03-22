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
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace BocuD.BuildHelper
{
    [DefaultExecutionOrder(-1000)]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class BuildHelperUdon : UdonSharpBehaviour
    {
        [Header("Build Information")]
        public string branchName;
        public DateTime buildDate;
        public int buildNumber;

        [Header("Build Information Forwarding")]
        public bool setProgramVariable;
        public Component[] targetBehaviours;
        public string[] targetTypes;
        public string[] targetVariableNames;
        public int[] sourceEnum;

        [Header("Build Number Checking")]
        public bool checkVersion;
        public UdonBehaviour eventBehaviour;
        public string onVersionMatch;
        public string onVersionMismatch;
        public string onVersionTimeout;

        public bool allowUpdates;
        public bool sendNetworkedEvent;
        public bool sendToAll;
        public string onVersionMismatchRemote;
        public bool singleCallback;
        
        [Header("TMP Output")]
        public bool useTMP;
        public bool isUgui;
        public TextMeshPro tmp;
        public TextMeshProUGUI tmpUGui;

        private void Start()
        {
            if(checkVersion && Networking.IsMaster)
                UpdateMasterBuildNumber();
            
            if (setProgramVariable)
            {
                if (targetBehaviours != null && targetTypes != null && targetVariableNames != null && sourceEnum != null)
                {
                    for (int index = 0; index < targetBehaviours.Length; index++)
                    {
                        if (sourceEnum[index] == 0) continue;
                        if (targetBehaviours[index] == null) continue;
                        if (targetTypes[index] == null) continue;
                        if (targetVariableNames[index] == null) continue;
                        
                        Component component = targetBehaviours[index];
                        UdonBehaviour ub = (UdonBehaviour) component;
                        switch (sourceEnum[index])
                        {
                            //branch name
                            case 1:
                                ub.SetProgramVariable(targetVariableNames[index], branchName);
                                break;
                            
                            //build number
                            case 2:
                                object dataToWrite = buildNumber;
                                switch (targetTypes[index])
                                {
                                    case "System.int":
                                        dataToWrite = (int) dataToWrite;
                                        break;
                                    case "System.uint":
                                        dataToWrite = (uint) dataToWrite;
                                        break;
                                    case "System.long":
                                        dataToWrite = (long) dataToWrite;
                                        break;
                                    case "System.ulong":
                                        dataToWrite = (ulong) dataToWrite;
                                        break;
                                    case "System.short":
                                        dataToWrite = (short) dataToWrite;
                                        break;
                                    case "System.ushort":
                                        dataToWrite = (ushort) dataToWrite;
                                        break;
                                }
                                ub.SetProgramVariable(targetVariableNames[index], dataToWrite);
                                break;
                            
                            //build date
                            case 3:
                                ub.SetProgramVariable(targetVariableNames[index], buildNumber);
                                break;
                        }
                    }
                }
            }

            if (useTMP)
            {
                if (isUgui)
                {
                    if (tmpUGui != null)
                        tmpUGui.text = $"{branchName}\nBuild {buildNumber}\n{buildDate}";
                }
                else
                {
                    if (tmp != null) 
                        tmp.text = tmp.text = $"{branchName}\nBuild {buildNumber}\n{buildDate}";
                }
            }
        }
        
        public override void OnDeserialization()
        {
            if (!checkVersion) return;
            
            _HandleVersionDetection();
        }

        [UdonSynced] private int _masterBuildNumber = 0;

        //Default value for a (networked) int is zero, so if it never changes beyond zero we know there is probably a communication problem.
        //By having the synced value offset from the actual number we can still "encode" 0. (0 will get synced as 1 etc)
        public int MasterBuildNumber
        {
            set => _masterBuildNumber = value + 1;
            get => _masterBuildNumber - 1;
        }
        private int failedAttempts;

        public void _HandleVersionDetection()
        {
            if (_masterBuildNumber == 0)
            {
                if (failedAttempts < 5)
                {
                    failedAttempts++;
                    SendCustomEventDelayedSeconds(nameof(_HandleVersionDetection), 1);
                    return;
                }

                VersionTimeout();
            }

            failedAttempts = 0;
            
            Debug.Log("[<color=green>BuildHelper Udon Link</color>] Checking Instance Build Version...");
            if (buildNumber == MasterBuildNumber)
            {
                VersionMatch();
            }
            else
            {
                VersionMismatch();
            }
        }

        private void VersionMatch()
        {
            Debug.Log("Local build matches instance build number!");
            SendEvent(onVersionMatch);
        }
        
        private void VersionMismatch()
        {
            Debug.LogError($"Version mismatch! Networked build: {MasterBuildNumber} | Local build: {buildNumber}");
            SendEvent(onVersionMismatch);
            if (sendNetworkedEvent)
            {
                if (sendToAll) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnRemoteMismatch));
                else SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnRemoteMismatch));
            }
        }

        public void OnRemoteMismatch()
        {
            if (buildNumber > MasterBuildNumber) return;
            
            Debug.LogError(
                $"[<color=green>BuildHelper Udon Link</color>] A player on a newer version of this world just joined. The world was most likely just updated.");
            SendEvent(onVersionMismatchRemote);
        }

        private void VersionTimeout()
        {
            Debug.LogError("[<color=green>BuildHelper Udon Link</color>] Couldn't verify instance build version, this probably means either " +
                           "the instance master is on an outdated build, or the network is overloaded.");
            SendEvent(onVersionTimeout);
        }

        private string sentCallbacks = "";
        
        private void SendEvent(string eventName)
        {
            string changedEventname = $"_{eventName}_";
            if (!singleCallback || singleCallback && !sentCallbacks.Contains(changedEventname))
            {
                if (eventBehaviour != null && !string.IsNullOrEmpty(eventName))
                    eventBehaviour.SendCustomEvent(eventName);

                sentCallbacks += changedEventname;
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (!checkVersion) return;
            
            if (player.isLocal && player.isMaster)
            {
                if (allowUpdates)
                    UpdateMasterBuildNumber();
            }
            else if (Networking.IsMaster)
            {
                Debug.LogError("[<color=green>BuildHelper Udon Link</color>] Looks like the local player isn't the object owner even though they should be. " +
                               "Changing ownership and updating build number..");
                
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                MasterBuildNumber = buildNumber;
                RequestSerialization();
            }
        }

        private void UpdateMasterBuildNumber()
        {
            Debug.Log("[<color=green>BuildHelper Udon Link</color>] Local player is the networking master. Lets update the build number..");
            Debug.Log($"Updating networked build to {buildNumber}");
            MasterBuildNumber = buildNumber;
            RequestSerialization();
        }
    }
}