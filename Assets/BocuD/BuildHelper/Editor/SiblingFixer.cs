using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BocuD.BuildHelper.Editor
{
    public static class SiblingFixer
    {
        //todo run this on vrc build callback
        [MenuItem("Tools/Build Helper/Fix sibling transforms")]
        public static void FixSiblings()
        {
            bool doit = EditorUtility.DisplayDialog("VRBuildHelper",
                "This will scan your scene for any Transforms sharing the same path and rename them to fix it.", "Continue", "Cancel");

            if (!doit) return;
        
            float progress = 0;
            EditorUtility.DisplayProgressBar("Memes", "Doing the shit", progress);

            GameObject[] GameObjectList = Object.FindObjectsOfType<GameObject>();
            int goCount = GameObjectList.Length;
            int fixedCount = 0;
        
            Scene currentScene = SceneManager.GetActiveScene();
            GameObject[] rootGameObjects = currentScene.GetRootGameObjects();

            try
            {
                for (int index = 0; index < goCount; index++)
                {
                    GameObject go = GameObjectList[index];
                
                    progress = (float)index / goCount;
                    EditorUtility.DisplayProgressBar("VRBuildHelper", "Fixing sibling transform names..", progress);

                    //handle scene root objects differently
                    if (go.transform.parent == null)
                    {
                        List<GameObject> gos = rootGameObjects.Where(sibling => sibling != go && sibling.name == go.name).ToList();
                        for (int i = 0; i < gos.Count; i++)
                            gos[i].name = $"{gos[i].name}-{i:00}";

                        fixedCount += gos.Count;
                        continue;
                    }
                
                    for (int idx = 0; idx < go.transform.parent.childCount; ++idx)
                    {
                        Transform transform = go.transform.parent.GetChild(idx);
                        
                        //skip self
                        if (transform == go.transform)
                            continue;

                        List<GameObject> gos = new List<GameObject>();
                        for (int c = 0; c < go.transform.parent.childCount; ++c)
                        {
                            if (go.transform.parent.GetChild(c) == go.transform) continue;
                            if (go.transform.parent.GetChild(c).name == go.name)
                                gos.Add(go.transform.parent.GetChild(c).gameObject);
                        }

                        for (int i = 0; i < gos.Count; ++i)
                            gos[i].name = $"{gos[i].name}-{i:00}";

                        fixedCount += gos.Count;
                    }
                }

                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog("VRBuildHelper", $"Scanned {goCount} Transforms, fixed {fixedCount} names",
                    "Ok");
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
