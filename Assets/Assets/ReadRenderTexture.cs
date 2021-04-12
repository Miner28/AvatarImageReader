
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class ReadRenderTexture : UdonSharpBehaviour
{
    /**
     * This script is meant to be used as a One time "Read" for a specific render texture (Since they can't be reused)
     * Once "Primed" by the CheckHirarchyScript, it will decode the retrieved RenderTexture
     */

    [SerializeField] RenderTexture renderTexture;
    [SerializeField] Texture2D texture2d;
    [SerializeField] Text debugText;

    public bool isNotReady = true;
    public bool isCompleted = false;

    public void Start()
    {

    }


    public void OnPostRender()
    {
        if (!isNotReady && !isCompleted)
        {
            Debug.Log("ReadRenderTexture: Starting");
            if (renderTexture != null)
            {
                // Copy the texture over so it can be read
                texture2d.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
                Debug.Log("ReadRenderTexture: Writing Information");
            }
            string output = ReadPicture(texture2d);
            Debug.Log("ReadRenderTexture Output: " + output);
            debugText.text = output;
        }
    }

    public string ReadPicture(Texture2D picture)
    {
        Debug.Log("ReadRenderTexture: Starting Read");
  
        string currentOutputString = "";

        int currentByteStorageIndex = 0;
        byte[] temporaryByteStorage = new byte[4];

        int currentColorStorageIndex = 0;
        byte[] temporaryColorStorage = new byte[8];


        Color32[] colors = picture.GetPixels32();
        System.Array.Reverse(colors);
        foreach (Color32 c in colors)
        {
        
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
                //Debug.LogError((temporaryColorStorage[0] == 255) + " " + (temporaryColorStorage[1] == 255) + " " + (temporaryColorStorage[2] == 255) + " " + (temporaryColorStorage[3] == 255) + " " + (temporaryColorStorage[4] == 255) + " " + (temporaryColorStorage[5] == 255) + " " + (temporaryColorStorage[6] == 255) + " " + (temporaryColorStorage[7] == 255) + " " + (c.b == 255));
                // Check if this is a terminator for the picture
                if (temporaryColorStorage[0] == 255 && temporaryColorStorage[1] == 255 && temporaryColorStorage[2] == 255 && temporaryColorStorage[3] == 255 && temporaryColorStorage[4] == 255 && temporaryColorStorage[5] == 255 && temporaryColorStorage[6] == 255 && temporaryColorStorage[7] == 255 && c.b == 255)
                {
                    isCompleted = true;
                    Debug.LogError(Time.time);
                    return currentOutputString;
                }
                else
                {
                    currentColorStorageIndex = 0;

                    // Add in the Bits collected from the colors
                    temporaryByteStorage[currentByteStorageIndex] = createByte(temporaryColorStorage[0] == 255, temporaryColorStorage[1] == 255, temporaryColorStorage[2] == 255, temporaryColorStorage[3] == 255, temporaryColorStorage[4] == 255, temporaryColorStorage[5] == 255, temporaryColorStorage[6] == 255, temporaryColorStorage[7] == 255);

                    currentByteStorageIndex++;

                    // See if this is the end of a Character
                    if (currentByteStorageIndex == 4)
                    {
                        string addedString = convertBytesToUTF32(temporaryByteStorage);
                        currentOutputString += addedString;
                        currentByteStorageIndex = 0;
                    }
                }
            }
        }
        return null;
    }

    public string convertBytesToUTF32(byte[] bytes)
    {
        return char.ConvertFromUtf32(bytes[0] + bytes[1] * 255 + bytes[2] * 65536 + bytes[3] * 16581375);
    }



    public byte createByte(bool bit0, bool bit1, bool bit2, bool bit3, bool bit4, bool bit5, bool bit6, bool bit7)
    {
        byte value = 0;
        if (bit0)
        {
            value ^= (1 << 0);
        }
        if (bit1)
        {
            value ^= (1 << 1);
        }
        if (bit2)
        {
            value ^= (1 << 2);
        }
        if (bit3)
        {
            value ^= (1 << 3);
        }
        if (bit4)
        {
            value ^= (1 << 4);
        }
        if (bit5)
        {
            value ^= (1 << 5);
        }
        if (bit6)
        {
            value ^= (1 << 6);
        }
        if (bit7)
        {
            value ^= (1 << 7);
        }


        return value;
    }
}
