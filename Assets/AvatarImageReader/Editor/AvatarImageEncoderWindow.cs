using System.IO;
using AvatarImageDecoder;
using BocuD.VRChatApiTools;
using BocuD.VRChatApiTools.Editor;
using UnityEditor;
using UnityEngine;
using VRC.Core;

namespace AvatarImageReader.Editor
{
    public class AvatarImageEncoderWindow : EditorWindow
    {
        private string text;
        private Texture2D output;
        private ApiAvatar selectedAvatar;

        private EditorCoroutine ImageUploader;

        [MenuItem("Tools/Avatar Image Encoder")]
        private static void ShowWindow()
        {
            AvatarImageEncoderWindow encoderWindow = GetWindow<AvatarImageEncoderWindow>();
        }
    
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Text to encode");
            text = EditorGUILayout.TextArea(text);

            if (GUILayout.Button("Encode Image"))
            {
                output = AvatarImageEncoder.EncodeUTF16Text(text, null);
            }

            if (output != null)
            {
                GUIContent texturePreview = new GUIContent(output);
                GUILayout.Box(texturePreview);

                if (GUILayout.Button("Save"))
                {
                    string path = EditorUtility.SaveFilePanel(
                        "Save texture as PNG",
                        Application.dataPath,
                        "output.png",
                        "png");

                    if (path.Length != 0)
                    {
                        var pngData = output.EncodeToPNG();
                        if (pngData != null)
                            File.WriteAllBytes(path, pngData);

                        path = "Assets" + path.Substring(Application.dataPath.Length);

                        AssetDatabase.WriteImportSettingsIfDirty(path);
                        AssetDatabase.ImportAsset(path);

                        TextureImporter importer = (TextureImporter) AssetImporter.GetAtPath(path);
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.maxTextureSize = 2048;
                        EditorUtility.SetDirty(importer);
                        AssetDatabase.WriteImportSettingsIfDirty(path);

                        AssetDatabase.ImportAsset(path);
                    }
                }
            
                if (GUILayout.Button("Select avatar"))
                {
                    AvatarPicker.ApiAvatarSelector(SetAvatarID);
                }

                if (selectedAvatar != null)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField("Selected Avatar:", selectedAvatar.name);
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("Upload Image"))
                    {
                        GameObject temp = new GameObject("ImageUploader");
                        temp.tag = "EditorOnly";
                        VRChatApiUploader uploader = temp.AddComponent<VRChatApiUploader>();

                        uploader.SetupAvatarImageUpdate(selectedAvatar, output);
                        //EditorCoroutine.Start(uploader.UpdateAvatarImage(selectedAvatar, output));
                    }
                }
            }
        }

        public void SetAvatarID(ApiAvatar avatar)
        {
            selectedAvatar = avatar;
        }
    }
}