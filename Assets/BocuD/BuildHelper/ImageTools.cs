#if UNITY_EDITOR && !COMPILER_UDONSHARP

using UnityEngine;

namespace BocuD.BuildHelper
{
    public static class ImageTools
    {
        public static string GetImagePath(string sceneID, string branchID)
        {
            return Application.dataPath + "/Resources/BuildHelper/" + sceneID + '_' + branchID + "-edit.png";
        }

        public static string GetImageAssetPath(string sceneID, string branchID, bool includeExtension = true)
        {
            return "Assets/Resources/BuildHelper/" + sceneID + '_' + branchID + (includeExtension ? "-edit.png" : "-edit");
        }
        
        public static string GenerateImageFeedback(int width, int height)
        {
            string feedback = "";
            
            //check aspect ratio and resolution
            if (width * 3 != height * 4)
            {
                if (width < 1200)
                {
                    feedback = "<color=yellow>" + "For best results, use a 4:3 image that is at least 1200x900.\n" + "</color>";
                }
                else
                {
                    feedback = "<color=yellow>" + "For best results, use a 4:3 image.\n" + "</color>";
                }
            }
            else
            {
                if (width < 1200)
                {
                    feedback = "<color=yellow>" + "For best results, use an image that is at least 1200x900.\n" + "</color>";
                }
                else if (width == 1200)
                {
                    feedback = "<color=green>" +
                               "Your new image is exactly 1200x900. This means it can be uploaded to VRChat in a 1:1 format. Awesome!\n" +
                               "</color>";
                }
                else
                {
                    feedback = "<color=green>" +
                               "Your new image has the correct aspect ratio and is high resolution. Nice!\nFor even better results, try using an image that is exactly 1200x900.\n" +
                               "</color>";
                }
            }

            return feedback;
        }

        public static Texture2D Resize(Texture2D texture2D, int targetX, int targetY)
        {
            RenderTexture renderTexture = new RenderTexture(targetX, targetY, 24);
            RenderTexture.active = renderTexture;
            Graphics.Blit(texture2D, renderTexture);
            Texture2D result = new Texture2D(targetX, targetY);
            result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
            result.Apply();
            return result;
        }
    }
}

#endif