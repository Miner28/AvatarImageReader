/* Original copyright notice and license

 MIT License

 Copyright (c) 2020 Haï~ (@vr_hai github.com/hai-vr)

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

/*
 Rewritten for use in VR Build Helper by BocuD on 26-10-2021
 Copyright (c) 2021 BocuD (github.com/BocuD)
*/

#if UNITY_EDITOR && !COMPILER_UDONSHARP

using System;
using static BocuD.VRChatApiTools.VRChatApiTools;

namespace BocuD.BuildHelper
{
    [Serializable]
    public class DeploymentData
    {
        public string deploymentPath;
        public string initialBranchName = "unused";
        public DeploymentUnit[] units;
    }
    
    [Serializable]
    public struct DeploymentUnit
    {
        public bool autoUploader;
        public string fileName;
        public int buildNumber;
        public long fileSize;
        public Platform platform;
        public string gitHash;
        public string filePath;
        public string pipelineID;
        public DateTime buildDate;
        public DateTime modifiedDate;
        public long modifiedFileTime;
    }
}

#endif