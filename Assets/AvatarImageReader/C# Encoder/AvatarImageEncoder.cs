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

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Encoders;
using UnityEngine;

namespace AvatarImageDecoder
{
    public static class AvatarImageEncoder
    {
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
        public static Texture2D EncodeUTF16Text(string input, string avatar="", Texture2D inputTextureNullable = null)
        {
            // gen.py:6
            var textbyteArray = Encoding.Unicode.GetBytes(input);

            if (avatar != "" && new Regex(@"avtr_[a-zA-Z0-9]{8}-[a-zA-Z0-9]{8}-[a-zA-Z0-9]{4}-[a-zA-Z0-9]{12}").Match(avatar).Length > 0)
            {
                avatar = avatar.Replace("avtr_", "").Replace("-", "");
                var hex = StringToByteArray(avatar);
                var textByteList = textbyteArray.ToList();
                foreach (var b in hex.Reverse())
                {
                    textByteList.Prepend<byte>(b);
                }

                textbyteArray = textByteList.ToArray();
            }
            else
            {
                var textByteList = textbyteArray.ToList();
                
                foreach (var b in new byte[] {255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255})
                {
                    textByteList.Prepend<byte>(b);
                }

                textbyteArray = textByteList.ToArray();
            }

            // gen.py:7
            var lengthOfTextbyteListWith4Bytes = textbyteArray.Length;
            var totalBytesWith4Bytes = BitConverter.GetBytes(lengthOfTextbyteListWith4Bytes);
            var totalBytes = new[] {totalBytesWith4Bytes[2], totalBytesWith4Bytes[1], totalBytesWith4Bytes[0]};

            // gen.py:9-13
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

            // gen.py:16-21
            var index = 0;
            foreach (var x in textbyteArray)
            {
                //Debug.Log($"{index} : {x}");
                index += 1;
            }

            // gen.py:23
            Texture2D img;
            if (inputTextureNullable != null)
            {
                // Deviation from Python script:
                // Instead of allocating a base image initialized with white pixels, accept an input texture.
                // Its pixels will not be reinitialized.
                img = inputTextureNullable;
            }
            else
            {
                var imageWidth = 128;
                var imageHeight = 96;
                
                img = new Texture2D(imageWidth, imageHeight, TextureFormat.RGBA32, false);
                var initialPixels = Enumerable.Repeat(Color.white, imageWidth * imageHeight).ToArray();
                img.SetPixels(initialPixels);
            }

            // gen.py:25-27
            var oppositePosition = 0;
            img.SetPixel(img.width - 1, img.height - 1 - oppositePosition, new Color(BtF(totalBytes[0]), BtF(totalBytes[1]), BtF(totalBytes[2])));
            //Debug.Log($"A{textbyteList.Length} {PythonStr(totalBytes)}");
            //Debug.Log($"B{PythonStr(new[] {totalBytes[0], totalBytes[1], totalBytes[2]})}");

            // gen.py:29
            var rangeLower = 1;
            var rangeUpperExclusive = textbyteArray.Length / 4 + 1;
            for (var x = rangeLower; x < rangeUpperExclusive; x++)
            {
                // gen.py:30
                var xPosition = PythonModulus(((img.width - 1) - x), img.width);
                var yOppositePosition = x / img.width;
                img.SetPixel(xPosition, img.height - 1 - yOppositePosition,
                    // gen.py:31
                    new Color(BtF(textbyteArray[(x - 1) * 4]), BtF(textbyteArray[(x - 1) * 4 + 1]), BtF(textbyteArray[(x - 1) * 4 + 2]), BtF(textbyteArray[(x - 1) * 4 + 3])));
            }

            // gen.py:33
            img.Apply();
            return img;
        }

        private static int PythonModulus(int a, int n)
        {
            // The % (modulo) operator in C# is the remainder operator.
            // The % (modulo) operator in Python is the modulus operator.
            return (a % n + n) % n;
        }

        private static float BtF(byte byteComponent)
        {
            // Byte To Float
            return byteComponent / 255f;
        }

        private static string PythonStr(byte[] byteArray)
        {
            return $"[{string.Join(", ", byteArray)}]";
        }
        public static byte[] StringToByteArray(string hex) {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
    
}
