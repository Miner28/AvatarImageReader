using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon.Editor.ProgramSources.Attributes;

namespace VRC.Udon.Editor
{
    [CustomEditor(typeof(UdonBehaviour))]
    public class UdonBehaviourEditor : UnityEditor.Editor
    {
        private const string VRC_UDON_NEW_PROGRAM_TYPE_PREF_KEY = "VRC.Udon.NewProgramType";

        private SerializedProperty _programSourceProperty;
        private SerializedProperty _serializedProgramAssetProperty;
        private int _newProgramType = 1;

        private void OnEnable()
        {
            _programSourceProperty = serializedObject.FindProperty("programSource");
            _serializedProgramAssetProperty = serializedObject.FindProperty("serializedProgramAsset");
            _newProgramType = EditorPrefs.GetInt(VRC_UDON_NEW_PROGRAM_TYPE_PREF_KEY, 1);

            UdonEditorManager.Instance.WantRepaint += Repaint;
        }

        private void OnDisable()
        {
            UdonEditorManager.Instance.WantRepaint -= Repaint;
        }

        enum SyncTypes
        {
            Continuous,
            Manual
        }

        public override void OnInspectorGUI()
        {
            UdonBehaviour udonTarget = (UdonBehaviour)target;

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                bool dirty = false;

                EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox));
                {
                    SyncTypes selected = (SyncTypes)EditorGUILayout.EnumPopup("Sync Method", udonTarget.Reliable ? SyncTypes.Manual : SyncTypes.Continuous);
                    
                    bool shouldBeReliable = selected == SyncTypes.Manual;
                    if (udonTarget.Reliable != shouldBeReliable)
                    {
                        udonTarget.Reliable = shouldBeReliable;
                        dirty = true;
                    }

                    if (udonTarget.Reliable)
                        EditorGUILayout.LabelField("Manual replication is intended for infrequently-updated variables of small or large size, and will not be tweened.", EditorStyles.wordWrappedMiniLabel);
                    else
                        EditorGUILayout.LabelField("Continuous replication is intended for frequently-updated variable of small size, and will be tweened.", EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Udon");

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                _programSourceProperty.objectReferenceValue = EditorGUILayout.ObjectField(
                    "Program Source",
                    _programSourceProperty.objectReferenceValue,
                    typeof(AbstractUdonProgramSource),
                    false
                );

                if (EditorGUI.EndChangeCheck())
                {
                    dirty = true;
                    serializedObject.ApplyModifiedProperties();
                }

                if (_programSourceProperty.objectReferenceValue == null)
                {
                    if (_serializedProgramAssetProperty.objectReferenceValue != null)
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = null;
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    }

                    List<(string displayName, Type newProgramType)> programSourceTypesForNewMenu = GetProgramSourceTypesForNewMenu();
                    if (GUILayout.Button("New Program"))
                    {
                        (string displayName, Type newProgramType) = programSourceTypesForNewMenu.ElementAt(_newProgramType);

                        string udonBehaviourName = udonTarget.name;
                        Scene scene = udonTarget.gameObject.scene;
                        if (string.IsNullOrEmpty(scene.path))
                        {
                            Debug.LogError("You need to save the scene before you can create new Udon program assets!");
                        }
                        else
                        {
                            AbstractUdonProgramSource newProgramSource = CreateUdonProgramSourceAsset(newProgramType, displayName, scene, udonBehaviourName);
                            _programSourceProperty.objectReferenceValue = newProgramSource;
                            _serializedProgramAssetProperty.objectReferenceValue = newProgramSource.SerializedProgramAsset;
                            serializedObject.ApplyModifiedProperties();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    EditorGUI.BeginChangeCheck();
                    _newProgramType = EditorGUILayout.Popup(
                        "",
                        Mathf.Clamp(_newProgramType, 0, programSourceTypesForNewMenu.Count),
                        programSourceTypesForNewMenu.Select(t => t.displayName).ToArray(),
                        GUILayout.ExpandWidth(false)
                    );

                    if (EditorGUI.EndChangeCheck())
                    {
                        EditorPrefs.SetInt(VRC_UDON_NEW_PROGRAM_TYPE_PREF_KEY, _newProgramType);
                    }
                }
                else
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.ObjectField(
                            "Serialized Udon Program Asset ID: ",
                            _serializedProgramAssetProperty.objectReferenceValue,
                            typeof(AbstractSerializedUdonProgramAsset),
                            false
                        );

                        EditorGUI.indentLevel--;
                    }

                    AbstractUdonProgramSource programSource = (AbstractUdonProgramSource)_programSourceProperty.objectReferenceValue;
                    AbstractSerializedUdonProgramAsset serializedUdonProgramAsset = programSource.SerializedProgramAsset;
                    if (_serializedProgramAssetProperty.objectReferenceValue != serializedUdonProgramAsset)
                    {
                        _serializedProgramAssetProperty.objectReferenceValue = serializedUdonProgramAsset;
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                EditorGUILayout.EndHorizontal();

                udonTarget.RunEditorUpdate(ref dirty);
                if (dirty && !Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(udonTarget.gameObject.scene);
                }
            }
        }

        private static AbstractUdonProgramSource CreateUdonProgramSourceAsset(Type newProgramType, string displayName, Scene scene, string udonBehaviourName)
        {
            string scenePath = Path.GetDirectoryName(scene.path) ?? "Assets";

            string folderName = $"{scene.name}_UdonProgramSources";
            string folderPath = Path.Combine(scenePath, folderName);

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder(scenePath, folderName);
            }

            string assetPath = Path.Combine(folderPath, $"{udonBehaviourName} {displayName}.asset");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AbstractUdonProgramSource asset = (AbstractUdonProgramSource)CreateInstance(newProgramType);
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        private static List<(string displayName, Type newProgramType)> GetProgramSourceTypesForNewMenu()
        {
            Type abstractProgramSourceType = typeof(AbstractUdonProgramSource);
            Type attributeNewMenuAttributeType = typeof(UdonProgramSourceNewMenuAttribute);

            List<(string displayName, Type newProgramType)> programSourceTypesForNewMenu = new List<(string displayName, Type newProgramType)>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                UdonProgramSourceNewMenuAttribute[] udonProgramSourceNewMenuAttributes;
                try
                {
                    udonProgramSourceNewMenuAttributes = (UdonProgramSourceNewMenuAttribute[])assembly.GetCustomAttributes(attributeNewMenuAttributeType, false);
                }
                catch
                {
                    udonProgramSourceNewMenuAttributes = new UdonProgramSourceNewMenuAttribute[0];
                }

                foreach (UdonProgramSourceNewMenuAttribute udonProgramSourceNewMenuAttribute in udonProgramSourceNewMenuAttributes)
                {
                    if (udonProgramSourceNewMenuAttribute == null)
                    {
                        continue;
                    }

                    if (!abstractProgramSourceType.IsAssignableFrom(udonProgramSourceNewMenuAttribute.Type))
                    {
                        continue;
                    }

                    programSourceTypesForNewMenu.Add((udonProgramSourceNewMenuAttribute.DisplayName, udonProgramSourceNewMenuAttribute.Type));
                }
            }

            programSourceTypesForNewMenu.Sort(
                (left, right) => string.Compare(
                    left.displayName,
                    right.displayName,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            return programSourceTypesForNewMenu;
        }
    }
}
