#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace BocuD.VRChatApiTools
{
    public class VRChatApiToolsUploadStatus : EditorWindow
    {
        public static VRChatApiToolsUploadStatus ShowStatus()
        {
            VRChatApiToolsUploadStatus window = GetWindow<VRChatApiToolsUploadStatus>(true);

            window.titleContent = new GUIContent("VRChat Api Tools Uploader");
            window.maxSize = new Vector2(400, 200);
            window.minSize = window.maxSize;
            window.autoRepaintOnSceneChange = true;

            window.Show();
            window.Repaint();

            return window;
        }
        
        public static VRChatApiToolsUploadStatus GetNew()
        {
            VRChatApiToolsUploadStatus window = CreateInstance<VRChatApiToolsUploadStatus>();
            window.ShowUtility();

            window.titleContent = new GUIContent("VRChat Api Tools Uploader");
            window.maxSize = new Vector2(400, 200);
            window.minSize = window.maxSize;
            window.autoRepaintOnSceneChange = true;

            window.Show();
            window.Repaint();

            return window;
        }

        private Vector2 logScroll;
        private string log;
        private string _header;
        private string _status;
        private string _subStatus;
        private float currentProgress;
        public bool cancelRequested;
        public bool uploadInProgress = true;
        
        public bool userConfirm = false;
        public string confirmHeader = "";
        public string confirmButton = "";
        public Action confirmAction;
        public Action failAction;
        public Action confirmUI;
        
        public enum UploadState
        {
            uploading,
            aborting,
            aborted,
            finished,
            failed
        }

        private UploadState uploadState = UploadState.uploading;

        private void OnGUI()
        {
            if (userConfirm)
            {
                DrawUserConfirmUI();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Upload Status");

            if (uploadInProgress)
            {
                EditorGUI.BeginDisabledGroup(cancelRequested);
                if (GUILayout.Button("Cancel"))
                {
                    uploadState = UploadState.aborting;
                    cancelRequested = true;
                }

                EditorGUI.EndDisabledGroup();
            }
            else
            {
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
            }

            EditorGUILayout.EndHorizontal();

            GUIContent icon = EditorGUIUtility.IconContent("d_console.infoicon@2x");
            GUIStyle stateLabel = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            GUIStyle iconStyle = new GUIStyle(GUI.skin.label) { fixedHeight = 30 };

            GUILayout.BeginHorizontal();

            switch (uploadState)
            {
                case UploadState.uploading:
                    icon = EditorGUIUtility.IconContent("UpArrow");
                    GUILayout.Label(_header, stateLabel);
                    break;

                case UploadState.aborting:
                    icon = EditorGUIUtility.IconContent("Error@2x");
                    GUILayout.Label("Aborting...", stateLabel);
                    break;

                case UploadState.failed:
                    icon = EditorGUIUtility.IconContent("Error@2x");
                    GUILayout.Label("Failed", stateLabel);
                    break;

                case UploadState.aborted:
                    icon = EditorGUIUtility.IconContent("d_console.infoicon@2x");
                    GUILayout.Label("Aborted", stateLabel);
                    break;

                case UploadState.finished:
                    icon = EditorGUIUtility.IconContent("d_Toggle Icon");
                    GUILayout.Label("Finished!", stateLabel);
                    break;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(icon, iconStyle);
            GUILayout.EndHorizontal();

            if (!uploadInProgress)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(15)), 1, "");
                if (uploadState == UploadState.failed)
                    EditorGUILayout.HelpBox(_status, MessageType.Error);
            }
            else
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(15)), currentProgress, _status);
            }

            GUIStyle logStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true, richText = true, alignment = TextAnchor.LowerLeft, fixedWidth = position.width - 33
            };

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.FlexibleSpace();
            logScroll = EditorGUILayout.BeginScrollView(logScroll);
            EditorGUILayout.LabelField(log, logStyle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        public void ConfirmButton(string header, string button, Action onSucces, Action onFail, Action contentDrawer = null)
        {
            userConfirm = true;
            confirmHeader = header;
            confirmButton = button;
            confirmAction = onSucces;
            failAction = onFail;
            confirmUI = contentDrawer;
        }

        private void DrawUserConfirmUI()
        {
            GUIStyle bigLabel = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            EditorGUILayout.LabelField(confirmHeader, bigLabel);
            confirmUI?.Invoke();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(confirmButton))
            {
                userConfirm = false;
                confirmAction?.Invoke();
            }
                
            if (GUILayout.Button("Abort"))
            {
                userConfirm = false;
                uploadState = UploadState.aborted;
                uploadInProgress = false;
                failAction?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void Awake()
        {
            Logger.statusWindow = this;
        }

        private void OnDestroy()
        {
            Logger.statusWindow = null;
            if(userConfirm)
                failAction?.Invoke();
        }

        public void SetUploadState(UploadState newState)
        {
            uploadState = newState;
            if (uploadState == UploadState.aborted || uploadState == UploadState.failed ||
                uploadState == UploadState.finished)
                uploadInProgress = false;

            Repaint();
        }

        public void SetStatus(string header, string status = null, string subStatus = null)
        {
            AddLog($"{(_status.IsNullOrWhitespace() ? $"{_header}" : $"{_status}{(_subStatus.IsNullOrWhitespace() ? "" : $": {_subStatus}")}")}");

            _header = header;
            _status = status;
            _subStatus = subStatus;
            Repaint();
        }

        public void SetUploadProgress(long uploaded, long total)
        {
            currentProgress = (float)uploaded / total;
            _status = $"Uploading: {uploaded.ToReadableBytes()} / {total.ToReadableBytes()} ({(currentProgress * 100):N1} %)";
            Repaint();
        }

        public void SetErrorState(string header, string details)
        {
            SetUploadState(UploadState.failed);
            _header = header;
            _status = details;
            AddLog($"<color=red>{details}</color>");
            Repaint();
            Logger.statusWindow = null;
        }

        public void AddLog(string contents)
        {
            log += $"\n[{DateTime.Now:HH:mm:ss}]: {contents}";
            
            logScroll.y += 1000;
        }
    }
}
#endif