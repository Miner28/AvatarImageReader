using System;
using System.IO;
using System.Linq;
using AvatarImageDecoder;
using BocuD.VRChatApiTools;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace AvatarImageReader.Editor
{
    [CustomEditor(typeof(AvatarImagePrefab))]
    public class AvatarImagePrefabEditor : UnityEditor.Editor
    {
        private AvatarImagePrefab reader;
        private string text = "";

        private const string root = "Assets/AvatarImageReader";
        private const string pedestalSavePath = "Assets/AvatarImageReader/PedestalData";
        
        private const string quadMaterialPath = "Assets/AvatarImageReader/Materials/RenderQuad.mat";
        
        private const string pcDonorImagePath = "Assets/AvatarImageReader/DonorImages/PC.png";
        private const string questDonorImagePath = "Assets/AvatarImageReader/DonorImages/Quest.png";
        
        private const string pcRTPath = "Assets/AvatarImageReader/DonorImages/PCRT.asset";
        private const string questRTPath = "Assets/AvatarImageReader/DonorImages/QuestRT.asset";
        
        private Texture2D output;

        private Vector2 scrollview;

        private int imageWidth;
        private int imageHeight;
        private int imageByteCount;
        private int imageContent;

        private bool init = true;

        private TextStorageObject textStorageObject;
        private int lastImageMode;

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            reader = (AvatarImagePrefab) target;
            reader.UpdateProxy();
            
            if (init)
            {
                if (reader.GetComponentInChildren<TextStorageObject>())
                {
                    textStorageObject = reader.GetComponentInChildren<TextStorageObject>();
                    text = textStorageObject.text;
                }
                else
                {
                    GameObject container = new GameObject("TextStorageObject") {tag = "EditorOnly"};
                    container.transform.SetParent(reader.transform);
                    container.AddComponent<TextStorageObject>();
                    container.hideFlags = HideFlags.HideInHierarchy;
                    init = false;
                }

                if (!reader.pedestalAssetsReady)
                {
                    UpdatePedestalAssets();
                    reader.pedestalAssetsReady = true;
                    MarkDirty();
                }

                lastImageMode = reader.imageMode;
            }

            GUIStyle bigHeaderStyle = new GUIStyle(EditorStyles.label) {richText = true, fontSize = 15};
            GUIStyle headerStyle = new GUIStyle(EditorStyles.label) {richText = true};
            
            EditorGUILayout.LabelField("<b>Avatar Image Reader</b>", bigHeaderStyle);
            
            RunChecks();
            
            
            EditorGUILayout.LabelField("<b>Linked Avatar</b>", headerStyle);

            if (reader.linkedAvatar.IsNullOrWhitespace())
            {
                EditorGUILayout.HelpBox("No avatar is currently selected. AvatarImageReader will not work without linking an avatar.", MessageType.Info);
            }
            VRChatApiToolsEditor.DrawAvatarInspector(reader.linkedAvatar);

            EditorGUILayout.BeginHorizontal();
            string changeAvatarString = reader.linkedAvatar.IsNullOrWhitespace() ? "Set avatar..." : "Change Avatar";
            if (GUILayout.Button(changeAvatarString))
            {
                AvatarPicker.ApiAvatarSelector(AvatarSelected);
            }

            EditorGUI.BeginDisabledGroup(reader.linkedAvatar.IsNullOrWhitespace());
            if (GUILayout.Button("Remove Avatar"))
            {
                AvatarSelected(null);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);


            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("<b>Image Options</b>", headerStyle);

            int pixelCount = 0;
            
            reader.imageMode = EditorGUILayout.Popup("Image mode: ", reader.imageMode, new [] {"Cross Platform", "PC Only"});

            if (reader.imageMode != lastImageMode)
            {
                UpdatePedestalAssets();
            }
            
            switch (reader.imageMode)
            {
                case 0:
                    EditorGUILayout.LabelField("Target resolution: ", "128x96");
                    pixelCount = 128 * 96;
                    break;
                
                case 1:
                    EditorGUILayout.HelpBox("You should only use PC Only mode if you are absolutely sure you are going to use all of the space it allows you to use.", MessageType.Warning);
                    EditorGUILayout.LabelField("Target resolution: ", "1200x900");
                    pixelCount = 1200 * 900;
                    break;
            }
            EditorGUILayout.Space(4);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
            


            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("<b>Data encoding</b>", headerStyle);

            reader.dataMode = EditorGUILayout.Popup("Data mode: ", reader.dataMode, new [] {"UTF16 Text", "ASCII Text (not available yet)", "Binary data (not available yet)"});
            reader.dataMode = 0;

            reader.patronMode = EditorGUILayout.Toggle("Link with Patreon Decoder:", reader.patronMode);
            if (reader.patronMode)
            {
                reader.dataMode = 0;
                EditorGUILayout.HelpBox("Make sure to link this reader to a decoder, and to select this reader on the decoder object!", MessageType.Info);
            }

            EditorGUI.BeginDisabledGroup(reader.patronMode);

            //remove one pixel (header)
            int byteCount = (pixelCount - 1) * 3;

            switch (reader.dataMode)
            {
                case 0:
                    EditorGUILayout.LabelField("Remaining characters: ", $"{byteCount / 2 - text.Length:n0} / {byteCount / 2:n0} ({((float)byteCount / 2 - text.Length) / ((float)byteCount / 2) * 100:n0}%)");

                    EditorGUILayout.BeginVertical();
                    scrollview = EditorGUILayout.BeginScrollView(scrollview);

                    GUIStyle textArea = new GUIStyle(EditorStyles.textArea) {wordWrap = true};
                    text = EditorGUILayout.TextArea(text, textArea);
                    
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    
                    if (text.Length > byteCount / 2)
                    {
                        EditorGUILayout.HelpBox("You are using more characters than the image can fit. Excess characters will be trimmed off.", MessageType.Error);
                    }

                    if (GUILayout.Button("Encode Image"))
                    {
                        imageWidth = reader.imageMode == 0 ? 128 : 1200;
                        imageHeight = reader.imageMode == 0 ? 96 : 900;

                        Texture2D img = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
                        Color[] initialPixels = Enumerable.Repeat(Color.white, imageWidth * imageHeight).ToArray();
                        img.SetPixels(initialPixels);

                        imageByteCount = text.Length * 2 + 1;
                        output = AvatarImageEncoder.EncodeUTF16Text(text, img);
                    }

                    if (output != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUIContent texturePreview = new GUIContent(output) {};
                        GUILayout.Box(texturePreview, GUILayout.Width(128), GUILayout.Height(96));
                        
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField("Image dimensions: ", $"{imageWidth} x {imageHeight}");
                        EditorGUILayout.LabelField("Image contents: ", $"{imageByteCount:n0} Bytes");
                        EditorGUILayout.LabelField("Image data type: ", "UTF16 Characters");

                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("Save Image"))
                        {
                            string path = EditorUtility.SaveFilePanel(
                                "Save texture as PNG",
                                Application.dataPath,
                                "output.png",
                                "png");

                            if (path.Length != 0)
                            {
                                byte[] pngData = output.EncodeToPNG();
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
                        
                        EditorGUI.BeginDisabledGroup(!APIUser.IsLoggedIn || !VRChatApiTools.avatarCache.ContainsKey(reader.linkedAvatar));
                        if (GUILayout.Button("Upload Image to Avatar"))
                        {
                            GameObject temp = new GameObject("ImageUploader") {tag = "EditorOnly"};
                            VRChatApiUploader uploader = temp.AddComponent<VRChatApiUploader>();

                            uploader.SetupAvatarImageUpdate(VRChatApiTools.avatarCache[reader.linkedAvatar], output);
                        }
                        EditorGUI.EndDisabledGroup();
                        
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.HelpBox($"This image will take {imageByteCount/3/reader.stepLength/72} seconds ({imageByteCount/3/reader.stepLength} frames) to decode with the current step length.", MessageType.Info);
                    }
                    break;
                case 1:
                    EditorGUILayout.LabelField("Available characters: ", $"{pixelCount * 3:n0}");
                    break;
                case 2:
                    EditorGUILayout.LabelField("Available data: ", $"{pixelCount * 3:n0} Bytes");
                    break;
            }
            EditorGUILayout.Space(4);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
            
            
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("<b>General Options</b>", headerStyle);
            GUIContent tooltip = new GUIContent()
                {text = "Decode step size", tooltip = "Increasing step size decreases decode time but increases frametimes"};
            reader.stepLength = EditorGUILayout.IntSlider(tooltip, reader.stepLength, 100, 5000);
            EditorGUILayout.Space(2);
            
            EditorGUILayout.LabelField("<i>On decode finish:</i>", headerStyle);
            reader.outputToText = EditorGUILayout.Toggle("Output to TextMeshPro", reader.outputToText);
            if (reader.outputToText)
            {
                reader.outputText = (TextMeshPro) EditorGUILayout.ObjectField("Target TextMeshPro: ", reader.outputText,
                    typeof(TextMeshPro), true);
            }

            if (reader.patronMode)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("<b>Patreon Decoder</b>", headerStyle);
                
                reader.callBackOnFinish = true;
                reader.callbackBehaviour = (UdonBehaviour)EditorGUILayout.ObjectField("Decoder Behaviour: ",
                    reader.callbackBehaviour, typeof(UdonBehaviour), true);

                if (reader.callbackBehaviour != null)
                {
                    if (UdonSharpEditorUtility.GetUdonSharpBehaviourType(reader.callbackBehaviour).ToString() ==
                        "PatreonDecoder")
                    {
                        EditorGUILayout.HelpBox(
                            "Valid Patreon Decoder detected! Make sure to open its inspector and link back to this reader.",
                            MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Specified UdonBehaviour doesn't appear to be of the correct type.",
                            MessageType.Error);
                    }
                }

                reader.callbackEventName = "_StartDecode";
            }
            else
            {
                reader.callBackOnFinish = EditorGUILayout.Toggle("Send Custom Event", reader.callBackOnFinish);
                if (reader.callBackOnFinish)
                {
                    reader.callbackBehaviour = (UdonBehaviour)EditorGUILayout.ObjectField("Target Behaviour: ",
                        reader.callbackBehaviour, typeof(UdonBehaviour), true);
                    if (reader.callbackBehaviour != null)
                    {
                        reader.callbackEventName = EditorGUILayout.TextField("Event name: ", reader.callbackEventName);
                    }
                }
            }

            EditorGUILayout.Space(4);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
            
            
            
            EditorGUI.BeginChangeCheck();
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
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
        }

        private void MarkDirty()
        {
            textStorageObject.text = text;
            
            reader.ApplyProxyModifications();
            
            if (reader.outputToText)
            {
                if (reader.outputText != null)
                {
                    reader.outputText.text = text;
                }
            }
            
            EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader));
            
            if (PrefabUtility.IsPartOfAnyPrefab(reader.gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    UdonSharpEditorUtility.GetBackingUdonBehaviour(reader));
            }
        }

        private bool checksFailedReadRenderTexture = false;
        private bool checksFailedPedestal = false;

        
        private void RunChecks()
        {
            if (reader.readRenderTexture != null)
            {
                if (!reader.readRenderTexture.GetComponent<Camera>())
                {
                    EditorGUILayout.HelpBox(
                        "The ReadRenderTexture child object of this AvatarImageReader doesn't have a camera attached. AvatarImageReader will not work. Please reference the provided prefab to find out what settings to use.",
                        MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "AvatarImageReader needs a reference to a ReadRenderTexture UdonBehaviour in its children.",
                    MessageType.Error);

                if (GUILayout.Button("Auto fix"))
                {
                    if (reader.GetUdonSharpComponentInChildren<ReadRenderTexture>())
                    {
                        reader.UpdateProxy();
                        reader.readRenderTexture = reader.GetUdonSharpComponentInChildren<ReadRenderTexture>();
                        reader.ApplyProxyModifications();
                    }
                    else checksFailedReadRenderTexture = true;
                }

                if (checksFailedReadRenderTexture)
                {
                    EditorGUILayout.HelpBox(
                        "Couldn't find an instance of ReadRenderTexture in the children of this AvatarImageReader. Please add one and try again, or use the provided prefab.",
                        MessageType.Error);
                }
            }

            if (reader.avatarPedestal == null)
            {
                EditorGUILayout.HelpBox(
                    "AvatarImageReader needs a reference to an AvatarPedestal in its children.",
                    MessageType.Error);

                if (GUILayout.Button("Auto fix"))
                {
                    if (reader.GetComponentInChildren<VRCAvatarPedestal>())
                    {
                        reader.UpdateProxy();
                        reader.avatarPedestal = reader.GetComponentInChildren<VRCAvatarPedestal>();
                        reader.ApplyProxyModifications();
                    }
                    else checksFailedPedestal = true;
                }

                if (checksFailedPedestal)
                {
                    EditorGUILayout.HelpBox(
                        "Couldn't find an AvatarPedestal in the children of this AvatarImageReader. Please add one and try again, or use the provided prefab.",
                        MessageType.Error);
                }
            }
        }

        private void UpdatePedestalAssets()
        {
            if (reader.uid.IsNullOrWhitespace())
            {
                reader.uid = GetUniqueID();
            }

            if (!AssetDatabase.IsValidFolder(pedestalSavePath))
            {
                AssetDatabase.CreateFolder(root, "PedestalData");
            }
            
            if (!AssetDatabase.IsValidFolder($"{pedestalSavePath}/{reader.uid}"))
            {
                AssetDatabase.CreateFolder(pedestalSavePath, reader.uid);

                AssetDatabase.CopyAsset(quadMaterialPath, $"{pedestalSavePath}/{reader.uid}/RenderQuad.mat");
                AssetDatabase.CopyAsset(pcDonorImagePath, $"{pedestalSavePath}/{reader.uid}/PCDonor.png");
                AssetDatabase.CopyAsset(questDonorImagePath, $"{pedestalSavePath}/{reader.uid}/QuestDonor.png");
                AssetDatabase.CopyAsset(pcRTPath, $"{pedestalSavePath}/{reader.uid}/PCRT.asset");
                AssetDatabase.CopyAsset(questRTPath, $"{pedestalSavePath}/{reader.uid}/QuestRT.asset");
            }

            Material renderQuatMat = AssetDatabase.LoadAssetAtPath<Material>($"{pedestalSavePath}/{reader.uid}/RenderQuad.mat");

            Texture2D pcDonor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pedestalSavePath}/{reader.uid}/PCDonor.png");
            Texture2D questDonor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{pedestalSavePath}/{reader.uid}/QuestDonor.png");
            
            RenderTexture pcRT = AssetDatabase.LoadAssetAtPath<RenderTexture>($"{pedestalSavePath}/{reader.uid}/PCRT.asset");
            RenderTexture questRT = AssetDatabase.LoadAssetAtPath<RenderTexture>($"{pedestalSavePath}/{reader.uid}/QuestRT.asset");

            reader.readRenderTexture.UpdateProxy();
            reader.readRenderTexture.renderTexture = reader.imageMode == 0 ? questRT : pcRT;
            reader.readRenderTexture.donorInput = reader.imageMode == 0 ? questDonor : pcDonor;

            reader.readRenderTexture.renderQuad.GetComponent<MeshRenderer>().material = renderQuatMat;
            reader.readRenderTexture.renderCamera.targetTexture = reader.imageMode == 0 ? questRT : pcRT;
            
            reader.readRenderTexture.ApplyProxyModifications();
            
            EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader.readRenderTexture));

            if (PrefabUtility.IsPartOfAnyPrefab(reader.readRenderTexture.gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader.readRenderTexture));
            }
        }

        private void AvatarSelected(ApiAvatar avatar)
        {
            if (reader == null)
            {
                Debug.LogError("[AvatarImagePrefabEditor] Avatar was selected but inspector target is null; inspector was likely closed.");
            }

            reader.UpdateProxy();
            
            if (avatar == null)
            {
                reader.linkedAvatar = "";
                reader.avatarPedestal.blueprintId = "";
            }
            else
            {
                reader.linkedAvatar = avatar.id;
                reader.avatarPedestal.blueprintId = avatar.id;
            }
            
            EditorUtility.SetDirty(reader.avatarPedestal);
            
            if (PrefabUtility.IsPartOfAnyPrefab(reader.avatarPedestal))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(reader.avatarPedestal);
            }
            
            reader.ApplyProxyModifications();
            EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader));

            if (PrefabUtility.IsPartOfAnyPrefab(reader.gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader));
            }
        }
        
        public static string GetUniqueID()
        {
            string [] split = DateTime.Now.TimeOfDay.ToString().Split(new Char [] {':','.'});
            string id = "";
            for (int i = 0; i < split.Length; i++)
            {
                id += split[i];
            }

            id = long.Parse(id).ToString("X");
            
            return id;
        }
    }

    public static class AvatarImageTools
    {
        private const string prefabNormal = "Assets/AvatarImageReader/Prefabs/Decoder.prefab";
        private const string prefabText = "Assets/AvatarImageReader/Prefabs/DecoderWithText.prefab";
        private const string prefabDebug = "Assets/AvatarImageReader/Prefabs/Decoder_Debug.prefab";

        [MenuItem("Tools/AvatarImageReader/Create Image Reader")]
        private static void CreateNormal()
        {
            GameObject toInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabNormal);
            GameObject instantiated = UnityEngine.Object.Instantiate(toInstantiate);
            instantiated.name = "New avatar image reader";
            AvatarImagePrefab imagePrefab = instantiated.GetUdonSharpComponent<AvatarImagePrefab>();
            imagePrefab.UpdateProxy();
            imagePrefab.uid = "";
            imagePrefab.ApplyProxyModifications();
            
            EditorUtility.SetDirty(instantiated);
        }
        
        [MenuItem("Tools/AvatarImageReader/Create Image Reader (With TMP)")]
        private static void CreateText()
        {
            GameObject toInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabText);
            GameObject instantiated = UnityEngine.Object.Instantiate(toInstantiate);
            instantiated.name = "New avatar image reader (TMP)";
            AvatarImagePrefab imagePrefab = instantiated.GetUdonSharpComponent<AvatarImagePrefab>();
            imagePrefab.UpdateProxy();
            imagePrefab.uid = "";
            imagePrefab.ApplyProxyModifications();
            
            EditorUtility.SetDirty(instantiated);
        }
        
        [MenuItem("Tools/AvatarImageReader/Create Image Reader (Debug)")]
        private static void CreateDebug()
        {
            GameObject toInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabDebug);
            GameObject instantiated = UnityEngine.Object.Instantiate(toInstantiate);
            instantiated.name = "New avatar image reader (debug)";
            AvatarImagePrefab imagePrefab = instantiated.GetUdonSharpComponent<AvatarImagePrefab>();
            imagePrefab.UpdateProxy();
            imagePrefab.uid = "";
            imagePrefab.ApplyProxyModifications();
            
            EditorUtility.SetDirty(instantiated);
        }
    }
}