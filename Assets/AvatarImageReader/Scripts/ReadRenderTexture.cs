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

        [Header("Render references")]
        public GameObject renderQuad;

        public Camera renderCamera;
        public RenderTexture renderTexture;
        public Texture2D donorInput;

        //internal
        private Color[] colors;
        private bool hasRun;
        [NonSerialized] public bool pedestalReady;
        private System.Diagnostics.Stopwatch stopwatch;

        
        public void OnPostRender()
        {
            if (pedestalReady && !hasRun)
            {
                stopwatch = new System.Diagnostics.Stopwatch();//TODO ONLY WHEN DEBUG
                stopwatch.Start();

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
                }

                hasRun = true;
            }
        }

        private void Log(string text)
        {
            Debug.Log($"[<color=#00fff7>ReadRenderTexture</color>] {text}"); //TODO Me == Lazy, Remove when done
            if (!prefab.debugLogger) return;

            Debug.Log($"[<color=#00fff7>ReadRenderTexture</color>] {text}");

            if (prefab.debugTMP)
            {
                prefab.loggerText.text += $"{text}\n";
            }
        }

        private void StartReadPicture(Texture2D picture)
        {
            Log("Starting Read");
            Log($"Input: {picture.width} x {picture.height} [{picture.format}]");

            outputString = "";

            int w = picture.width;
            int h = picture.height;

            colors = new Color[w * h]; //TODO Test if this is needed, I believe this is not required
            colors = picture.GetPixels();

            Array.Reverse(colors);
            
            Color color = colors[0];
            dataLength = (byte) (color.r * 255) << 16 | (byte) (color.g * 255) << 8 |
                         (byte) (color.b * 255);

            Log($"Data length: {dataLength} bytes");
            
            nextAvi = "";
            for (int i = 1; i < 6; i++)
            {
                color = colors[i];
                nextAvi += ConvertByteToHEX((byte) (color.r * 255));
                nextAvi += ConvertByteToHEX((byte) (color.g * 255));
                nextAvi += ConvertByteToHEX((byte) (color.b * 255));
                nextAvi += ConvertByteToHEX((byte) (color.a * 255));
            }
            
            nextAvi = $"avtr_{nextAvi.Substring(0, 8)}-{nextAvi.Substring(8, 4)}-{nextAvi.Substring(12, 4)}-{nextAvi.Substring(16,4)}-{nextAvi.Substring(20,12)}";

            Log($"AVATAR FOUND - {nextAvi}");

            index = 5;
            byteIndex = 18;
            
            SendCustomEventDelayedFrames(nameof(ReadPictureStep), 2);
        }

        private string nextAvi = "";
        private int index = 1;
        private int byteIndex = 0;
        private int dataLength;
        
        public void ReadPictureStep()
        {
            Log($"Reading step {index}\n");

            int stepLength = prefab.stepLength; //TODO GONE

            string tempString = "";

            for (int step = 0; step < stepLength; step++) //TODO Stopwatch new performance and adjust DEFAULT speeds + MAX speed
            {
                Color c = colors[index];
                
                tempString += ConvertBytesToUTF16((byte) (c.r * 255), (byte) (c.g * 255));
                tempString += ConvertBytesToUTF16((byte) (c.b*255), (byte)(c.a*255));
                byteIndex += 4;
                
                
                if (byteIndex >= dataLength)
                {
                    Log($"Reached data length: {dataLength}; byteIndex: {byteIndex}");
                    //TODO Needs checking if ended on % 4 == 0, So it doesn't append 1 non-sense char
                    
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
            Log($"|{outputString}|");

            if (nextAvi != "")//TODO "" > "f*??" Needs replacing check with full F avatar.
            {
                
                //TODO RESET, RELOAD, RETRY
                return;
            }
            
            stopwatch.Stop();//TODO This should only happen if DEBUG is enabled
            Log($"Took: {stopwatch.ElapsedMilliseconds} ms");

            if (prefab.outputToText) prefab.outputText.text = outputString;
            prefab.outputString = outputString;

            Log("Reading Complete: " + outputString);
            if (prefab.callBackOnFinish && prefab.callbackBehaviour != null && prefab.callbackEventName != "")
                prefab.callbackBehaviour.SendCustomEvent(prefab.callbackEventName);

            gameObject.SetActive(false);

        }

        private char ConvertBytesToUTF16(byte byte1, byte byte2)
        {
            Log(((char) (byte1 | (byte2 << 8))).ToString()); //TODO Remove when debugging done
            return (char) (byte1 | (byte2 << 8));
        }

        private string ConvertByteToHEX(byte b)
        {
            return $"{b >> 4:x}{b & 0xF:x}";
        }
        
    }
}