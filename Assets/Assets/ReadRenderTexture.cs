using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

public class ReadRenderTexture : UdonSharpBehaviour
{
    /**
     * This script is meant to be used as a One time "Read" for a specific render texture (Since they can't be reused)
     * Once "Primed" by the CheckHirarchyScript, it will decode the retrieved RenderTexture
     */
    [SerializeField] private RenderTexture renderTexture;

    [SerializeField] private Texture2D texture2d;

    [SerializeField] private bool debugLogging;
    public KeypadNew keypad;
#if UNITY_STANDALONE_WIN

    private Color32[] colors;
    private int currentByteStorageIndex;
    private int currentColorStorageIndex;
    private string currentOutputString;
    private bool hasRun;

    private int index = 0;

    public bool isNotReady = true;
    private System.Diagnostics.Stopwatch stopwatch;
    private byte[] temporaryByteStorage;
    private byte[] temporaryColorStorage;
    

    public void OnPostRender()
    {
        if (!isNotReady && !hasRun)
        {
            hasRun = true;
            if (debugLogging)
            {
                stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
            }

            Log("Starting");
            if (renderTexture != null)
            {
                // Copy the texture over so it can be read
                texture2d.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
                StartReadPicture(texture2d);
                Log("Writing Information");
            }
        }
    }

    private void Log(string text)
    {
        if (debugLogging) Debug.Log($"[<color=#00fff7>ReadRenderTexture</color>] |{text}");
    }

    public void StartReadPicture(Texture2D picture)
    {
        Log("ReadRenderTexture: Starting Read");
        currentOutputString = "";
        currentByteStorageIndex = 0;
        temporaryByteStorage = new byte[4];
        currentColorStorageIndex = 0;
        temporaryColorStorage = new byte[8];
        colors = picture.GetPixels32();
        Array.Reverse(colors);
        index = 0;

        SendCustomEventDelayedFrames(nameof(ReadPictureStep), 2);
    }

    public void ReadDone()
    {
        if (debugLogging)
        {
            stopwatch.Stop();
        }

        //Your stuff here
        Destroy(this);
        
    }

    public void ReadPictureStep()
    {
        Log($"ReadRenderTexture: Reading {index}");
        for (var step = 0; index < colors.Length && step < 500; step++, index++)
        {
            var c = colors[index];
            temporaryColorStorage[currentColorStorageIndex] = c.r;
            currentColorStorageIndex++;
            temporaryColorStorage[currentColorStorageIndex] = c.g;
            currentColorStorageIndex++;
            if (currentColorStorageIndex != 8)
            {
                temporaryColorStorage[currentColorStorageIndex] = c.b;
                currentColorStorageIndex++;
            }
            else
            {
                
                //LogError((temporaryColorStorage[0] == 255) + " " + (temporaryColorStorage[1] == 255) + " " + (temporaryColorStorage[2] == 255) + " " + (temporaryColorStorage[3] == 255) + " " + (temporaryColorStorage[4] == 255) + " " + (temporaryColorStorage[5] == 255) + " " + (temporaryColorStorage[6] == 255) + " " + (temporaryColorStorage[7] == 255) + " " + (c.b == 255));
                // Check if this is a terminator for the picture
                if (temporaryColorStorage[0] == 255 && temporaryColorStorage[1] == 255 && temporaryColorStorage[2] == 255 && temporaryColorStorage[3] == 255 && temporaryColorStorage[4] == 255 && temporaryColorStorage[5] == 255 && temporaryColorStorage[6] == 255 && temporaryColorStorage[7] == 255 && c.b == 255)
                {
                    ReadDone();
                    return;
                }

                currentColorStorageIndex = 0;

                // Add in the Bits collected from the colors
                temporaryByteStorage[currentByteStorageIndex] = createByte(temporaryColorStorage[0] == 255, temporaryColorStorage[1] == 255, temporaryColorStorage[2] == 255, temporaryColorStorage[3] == 255, temporaryColorStorage[4] == 255, temporaryColorStorage[5] == 255, temporaryColorStorage[6] == 255, temporaryColorStorage[7] == 255);

                currentByteStorageIndex++;

                // See if this is the end of a Character
                if (currentByteStorageIndex == 4)
                {
                    var addedString = convertBytesToUTF32(temporaryByteStorage);
                    currentOutputString += addedString;
                    currentByteStorageIndex = 0;
                }
            }
        }

        SendCustomEventDelayedFrames(nameof(ReadPictureStep), 1);
    }

    public string convertBytesToUTF32(byte[] bytes)
    {
        return char.ConvertFromUtf32(bytes[0] + bytes[1] * 255 + bytes[2] * 65536 + bytes[3] * 16581375);
    }


    public byte createByte(bool bit0, bool bit1, bool bit2, bool bit3, bool bit4, bool bit5, bool bit6, bool bit7)
    {
        byte value = 0;
        if (bit0) value ^= 1 << 0;
        if (bit1) value ^= 1 << 1;
        if (bit2) value ^= 1 << 2;
        if (bit3) value ^= 1 << 3;
        if (bit4) value ^= 1 << 4;
        if (bit5) value ^= 1 << 5;
        if (bit6) value ^= 1 << 6;
        if (bit7) value ^= 1 << 7;


        return value;
    }
    #else
public void Start()
{
    Destroy(this);
}
#endif
}
