using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon;
using AvatarImageReader.Enums;

namespace AvatarImageReader
{
    [RequireComponent(typeof(VRCAvatarPedestal))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(TextStorageObject))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RuntimeDecoder : UdonSharpBehaviour
    {
        #region Prefab
        public string[] linkedAvatars;
        public string uid = "";
        public bool pedestalAssetsReady;
        public int actionOnLoad = 0;

        [Header("Image Options")]
        //0 cross platform, 1 pc only
        public Platform imageMode = Platform.Android;

        [Header("General Options")]
        [Tooltip("Increasing step size decreases decode time but increases frametimes")]

        public bool outputToText;
        public bool autoFillTMP;
        public TextMeshPro outputText;

        public bool callBackOnFinish = false;
        public UdonBehaviour callbackBehaviour;
        public string callbackEventName;

        [Header("Data Encoding")]
        //0 UTF16 string, 1 UTF-8, 2 ASCII string, 3 Binary
        public DataMode dataMode = DataMode.UTF8;
        public bool patronMode;

        [Header("Debugging")]
        public bool debugLogging = false;
        public bool debugTMP;
        public TextMeshPro loggerText;

        [Header("Output")]
        public string outputString;

        [Header("Internal")]
        public VRCAvatarPedestal avatarPedestal;

        private const string LOG_PREFIX = "[<color=#00fff7>AvatarImageReader</color>]:";
        #endregion

        #region Check Hierarchy
        //[SerializeField] private GameObject renderQuad;
        //[SerializeField] private ReadRenderTexture readRenderTexture;

        [Header("Debug")]
        [SerializeField] private GameObject textureComparisonPlane;
        [SerializeField] private Texture2D overrideTexture;

        private Texture pedestalTexture;

        private Transform pedestalClone;

        private const string AVATAR_PEDESTAL_CLONE_NAME = "AvatarPedestal(Clone)";

        private void Start()
        {
            pedestal = GetComponent<VRCAvatarPedestal>();

            renderQuadRenderer = renderQuad.GetComponent<MeshRenderer>();

            GetComponent<MeshRenderer>().enabled = true;

            _CheckHierarchy();
        }

        public void _CheckHierarchy()
        {
            if ((pedestalClone = transform.Find(AVATAR_PEDESTAL_CLONE_NAME)) != null)
            {
                for (int i = 0; i < pedestalClone.childCount; i++)
                {
                    Transform child = pedestalClone.GetChild(i);

                    // Find the Child used for the image component
                    if (child.name.Equals("Image"))
                    {
                        pedestalMaterial = child.GetComponent<MeshRenderer>().material;

                        pedestalTexture = pedestalMaterial.GetTexture("_WorldTex");

                        if (pedestalTexture != null)
                        {
                            Debug.Log("CheckHierarchyScript: Retrieving Avatar Pedestal Texture");

                            // Assign the Texture to the Render pane and the comparison pane
                            if (renderQuad != null)
                            {
                                renderQuadRenderer.material.SetTexture(1, pedestalTexture);
                                renderQuadRenderer.enabled = true;
                            }

                            // Render the texture to a comparison plane if that is enabled for debugging purposes
                            if (textureComparisonPlane != null)
                            {
                                textureComparisonPlane.GetComponent<MeshRenderer>().material.SetTexture(1, pedestalTexture);
                            }

                            pedestalReady = true;

                            return;
                        }
                    }
                }
            }

            SendCustomEventDelayedFrames(nameof(_CheckHierarchy), 0);
        }
        #endregion

        #region Read RenderTexture
        private VRCAvatarPedestal pedestal;
        private MeshRenderer renderQuadRenderer;

        private int transferSpeed = 2500; //Amount of iteration steps that Color32[] to byte[] will perform per frame
        private int decodeSpeed = 7500; //Amount of iteration steps byte[] to string decoder will perform per frame

        [Header("Render references")] public GameObject renderQuad;

        public Camera renderCamera;
        public CustomRenderTexture renderTexture;
        public Texture2D donorInput;

        public byte[] outputBytes;

        //internal
        [NonSerialized] public bool pedestalReady;
        private Color32[] colors;
        private int frameCounter;
        private bool frameSkip;
        private bool hasRun;
        private Texture lastInput;
        private Material pedestalMaterial;

        private int avatarCounter = 0;

        private bool waitForNew;

        public void OnPostRender()
        {
            //set by CheckHierarchy.cs when the pedestal is ready
            if (!pedestalReady) return;

            if (!hasRun)
            {
                Log("ReadRenderTexture: Starting");

                if (renderTexture != null)
                {
                    lastInput = pedestalMaterial.GetTexture("_WorldTex");

                    outputString = "";

                    //copy the first texture over so it can be read
                    donorInput.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);

                    //initialize output array
                    outputBytes = new byte[0];

                    InitializeRead(donorInput);

                    _DisableCamera();

                    avatarCounter = 0;

                    hasRun = true;
                }
                else
                {
                    LogError("Target RenderTexture is null. Aborting read");
                }
            }

            if (waitForNew)
            {
                if (IsSame())
                {
                    if (currentTry > 10)
                    {
                        LogError("Failed to load new Pedestal within 2 seconds. Aborting read.");

                        _DisableCamera();

                        return;
                    }

                    Log($"lastInput == donorInput - Will try loading again later - {currentTry++}");

                    _DisableCamera();

                    SendCustomEventDelayedSeconds(nameof(_ReEnableCamera), 0.2f);
                }
                else
                {
                    if (!frameSkip)
                    {
                        frameSkip = true;
                        return;
                    }

                    frameSkip = false;

                    donorInput.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    lastInput = pedestalMaterial.GetTexture("_WorldTex");

                    _DisableCamera();

                    Log("donorInput updated - Avatar pedestal successfully reloaded. Loading ImageData");
                    InitializeRead(donorInput);
                }
            }
        }


        private void InitializeRead(Texture2D picture)
        {
            int maxDataLength = (picture.width * picture.height - 5) * 4;

            colors = picture.GetPixels32();

            Array.Reverse(colors);

            Color32 idColor = colors[0];
            dataLength = (idColor.r << 24) | (idColor.g << 16) |
                         (idColor.b << 8) | (idColor.a);

            if (dataLength > maxDataLength)
            {
                LogError($"WARNING: Encoded data length is {dataLength} bytes, only {maxDataLength} bytes will be read. Check your encoder.");
                dataLength = maxDataLength;
            }

            nextAvatar = "";
            for (int i = 1; i < 6; i++)
            {
                idColor = colors[i];
                nextAvatar += $"{idColor.r:x2}";
                nextAvatar += $"{idColor.g:x2}";
                nextAvatar += $"{(idColor.b):x2}";
                nextAvatar += $"{(idColor.a):x2}";
            }

            nextAvatar = $"avtr_{nextAvatar.Substring(0, 8)}-{nextAvatar.Substring(8, 4)}-{nextAvatar.Substring(12, 4)}-{nextAvatar.Substring(16, 4)}-{nextAvatar.Substring(20, 12)}";

            Log($"<color=cyan>Starting Read for avatar {avatarCounter}</color>");
            Log($"Input: {picture.width} x {picture.height} [{picture.format}]");

            Log($"Data length: {dataLength} bytes");

            avatarCounter++;

            Log($"Found next avatar in header - {nextAvatar}");

            //initialize decoding intermediates
            byteIndex = 0; //index of next byte to read starting at 0
            avatarBytes = new byte[dataLength];
            pixelIndex = 5; //start decoding at 5th pixel (skip the header)
            maxIndex = dataLength / 4 + 5; //last pixel we should read, data length + header size

            SendCustomEventDelayedFrames(nameof(_ReadPictureStep), 0);
        }

        private Color32 color;
        private int byteIndex;
        private byte[] avatarBytes;
        private int pixelIndex;
        private int maxIndex;

        public void _ReadPictureStep()
        {
            int toIterate = Math.Min(pixelIndex + transferSpeed, maxIndex);

            Log($"Frame {frameCounter} of TransferStep; current index: {pixelIndex}, will iterate to {toIterate}");
            frameCounter++;

            //loop through colors and convert to bytes
            for (; pixelIndex < toIterate; pixelIndex++)
            {
                color = colors[pixelIndex];
                avatarBytes[byteIndex++] = color.r;
                avatarBytes[byteIndex++] = color.g;
                avatarBytes[byteIndex++] = color.b;
                avatarBytes[byteIndex++] = color.a;
            }

            //if we are not done continue next frame
            if (pixelIndex != maxIndex)
            {
                SendCustomEventDelayedFrames(nameof(_ReadPictureStep), 0);
                return;
            }

            //if we are done, decode any possible remaining bytes one by one
            pixelIndex++;

            if (byteIndex < dataLength) avatarBytes[byteIndex++] = colors[pixelIndex].r;
            if (byteIndex < dataLength) avatarBytes[byteIndex++] = colors[pixelIndex].g;
            if (byteIndex < dataLength) avatarBytes[byteIndex++] = colors[pixelIndex].b;
            if (byteIndex < dataLength) avatarBytes[byteIndex] = colors[pixelIndex].a;

            //resize the outputBytes array and copy over the newly read data

            //create a temporary intermediate array
            byte[] temp = new byte[outputBytes.Length];
            Array.Copy(outputBytes, temp, outputBytes.Length);

            //resize the outputBytes array and fill it with temp and avatarBytes
            outputBytes = new byte[outputBytes.Length + avatarBytes.Length];
            Array.Copy(temp, outputBytes, temp.Length);
            Array.Copy(avatarBytes, 0, outputBytes, temp.Length, avatarBytes.Length);

            //clean up
            avatarBytes = new byte[0];

            //load next avatar
            if (nextAvatar != "avtr_ffffffff-ffff-ffff-ffff-ffffffffffff")
            {
                pedestalClone.gameObject.SetActive(true);
                pedestal.SwitchAvatar(nextAvatar);
                Log($"Switched Avatar to - {nextAvatar} - Restarting loading");

                currentTry = 0;
                SendCustomEventDelayedSeconds(nameof(_ReEnableCamera), 0.1f);
                return;
            }

            InitializeDecode();
        }

        private string nextAvatar = "";
        private int dataLength;
        private int currentTry;

        private char[] chars;
        private int charIndex;
        private int character;
        private byte charCounter;
        private int bytesCount;
        private int dIndex;

        private void InitializeDecode()
        {
            bytesCount = outputBytes.Length;
            chars = new char[bytesCount];
            charIndex = 0;
            character = 0;
            charCounter = 0;
            dIndex = 0;
            frameCounter = 1;

            Log($"Starting UTF8 decoder: decoding {bytesCount} bytes will take {bytesCount / decodeSpeed + 1} frames");

            SendCustomEventDelayedFrames(nameof(_DecodeStep), 0);
        }

        private const char InvalidChar = (char)0;

        public void _DecodeStep()
        {
            int toIterate = Math.Min(dIndex + decodeSpeed, bytesCount);

            Log($"Frame {frameCounter} of DecodeStep; current index: {dIndex}, will iterate to {toIterate}");
            frameCounter++;

            for (; dIndex < toIterate; dIndex++)
            {
                byte value = outputBytes[dIndex];
                if ((value & 0x80) == 0)
                {
                    chars[charIndex++] = (char)value;
                    charCounter = 0;
                }
                else if ((value & 0xC0) == 0x80)
                {
                    if (charCounter > 0)
                    {
                        character = character << 6 | (value & 0x3F);
                        charCounter--;
                        if (charCounter == 0)
                        {
                            chars[charIndex++] = char.ConvertFromUtf32(character)[0];
                        }
                    }
                }
                else if ((value & 0xE0) == 0xC0)
                {
                    charCounter = 1;
                    character = value & 0x1F;
                }
                else if ((value & 0xF0) == 0xE0)
                {
                    charCounter = 2;
                    character = value & 0x0F;
                }
                else if ((value & 0xF8) == 0xF0)
                {
                    charCounter = 3;
                    character = value & 0x07;
                }
            }

            //if we are not done decoding yet continue next frame
            if (dIndex != bytesCount)
            {
                SendCustomEventDelayedFrames(nameof(_DecodeStep), 0);
                return;
            }

            int toCopy = chars.Length;
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                if (chars[i] == InvalidChar)
                {
                    toCopy--;
                }
                else break;
            }

            object[] newChars = new object[toCopy]; //We have to use object[] due to fun Udon shit
            Array.Copy(chars, newChars, toCopy);

            outputString += string.Concat(newChars);


            ReadDone();
        }

        private void ReadDone()
        {
            Log("Read Finished");
            if (outputToText)
            {
                int textLength = Math.Min(outputString.Length, 10000);
                outputText.text = outputString.Substring(0, textLength);
            }

            Debug.Log(outputString);

            if (callBackOnFinish && callbackBehaviour != null && callbackEventName != "")
                callbackBehaviour.SendCustomEvent(callbackEventName);

            Destroy(pedestal);
            Destroy(pedestalClone.gameObject);
        }

        public void _ReEnableCamera()
        {
            renderCamera.enabled = true;
            renderQuadRenderer.enabled = true;
            waitForNew = true;
        }

        public void _DisableCamera()
        {
            renderCamera.enabled = false;
            renderQuadRenderer.enabled = false;
            waitForNew = false;
        }

        private bool IsSame()
        {
            renderQuadRenderer.material.SetTexture(1, pedestalMaterial.GetTexture("_WorldTex"));
            return lastInput == pedestalMaterial.GetTexture("_WorldTex");
        }

        private void Log(string text)
        {
            if (!debugLogging) return;

            Debug.Log($"{LOG_PREFIX} {text}");

            if (debugTMP)
            {
                loggerText.text += $"{text}\n";
            }
        }

        private void LogError(string text)
        {
            if (!debugLogging) return;

            Debug.LogError($"{LOG_PREFIX} {text}");

            if (debugTMP)
            {
                loggerText.text += $"<color=red>{text}</color>\n";
            }
        }
        #endregion
    }
}