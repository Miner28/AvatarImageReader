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
        public RenderTexture renderTexture;
        public Texture2D donorInput;

        private Color[] lastInput;
        private bool waitForNew;

        //internal
        private Color[] colors;
        private bool hasRun;
        [NonSerialized] public bool pedestalReady;
        private System.Diagnostics.Stopwatch stopwatch;


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
                    outputString = "";
                    stepLength = prefab.stepLength;

                    //copy the first texture over so it can be read
                    donorInput.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    donorInput.Apply();
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
                    if (currentTry * 0.1f > 7.5f)
                    {
                        Log($"Failed to load new Pedestal within 7.5 seconds. Ending loading. Load unsuccessful.");
                        waitForNew = false;
                        renderCamera.enabled = false;
                        renderQuad.SetActive(false);
                        return;
                    }

                    Log($"lastInput == donorInput - Will try loading again later - {currentTry}");
                    currentTry++;

                    waitForNew = false;
                    SendCustomEventDelayedSeconds(nameof(_WaitForReload), 0.1f);
                }
                else
                {
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


            colors = picture.GetPixels();

            lastInput = colors;

            Array.Reverse(colors);

            Color color = colors[0];
            dataLength = (byte) (color.r * 255) << 16 | (byte) (color.g * 255) << 8 |
                         (byte) (color.b * 255);

            Log("Decoding header");
            Debug.Log($"Data length: {dataLength} bytes");

            nextAvatar = "";
            for (int i = 1; i < 6; i++)
            {
                color = colors[i];
                nextAvatar += $"{(byte) (color.r * 255):x2}";
                nextAvatar += $"{(byte) (color.g * 255):x2}";
                nextAvatar += $"{(byte) (color.b * 255):x2}";
                nextAvatar += $"{(byte) (color.a * 255):x2}";
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
        private int byteIndex;
        private int dataLength;
        private int stepLength = 1000;
        private int currentTry;

        public void _ReadPictureStep()
        {
            Log($"Reading step {index}\n");

            string tempString = "";

            for (int step = 0;
                 step < stepLength;
                 step++)
            {
                Color c = colors[index];

                tempString += (char) ((byte) (c.r * 255) | ((byte) (c.g * 255) << 8));
                tempString += (char) ((byte) (c.b * 255) | ((byte) (c.a * 255) << 8));
                
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

            SendCustomEventDelayedFrames(nameof(_ReadPictureStep), 1);
        }


        private void ReadDone()
        {
            if (nextAvatar != "avtr_ffffffff-ffff-ffff-ffff-ffffffffffff")
            {
                //TODO RESET, RELOAD, RETRY
                pedestal.SwitchAvatar(nextAvatar);
                Log($"Switched Avatar to - {nextAvatar} - Restarting loading");

                currentTry = 0;
                SendCustomEventDelayedSeconds(nameof(_WaitForReload), 0.5f);

                if (prefab.debugLogger)
                {
                    Log($"Loading of current pedestal took: {stopwatch.ElapsedMilliseconds} ms");
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

            gameObject.SetActive(false);
        }
        
        public void _WaitForReload()
        {
            renderCamera.enabled = true;
            renderQuad.SetActive(true);
            waitForNew = true;
        }

        private char ConvertBytesToUTF16(byte byte1, byte byte2) => (char) (byte1 | (byte2 << 8));

        private bool IsSame()
        {
            renderQuad.GetComponent<MeshRenderer>().material.SetTexture(1,
                transform.parent.GetChild(0).GetChild(0).GetChild(1).GetComponent<MeshRenderer>().material
                    .GetTexture("_WorldTex"));
            donorInput.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            donorInput.Apply();

            //Check first 10 pixels if they are same. (If all 10 are same we are assuming that the rest will be same too)
            var compareColors = donorInput.GetPixels();
            Array.Reverse(compareColors);

            for (int i = 0; i < 10; i++)
            {
                if (colors[i] == compareColors[i])
                    continue;

                return false;
            }

            return true;
        }
    }
}