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
using UnityEditor;
using UnityEngine;
using static BocuD.VRChatApiTools.VRChatApiTools;

namespace BocuD.BuildHelper
{
    public class AutonomousBuilderStatus : EditorWindow
    {
        private AutonomousBuildState _currentState;
        private string log;
        public string failReason;
        private Vector2 logScroll;
        public bool abort = false;
        public AutonomousBuilder.AutonomousBuildData buildInfo;

        public bool uploading;
        public float uploadProgress;
        public string uploadHeader;
        public string uploadStatus;
        
        public void UploadStatus(string header, string status = null, string subStatus = null)
        {
            uploadHeader = header;
            uploadStatus = status;
            Repaint();
        }

        public void UploadProgress(long uploaded, long total)
        {
            uploadProgress = (float)uploaded / total;
            uploadStatus = $"Uploading: {uploaded.ToReadableBytes()} / {total.ToReadableBytes()} ({(uploadProgress * 100):N1} %)";
            Repaint();
        }

        public void OnError(string header, string details)
        {
            currentState = AutonomousBuildState.failed;
            failReason = $"{header}: {details}";
            buildInfo._failed = true;
            buildInfo.activeBuild = false;
            AddLog($"<color=red>{header}: {details}</color>");
            Repaint();
        }

        public void Aborted()
        {
            currentState = AutonomousBuildState.aborted;
            buildInfo._failed = true;
            buildInfo.activeBuild = false;
            Repaint();
        }
        
        public AutonomousBuildState currentState
        {
            set
            {
                AutonomousBuilderStatus window = (AutonomousBuilderStatus) GetWindow(typeof(AutonomousBuilderStatus));
                _currentState = value;
                AddLog(GetStateString(_currentState));
                window.Repaint();
                if (_currentState == AutonomousBuildState.finished) Application.logMessageReceived -= Log;
            }
            get => _currentState;
        }

        public Platform currentPlatform;

        public void AddLog(string contents)
        {
            log += $"\n[{DateTime.Now:HH:mm:ss}]: {contents}";
        }

        public void BindLogger()
        {
            Application.logMessageReceived += Log;
        }

        private void OnEnable()
        {
            if (buildInfo != null && buildInfo.activeBuild)
                BindLogger();
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= Log;
        }

        public static AutonomousBuilderStatus ShowStatus()
        {
            AutonomousBuilderStatus window = (AutonomousBuilderStatus) GetWindow(typeof(AutonomousBuilderStatus), true);

            window.titleContent = new GUIContent("Autonomous Builder");
            window.maxSize = new Vector2(400, 200);
            window.minSize = window.maxSize;
            window.autoRepaintOnSceneChange = true;

            window.Show();
            window.Repaint();

            return window;
        }

        private string GetStateString(AutonomousBuildState state)
        {
            switch (state)
            {
                case AutonomousBuildState.building:
                    return $"Building for {currentPlatform}";
                case AutonomousBuildState.waitingForApi:
                    return "Waiting for VRChat Api..";
                case AutonomousBuildState.switchingPlatform:
                    return $"Switching platform to {currentPlatform}";
                case AutonomousBuildState.uploading:
                    return uploading ? uploadHeader : $"Uploading for {currentPlatform}";
                case AutonomousBuildState.finished:
                    return "Finished!";
                case AutonomousBuildState.aborting:
                    return "Aborting...";
                case AutonomousBuildState.aborted:
                    return "Aborted";
                case AutonomousBuildState.failed:
                    return $"Failed: {failReason}";
            }

            return "Unknown status";
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Autonomous Builder Status:");
            if (_currentState != AutonomousBuildState.aborted && _currentState != AutonomousBuildState.finished && _currentState != AutonomousBuildState.aborting)
            {
                if (GUILayout.Button("Abort"))
                {
                    if (EditorUtility.DisplayDialog("Autonomous Builder",
                            "Are you sure you want to abort the Autonomous Build?",
                            "Yes", "No"))
                    {
                        abort = true;
                        currentState = AutonomousBuildState.aborting;
                        //DestroyImmediate(this);
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Close"))
                {
                    if (_currentState == AutonomousBuildState.aborting)
                    {
                        if (EditorUtility.DisplayDialog("Autonomous Builder",
                                "The current autonomous build task is already being aborted. If you force it to close now, the task may not be properly aborted. Are you sure you want to continue?",
                                "Yes", "No"))
                        {
                            _currentState = AutonomousBuildState.aborted;
                            Close();
                            DestroyImmediate(this);
                        }
                    }
                    else
                    {
                        Close();
                        DestroyImmediate(this);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            GUIStyle stateLabel = new GUIStyle(GUI.skin.label) {fontSize = 24, wordWrap = true};
            GUIContent icon = null;
            GUIStyle iconStyle = new GUIStyle(GUI.skin.label) {fixedHeight = 30};

            GUILayout.BeginHorizontal();
            GUILayout.Label(GetStateString(_currentState), stateLabel);
            GUILayout.FlexibleSpace();
            
            switch (_currentState)
            {
                case AutonomousBuildState.building:
                    icon = EditorGUIUtility.IconContent(currentPlatform == Platform.Windows
                        ? "BuildSettings.Metro On"
                        : "BuildSettings.Android On");
                    break;
                
                case AutonomousBuildState.switchingPlatform:
                    icon = EditorGUIUtility.IconContent("RotateTool On@2x");
                    break;
                
                case AutonomousBuildState.uploading:
                    icon = EditorGUIUtility.IconContent("UpArrow");
                    break;
                
                case AutonomousBuildState.finished:
                    icon = EditorGUIUtility.IconContent("d_Toggle Icon");
                    break;
                
                case AutonomousBuildState.aborting:
                case AutonomousBuildState.aborted:
                    icon = EditorGUIUtility.IconContent("Error@2x");
                    break;
                
                case AutonomousBuildState.failed:
                    icon = EditorGUIUtility.IconContent("Error@2x");
                    break;
            }

            if (icon != null)
                GUILayout.Label(icon, iconStyle);
            GUILayout.EndHorizontal();
            
            if (currentState == AutonomousBuildState.uploading)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(15)), uploadProgress, uploadStatus);
            }

            GUIStyle logStyle = new GUIStyle(EditorStyles.label)
                {wordWrap = true, richText = true, alignment = TextAnchor.LowerLeft, fixedWidth = position.width - 33};
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.FlexibleSpace();
            logScroll = EditorGUILayout.BeginScrollView(logScroll);
            EditorGUILayout.LabelField(log, logStyle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void Log(string logString, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    AddLog($"<color=red>{logString}</color>");

                    if (logString.Contains("Building AssetBundles was canceled.") &&
                        _currentState != AutonomousBuildState.failed)
                    {
                        failReason = "Build was cancelled";
                        currentState = AutonomousBuildState.failed;
                    }

                    else if (logString.Contains("Error building Player") &&
                        _currentState != AutonomousBuildState.failed)
                    {
                        failReason = "Error building Player";
                        currentState = AutonomousBuildState.failed;
                    }
            
                    else if (logString.Contains("AndroidPlayer") &&
                            _currentState != AutonomousBuildState.failed)
                    {
                        failReason = "Couldn't switch platform to Android";
                        currentState = AutonomousBuildState.failed;
                    }

                    else if (logString.Contains("Export Exception") &&
                            _currentState != AutonomousBuildState.failed)
                    {
                        failReason = "Export Exception";
                        currentState = AutonomousBuildState.failed;
                    }
                    break;
                
                case LogType.Warning:
                    break;
                
                case LogType.Log:
                    AddLog($"<color=grey>{logString}</color>");
                    break;
            }
            

            logScroll.y += 1000;
        }

        private void OnDestroy()
        {
            if (buildInfo == null) return;
            
            switch (_currentState)
            {
                case AutonomousBuildState.aborting:
                    BuildHelperData data = BuildHelperData.GetDataBehaviour();

                    //spawn new window if we still need to process the abort
                    if (data && buildInfo.activeBuild)
                    {
                        AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                        status.ShowUtility();
                        status.titleContent = new GUIContent("Autonomous Builder");
                        status.log = log;
                        status.currentPlatform = currentPlatform;
                        status._currentState = _currentState;
                        status.Repaint();
                        return;
                    }

                    //spawn new window if we are still in the build process
                    if (BuildPipeline.isBuildingPlayer)
                    {
                        AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                        status.ShowUtility();
                        status.titleContent = new GUIContent("Autonomous Builder");
                        status.log = log;
                        status.currentPlatform = currentPlatform;
                        status._currentState = _currentState;
                        status.Repaint();
                        return;
                    }

                    break;

                case AutonomousBuildState.failed:
                case AutonomousBuildState.finished:
                case AutonomousBuildState.aborted:
                    return;

                default:
                    if (EditorUtility.DisplayDialog("Autonomous Builder",
                            "Are you sure you want to cancel your autonomous build?",
                            "Abort build", "Continue build"))
                    {
                        AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                        status.ShowUtility();
                        status.titleContent = new GUIContent("Autonomous Builder");
                        status.log = log;
                        status.currentPlatform = currentPlatform;
                        status.currentState = AutonomousBuildState.aborting;
                        status.abort = true;
                    }
                    else
                    {
                        AutonomousBuilderStatus status = CreateInstance<AutonomousBuilderStatus>();
                        status.ShowUtility();
                        status.titleContent = new GUIContent("Autonomous Builder");
                        status.log = log;
                        status.currentPlatform = currentPlatform;
                        status._currentState = _currentState;
                        status.Repaint();
                    }

                    break;
            }
        }
    }

    public enum AutonomousBuildState
    {
        building,
        waitingForApi,
        switchingPlatform,
        uploading,
        finished,
        aborting,
        aborted,
        failed
    }
}

#endif