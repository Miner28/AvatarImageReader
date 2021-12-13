using System.IO;
using AvatarImageDecoder;
using UnityEditor;
using UnityEngine;

public class AvatarImageEncoderWindow : EditorWindow
{
    private string text;
    private Texture2D output;
    
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
        }
    }
}
