
using UnityEngine;
using System;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace Nessie.Utilities.Package
{
    public class PackageBuilder : EditorWindow
    {
        private SerializedObject thisSO;

        public PackageData PackageData;
        private SerializedObject dataSO;

        [SerializeField] private List<UnityEngine.Object> assetList;
        private ReorderableList assetRList;

        [MenuItem("Nessie/Package Builder")]
        static void Init()
        {
            // Get existing open window or if none, make a new one:
            PackageBuilder window = (PackageBuilder)GetWindow(typeof(PackageBuilder));
            window.titleContent.text = "Nessies Package Builder";
            window.Show();
        }

        private void OnEnable()
        {
            thisSO = new SerializedObject(this);

            assetRList = new ReorderableList(thisSO, thisSO.FindProperty(nameof(assetList)), true, false, true, true);
            assetRList.drawHeaderCallback = (Rect rect) => { EditorGUI.LabelField(rect, new GUIContent("Asset References", "Assets packed into the Unity Package.")); };
            assetRList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                Rect elementRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);

                SerializedProperty elementSP = assetRList.serializedProperty.GetArrayElementAtIndex(index);

                UnityEngine.Object asset = EditorGUI.ObjectField(elementRect, elementSP.objectReferenceValue, typeof(UnityEngine.Object), false);

                elementSP.objectReferenceValue = asset;

                //EditorGUI.PropertyField(testFieldRect, assetRList.serializedProperty.GetArrayElementAtIndex(index), label: new GUIContent());
            };
        }

        private void OnGUI()
        {
            thisSO.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(thisSO.FindProperty(nameof(PackageData)));

            if (EditorGUI.EndChangeCheck())
            {
                thisSO.ApplyModifiedProperties();

                if (PackageData != null)
                {
                    dataSO = new SerializedObject(PackageData);

                    assetList = FindAssets(PackageData);
                }
            }

            if (PackageData != null)
            {
                if (dataSO == null) dataSO = new SerializedObject(PackageData);

                dataSO.Update();

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(dataSO.FindProperty(nameof(PackageData.PackageName)));
                EditorGUILayout.PropertyField(dataSO.FindProperty(nameof(PackageData.PackagePath)));
                EditorGUILayout.PropertyField(dataSO.FindProperty(nameof(PackageData.Flags)));
                //EditorGUILayout.PropertyField(dataSO.FindProperty(nameof(Data.AssetPaths)));

                if (EditorGUI.EndChangeCheck())
                {
                    dataSO.ApplyModifiedProperties();
                }

                EditorGUI.BeginChangeCheck();
                assetRList.DoLayoutList();
                if (EditorGUI.EndChangeCheck())
                {
                    thisSO.ApplyModifiedProperties();

                    SerializedProperty assetPaths = dataSO.FindProperty(nameof(PackageData.AssetPaths));
                    assetPaths.arraySize = assetList.Count;
                    for (int i = 0; i < assetList.Count; i++)
                        assetPaths.GetArrayElementAtIndex(i).stringValue = AssetDatabase.GetAssetPath(assetList[i]);

                    dataSO.ApplyModifiedProperties();
                }

                if (GUILayout.Button("Build Package"))
                {
                    BuildPackage(PackageData);
                }
            }
        }

        private static List<UnityEngine.Object> FindAssets(PackageData Data)
        {
            List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            foreach (string path in Data.AssetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset != null) assets.Add(asset);
            }
            return assets;
        }

        private static void BuildPackage(PackageData data)
        {
            string packagePath = data.PackagePath.EndsWith("/") ? data.PackagePath : $"{data.PackagePath}/"; // Add final forward slash if missing.
            string packageName = data.PackageName.EndsWith(".unitypackage") ? data.PackageName : $"{data.PackageName}.unitypackage"; // Add UnityPackage suffix if missing.
            string finalPath = $"{packagePath}{packageName}";

            ReadyPath(finalPath);

            AssetDatabase.ExportPackage(data.AssetPaths, finalPath, data.Flags);
            AssetDatabase.ImportAsset(data.PackagePath);

            AssetDatabase.Refresh();
        }

        private static void ReadyPath(string folderPath)
        {
            if (!System.IO.Directory.Exists(folderPath))
                System.IO.Directory.CreateDirectory(folderPath);
        }
    }
}