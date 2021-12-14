using AvatarImageDecoder.Editor;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace AvatarImageReader.Editor
{
    [CustomEditor(typeof(AvatarImagePrefab))]
    public class AvatarImagePrefabEditor : UnityEditor.Editor
    {
        private AvatarImagePrefab reader;
        
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            reader = (AvatarImagePrefab) target;

            GUIStyle bigHeaderStyle = new GUIStyle(EditorStyles.label) {richText = true, fontSize = 15};
            GUIStyle headerStyle = new GUIStyle(EditorStyles.label) {richText = true};

            EditorGUILayout.LabelField("<b>Avatar Image Reader</b>", bigHeaderStyle);

            EditorGUILayout.LabelField("<b>Linked Avatar</b>", headerStyle);

            if (reader.linkedAvatar.IsNullOrWhitespace())
            {
                EditorGUILayout.HelpBox("No avatar is currently selected. AvatarImageReader will not work without linking an avatar.", MessageType.Info);
            }
            VRChatApiToolsEditor.DrawAvatarInspector(reader.linkedAvatar);
            
            if (GUILayout.Button("Change Avatar"))
            {
                AvatarPicker.ApiAvatarSelector(AvatarSelected);
            }
            EditorGUILayout.Space(4);
            
            EditorGUILayout.LabelField("<b>Image Options</b>", headerStyle);
            reader.imageMode = EditorGUILayout.Popup("Image mode: ", reader.imageMode, new [] {"Cross Platform", "PC Only"});
            switch (reader.imageMode)
            {
                case 0:
                    EditorGUILayout.LabelField("Target resolution: ", "128x96");
                    break;
                
                case 1:
                    EditorGUILayout.HelpBox("You should only use PC Only mode if you are absolutely sure you are going to use all of the space it allows you to use.", MessageType.Warning);
                    EditorGUILayout.LabelField("Target resolution: ", "1200x900");
                    break;
            }
            EditorGUILayout.Space(4);
            
            EditorGUILayout.LabelField("<b>General Options</b>", headerStyle);
            reader.outputToText = EditorGUILayout.Toggle("Output to TextMeshPro: ", reader.outputToText);
            if (reader.outputToText)
            {
                reader.outputText = (TextMeshPro) EditorGUILayout.ObjectField("Target TextMeshPro: ", reader.outputText,
                    typeof(TextMeshPro), true);
            }
            EditorGUILayout.Space(4);
            
            EditorGUILayout.LabelField("<b>Debugging</b>", headerStyle);
            reader.debugLogger = EditorGUILayout.Toggle("Enable debug logging", reader.debugLogger);
            if (reader.debugLogger)
            {
                reader.debugTMP = EditorGUILayout.Toggle("Enable logging to TextMeshPro", reader.debugTMP);
                if (reader.debugTMP)
                {
                    reader.loggerText = (TextMeshPro) EditorGUILayout.ObjectField("Target TextMeshPro: ",
                        reader.loggerText, typeof(TextMeshPro), true);
                }
            }
            else
            {
                reader.debugTMP = false;
            }
        }

        private void AvatarSelected(ApiAvatar avatar)
        {
            if (reader == null)
            {
                Debug.LogError("[AvatarImagePrefabEditor] Avatar was selected but inspector target is null; inspector was likely closed.");
            }
            reader.UpdateProxy();
            reader.linkedAvatar = avatar.id;
            reader.ApplyProxyModifications();
        }
    }
}