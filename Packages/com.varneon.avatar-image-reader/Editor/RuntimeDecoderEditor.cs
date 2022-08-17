#if UNITY_EDITOR && !COMPILER_UDONSHARP

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
using AvatarImageReader.Enums;

using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AvatarImageReader.Editor
{
    [CustomEditor(typeof(RuntimeDecoder))]
    public class RuntimeDecoderEditor : UnityEditor.Editor
    {
        [SerializeField]
        private VisualTreeAsset inspectorUXML;

        private Dictionary<Platform, Vector2> platformResolutions = new Dictionary<Platform, Vector2>() { { Platform.Android, new Vector2(128, 96) }, { Platform.PC, new Vector2(1200, 900) } };

        private Platform currentPlatform;

        private Vector2 currentResolution;

        public RuntimeDecoder reader;
        private string text { get => textStorageObject.text; set => textStorageObject.text = value; }

        private const string quadMaterialPath = "Packages/com.varneon.avatar-image-reader/Materials/RenderQuad.mat";

        private const string pcDonorImagePath = "Packages/com.varneon.avatar-image-reader/DonorImages/PC.png";
        private const string questDonorImagePath = "Packages/com.varneon.avatar-image-reader/DonorImages/Quest.png";

        private const string pcRTPath = "Packages/com.varneon.avatar-image-reader/DonorImages/PCCRT.asset";
        private const string questRTPath = "Packages/com.varneon.avatar-image-reader/DonorImages/QuestCRT.asset";

        private int pixelCount;
        private int maxByteCount;
        private int currentByteCount;

        private Texture2D[] output;
        private GUIContent[] texturePreview;

        private int imageWidth;
        private int imageHeight;

        #region UIElements Inspector References
        private VisualElement capacityExceededError;

        private Label totalLinkedAvatarCountLabel;
        private Label remainingCapacityLabel;

        private Button setOrChangeAvatarButton;
        private Button unlinkAvatarsButton;
        #endregion

        private TextStorageObject textStorageObject;
        private Platform lastImageMode;

        private bool init = false;

        private void OnEnable()
        {
            if (reader == null) { reader = (RuntimeDecoder)target; }

            if (!init) Init();

            // Make sure the first avatar is always initialized
            if (reader.linkedAvatars == null || reader.linkedAvatars.Length == 0)
            {
                reader.linkedAvatars = new string[1];
                reader.linkedAvatars[0] = "";
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (reader == null) { reader = (RuntimeDecoder)target; }

            SerializedObject decoderSO = new SerializedObject(reader);

            VisualElement root = inspectorUXML.CloneTree();

            root.Q<IMGUIContainer>("IMGUIContainer_AvatarPreview").onGUIHandler += () => VRChatApiToolsGUI.DrawBlueprintInspector(reader.linkedAvatars[0]);

            totalLinkedAvatarCountLabel = root.Q<Label>("Label_TotalLinkedAvatarCount");

            UpdateTotalLinkedAvatarCountLabel();

            // Initialize the avatar action buttons
            setOrChangeAvatarButton = root.Q<Button>("Button_SetOrChangeAvatar");
            unlinkAvatarsButton = root.Q<Button>("Button_UnlinkAvatars");

            setOrChangeAvatarButton.clicked += () => BlueprintPicker.BlueprintSelector<ApiAvatar>(avatar => AvatarSelected(avatar, 0));
            root.Q<Button>("Button_ManageAvatars").clicked += () => MultiAvatarManager.SpawnEditor(this);
            root.Q<Button>("Button_UnlinkAvatars").clicked += () => {
                reader.linkedAvatars = new string[0];
                AvatarSelected(null, 0);
                MarkFirstAvatarAsValid();
            };

            remainingCapacityLabel = root.Q<Label>("Label_RemainingCharactersPreview");
            capacityExceededError = root.Q("ErrorBox_CharactersExceeded");

            Action<DataMode> onDataModeChanged = (DataMode newDataMode) =>
            {
                root.Q<Label>("Label_RemainingDataCapacity").text = GetDataModeRemainingCapacityLabel(newDataMode);

                UpdateRemainingCapacityLabel(newDataMode);
            };

            // Data Encoding > Data Mode
            EnumField dataModeField = root.Q<EnumField>("EnumField_DataMode");
            dataModeField.Init(reader.dataMode);
            dataModeField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.dataMode)));
            dataModeField.RegisterValueChangedCallback(a => onDataModeChanged((DataMode)a.newValue));
            onDataModeChanged(reader.dataMode);

            // Create action for when the link Patreon decoder toggle state changes
            Action<bool> setPatreonDecoderLinkedState = (bool isLinked) =>
            {
                // Data Mode enum field should be disabled at all times since other data modes don't have support yet
                //dataModeField.SetEnabled(!isLinked);
                //if (isLinked) { dataModeField.value = DataMode.UTF16; }

                SetElementsVisibleState(isLinked, root.Q("HelpBox_PatreonDecoderInfo"));
            };

            // Data Encoding > Link Patreon Decoder
            Toggle linkPatreonDecoderToggle = root.Q<Toggle>("Toggle_LinkPatreonDecoder");
            linkPatreonDecoderToggle.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.patronMode)));
            linkPatreonDecoderToggle.RegisterValueChangedCallback(a => setPatreonDecoderLinkedState(a.newValue));
            setPatreonDecoderLinkedState(linkPatreonDecoderToggle.value);

            // Workaround for error 'Generated text will be truncated because it exceeds 49152 vertices.'
            // Use the IMGUI TextArea instead of UIElements TextField
            IMGUIContainer dataInputIMGUIContainer = root.Q<IMGUIContainer>("IMGUIContainer_DataInput");
            dataInputIMGUIContainer.onGUIHandler = () =>
            {
                using (var scope = new EditorGUI.ChangeCheckScope())
                {
                    string data = GUILayout.TextArea(text, EditorStyles.textArea);

                    if (scope.changed)
                    {
                        text = data;

                        UpdateRemainingCapacityLabel(reader.dataMode);
                    }
                }
            };

            // Create action for changing the platform
            Action<Platform> updateImageModeAction = (Platform platform) => {
                root.Q<Label>("Label_ResolutionPreview").text = GetPlatformResolutionPreviewText(platform);
                SetPlatform(platform);
                UpdateRemainingCapacityLabel(reader.dataMode);
            };

            // Image Options > Image Mode
            EnumField imageModeField = root.Q<EnumField>("EnumField_ImageMode");
            imageModeField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.imageMode)));
            imageModeField.RegisterValueChangedCallback(a => updateImageModeAction((Platform)a.newValue));
            imageModeField.Init(reader.imageMode);
            updateImageModeAction((Platform)imageModeField.value);

            // Data Encoding > Encode Image(s)
            root.Q<Button>("Button_EncodeImages").clicked += () => EncodeImages();

            root.Q<IMGUIContainer>("IMGUIContainer_EncodedImages").onGUIHandler = () => DisplayEncodedImages();

            // General > Event Name
            TextField callbackEventNameField = root.Q<TextField>("TextField_CallbackEventName");
            callbackEventNameField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.callbackEventName)));

            // General > Target UdonBehaviour
            ObjectField callbackUdonBehaviourField = root.Q<ObjectField>("ObjectField_CallbackUdonBehaviour");
            callbackUdonBehaviourField.objectType = typeof(UdonBehaviour);
            callbackUdonBehaviourField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.callbackBehaviour)));
            callbackUdonBehaviourField.RegisterValueChangedCallback(a => { SetElementsVisibleState(a.newValue != null, callbackEventNameField); });
            SetElementsVisibleState(callbackUdonBehaviourField.value != null, callbackEventNameField);

            // Create action for setting UdonBehaviour callback enabled
            Action<bool> enableSendCustomEventAction = (bool enabled) =>
            {
                SetElementsVisibleState(enabled, callbackUdonBehaviourField);
                SetElementsVisibleState(enabled && callbackUdonBehaviourField.value != null, callbackEventNameField);
            };

            // General > Send Custom Event
            Toggle sendCustomEventToggle = root.Q<Toggle>("Toggle_SendCustomEvent");
            sendCustomEventToggle.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.callBackOnFinish)));
            sendCustomEventToggle.RegisterValueChangedCallback(a => {
                enableSendCustomEventAction(a.newValue);
            });
            enableSendCustomEventAction(sendCustomEventToggle.value);

            // General > Target TextMeshPro
            ObjectField outputTMPField = root.Q<ObjectField>("ObjectField_OutputTMP");
            outputTMPField.objectType = typeof(TextMeshPro);
            outputTMPField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.outputText)));

            // General > Auto Fill TextMeshPro
            Toggle autoFillTMPToggle = root.Q<Toggle>("Toggle_AutoFillTMP");
            autoFillTMPToggle.BindProperty(decoderSO.FindProperty("autoFillTMP"));

            // General > Output to TextMeshPro
            Toggle outputToTMPToggle = root.Q<Toggle>("Toggle_OutputToTMP");
            outputToTMPToggle.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.outputToText)));
            outputToTMPToggle.RegisterValueChangedCallback(a => { SetElementsVisibleState(a.newValue, outputTMPField, autoFillTMPToggle); });
            SetElementsVisibleState(outputToTMPToggle.value, outputTMPField, autoFillTMPToggle);

            // Debugging > Target TextMeshPro
            ObjectField logTMPField = root.Q<ObjectField>("ObjectField_LogTMP");
            logTMPField.objectType = typeof(TextMeshPro);
            logTMPField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.loggerText)));

            // Debugging > Log to TextMeshPro
            Toggle logToTMPToggle = root.Q<Toggle>("Toggle_EnableDebugTMP");
            logToTMPToggle.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.debugTMP)));
            logToTMPToggle.RegisterValueChangedCallback(a => SetElementsVisibleState(a.newValue, logTMPField));
            SetElementsVisibleState(logToTMPToggle.value, logTMPField);

            Action<bool> setDebugEnabledAction = (bool debugEnabled) =>
            {
                SetElementsVisibleState(debugEnabled, logToTMPToggle);
                SetElementsVisibleState(debugEnabled && logToTMPToggle.value, logTMPField);
            };

            // Debugging > Enable Debug Logging
            Toggle enableDebugToggle = root.Q<Toggle>("Toggle_EnableDebug");
            enableDebugToggle.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.debugLogging)));
            enableDebugToggle.RegisterValueChangedCallback(a => {
                setDebugEnabledAction(a.newValue);
            });
            setDebugEnabledAction(enableDebugToggle.value);

            return root;
        }

        private void Init()
        {
            if (reader == null)
                return;

            SetPlatform(reader.imageMode);

            //set up TextStorageObject monobehaviour
            if (reader.GetComponentInChildren<TextStorageObject>())
            {
                textStorageObject = reader.GetComponentInChildren<TextStorageObject>();
            }
            else
            {
                GameObject container = new GameObject("TextStorageObject") { tag = "EditorOnly" };
                container.transform.SetParent(reader.transform);
                container.AddComponent<TextStorageObject>();
                container.hideFlags = HideFlags.HideInHierarchy;
            }

            if (!reader.pedestalAssetsReady)
            {
                UpdatePedestalAssets();
                reader.pedestalAssetsReady = true;
                MarkDirty();
            }

            lastImageMode = reader.imageMode;

            init = true;
        }

        private void MarkFirstAvatarAsValid()
        {
            bool isValid = !string.IsNullOrWhiteSpace(reader.linkedAvatars[0]);

            setOrChangeAvatarButton.text = isValid ? "Change Avatar" : "Set avatar...";
            unlinkAvatarsButton.SetEnabled(isValid);
        }

        internal void OnLinkedAvatarsUpdated()
        {
            MarkFirstAvatarAsValid();

            UpdateTotalLinkedAvatarCountLabel();

            UpdateDataCapacity();
        }

        private void UpdateTotalLinkedAvatarCountLabel()
        {
            totalLinkedAvatarCountLabel.text = string.Format("Total linked avatar count: {0}", reader.linkedAvatars.Length);
        }

        private void UpdateRemainingCapacityLabel(DataMode dataMode)
        {
            bool exceedsCapacity = false;

            switch (dataMode)
            {
                case DataMode.UTF16:
                    exceedsCapacity = maxByteCount / 2 < text.Length;
                    remainingCapacityLabel.text = $"{maxByteCount / 2 - text.Length:n0} / {maxByteCount / 2:n0} ({((float)maxByteCount / 2 - text.Length) / ((float)maxByteCount / 2) * 100:n0}%)";
                    break;
                case DataMode.UTF8:
                    currentByteCount = Encoding.UTF8.GetByteCount(text);
                    exceedsCapacity = maxByteCount < currentByteCount;
                    remainingCapacityLabel.text = $"{currentByteCount} / {maxByteCount} ({(float)currentByteCount / (float)maxByteCount * 100:n0}%)";
                    break;
            }

            SetElementsVisibleState(exceedsCapacity, capacityExceededError);
        }

        private string GetDataModeRemainingCapacityLabel(DataMode dataMode)
        {
            switch (dataMode)
            {
                case DataMode.UTF16:
                    return "Remaining Characters:";
                case DataMode.UTF8:
                    return "Remaining Bytes:";
                default:
                    throw new NotImplementedException();
            }
        }

        private void DisplayEncodedImages()
        {
            if (output != null)
            {
                for (int i = 0; i < texturePreview.Length && i < reader.linkedAvatars.Length; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.BeginVertical(GUILayout.Width(100));
                    GUILayout.Box(texturePreview[i]);
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField($"Image {i + 1}/{texturePreview.Length}", GUILayout.Width(100));
                    EditorGUILayout.LabelField("Image dimensions: ", GUILayout.Width(120));
                    EditorGUILayout.LabelField("Image data type: ", GUILayout.Width(120));
                    EditorGUILayout.LabelField("Target avatar id: ", GUILayout.Width(120));
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.BeginVertical();
                    if (GUILayout.Button("Save Image", GUILayout.Width(120)))
                    {
                        string path = EditorUtility.SaveFilePanel(
                            "Save texture as PNG",
                            Application.dataPath,
                            "output.png",
                            "png");

                        if (path.Length != 0)
                        {
                            byte[] pngData = output[i].EncodeToPNG();
                            if (pngData != null)
                                File.WriteAllBytes(path, pngData);

                            path = "Assets" + path.Substring(Application.dataPath.Length);

                            AssetDatabase.WriteImportSettingsIfDirty(path);
                            AssetDatabase.ImportAsset(path);

                            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                            importer.npotScale = TextureImporterNPOTScale.None;
                            importer.textureCompression = TextureImporterCompression.Uncompressed;
                            importer.maxTextureSize = 2048;
                            EditorUtility.SetDirty(importer);
                            AssetDatabase.WriteImportSettingsIfDirty(path);

                            AssetDatabase.ImportAsset(path);
                        }
                    }
                    EditorGUILayout.LabelField($"{imageWidth} x {imageHeight}");
                    EditorGUILayout.LabelField("UTF16 Characters");
                    EditorGUILayout.LabelField(reader.linkedAvatars[i]);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }

                bool uploadBlocked = string.IsNullOrEmpty(reader.linkedAvatars[0]) || reader.linkedAvatars.Length < texturePreview.Length;

                EditorGUI.BeginDisabledGroup(uploadBlocked);
                GUIContent uploadButton = uploadBlocked
                        ? new GUIContent("Upload Image(s) to Avatar(s)",
                            "You don't currently have enough linked avatars to upload this data")
                        : new GUIContent("Upload Image(s) to Avatar(s)");
                if (GUILayout.Button(uploadButton))
                {
                    RunUploadTask(output, reader.linkedAvatars);
                }
                EditorGUI.EndDisabledGroup();
            }
        }

        private void SetPlatform(Platform platform)
        {
            currentPlatform = platform;
            currentResolution = platformResolutions[platform];
            UpdateDataCapacity();
        }

        private void UpdateDataCapacity()
        {
            pixelCount = (int)currentResolution.x * (int)currentResolution.y * reader.linkedAvatars.Length;
            maxByteCount = (pixelCount - 5) * 4;
        }

        private void SetElementsVisibleState(bool visible, params VisualElement[] elements)
        {
            foreach(VisualElement element in elements)
            {
                element.EnableInClassList("hidden-element", !visible);
            }
        }

        private string GetPlatformResolutionPreviewText(Platform platform)
        {
            Vector2 resolution = platformResolutions[platform];

            return string.Format("{0}x{1}", resolution.x, resolution.y);
        }

        private void EncodeImages()
        {
            imageWidth = reader.imageMode == 0 ? 128 : 1200;
            imageHeight = reader.imageMode == 0 ? 96 : 900;

            output = AvatarImageEncoder.Encode(reader.dataMode, text, reader.linkedAvatars, imageWidth, imageHeight);

            texturePreview = new GUIContent[output.Length];
            for (int i = 0; i < output.Length; i++)
            {
                texturePreview[i] = new GUIContent(output[i]);
            }
        }

        #region LEGACY CODE
        
        private Vector2 scrollview;
        private int imageContent;
       
        
        public override void OnInspectorGUI()
        {
            return;

            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            reader = (RuntimeDecoder)target;

            if(!init) Init();
            
            reader.UpdateProxy();
            
            //make sure the first avatar is always initialised
            if (reader.linkedAvatars == null || reader.linkedAvatars.Length == 0)
            {
                reader.linkedAvatars = new string[1];
                reader.linkedAvatars[0] = "";
            }

            GUIStyle bigHeaderStyle = new GUIStyle(EditorStyles.label) {richText = true, fontSize = 15};
            GUIStyle headerStyle = new GUIStyle(EditorStyles.label) {richText = true};
            
            EditorGUILayout.LabelField("<b>Avatar Image Reader</b>", bigHeaderStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("<b>Main Avatar</b>", headerStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Total linked avatar count: {reader.linkedAvatars.Length}", GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();
            
            VRChatApiToolsGUI.DrawBlueprintInspector(reader.linkedAvatars[0]);

            EditorGUILayout.BeginHorizontal();
            string changeAvatarString = reader.linkedAvatars[0].IsNullOrWhitespace() ? "Set avatar..." : "Change Avatar";
            if (GUILayout.Button(changeAvatarString))
            {
                BlueprintPicker.BlueprintSelector<ApiAvatar>(avatar => AvatarSelected(avatar, 0));
            }

            EditorGUI.BeginDisabledGroup(reader.linkedAvatars[0].IsNullOrWhitespace());
            if (GUILayout.Button("Manage Avatars"))
            {
                MultiAvatarManager.SpawnEditor(this);
            }
            Color temp = GUI.backgroundColor;
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Unlink Avatars"))
            {
                reader.linkedAvatars = new string[0];
                
                reader.ApplyProxyModifications();
                
                AvatarSelected(null, 0);
            }
            GUI.backgroundColor = temp;
            EditorGUI.EndDisabledGroup();
            
            /*
            EditorGUI.BeginDisabledGroup(!APIUser.IsLoggedIn);
            if (GUILayout.Button("Create Empty"))
            {
                //TODO Create Empty Avatar
            }
            
            EditorGUI.EndDisabledGroup();
            */
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
            
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("<b>Image Options</b>", headerStyle);

            int pixelCount = 0;
            
            reader.imageMode = (Platform)EditorGUILayout.EnumPopup("Image mode: ", reader.imageMode);

            if (reader.imageMode != lastImageMode)
            {
                UpdatePedestalAssets();
            }
            
            switch (reader.imageMode)
            {
                case Platform.Android:
                    EditorGUILayout.LabelField("Target resolution: ", "128x96");
                    pixelCount = 128 * 96 * reader.linkedAvatars.Length;
                    break;
                
                case Platform.PC:
                    EditorGUILayout.HelpBox("You should only use PC Only mode if you are absolutely sure you are going to use all of the space it allows you to use.", MessageType.Warning);
                    EditorGUILayout.LabelField("Target resolution: ", "1200x900");
                    pixelCount = 1200 * 900 * reader.linkedAvatars.Length;
                    break;
            }
            EditorGUILayout.Space(4);
            if (EditorGUI.EndChangeCheck())
            {
                MarkDirty();
            }
            
            
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("<b>Data encoding</b>", headerStyle);

            using (new EditorGUI.DisabledGroupScope(true))
            {
                reader.dataMode = (DataMode)EditorGUILayout.EnumPopup("Data mode: ", reader.dataMode);
            }

            reader.patronMode = EditorGUILayout.Toggle("Link with Patreon Decoder:", reader.patronMode);
            if (reader.patronMode)
            {
                reader.dataMode = 0;
                EditorGUILayout.HelpBox("Make sure to link this reader to a decoder, and to select this reader on the decoder object!", MessageType.Info);
            }

            EditorGUI.BeginDisabledGroup(reader.patronMode);

            //remove 5 pixels (header)
            int byteCount = (pixelCount - 5) * 4;

            switch (reader.dataMode)
            {
                case DataMode.UTF16:
                    EditorGUILayout.LabelField("Remaining characters: ", $"{byteCount / 2 - text.Length:n0} / {byteCount / 2:n0} ({((float)byteCount / 2 - text.Length) / ((float)byteCount / 2) * 100:n0}%)");

                    using (GUILayout.ScrollViewScope scroll = new GUILayout.ScrollViewScope(scrollview, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(200))) //, GUILayout.MaxHeight(300)))
                    {
                        scrollview = scroll.scrollPosition;
                        GUIStyle textArea = new GUIStyle(EditorStyles.textArea) {wordWrap = true };
                        text = EditorGUILayout.TextArea(text, textArea, GUILayout.ExpandHeight(true));
                    }
                    
                    GUILayout.FlexibleSpace();
                    
                    
                    if (text.Length > byteCount / 2)
                    {
                        EditorGUILayout.HelpBox("You are using more characters than the image can fit. Excess characters will be trimmed off.", MessageType.Error);
                    }

                    if (GUILayout.Button("Encode Image(s)"))
                    {
                        imageWidth = reader.imageMode == 0 ? 128 : 1200;
                        imageHeight = reader.imageMode == 0 ? 96 : 900;

                        output = AvatarImageEncoder.EncodeUTF16Text(text, reader.linkedAvatars, imageWidth, imageHeight);

                        texturePreview = new GUIContent[output.Length];
                        for (int i = 0; i < output.Length; i++)
                        {
                            texturePreview[i] = new GUIContent(output[i]);
                        }
                    }

                    if (output != null)
                    {
                        for (int i = 0; i < texturePreview.Length && i < reader.linkedAvatars.Length; i++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.BeginVertical(GUILayout.Width(100));
                            GUILayout.Box(texturePreview[i]);
                            EditorGUILayout.EndVertical();
                            
                            EditorGUILayout.BeginVertical();
                            
                            EditorGUILayout.BeginHorizontal();
                            
                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.LabelField($"Image {i+1}/{texturePreview.Length}", GUILayout.Width(100));
                            EditorGUILayout.LabelField("Image dimensions: ", GUILayout.Width(120));
                            EditorGUILayout.LabelField("Image data type: ", GUILayout.Width(120));
                            EditorGUILayout.LabelField("Target avatar id: ", GUILayout.Width(120));
                            EditorGUILayout.EndVertical();
                            
                            EditorGUILayout.BeginVertical();
                            if (GUILayout.Button("Save Image", GUILayout.Width(120)))
                            {
                                string path = EditorUtility.SaveFilePanel(
                                    "Save texture as PNG",
                                    Application.dataPath,
                                    "output.png",
                                    "png");

                                if (path.Length != 0)
                                {
                                    byte[] pngData = output[i].EncodeToPNG();
                                    if (pngData != null)
                                        File.WriteAllBytes(path, pngData);

                                    path = "Assets" + path.Substring(Application.dataPath.Length);

                                    AssetDatabase.WriteImportSettingsIfDirty(path);
                                    AssetDatabase.ImportAsset(path);

                                    TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                                    importer.npotScale = TextureImporterNPOTScale.None;
                                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                                    importer.maxTextureSize = 2048;
                                    EditorUtility.SetDirty(importer);
                                    AssetDatabase.WriteImportSettingsIfDirty(path);

                                    AssetDatabase.ImportAsset(path);
                                }
                            }
                            EditorGUILayout.LabelField($"{imageWidth} x {imageHeight}");
                            EditorGUILayout.LabelField("UTF16 Characters");
                            EditorGUILayout.LabelField(reader.linkedAvatars[i]);
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();
                            
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();
                        }
                        
                        bool uploadBlocked = string.IsNullOrEmpty(reader.linkedAvatars[0]) || reader.linkedAvatars.Length < texturePreview.Length;

                        EditorGUI.BeginDisabledGroup(uploadBlocked);
                        GUIContent uploadButton = uploadBlocked
                                ? new GUIContent("Upload Image(s) to Avatar(s)",
                                    "You don't currently have enough linked avatars to upload this data")
                                : new GUIContent("Upload Image(s) to Avatar(s)");
                        if (GUILayout.Button(uploadButton))
                        {
                            RunUploadTask(output, reader.linkedAvatars);
                        }
                        EditorGUI.EndDisabledGroup();
                    }
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

            reader.outputToText = EditorGUILayout.Toggle("Output to TextMeshPro", reader.outputToText);
            if (reader.outputToText)
            {
                reader.autoFillTMP =
                    EditorGUILayout.Toggle(
                        new GUIContent("Auto fill TMP",
                            "Enabling this will automatically replace the text inside the output TMP so at least some data (albeit not necessarily up to date) will be shown if loading fails."),
                        reader.autoFillTMP);
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
            reader.debugLogging = EditorGUILayout.Toggle("Enable debug logging", reader.debugLogging);
            
            if (reader.debugLogging)
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
            //textStorageObject.text = text;
            
            reader.ApplyProxyModifications();
            
            if (reader.outputToText)
            {
                if (reader.outputText != null && reader.autoFillTMP)
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

        private static async void RunUploadTask(Texture2D[] textures, string[] avatarIDs)
        {
            try
            {
                EditorApplication.LockReloadAssemblies();
                for (int index = 0; index < textures.Length; index++)
                {
                    Texture2D texture = textures[index];
                    ApiAvatar avatar = await VRChatApiTools.FetchApiAvatarAsync(avatarIDs[index]);
                    if (avatar.authorId != APIUser.CurrentUser.id)
                    {
                        throw new Exception("Logged in user doesn't own the target avatar");
                    }

                    VRChatApiUploaderAsync uploader = new VRChatApiUploaderAsync();
                    uploader.UseStatusWindow();
                    uploader.uploadStatus.titleContent =
                        new GUIContent($"Uploading Avatar Image {index + 1} / {textures.Length}");
                    try
                    {
                        await uploader.UpdateBlueprintImage(avatar, texture);
                    }
                    catch (Exception e)
                    {
                        if (e.Message.Contains("The file to upload matches the remote file already."))
                        {
                            //do nothing
                        }
                        else throw;
                    }

                    uploader.uploadStatus.Close();
                }
                
                EditorUtility.DisplayDialog($"Avatar Image Reader", "Avatar Image Upload(s) successful!", "Close");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog($"Avatar Image Reader", $"Uploading avatars failed: An unhandled exception occured: {e.Message}", "Close");
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        private bool checksFailedReadRenderTexture = false;
        private bool checksFailedPedestal = false;

        private void UpdatePedestalAssets()
        {
            Material renderQuadMat = AssetDatabase.LoadAssetAtPath<Material>(quadMaterialPath);

            Texture2D pcDonor = AssetDatabase.LoadAssetAtPath<Texture2D>(pcDonorImagePath);
            Texture2D questDonor = AssetDatabase.LoadAssetAtPath<Texture2D>(questDonorImagePath);
            
            CustomRenderTexture pcRT = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(pcRTPath);
            CustomRenderTexture questRT = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(questRTPath);

            reader.UpdateProxy();
            reader.renderTexture = reader.imageMode == 0 ? questRT : pcRT;
            reader.donorInput = reader.imageMode == 0 ? questDonor : pcDonor;

            reader.GetComponent<MeshRenderer>().material = renderQuadMat;
            reader.GetComponent<Camera>().targetTexture = reader.imageMode == 0 ? questRT : pcRT;
            
            reader.ApplyProxyModifications();
            
            EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader));

            if (PrefabUtility.IsPartOfAnyPrefab(reader.gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(reader));
            }
        }

        public void AvatarSelected(ApiAvatar avatar, int avatarIndex)
        {
            //invalidate existing output
            output = null;
            texturePreview = null;
            
            if (reader == null)
            {
                Debug.LogError("[AvatarImagePrefabEditor] Avatar was selected but inspector target is null; inspector was likely closed.");
            }

            reader.UpdateProxy();

            if (reader.linkedAvatars.Any(id => id == avatar.id))
            {
                Debug.LogError("The selected avatar is already in the list");
                return;
            }
            
            //make sure there is room in the array
            while (reader.linkedAvatars.Length <= avatarIndex)
            {
                ArrayUtility.Add(ref reader.linkedAvatars, "");
            }

            if (avatar == null)
            {
                reader.linkedAvatars[avatarIndex] = "";
                
                if (avatarIndex == 0)
                {
                    reader.avatarPedestal.blueprintId = "";
                }
            }
            else
            {
                reader.linkedAvatars[avatarIndex] = avatar.id;
                
                if (avatarIndex == 0)
                {
                    reader.avatarPedestal.blueprintId = avatar.id;
                }
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

            MarkFirstAvatarAsValid();

            UpdateTotalLinkedAvatarCountLabel();

            UpdateDataCapacity();
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

    public class MultiAvatarManager : EditorWindow
    {
        public static void SpawnEditor(RuntimeDecoderEditor prefabEditor)
        {
            MultiAvatarManager window = GetWindow<MultiAvatarManager>();
            window.titleContent = new GUIContent("Linked Avatar Editor");
            window.minSize = new Vector2(450, 400);
            window.prefabEditor = prefabEditor;
        }

        public RuntimeDecoderEditor prefabEditor;
        private Vector2 scrollView;
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField($"Multi Avatar Manager for {prefabEditor.reader.name}");
            scrollView = EditorGUILayout.BeginScrollView(scrollView);

            bool ownershipError = false;
            
            for (int a = 0; a < prefabEditor.reader.linkedAvatars.Length; a++)
            {
                VRChatApiToolsGUI.DrawBlueprintInspector(prefabEditor.reader.linkedAvatars[a], true, () =>
                {
                    if (GUILayout.Button("Change Avatar"))
                    {
                        BlueprintPicker.BlueprintSelector<ApiAvatar>(avatar => prefabEditor.AvatarSelected(avatar, a));
                    }
                    
                    if (GUILayout.Button("Remove Avatar"))
                    {
                        ArrayUtility.RemoveAt(ref prefabEditor.reader.linkedAvatars, a);
                        prefabEditor.reader.ApplyProxyModifications();
                        prefabEditor.OnLinkedAvatarsUpdated();
                    }
                });
                
                if(VRChatApiTools.blueprintCache.TryGetValue(prefabEditor.reader.linkedAvatars[a], out ApiModel m))
                {
                    if (m is ApiAvatar avatar && avatar.authorId != APIUser.CurrentUser.id)
                    {
                        ownershipError = true;
                    }
                }
            }

            if (ownershipError)
            {
                EditorGUILayout.HelpBox("One or more avatars isn't owned by the currently logged in user",
                    MessageType.Error);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current avatar count: ", prefabEditor.reader.linkedAvatars.Length.ToString());
            if (GUILayout.Button("Add new Avatar"))
            {
                BlueprintPicker.BlueprintSelector<ApiAvatar>(avatar => prefabEditor.AvatarSelected(avatar, prefabEditor.reader.linkedAvatars.Length));
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    public static class AvatarImageTools
    {
        private const string prefabNormal = "Packages/com.varneon.avatar-image-reader/Prefabs/Decoder.prefab";
        private const string prefabText = "Packages/com.varneon.avatar-image-reader/Prefabs/DecoderWithText.prefab";
        private const string prefabDebug = "Packages/com.varneon.avatar-image-reader/Prefabs/Decoder_Debug.prefab";

        [MenuItem("Tools/AvatarImageReader/Create Image Reader")]
        private static void CreateNormal()
        {
            GameObject toInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabNormal);
            GameObject instantiated = UnityEngine.Object.Instantiate(toInstantiate);
            instantiated.name = "New avatar image reader";
            RuntimeDecoder imagePrefab = instantiated.GetUdonSharpComponent<RuntimeDecoder>();
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
            RuntimeDecoder imagePrefab = instantiated.GetUdonSharpComponent<RuntimeDecoder>();
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
            RuntimeDecoder imagePrefab = instantiated.GetUdonSharpComponent<RuntimeDecoder>();
            imagePrefab.UpdateProxy();
            imagePrefab.uid = "";
            imagePrefab.ApplyProxyModifications();
            
            EditorUtility.SetDirty(instantiated);
        }

        #endregion
    }
}

#endif