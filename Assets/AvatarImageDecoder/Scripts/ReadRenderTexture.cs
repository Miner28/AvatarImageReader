using System;
using System.Linq;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using VRC.Udon;

public class ReadRenderTexture : UdonSharpBehaviour
{
    /*
     * This script is meant to be used as a One time "Read" for a specific render texture (Since they can't be reused)
     * Once "Primed" by the CheckHirarchyScript, it will decode the retrieved RenderTexture
     */

    [Header("Increasing step size decreases decode time but increases frametimes")]
    [SerializeField] private int stepLength = 200;
    
    [Header("Call event when finished reading")]
    [SerializeField] private bool callBackOnFinish = false;
    [SerializeField] private UdonBehaviour callbackBehaviour;
    [SerializeField] private string callbackEventName;
    
    [Header("Render references")]
    [SerializeField] private GameObject renderQuad;
    [SerializeField] private Camera renderCamera;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Texture2D donorInput;

    [Header("Debugging")]
    [SerializeField] private bool debugLogger;
    [SerializeField] private TextMeshProUGUI loggerText;
    [SerializeField] private bool outputToText;
    [SerializeField] private TextMeshProUGUI outputText;
    
    //internal
    private Color[] colors;
    public string currentOutputString;
    private bool hasRun;
    [HideInInspector] public bool pedestalReady;
    private System.Diagnostics.Stopwatch stopwatch;

    public void OnPostRender()
    {
        if (pedestalReady && !hasRun)
        {
            if (debugLogger)
            {
                stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
                Log("ReadRenderTexture: Starting");
            }
            
            if (renderTexture != null)
            {
                //copy the texture over so it can be read
                donorInput.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                donorInput.Apply();
                StartReadPicture(donorInput);
                
                //disable the renderquad to prevent VR users from getting a seizure (disable the camera first so it only renders one frame)
                renderCamera.enabled = false;
                renderQuad.SetActive(false);
                
                if(debugLogger) Log("ReadRenderTexture: Writing Information");
            }
            
            hasRun = true;
        }
    }

    private void Log(string text)
    {
        Debug.Log($"[<color=#00fff7>ReadRenderTexture</color>] |{text}");
        loggerText.text += $" | {text}";
        //Comment above line if not using TMPLogger
    }

    private void StartReadPicture(Texture2D picture)
    {
        if(debugLogger)
        {
            Log("Starting Read");
            Log($"Input: {picture.width} x {picture.height} [{picture.format}]");
        }
        
        currentOutputString = "";

        int w = picture.width;
        int h = picture.height;

        colors = new Color[w * h];
        colors = picture.GetPixels();
        
        Array.Reverse(colors);

        Color firstColor = colors[0];
        dataLength = (byte) (firstColor.r * 255) << 16 | (byte) (firstColor.g * 255) << 8 | (byte) (firstColor.b * 255);
        if(debugLogger) Log("Data length: " + dataLength);

        SendCustomEventDelayedFrames(nameof(ReadPictureStep), 2);
    }

    private int index = 1;
    private int byteIndex = 0;
    private int dataLength;

    private byte[] colorBytes = new byte[3];
    private byte[] byteCache = new byte[2];
    private bool lastIndex = true;

    public void ReadPictureStep()
    {
        if(debugLogger) Log($"ReadRenderTexture: Reading {index}\n");

        for (int step = 0; step < stepLength; step++)
        {
            Color c = colors[index];

            colorBytes[0] = (byte)(c.r * 255);
            colorBytes[1] = (byte)(c.g * 255);
            colorBytes[2] = (byte)(c.b * 255);

            for (int b = 0; b < 3; b++)
            {
                if (lastIndex)
                {
                    byteCache[0] = colorBytes[b];
                    lastIndex = false;
                }
                else
                {
                    byteCache[1] = colorBytes[b];
                    currentOutputString += convertBytesToUTF16(byteCache);
                    lastIndex = true;
                }

                byteIndex++;
                if (byteIndex > dataLength)
                {
                    if(debugLogger) Log($"Reached data length: {dataLength}; byteIndex: {byteIndex}");
                    ReadDone();
                    return;
                }
            }

            index++;
        }
        
        SendCustomEventDelayedFrames(nameof(ReadPictureStep), 1);
    }

    private void ReadDone()
    {
        if (debugLogger)
        {
            stopwatch.Stop();
            Log($"Took: {stopwatch.ElapsedMilliseconds} ms");
        }
        
        if(outputToText) outputText.text = currentOutputString;
        
        if(debugLogger) Log("Reading Complete: " + currentOutputString);
        if(callBackOnFinish) callbackBehaviour.SendCustomEvent(callbackEventName);

        gameObject.SetActive(false);
    }

    private string empty = "";
    private string convertBytesToUTF16(byte[] bytes)
    {
        return empty + (char)(bytes[0] | (bytes[1] << 8));
    }
}
