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

#if VRCHAT_API_TOOLS_IMPORTED

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AvatarImageReader.Enums;
using BocuD.VRChatApiTools;
using UnityEditor;
using UnityEngine;

namespace AvatarImageReader.Editor
{
    public static class AvatarImageEncoder
    { 
        private static readonly Version Version = new Version(3, 0, 0);
        
        public static Texture2D[] EncodeText(string input, string[] availableAvatars, int width, int height, DataMode dataMode)
        {
            byte[] bytesToEncode;
            
            switch (dataMode)
            {
                case DataMode.UTF8:
                    bytesToEncode = Encoding.UTF8.GetBytes(input);
                    break;
                
                case DataMode.UTF16:
                    bytesToEncode = Encoding.Unicode.GetBytes(input);
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(dataMode), dataMode, null);
            }

            int bytesToEncodeLength = bytesToEncode.Length;
            int maxBytes = (width * height - 7) * 4;
            
            //slice bytesToEncode into segments
            List<byte[]> byteSegments = new List<byte[]>();
            
            for (int i = 0; i < bytesToEncodeLength / maxBytes + 1; i++)
            {
                byteSegments.Add(bytesToEncode.Slice(i * maxBytes, maxBytes));
            }

            //check if there are enough avatars
            if (byteSegments.Count > availableAvatars.Length)
            {
                Debug.LogError($"More byteSegments than available avatars! Aborting encode. ({byteSegments.Count} segments, {availableAvatars.Length} avatars)");
                return null;
            }

            //generate the textures
            Texture2D[] output = new Texture2D[byteSegments.Count];
            
            for (int index = 0; index < byteSegments.Count; index++)
            {
                byte[] segment = byteSegments[index];

                string avatarID = index == byteSegments.Count - 1 ? "avtr_ffffffff-ffff-ffff-ffff-ffffffffffff" : availableAvatars[index + 1];
                
                byte[] header = GenerateHeader(segment.Length, avatarID, Version, dataMode);
                byte[] data = new byte[segment.Length + header.Length];
                
                header.CopyTo(data, 0);
                segment.CopyTo(data, header.Length);
                
                while(data.Length % 4 != 0)
                {
                    ArrayUtility.Add(ref data, (byte)16);
                }
                
                output[index] = GenerateImage(data, width, height);
            }

            return output;
        }
        
        public static byte[] GenerateHeader(int length, string avatarID, Version version, DataMode dataMode)
        {
            byte[] header = new byte[7 * 4];
            
            byte[] lengthBytes = BitConverter.GetBytes(length).Reverse().ToArray();

            string parsedAvatarID = avatarID.Replace("avtr_", "").Replace("-", "");
            
            byte[] avatarIDBytes = new byte[16];
            
            //convert the avatarID to bytes
            for (int i = 0; i < 16; i++)
            {
                avatarIDBytes[i] = Convert.ToByte(parsedAvatarID.Substring(i * 2, 2), 16);
            }
            
            byte[] versionBytes = { (byte)version.Major, (byte)version.Minor };
            byte[] dataModeBytes = { (byte)dataMode };
            
            for (int i = 0; i < 4; i++)
            {
                header[i] = lengthBytes[i];
                
                header[i + 4] = avatarIDBytes[i];
                header[i + 8] = avatarIDBytes[i + 4];
                header[i + 12] = avatarIDBytes[i + 8];
                header[i + 16] = avatarIDBytes[i + 12];
            }
            
            //add null pixel to signify new encoding method
            header[20] = 0;
            header[21] = 0;
            header[22] = 0;
            header[23] = 0;
            
            header[24] = dataModeBytes[0];
            header[25] = versionBytes[0];
            header[26] = versionBytes[1];
            header[27] = 0; //signifies C# encoder

            return header;
        }

        public static Texture2D GenerateImage(byte[] data, int width, int height)
        {
            Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = Enumerable.Repeat(new Color32(255, 255, 255, 255), texture2D.width * texture2D.height).ToArray();
            
            for(int i = 0; i < pixels.Length; i++)
            {
                if (i * 4 > data.Length - 1) break;
                
                pixels[i] = new Color32(data[i * 4], data[i * 4 + 1], data[i * 4 + 2], data[i * 4 + 3]);
            }
            
            texture2D.SetPixels32(pixels.Reverse().ToArray());
            return texture2D;
        }
    }
}

public static class Extensions
{
    public static T[] Slice<T>(this T[] source, int index, int length)
    {
        if (index + length > source.Length)
        {
            length = source.Length - index;
        }
        T[] slice = new T[length];
        Array.Copy(source, index, slice, 0, length);
        return slice;
    }
}
#endif
