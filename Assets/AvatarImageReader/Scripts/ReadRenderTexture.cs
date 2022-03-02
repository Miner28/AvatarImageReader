using System;
using UdonSharp;
using UnityEngine;

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
        public string outputString;

        [Header("Render references")] public GameObject renderQuad;

        public Camera renderCamera;
        public RenderTexture renderTexture;
        public Texture2D donorInput;

        //internal
        private Color[] colors;
        private bool hasRun = false;
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

                if (renderTexture != null)
                {
                    //copy the texture over so it can be read
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

            outputString = "";

            stepLength = prefab.stepLength;

            colors = picture.GetPixels();

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
                nextAvatar += ConvertByteToHEX((byte) (color.r * 255));
                nextAvatar += ConvertByteToHEX((byte) (color.g * 255));
                nextAvatar += ConvertByteToHEX((byte) (color.b * 255));
                nextAvatar += ConvertByteToHEX((byte) (color.a * 255));
            }

            nextAvatar =
                $"avtr_{nextAvatar.Substring(0, 8)}-{nextAvatar.Substring(8, 4)}-{nextAvatar.Substring(12, 4)}-{nextAvatar.Substring(16, 4)}-{nextAvatar.Substring(20, 12)}";

            Debug.Log($"AVATAR FOUND - {nextAvatar}");

            index = 5;
            byteIndex = 16;

            SendCustomEventDelayedFrames(nameof(ReadPictureStep), 2);
        }

        private string nextAvatar = "";
        private int index = 1;
        private int byteIndex = 0;
        private int dataLength;
        private int stepLength = 1000;

        public void ReadPictureStep()
        {
            Log($"Reading step {index}\n");

            string tempString = "";

            for (int step = 0;
                 step < stepLength;
                 step++)
            {
                Color c = colors[index];

                tempString += ConvertBytesToUTF16((byte) (c.r * 255), (byte) (c.g * 255));
                tempString += ConvertBytesToUTF16((byte) (c.b * 255), (byte) (c.a * 255));
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

            SendCustomEventDelayedFrames(nameof(ReadPictureStep), 1);
        }

        private void ReadDone()
        {
            if (nextAvatar != "avtr_ffffffff-ffff-ffff-ffff-ffffffffffff")
            {
                //TODO RESET, RELOAD, RETRY
                
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

        private char ConvertBytesToUTF16(byte byte1, byte byte2)
        {
            return (char) (byte1 | (byte2 << 8));
        }

        private string ConvertByteToHEX(byte b)
        {
            return $"{b:xx}";
        }
    }
}