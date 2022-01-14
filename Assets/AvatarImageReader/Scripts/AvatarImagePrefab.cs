using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon;

namespace AvatarImageReader
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class AvatarImagePrefab : UdonSharpBehaviour
    {
        public string linkedAvatar;
        public string uid = "";
        public bool pedestalAssetsReady;
        public bool destroyPedestalOnComplete = true;
        
        [Header("Image Options")]
        //0 cross platform, 1 pc only
        public int imageMode = 0;

        [Header("General Options")]
        [Tooltip("Increasing step size decreases decode time but increases frametimes")] 
        public int stepLength = 200;
    
        public bool outputToText;
        public bool autoFillTMP;
        public TextMeshPro outputText;
    
        public bool callBackOnFinish = false;
        public UdonBehaviour callbackBehaviour;
        public string callbackEventName;
    
        [Header("Data Encoding")]
        //0 UTF16 string, 1 ASCII string, 2 Binary
        public int dataMode = 0;
        public bool patronMode;
    
        [Header("Debugging")] 
        public bool debugLogger;
        public bool debugTMP;
        public TextMeshPro loggerText;

        [Header("Output")] 
        public string outputString;

        [Header("Internal")]
        public ReadRenderTexture readRenderTexture;
        public VRCAvatarPedestal avatarPedestal;
    }
}