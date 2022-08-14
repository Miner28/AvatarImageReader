// MIT License
//
// Copyright (c) 2021 Miner28, GlitchyDev, BocuD
// Copyright (c) 2021 Haï~
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// This is a C#/UnityEngine implementation of an algorithm originally written in Python:
// "https://github.com/Miner28/AvatarImageReader/tree/main/Assets/AvatarImageDecoder/Python%20Encoder/gen.py"
// This implementation was produced by Haï~ (@vr_hai github.com/hai-vr),
// released under the terms of the License used by "https://github.com/Miner28/AvatarImageReader" at the time of writing,
// included in the header of this C# file.

#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BocuD.VRChatApiTools;
using UnityEngine;

namespace AvatarImageDecoder
{
    public static class AvatarImageEncoder
    {
        private const int headerSize = 20;
        private const int questBytes = (128 * 96 * 4) - headerSize;
        private const int pcBytes = (1200 * 900 * 4) - headerSize;

        /// <summary>
        /// Multi Avatar Image Encoder. Internally calls the existing single image EncodeUTF16Text function, but handles multi avatar headers.
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="availableAvatars">Array of blueprint IDs to use for encoding headers</param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Texture2D[] EncodeUTF16Text(string input, string[] availableAvatars, int width, int height)
        {
            availableAvatars = availableAvatars.Append("avtr_ffffffff_ffff_ffff_ffff_ffffffffffff").ToArray();
            Debug.Log($"Starting Multi Avatar Image Encoder");
            Debug.Log($"Input character count: {input.Length}");
            
            int imageByteCount = (width * height * 4) - headerSize;
            Debug.Log($"Image byte count: {imageByteCount}");
            int imageCharCount = imageByteCount / 2;
            Debug.Log($"Image char count: {imageCharCount}");
            int outputImageCount = (int)Math.Ceiling((float)input.Length / imageCharCount);
            if (outputImageCount == 0) outputImageCount = 1;
            Debug.Log($"Output Image count: {outputImageCount}");

            if (outputImageCount - 1 <= availableAvatars.Length-1)
            {
                Texture2D[] outTex = new Texture2D[outputImageCount];
                string[] outputStrings = new string[outputImageCount];

                for (int i = 0; i < outputStrings.Length; i++)
                {
                    int startIndex = imageCharCount * i;
                    int length = Mathf.Min(imageCharCount, input.Length - startIndex);

                    Texture2D inputTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    
                    outputStrings[i] = input.Substring(startIndex, length);
                    
                    string snippet = outputStrings[i].Length > 30 ? "..." + outputStrings[i].Substring(0, 30) + "..." : "string too short to get snippet";
                    Debug.Log($"Encoding Image {i+1} / {outputImageCount}: Offset {startIndex}; Length {length}; Header Avatar: {availableAvatars[i+1]}; String snippet: {snippet}");
                    
                    outTex[i] = EncodeUTF16Text(outputStrings[i], availableAvatars[i+1], inputTexture);
                }

                return outTex;
            }
            else
            {
                throw new Exception("Not enough avatar IDs were provided to encode the provided string");
            }
        }
        
        /// <summary>
        /// This is a C# implementation of "https://github.com/Miner28/AvatarImageReader/tree/main/Assets/AvatarImageDecoder/Python%20Encoder/gen.py".
        /// The Python script implementation is authoritative over this C# implementation.
        /// 
        /// For this reason, this implementation closely follows the conventions used by the Python script
        /// with only little alteration and no simplifications of the original algorithm,
        /// to allow for future maintenance of the Python script first, followed by porting the updated
        /// Python implementation back into this C# implementation.
        /// </summary>
        /// <param name="input">Input string</param>
        /// <param name="avatar">Next-up Avatar to be encoded in header</param>
        /// <param name="inputTextureNullable">Optional 128x96 texture to use as an input. The pixels will not be reinitialized.</param>
        /// <returns>Encoded image</returns>
        public static Texture2D EncodeUTF16Text(string input, string avatar = "", Texture2D inputTextureNullable = null)
        {
            byte[] textbyteArray = Encoding.Unicode.GetBytes(input);

            if (!string.IsNullOrEmpty(avatar) && Regex.IsMatch(avatar, VRChatApiTools.avatar_regex))
            {
                
                avatar = avatar.Replace("avtr_", "").Replace("-", "");
                byte[] hex = StringToByteArray(avatar);
                foreach (byte b in hex.Reverse())
                {
                    textbyteArray = textbyteArray.Prepend<byte>(b).ToArray();
                }

            }
            else
            {
                foreach (var b in new byte[] {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255})
                {
                    textbyteArray = textbyteArray.Prepend<byte>(b).ToArray();
                }
            }
            
            var lengthOfTextbyteListWith4Bytes = textbyteArray.Length;
            var totalBytesWith4Bytes = BitConverter.GetBytes(lengthOfTextbyteListWith4Bytes).Reverse().ToArray();

            if (textbyteArray.Length % 4 != 0)
            {
                textbyteArray = textbyteArray.Append((byte) 16).ToArray();
            }
            if (textbyteArray.Length % 4 != 0)
            {
                textbyteArray = textbyteArray.Append((byte) 16).ToArray();
            }
            if (textbyteArray.Length % 4 != 0)
            {
                textbyteArray = textbyteArray.Append((byte) 16).ToArray();
            }

            Texture2D img;
            if (inputTextureNullable != null)
            {
                img = inputTextureNullable;
            }
            else
            {
                var imageWidth = 128;
                var imageHeight = 96;
                
                img = new Texture2D(imageWidth, imageHeight, TextureFormat.RGBA32, false);
            }
            
            var pixels = Enumerable.Repeat(new Color32(255, 255, 255 ,255), img.width * img.height).ToArray();
            
            textbyteArray = textbyteArray.Prepend(totalBytesWith4Bytes[3]).ToArray();
            textbyteArray = textbyteArray.Prepend(totalBytesWith4Bytes[2]).ToArray();
            textbyteArray = textbyteArray.Prepend(totalBytesWith4Bytes[1]).ToArray();
            textbyteArray = textbyteArray.Prepend(totalBytesWith4Bytes[0]).ToArray();


            

            for (int i = 0; i < pixels.Length ; i++)
            {
                pixels[i] = new Color32(textbyteArray[i* 4], textbyteArray[i* 4 + 1], textbyteArray[i* 4 + 2], textbyteArray[i* 4 + 3]);
                if (textbyteArray.Length < i* 4 +5)
                {
                    break;
                }
            }
            img.SetPixels32(pixels.Reverse().ToArray());


            return img;
        }
        
        public static byte[] StringToByteArray(string hex) {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}

#endif
