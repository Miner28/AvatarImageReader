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
        /// <summary>
        /// All linked avatar IDs for decoding during runtime
        /// </summary>
        public string[] linkedAvatars;

        public string uid = "";

        /// <summary>
        /// Flag for indicating whether the custom editor has initialized the required assets for decoding the pedestal image
        /// </summary>
        public bool pedestalAssetsReady;

        public int actionOnLoad = 0;

        /// <summary>
        /// Which platform's shared maximum resolution is used for the decoder
        /// </summary>
        /// <remarks>
        /// PC should only be used if the world isn't cross-compatible and the size of the data warrants it
        /// </remarks>
        public Platform imageMode = Platform.Android;

        /// <summary>
        /// Should the decoded text be displayed on TMP
        /// </summary>
        public bool outputToText;

        /// <summary>
        /// In case the decoding fails, should the default data stored alongside with the world upload be used as a fallback
        /// </summary>
        public bool autoFillTMP;

        /// <summary>
        /// Target TMP for displaying the decoded text
        /// </summary>
        public TextMeshPro outputText;

        /// <summary>
        /// Should a callback method on an UdonBehaviour get invoked after decoding is finished
        /// </summary>
        public bool callBackOnFinish = false;
        
        /// <summary>
        /// UdonBehaviour for receiving a callback on finish
        /// </summary>
        public UdonBehaviour callbackBehaviour;

        /// <summary>
        /// Name of the callback method for finishing decoding
        /// </summary>
        public string callbackEventName;

        /// <summary>
        /// Data encoding mode
        /// <para>0 = UTF16</para>
        /// <para>1 = UTF8</para>
        /// <para>2 = ASCII (Not supported yet)</para>
        /// <para>3 = Binary (Not supported yet)</para>
        /// </summary>
        public DataMode dataMode = DataMode.UTF8;

        public bool patronMode;

        /// <summary>
        /// Should the decoder output debug logs
        /// </summary>
        public bool debugLogging = false;

        /// <summary>
        /// Should the decoder output debug logs to TMP
        /// </summary>
        public bool debugTMP;

        /// <summary>
        /// TMP for outputting debug logs
        /// </summary>
        public TextMeshPro loggerText;

        /// <summary>
        /// The final output of the decoder
        /// </summary>
        public string outputString;

        /// <summary>
        /// Avatar pedestal for decoding the images
        /// </summary>
        public VRCAvatarPedestal avatarPedestal;

        /// <summary>
        /// Log prefix for the decoder
        /// </summary>
        private const string LOG_PREFIX = "[<color=#00fff7>AvatarImageReader</color>]:";
        #endregion
        #region Check Hierarchy
        //[SerializeField] private GameObject renderQuad;
        //[SerializeField] private ReadRenderTexture readRenderTexture;

        [Header("Debug")]
        [SerializeField] private GameObject textureComparisonPlane;
        [SerializeField] private Texture2D overrideTexture;

        private Texture pedestalTexture;

        /// <summary>
        /// Automatically generated avatar pedestal hierarchy
        /// </summary>
        /// <remarks>
        /// Only this transform should be used to target objects on the pedestal
        /// </remarks>
        private Transform pedestalClone;

        /// <summary>
        /// Name of the automatically generated avatar pedestal hierarchy root below the root decoder
        /// </summary>
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
        #region Detect and load image
        /// <summary>
        /// Avatar pedestal for decoding the images
        /// </summary>
        private VRCAvatarPedestal pedestal;

        /// <summary>
        /// MeshRenderer for the fullscreen override
        /// </summary>
        private MeshRenderer renderQuadRenderer;
        
        [Obsolete]
        public GameObject renderQuad;

        /// <summary>
        /// Camera for rendering the fullscreen override
        /// </summary>
        public Camera renderCamera;

        public CustomRenderTexture renderTexture;

        public Texture2D donorInput;

        public byte[] outputBytes;

        /// <summary>
        /// Is the avatar pedestal ready for decoding
        /// </summary>
        private bool pedestalReady;
        private Color32[] colors;
        private int frameCounter;
        private bool frameSkip;
        private bool hasRun;
        private Texture lastInput;
        private Material pedestalMaterial;

        private int avatarCounter = 0;

        private bool waitForNew;
        
        private int currentTry;

        public void OnPostRender()
        {
            // Will be set true when decoder detects that VRChat has generated the avatar pedestal
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

        private void QueueNextAvatarLoad()
        {
            pedestalClone.gameObject.SetActive(true);
            pedestal.SwitchAvatar(nextAvatar);
            Log($"Switched Avatar to - {nextAvatar} - Restarting loading");

            currentTry = 0;
            SendCustomEventDelayedSeconds(nameof(_ReEnableCamera), 0.1f);
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
        
        #endregion
        #region Read RenderTexture
        
        private Color32 color;
        private int byteIndex;
        private byte[] avatarBytes;
        private int pixelIndex;
        private int maxIndex;

        private string nextAvatar = "";
        private int dataLength;
        
        private int transferSpeed = 2500; //Amount of iteration steps that Color32[] to byte[] will perform per frame

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

            var headerInfo = colors[6];
            dataMode = (DataMode) headerInfo.r;
            Debug.LogError($"<color=#00FFFF>Loading AvatarImageReader Image</color> V{headerInfo.g}.{headerInfo.b} Encoder: {headerInfo.a} DataMode: {dataMode}");
            
            nextAvatar = $"avtr_{nextAvatar.Substring(0, 8)}-{nextAvatar.Substring(8, 4)}-{nextAvatar.Substring(12, 4)}-{nextAvatar.Substring(16, 4)}-{nextAvatar.Substring(20, 12)}";

            Log($"<color=#00AAFF>Starting Read for avatar {avatarCounter}</color>");
            Log($"Input: {picture.width} x {picture.height} [{picture.format}]");

            Log($"Data length: {dataLength} bytes");

            avatarCounter++;

            Log($"Found next avatar in header - {nextAvatar}");

            //initialize decoding intermediates
            byteIndex = 0; //index of next byte to read starting at 0
            avatarBytes = new byte[dataLength];
            pixelIndex = 6; //start decoding at 6th pixel (skip the header)
            maxIndex = dataLength / 4 + 6; //last pixel we should read, data length + header size

            SendCustomEventDelayedFrames(nameof(_ReadPictureStep), 0);
        }

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
                QueueNextAvatarLoad();
                return;
            }

            switch (dataMode)
            {
                case DataMode.UTF16:
                    InitializeDecodeUTF16();
                    break;
                
                case DataMode.UTF8:
                    InitializeDecodeUTF8();
                    break;
            }
        }
        #endregion
        
        private string[] _utf8Chars;
        private char[] _utf16Chars;
        private byte charCounter;
        private int bytesCount;
        private int decodeIndex;
        
        #region UTF8 Decoder

        private int decodeSpeedUTF8 = 7500; //Amount of iteration steps UTF8 decoder will perform per frame

        private int character;
        private int charIndex;

        private const byte
            _0x80 = 0x80,
            _0xC0 = 0xC0,
            _0x3F = 0x3F,
            _0xF0 = 0xF0,
            _0xF8 = 0xF8,
            _0x07 = 0x07,
            _0x1F = 0x1F,
            _0x0F = 0x0F,
            _0xE0 = 0xE0;

        private void InitializeDecodeUTF8()
        {
            bytesCount = outputBytes.Length;
            _utf8Chars = new string[bytesCount];
            charIndex = 0;
            character = 0;
            charCounter = 0;
            decodeIndex = 0;
            frameCounter = 1;

            Log($"Starting UTF8 decoder: decoding {bytesCount} bytes will take {bytesCount / decodeSpeedUTF8 + 1} frames");

            SendCustomEventDelayedFrames(nameof(_DecodeStepUTF8), 0);
        }

        public void _DecodeStepUTF8()
        {
            int toIterate = Math.Min(decodeIndex + decodeSpeedUTF8, bytesCount);

            Log($"Frame {frameCounter} of DecodeStep; current index: {decodeIndex}, will iterate to {toIterate}");
            frameCounter++;

            for (; decodeIndex < toIterate; decodeIndex++)
            {
                byte value = outputBytes[decodeIndex];
                if ((value & _0x80) == 0)
                {
                    _utf8Chars[charIndex++] = ((char)value).ToString();
                }
                else if ((value & _0xC0) == _0x80)
                {
                    if (charCounter > 0)
                    {
                        character = character << 6 | (value & _0x3F);
                        charCounter--;
                        if (charCounter == 0)
                        {
                            _utf8Chars[charIndex++] = char.ConvertFromUtf32(character);
                        }
                    }
                }
                else if ((value & _0xE0) == _0xC0)
                {
                    charCounter = 1;
                    character = value & _0x1F;
                }
                else if ((value & _0xF0) == _0xE0)
                {
                    charCounter = 2;
                    character = value & _0x0F;
                }
                else if ((value & _0xF8) == _0xF0)
                {
                    charCounter = 3;
                    character = value & _0x07;
                }
            }

            //if we are not done decoding yet continue next frame
            if (decodeIndex != bytesCount)
            {
                SendCustomEventDelayedFrames(nameof(_DecodeStepUTF8), 0);
                return;
            }

            outputString += string.Concat(_utf8Chars);
            
            ReadDone();
        }
        #endregion
        #region UTF16 Decoder
        
        private int decodeSpeedUTF16 = 10000; //Amount of iteration steps UTF16 decoder will perform per frame

        private void InitializeDecodeUTF16()
        {
            bytesCount = outputBytes.Length;
            _utf16Chars = new char[bytesCount / 2];
            decodeIndex = 0;
            frameCounter = 1;

            Log($"Starting UTF16 decoder: decoding {bytesCount} bytes will take {bytesCount / decodeSpeedUTF16/2 + 1} frames");

            SendCustomEventDelayedFrames(nameof(_DecodeStepUTF16), 0);
        }

        public void _DecodeStepUTF16()
        {
            int toIterate = Math.Min(decodeIndex + decodeSpeedUTF16, bytesCount);

            Log($"Frame {frameCounter} of DecodeStep; current index: {decodeIndex}, will iterate to {toIterate}");
            frameCounter++;

            for (; decodeIndex < toIterate; decodeIndex += 2)
            {
                _utf16Chars[decodeIndex / 2] = (char)(outputBytes[decodeIndex] | outputBytes[decodeIndex + 1] << 8);
            }

            //if we are not done decoding yet continue next frame
            if (decodeIndex != bytesCount)
            {
                SendCustomEventDelayedFrames(nameof(_DecodeStepUTF16), 0);
                return;
            }

            outputString += new string(_utf16Chars);
            ReadDone();
        }
        #endregion
        
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

        private void Log(string text)
        {
            if (!debugLogging) return;

            Debug.Log($"{LOG_PREFIX} {text}");

            if (debugTMP)
            {
                DateTime now = DateTime.Now;
                loggerText.text += $"{now.ToLongTimeString()}: {text}\n";
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
    }
}