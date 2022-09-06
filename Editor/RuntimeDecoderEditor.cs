using AvatarImageReader.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.Core;
using VRC.Udon;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using Platform = AvatarImageReader.Enums.Platform;

#if VRCHAT_API_TOOLS_IMPORTED
using BocuD.VRChatApiTools;
#endif

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

        /// <summary>
        /// Text input stored on the decoder
        /// </summary>
        /// <remarks>
        /// TextStorageObject is a required component on the decoder and guaranteed to always exist
        /// </remarks>
        private string text { get => textStorageObject.text; set => textStorageObject.text = value; }

        private const string quadMaterialPath = "Packages/com.miner28.avatar-image-reader/Materials/RenderQuad.mat";

        private const string pcDonorImagePath = "Packages/com.miner28.avatar-image-reader/DonorImages/PC.png";
        private const string questDonorImagePath = "Packages/com.miner28.avatar-image-reader/DonorImages/Quest.png";

        private const string pcRTPath = "Packages/com.miner28.avatar-image-reader/DonorImages/PCCRT.asset";
        private const string questRTPath = "Packages/com.miner28.avatar-image-reader/DonorImages/QuestCRT.asset";

        private const string FOLDOUT_PERSISTENCE_KEY = "Miner28/AvatarImageReader/RuntimeDecoder/Editor/InspectorFoldouts";

        private List<Foldout> foldouts;

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

            // Cache the TextStorageObject on the decoder
            textStorageObject = reader.GetComponent<TextStorageObject>();

            foreach (Component c in reader.GetComponents<Component>())
            {
                if(c == reader || c.GetType() == typeof(Transform) || c == UdonSharpEditorUtility.GetBackingUdonBehaviour(reader)) { continue; }
                c.hideFlags = HideFlags.HideInInspector;
            }

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

            remainingCapacityLabel = root.Q<Label>("Label_RemainingCharactersPreview");
            capacityExceededError = root.Q("ErrorBox_CharactersExceeded");

            SetPlatform(reader.imageMode);

#if VRCHAT_API_TOOLS_IMPORTED
            root.Q<IMGUIContainer>("IMGUIContainer_AvatarPreview").onGUIHandler += () => VRChatApiToolsGUI.DrawBlueprintInspector(reader.linkedAvatars[0], false);

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
#else
            SetElementsVisibleState(true, root.Q("ErrorBox_VRChatApiToolsNotImported"));
            root.Q<Button>("Button_VRChatApiToolsGitHub").clicked += () => Application.OpenURL("https://github.com/BocuD/VRChatApiTools");
            SetElementsVisibleState(false, root.Q("AvatarPreview"));
            PropertyField linkedAvatarsFallbackField = root.Q<PropertyField>("PropertyField_LinkedAvatarsFallback");
            linkedAvatarsFallbackField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.linkedAvatars)));
            SetElementsVisibleState(true, linkedAvatarsFallbackField);
#endif

            Action<DataMode> onDataModeChanged = UpdateRemainingCapacityLabel;

            // Data Encoding > Data Mode
            EnumField dataModeField = root.Q<EnumField>("EnumField_DataMode");
            dataModeField.Init(reader.dataMode);
            dataModeField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.dataMode)));
            dataModeField.RegisterValueChangedCallback(a => { if (a.newValue != null) { onDataModeChanged((DataMode)a.newValue); } });
            onDataModeChanged(reader.dataMode);

            // Create action for when the link Patreon decoder toggle state changes
            Action<bool> setPatreonDecoderLinkedState = (bool isLinked) =>
            {
                // Varneon: If Patreon decoder is linked, should the data mode be enforced to UTF16?
                //if (isLinked) { dataModeField.value = DataMode.UTF16; }

                SetElementsVisibleState(isLinked, root.Q("HelpBox_PatreonDecoderInfo"));
            };

            // Data Encoding > Link Patreon Decoder
            Toggle linkPatreonDecoderToggle = root.Q<Toggle>("Toggle_LinkPatreonDecoder");
            linkPatreonDecoderToggle.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.patronMode)));
            linkPatreonDecoderToggle.RegisterValueChangedCallback(a => setPatreonDecoderLinkedState(a.newValue));
            setPatreonDecoderLinkedState(linkPatreonDecoderToggle.value);

#if VRCHAT_API_TOOLS_IMPORTED
            // Workaround for error 'Generated text will be truncated because it exceeds 49152 vertices.'
            // Use the IMGUI TextArea instead of UIElements TextField
            IMGUIContainer dataInputIMGUIContainer = root.Q<IMGUIContainer>("IMGUIContainer_DataInput");
            dataInputIMGUIContainer.onGUIHandler = () =>
            {
                using (var scope = new EditorGUI.ChangeCheckScope())
                {
                    GUIStyle textArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                    text = EditorGUILayout.TextArea(text, textArea, GUILayout.ExpandHeight(true));

                    if (scope.changed)
                    {
                        UpdateRemainingCapacityLabel(reader.dataMode);
                    }
                }
            };

            // Data Encoding > Encode Image(s)
            root.Q<Button>("Button_EncodeImages").clicked += () => EncodeImages();
#else
            SetElementsVisibleState(false, root.Q("DataEncoding"));
            SetElementsVisibleState(false, root.Q("ImageOptions"));
#endif

            // Create action for changing the platform
            Action<Platform> updateImageModeAction = (Platform platform) => {
                root.Q<Label>("Label_ResolutionPreview").text = GetPlatformResolutionPreviewText(platform);
                SetPlatform(platform);
                UpdateRemainingCapacityLabel(reader.dataMode);
            };

            // Image Options > Image Mode
            EnumField imageModeField = root.Q<EnumField>("EnumField_ImageMode");
            imageModeField.Init(reader.imageMode);
            imageModeField.BindProperty(decoderSO.FindProperty(nameof(RuntimeDecoder.imageMode)));
            imageModeField.RegisterValueChangedCallback(a => { if (a.newValue != null) { updateImageModeAction((Platform)a.newValue); } });
            updateImageModeAction((Platform)imageModeField.value);

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

            // Get all foldouts in the inspector
            foldouts = root.Query<Foldout>().Build().ToList();

            // If the editor preference key for foldout states exists, apply it
            if (EditorPrefs.HasKey(FOLDOUT_PERSISTENCE_KEY))
            {
                int states = EditorPrefs.GetInt(FOLDOUT_PERSISTENCE_KEY);

                for (int i = 0; i < foldouts.Count; i++)
                {
                    foldouts[i].value = (states & (1 << i)) != 0;
                }
            }

            return root;
        }

        private void OnDestroy()
        {
            ApplyInspectorFoldoutPersistenceState();
        }

        /// <summary>
        /// Applies the current inspector foldout persistence state
        /// </summary>
        private void ApplyInspectorFoldoutPersistenceState()
        {
            if (foldouts == null) { return; }

            int states = 0;

            for (int i = 0; i < foldouts.Count; i++)
            {
                if (foldouts[i].value)
                {
                    states |= 1 << i;
                }
            }

            EditorPrefs.SetInt(FOLDOUT_PERSISTENCE_KEY, states);
        }

        private void Init()
        {
            if (reader == null)
                return;
            
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
                    currentByteCount = Encoding.Unicode.GetByteCount(text);
                    exceedsCapacity = currentByteCount > maxByteCount;
                    remainingCapacityLabel.text = $"{currentByteCount} / {maxByteCount} ({(float)currentByteCount / maxByteCount * 100:n0}%) (UTF-16 {Encoding.Unicode.GetByteCount(text)}/{maxByteCount})";
                    break;
                case DataMode.UTF8:
                    currentByteCount = Encoding.UTF8.GetByteCount(text);
                    exceedsCapacity = currentByteCount > maxByteCount;
                    remainingCapacityLabel.text = $"{currentByteCount} / {maxByteCount} ({(float)currentByteCount / maxByteCount * 100:n0}%) (UTF-8 {Encoding.UTF8.GetByteCount(text)}/{maxByteCount})";
                    break;
            }

            SetElementsVisibleState(exceedsCapacity, capacityExceededError);
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
                    EditorGUILayout.LabelField(outputMode.ToString());
                    EditorGUILayout.LabelField(reader.linkedAvatars[i]);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }

                bool uploadBlocked = string.IsNullOrEmpty(reader.linkedAvatars[0]) ||
                                     reader.linkedAvatars.Length < texturePreview.Length;

                if (reader.dataMode != outputMode)
                {
                    EditorGUILayout.HelpBox(
                        $"These images contain data encoded in {outputMode} mode, but the pedestal data mode is {reader.dataMode}. You should re encode before uploading.", MessageType.Warning);
                }

#if VRCHAT_API_TOOLS_IMPORTED
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
#endif
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
            UpdateRemainingCapacityLabel(reader.dataMode);
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

        private DataMode outputMode;

#if VRCHAT_API_TOOLS_IMPORTED
        private void EncodeImages()
        {
            outputMode = reader.dataMode;
            
            imageWidth = reader.imageMode == 0 ? 128 : 1200;
            imageHeight = reader.imageMode == 0 ? 96 : 900;

            output = AvatarImageEncoder.Encode(reader.dataMode, text, reader.linkedAvatars, imageWidth, imageHeight);

            texturePreview = new GUIContent[output.Length];
            for (int i = 0; i < output.Length; i++)
            {
                texturePreview[i] = new GUIContent(output[i]);
            }
        }
#endif

#region LEGACY CODE

        private void MarkDirty()
        {
            //textStorageObject.text = text;
            
            if (reader.outputToText)
            {
                if (reader.outputText != null && reader.autoFillTMP)
                {
                    reader.outputText.text = text;
                }
            }
            
            EditorUtility.SetDirty(reader);
            
            if (PrefabUtility.IsPartOfAnyPrefab(reader.gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(reader);
            }
        }

#if VRCHAT_API_TOOLS_IMPORTED
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
#endif

        private bool checksFailedReadRenderTexture = false;
        private bool checksFailedPedestal = false;

        private void UpdatePedestalAssets()
        {
            Material renderQuadMat = AssetDatabase.LoadAssetAtPath<Material>(quadMaterialPath);

            Texture2D pcDonor = AssetDatabase.LoadAssetAtPath<Texture2D>(pcDonorImagePath);
            Texture2D questDonor = AssetDatabase.LoadAssetAtPath<Texture2D>(questDonorImagePath);
            
            CustomRenderTexture pcRT = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(pcRTPath);
            CustomRenderTexture questRT = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(questRTPath);

            reader.renderTexture = reader.imageMode == 0 ? questRT : pcRT;
            reader.donorInput = reader.imageMode == 0 ? questDonor : pcDonor;

            reader.GetComponent<MeshRenderer>().material = renderQuadMat;
            reader.GetComponent<Camera>().targetTexture = reader.imageMode == 0 ? questRT : pcRT;
            
            EditorUtility.SetDirty(reader);

            if (PrefabUtility.IsPartOfAnyPrefab(reader))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(reader);
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
            
            EditorUtility.SetDirty(reader);

            if (PrefabUtility.IsPartOfAnyPrefab(reader.gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(reader);
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
#endregion
}
