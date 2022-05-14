using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

namespace AvatarImageReader
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ReadRenderTexture : UdonSharpBehaviour
    {
        /*
         * This script is meant to be used as a One time "Read" for a specific render texture (Since they can't be reused)
         * Once "Primed" by the CheckHirarchyScript, it will decode the retrieved RenderTexture
         */

        public AvatarImagePrefab prefab;
        public VRCAvatarPedestal pedestal;

        public string outputString;

        [Header("Render references")] public GameObject renderQuad;

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

        public void OnPostRender()
        {
            if (pedestalReady && !hasRun)
            {
                if (prefab.debugLogger)
                {
                    stopwatch = new System.Diagnostics.Stopwatch();
                    stopwatch.Start();
                }

                Log("ReadRenderTexture: Starting");

                if (renderTexture != null) // All code inside should be called only ONCE (initialization)
                {
                    //SETUP
                    pedestalMaterial = transform.parent.GetChild(0).GetChild(0).GetChild(1).GetComponent<MeshRenderer>().material;
                    lastInput = pedestalMaterial.GetTexture("_WorldTex");
                    
                    
                    outputString = "";

                    //copy the first texture over so it can be read
                    donorInput.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    StartReadPicture(donorInput);

                    //disable the renderquad to prevent VR users from getting a seizure (disable the camera first so it only renders one frame)
                    renderCamera.enabled = false;
                    renderQuad.SetActive(false);

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
                        renderQuad.SetActive(false);
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
                    renderQuad.SetActive(false);
                    Log("donorInput updated - Avatar pedestal successfully reloaded. Loading ImageData");
                    StartReadPicture(donorInput);
                }
            }
        }


        private void Log(string text)
        {
            if (!prefab.debugLogger) return;

            Debug.Log($"[<color=#00fff7>ReadRenderTexture</color>] {text}");

            if (prefab.debugTMP)
            {
                prefab.loggerText.text += $"{text} | ";
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

        private string nextAvatar = "";
        private int index = 1;
        private int byteIndex = 0;
        private int dataLength;
        private int currentTry;

        private System.Diagnostics.Stopwatch frameTimer = new System.Diagnostics.Stopwatch();

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
                
                tempString += $"{(char)(c.r | (c.g << 8))}{(char) (c.b | (c.a << 8))}";

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
                pedestal.gameObject.SetActive(true);
                pedestal.SwitchAvatar(nextAvatar);
                Log($"Switched Avatar to - {nextAvatar} - Restarting loading");

                currentTry = 0;
                SendCustomEventDelayedSeconds(nameof(_WaitForReload), 0.5f);

                if (prefab.debugLogger)
                {
                    Log($"Current time: {stopwatch.ElapsedMilliseconds} ms");
                }

                return;
            }

            if (prefab.debugLogger)
            {
                Log($"Output string: {outputString}");
                stopwatch.Stop();
                Log($"Took: {stopwatch.ElapsedMilliseconds} ms");
            }

            if (prefab.outputToText)
                prefab.outputText.text = outputString;

            prefab.outputString = outputString;

            if (prefab.callBackOnFinish && prefab.callbackBehaviour != null && prefab.callbackEventName != "")
                prefab.callbackBehaviour.SendCustomEvent(prefab.callbackEventName);
            
            pedestal.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
        
        public void _WaitForReload()
        {
            renderCamera.enabled = true;
            renderQuad.SetActive(true);
            waitForNew = true;
        }


        private bool IsSame()
        {
            renderQuad.GetComponent<MeshRenderer>().material.SetTexture(1,
                pedestalMaterial
                    .GetTexture("_WorldTex"));
            return lastInput == pedestalMaterial.GetTexture("_WorldTex");
        }
    }
}