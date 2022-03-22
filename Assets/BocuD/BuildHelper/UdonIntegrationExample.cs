/* MIT License
 Copyright (c) 2021 BocuD (github.com/BocuD)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
*/

using System;
using UdonSharp;
using UnityEngine;

namespace BocuD.BuildHelper
{
    public class UdonIntegrationExample : UdonSharpBehaviour
    {
        [SerializeField] private DateTime dateTime1;
        [SerializeField] private DateTime dateTime2;
        [SerializeField] private DateTime dateTime3;
        [SerializeField] private DateTime dateTime4;
        
        [SerializeField] private int number1;
        [SerializeField] private uint number2;
        [SerializeField] private short number3;
        [SerializeField] private ushort number4;
        
        [SerializeField] private string string1;
        [SerializeField] private string string2;

        public void _VersionMatch()
        {
            Debug.LogError("Version match callback");
        }

        public void _VersionMismatch()
        {
            Debug.LogError("Version mismatch callback");
        }

        public void _VersionMismatchRemote()
        {
            Debug.LogError("Version mismatch remote callback");
        }

        public void _VersionTimeout()
        {
            Debug.LogError("Version timeout callback");
        }
    }
}
