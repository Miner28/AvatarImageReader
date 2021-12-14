using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.Udon;

public class AvatarImagePrefab : UdonSharpBehaviour
{
    public string linkedAvatar;
    
    [Header("Image Options")]
    //0 cross platform, 1 pc only
    public int imageMode = 0;

    [Header("General Options")]
    public bool outputToText;
    public TextMeshPro outputText;
    
    [Header("Debugging")] 
    public bool debugLogger;
    public bool debugTMP;
    public TextMeshPro loggerText;
    
    

    [Header("Increasing step size decreases decode time but increases frametimes")] [SerializeField]
    private int stepLength = 200;

    [Header("Call event when finished reading")] [SerializeField]
    private bool callBackOnFinish = false;

    [SerializeField] private UdonBehaviour callbackBehaviour;
    [SerializeField] private string callbackEventName;

    [Header("Render references")] [SerializeField]
    private GameObject renderQuad;

    [SerializeField] private Camera renderCamera;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private Texture2D donorInput;
}