using UnityEditor;
using UnityEngine;

namespace AvatarImageReader.Editor
{
    public static class PrefabInstantiationUtility
    {
        private const string prefabNormal = "Packages/com.miner28.avatar-image-reader/Prefabs/Decoder.prefab";
        private const string prefabText = "Packages/com.miner28.avatar-image-reader/Prefabs/DecoderWithText.prefab";
        private const string prefabDebug = "Packages/com.miner28.avatar-image-reader/Prefabs/Decoder_Debug.prefab";

        [MenuItem("Tools/AvatarImageReader/Create Image Reader")]
        private static void CreateNormal()
        {
            GameObject toInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabNormal);
            GameObject instantiated = Object.Instantiate(toInstantiate);
            instantiated.name = "New avatar image reader";

            EditorUtility.SetDirty(instantiated);
        }

        [MenuItem("Tools/AvatarImageReader/Create Image Reader (With TMP)")]
        private static void CreateText()
        {
            GameObject toInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabText);
            GameObject instantiated = Object.Instantiate(toInstantiate);
            instantiated.name = "New avatar image reader (TMP)";

            EditorUtility.SetDirty(instantiated);
        }

        [MenuItem("Tools/AvatarImageReader/Create Image Reader (Debug)")]
        private static void CreateDebug()
        {
            GameObject toInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(prefabDebug);
            GameObject instantiated = Object.Instantiate(toInstantiate);
            instantiated.name = "New avatar image reader (debug)";

            EditorUtility.SetDirty(instantiated);
        }
    }
}
