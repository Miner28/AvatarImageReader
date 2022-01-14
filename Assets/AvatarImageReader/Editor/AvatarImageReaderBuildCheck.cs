using System.Collections;
using System.Collections.Generic;
using AvatarImageReader;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

public class AvatarImageReaderBuildCheck : IVRCSDKBuildRequestedCallback
{
    public int callbackOrder => 10;

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Avatar) return true;

        GameObject[] rootGameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        List<AvatarImagePrefab> prefabs = new List<AvatarImagePrefab>();

        foreach (GameObject obj in rootGameObjects)
        {
            prefabs.AddRange(obj.GetUdonSharpComponentsInChildren<AvatarImagePrefab>());
        }

        int failCount = 0;
        
        foreach (AvatarImagePrefab prefab in prefabs)
        {
            prefab.UpdateProxy();
            if (prefab.linkedAvatar.IsNullOrWhitespace())
            {
                Debug.LogWarning($"The AvatarImageReader system '{prefab.gameObject.name}' doesn't have a linked avatar set.", prefab.gameObject);
                failCount++;
            }
        }

        if (failCount > 0)
        {
            if (EditorUtility.DisplayDialog("AvatarImageReader",
                    $"The current scene contains {failCount} AvatarImageReader systems without a linked avatar set. Without setting a linked avatar, AvatarImageReader will not work. Are you sure you want to continue the current build?",
                    "Yes", "No"))
            {
                return true;
            }

            return false;
        }

        return true;
    }
}
