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
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class RuntimeDecoder : UdonSharpBehaviour
    {
        #region Prefab
        public string[] linkedAvatars;
        public string uid = "";
        public bool pedestalAssetsReady;
        
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
        //0 UTF16 string, 1 ASCII string, 2 Binary
        public DataMode dataMode = DataMode.UTF16;
        public bool patronMode;
    
        [Header("Debugging")] 
        public bool debugLogger;
        public bool debugTMP;
        public TextMeshPro loggerText;

        [Header("Output")] 
        public string outputString;

        [Header("Internal")]
        //public ReadRenderTexture readRenderTexture;
        public VRCAvatarPedestal avatarPedestal;
        #endregion

        #region Check Hierarchy
        //[SerializeField] private GameObject renderQuad;
        //[SerializeField] private ReadRenderTexture readRenderTexture;

        [Header("Debug")]
        [SerializeField] private GameObject textureComparisonPlane;
        [SerializeField] private Texture2D overrideTexture;

        private Texture pedestalTexture;

        private const string AVATAR_PEDESTAL_CLONE_NAME = "AvatarPedestal(Clone)";

        private void Start()
        {
            renderQuadRenderer = renderQuad.GetComponent<MeshRenderer>();

            GetComponent<MeshRenderer>().enabled = true;

            _CheckHierarchy();
        }

        public void _CheckHierarchy()
        {
            Transform pedestalClone;

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
        //public AvatarImagePrefab prefab;
        public VRCAvatarPedestal pedestal;

        //public string outputString;

        [Header("Render references")] public GameObject renderQuad;
        private MeshRenderer renderQuadRenderer;

        public Camera renderCamera;
        public CustomRenderTexture renderTexture;
        public Texture2D donorInput;

        private Texture lastInput;
        private Material pedestalMaterial;
        private bool waitForNew;

        //internal
        private Color32[] colors;
        private bool hasRun;
        [NonSerialized] public bool pedestalReady;
        private System.Diagnostics.Stopwatch stopwatch;

        private bool frameSkip = false;


        private string nextAvatar = "";
        private int index = 1;
        private int byteIndex = 0;
        private int dataLength;
        private int currentTry;

        private System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();

        public void OnPostRender()
        {
            if (pedestalReady && !hasRun)
            {
                if (debugLogger)
                {
                    stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();
                }

                Log("ReadRenderTexture: Starting");

                if (renderTexture != null) // All code inside should be called only ONCE (initialization)
                {
                    //SETUP
                    lastInput = pedestalTexture;

                    outputString = "";

                    //copy the first texture over so it can be read
                    donorInput.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    StartReadPicture(donorInput);

                    //disable the renderquad to prevent VR users from getting a seizure (disable the camera first so it only renders one frame)
                    renderCamera.enabled = false;
                    renderQuadRenderer.enabled = false;

                    Log("ReadRenderTexture: Writing Information");
                    hasRun = true;
                }
                else
                {
                    Debug.LogError(
                        "[<color=#00fff7>ReadRenderTexture</color>] Target RenderTexture is null, aborting read");
                }
            }

            if (waitForNew)
            {
                if (IsSame())
                {
                    if (currentTry > 10)
                    {
                        Log("Failed to load new Pedestal within 2 seconds. Ending loading. Load unsuccessful.");
                        waitForNew = false;
                        renderCamera.enabled = false;
                        renderQuadRenderer.enabled = false;
                        return;
                    }
                    Log($"lastInput == donorInput - Will try loading again later - {currentTry++}");

                    waitForNew = false;
                    SendCustomEventDelayedSeconds(nameof(_WaitForReload), 0.2f);
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
                    waitForNew = false;
                    renderCamera.enabled = false;
                    renderQuadRenderer.enabled = false;
                    Log("donorInput updated - Avatar pedestal successfully reloaded. Loading ImageData");
                    StartReadPicture(donorInput);
                }
            }
        }


        private void Log(string text)
        {
            if (!debugLogger) return;

            Debug.Log($"[<color=#00fff7>ReadRenderTexture</color>] {text}");

            if (debugTMP)
            {
                loggerText.text += $"{text} | ";
            }
        }

        private void StartReadPicture(Texture2D picture)
        {
            Log("Starting Read");
            Log($"Input: {picture.width} x {picture.height} [{picture.format}]");


            colors = picture.GetPixels32();

            Array.Reverse(colors);

            Color32 color = colors[0];
            dataLength = (color.r << 24) | (color.g << 16) |
                         (color.b << 8) | (color.a);

            Log("Decoding header");
            Debug.Log($"Data length: {dataLength} bytes");

            nextAvatar = "";
            for (int i = 1; i < 6; i++)
            {
                color = colors[i];
                nextAvatar += $"{color.r:x2}";
                nextAvatar += $"{color.g:x2}";
                nextAvatar += $"{(color.b):x2}";
                nextAvatar += $"{(color.a):x2}";
            }

            nextAvatar =
                $"avtr_{nextAvatar.Substring(0, 8)}-{nextAvatar.Substring(8, 4)}-{nextAvatar.Substring(12, 4)}-{nextAvatar.Substring(16, 4)}-{nextAvatar.Substring(20, 12)}";

            Debug.Log($"AVATAR FOUND - {nextAvatar}");

            index = 5;
            byteIndex = 16;

            SendCustomEventDelayedFrames(nameof(_ReadPictureStep), 2);
        }

        public void _ReadPictureStep()
        {
            Log($"Reading step {index} - {frameTimer.ElapsedMilliseconds}");
            if (frameTimer.IsRunning)
            {
            }
            else
            {
                frameTimer.Reset();
                frameTimer.Start();
            }

            string tempString = "";

            for (int step = 0;
                 step < 500;
                 step++)
            {
                Color32 c = colors[index];

                tempString += $"{(char)(c.r | (c.g << 8))}{(char)(c.b | (c.a << 8))}";

                byteIndex += 4;

                if (byteIndex >= dataLength)
                {
                    Log($"Reached data length: {dataLength}; byteIndex: {byteIndex}");
                    if ((byteIndex - dataLength) % 4 == 2)
                    {
                        outputString += tempString.Substring(0, tempString.Length - 1);
                    }
                    else
                        outputString += tempString;

                    ReadDone();
                    return;
                }

                index++;
            }

            outputString += tempString;


            if (frameTimer.ElapsedMilliseconds > 35)
            {
                SendCustomEventDelayedFrames(nameof(_ReadPictureStep), 1);
                frameTimer.Stop();
            }
            else
            {
                _ReadPictureStep();
            }
        }


        private void ReadDone()
        {
            frameTimer.Stop();
            if (nextAvatar != "avtr_ffffffff-ffff-ffff-ffff-ffffffffffff")
            {
                //pedestal.gameObject.SetActive(true);
                pedestal.SwitchAvatar(nextAvatar);
                Log($"Switched Avatar to - {nextAvatar} - Restarting loading");

                currentTry = 0;
                SendCustomEventDelayedSeconds(nameof(_WaitForReload), 0.5f);

                if (debugLogger)
                {
                    Log($"Current time: {stopwatch.ElapsedMilliseconds} ms");
                }

                return;
            }

            if (debugLogger)
            {
                Log($"Output string: {outputString}");
                stopwatch.Stop();
                Log($"Took: {stopwatch.ElapsedMilliseconds} ms");
            }

            if (outputToText)
                outputText.text = outputString;

            if (callBackOnFinish && callbackBehaviour != null && callbackEventName != "")
                callbackBehaviour.SendCustomEvent(callbackEventName);

            // After image reading is complete, destroy all of the components except the decoder
            Destroy(pedestal);
            Destroy(renderQuadRenderer);
            Destroy(GetComponent<MeshFilter>());
            Destroy(renderCamera);
        }

        public void _WaitForReload()
        {
            renderCamera.enabled = true;
            renderQuadRenderer.enabled = true;
            waitForNew = true;
        }

        private bool IsSame()
        {
            renderQuadRenderer.material.SetTexture(1,
                pedestalMaterial
                    .GetTexture("_WorldTex"));
            return lastInput == pedestalMaterial.GetTexture("_WorldTex");
        }
        #endregion
    }
}