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

#if UNITY_EDITOR && !COMPILER_UDONSHARP

using UnityEngine;
using UnityEngine.UI;

namespace BocuD.BuildHelper
{
    public class BuildHelperToolsMenu : MonoBehaviour
    {
        [Header("Cam menu")]
        public Toggle saveCamPosition;
        public Toggle uniqueCamPosition;

        [Header("Image menu")]
        public Dropdown imageSourceDropdown;
        public Button selectImageButton;
        public RawImage imagePreview;
        public Text aspectRatioWarning;

        public GameObject camOptions;
        public GameObject imageOptions;

        public Material CoverVRCCamMat;
        public Transform CoverVRCCam;

        private Texture2D overrideImage;

        //i hate this. thanks vrc.
        private bool init;
        private void Update()
        {
            if (!init)
            {
                imageSourceDropdown.onValueChanged.AddListener(DropdownUpdate);
                selectImageButton.onClick.AddListener(ChangeImage);
                init = true;
            }
        }

        public void DropdownUpdate(int value)
        {
            switch (value)
            {
                case 0:
                    ShowCamOptions();
                    break;
                case 1:
                    ShowImageOptions();
                    break;
            }
        }

        public void ChangeImage()
        {
            string[] allowedFileTypes = new[] {"png"};
            NativeFilePicker.PickFile(OnFileSelected, allowedFileTypes);
        }

        public void OnFileSelected(string filePath)
        {
            Logger.Log($"Loading override image from {filePath}");
            overrideImage = null;
            byte[] fileData;

            if (System.IO.File.Exists(filePath))
            {
                fileData = System.IO.File.ReadAllBytes(filePath);
                overrideImage = new Texture2D(2, 2);
                overrideImage.LoadImage(fileData); //..this will auto-resize the texture dimensions.

                imagePreview.texture = overrideImage;
                CoverVRCCamMat.mainTexture = overrideImage;

                aspectRatioWarning.supportRichText = true;
                //check aspectRatio and resolution
                aspectRatioWarning.text = ImageTools.GenerateImageFeedback(overrideImage.width, overrideImage.height);
            }
        }
    
        private void ShowCamOptions()
        {
            camOptions.SetActive(true);
            imageOptions.SetActive(false);
        }

        private void ShowImageOptions()
        {
            camOptions.SetActive(false);
            imageOptions.SetActive(true);
        
            Transform VRCCam = GameObject.Find("VRCCam").transform;
            CoverVRCCam.position = VRCCam.position;
            CoverVRCCam.rotation = VRCCam.rotation;
            CoverVRCCam.position += VRCCam.forward;
        }
    }
}

#endif