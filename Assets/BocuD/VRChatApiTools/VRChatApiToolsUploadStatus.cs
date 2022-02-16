#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
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

        private Vector2 logScroll;
        private string log;
        private string status;
        private string subStatus;
        private float currentProgress;

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Upload Status");

            EditorGUILayout.EndHorizontal();

            GUIStyle stateLabel = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            GUIContent icon;
            GUIStyle iconStyle = new GUIStyle(GUI.skin.label) { fixedHeight = 30 };

            icon = EditorGUIUtility.IconContent("UpArrow");

            GUILayout.BeginHorizontal();
            GUILayout.Label(status, stateLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(icon, iconStyle);
            GUILayout.EndHorizontal();

            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), currentProgress, subStatus);

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

        public void SetStatus(string state, float progress, string substate = null)
        {
            if (state != status || substate != subStatus)
            {
                AddLog($"{(subStatus.IsNullOrWhitespace() ? $"{status}" : $"({subStatus})")}");
            }

            status = state;
            subStatus = substate;
            currentProgress = progress;
            Repaint();
        }

        public void AddLog(string contents)
        {
            log += $"\n[{DateTime.Now:HH:mm:ss}]: {contents}";
        }
    }
}
#endif