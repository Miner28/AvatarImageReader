using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace BocuD.BuildHelper
{
    [CustomEditor(typeof(BuildHelperUdon))]
    public class BuildHelperUdonEditor : UnityEditor.Editor
    {
        #region Convertable type lists
        private static readonly Type[] convertableTypes = {
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(short),
            typeof(ushort),
            typeof(DateTime),
            typeof(string),
        };
        
        private static readonly Type[] convertableTypesString = {
            typeof(string),
        };
        
        private static readonly Type[] convertableTypesInt = {
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(short),
            typeof(ushort),
        };
        
        private static readonly Type[] convertableTypesDateTime = {
            typeof(DateTime),
        };
        
        private static readonly Type[] convertableTypesBool = {
            typeof(bool),
        };

        private static readonly Dictionary<Type, string> FancyTypeToLabel = new Dictionary<Type, string>()
        {
            {typeof(int), "<color=#9999FF>int</color>"},
            {typeof(uint), "<color=#9999FF>uint</color>"},
            {typeof(long), "<color=#9999FF>long</color>"},
            {typeof(ulong), "<color=#9999FF>ulong</color>"},
            {typeof(short), "<color=#9999FF>short</color>"},
            {typeof(ushort), "<color=#9999FF>ushort</color>"},
            {typeof(DateTime), "<color=#AAFFAA>DateTime</color>"},
            {typeof(string), "<color=#9999FF>string</color>"},
            {typeof(bool), "<color=#9999FF>bool</color>"},
        };
        
        private static readonly Dictionary<Type, string> TypeToLabel = new Dictionary<Type, string>()
        {
            {typeof(int), "int"},
            {typeof(uint), "uint"},
            {typeof(long), "long"},
            {typeof(ulong), "ulong"},
            {typeof(short), "short"},
            {typeof(ushort), "ushort"},
            {typeof(DateTime), "DateTime"},
            {typeof(string), "string"},
            {typeof(bool), "bool"},
        };
        
        #endregion
        
        private static readonly Dictionary<VariableInstruction.Source, Type[]> validTypesDictionary = new Dictionary<VariableInstruction.Source, Type[]>
        {
            {VariableInstruction.Source.none, convertableTypes},
            {VariableInstruction.Source.branchName, convertableTypesString},
            {VariableInstruction.Source.buildDate, convertableTypesDateTime},
            {VariableInstruction.Source.buildNumber, convertableTypesInt},
        };

        private BuildHelperUdon inspectorBehaviour;

        private class VariableInstruction
        {
            public UdonBehaviour targetBehaviour;
            public int variableIndex;
            
            public string[] variableNames;
            public string[] FancyVariableNames;
            public Type[] variableTypes;

            public enum Source
            {
                none,
                branchName,
                buildNumber,
                buildDate,
            }

            public Source source;

            public VariableInstruction()
            {
                targetBehaviour = null;
                variableIndex = -1;
                variableNames = new string[0];
                FancyVariableNames = new string[0];
                variableTypes = Type.EmptyTypes;
            }
            
            public void PrepareLabels()
            {
                string[] newLabels = new string[Math.Min(variableNames.Length, variableTypes.Length)];
                string[] fancyLabels = new string[Math.Min(variableNames.Length, variableTypes.Length)];
                for (int i = 0; i < fancyLabels.Length; i++)
                {
                    if (FancyTypeToLabel.TryGetValue(variableTypes[i], out string label))
                        fancyLabels[i] = $"{label} {variableNames[i]}";
                    else fancyLabels[i] = $"{variableTypes[i]} {variableNames[i]}";
                    if (TypeToLabel.TryGetValue(variableTypes[i], out string label2))
                        newLabels[i] = $"{label2} {variableNames[i]}";
                    else newLabels[i] = $"{variableTypes[i]} {variableNames[i]}";
                }
                VariableLabels = newLabels;
                FancyVariableNames = fancyLabels;
            }

            public string[] VariableLabels = new string[0];
        }

        private VariableInstruction[] variableInstructions;

        public override void OnInspectorGUI()
        {
            // Draws the default convert to UdonBehaviour button, program asset field, sync settings, etc.
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target) || target == null) return;

            if (target != null)
            {
                //yes this is bad and performance shit but muh undo
                inspectorBehaviour = (BuildHelperUdon) target;
                variableInstructions = ImportFromUdonBehaviour();
            }
            else return;

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) {richText = true, fontSize = 15};
            EditorGUILayout.LabelField("<b>VR Build Helper Udon Link</b>", headerStyle);
            
            DrawProgramVariableList();
            
            EditorGUILayout.Space(10);

            DrawUpdateCheckerEditor();

            EditorGUILayout.Space(10);
            
            DrawTMPEditor();
        }

        private UdonBehaviour eventBehaviour;
        private string onVersionMatch;
        private string onVersionMismatch;
        private string onVersionTimeout;
        private bool sendNetworkedEvent;
        private string onVersionMismatchRemote;
        private bool allowUpdates;
        private bool sendToAll;
        private bool singleCallback;
        
        private void DrawUpdateCheckerEditor()
        {
            eventBehaviour = inspectorBehaviour.eventBehaviour;
            onVersionMatch = inspectorBehaviour.onVersionMatch;
            onVersionMismatch = inspectorBehaviour.onVersionMismatch;
            onVersionTimeout = inspectorBehaviour.onVersionTimeout;
            sendNetworkedEvent = inspectorBehaviour.sendNetworkedEvent;
            onVersionMismatchRemote = inspectorBehaviour.onVersionMismatchRemote;
            allowUpdates = inspectorBehaviour.allowUpdates;
            sendToAll = inspectorBehaviour.sendToAll;
            singleCallback = inspectorBehaviour.singleCallback;
            
            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUI.BeginChangeCheck();
            bool checkVersion = EditorGUILayout.Toggle("Detect World Updates", inspectorBehaviour.checkVersion);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Change BuildHelper Version Checking Settings");
                inspectorBehaviour.checkVersion = checkVersion;
            }
            if (checkVersion)
            {
                EditorGUILayout.HelpBox(
                    "Build Helper can check the world version of the instance master when joining an existing instance, and send out an event when a mismatch is detected. " +
                    "Mismatches can happen quite often when you update your world (as people that are already in an instance when updating will still be on the old version). " +
                    "You can use the mismatch and timeout events to alert the user (and master) that they should rejoin the world.", MessageType.Info);

                if (BuildHelperEditorPrefs.PlatformSwitchMode == 1)
                {
                    EditorGUILayout.HelpBox(
                        "Build Number increment mode is currently set to Always Increment. " +
                        "Make sure to use the autonomous publisher when updating your world if it supports more than one platform, " +
                        "as manually updating the world for one platform at a time will not properly update the world number causing version detection to break.",
                        MessageType.Warning);
                }

                if (inspectorBehaviour.transform.parent != null || inspectorBehaviour.transform.GetSiblingIndex() != 0)
                {
                    EditorGUILayout.HelpBox("To reduce the chances of version checking timing out when the hierarchy changes, you should move the GameObject containing this UdonBehaviour to the top of your scene root.", MessageType.Warning);
                    if (GUILayout.Button("Auto fix"))
                    {
                        inspectorBehaviour.transform.SetParent(null);
                        inspectorBehaviour.transform.SetSiblingIndex(0);
                    }
                }

                EditorGUI.BeginChangeCheck();
                eventBehaviour = (UdonBehaviour)EditorGUILayout.ObjectField(
                    new GUIContent("Event behaviour",
                        "Events will be called on this behaviour when a version error is detected"), eventBehaviour,
                    typeof(UdonBehaviour), true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspectorBehaviour, "Change BuildHelper Event Target");
                    inspectorBehaviour.eventBehaviour = eventBehaviour;
                }

                if (eventBehaviour != null)
                {
                    EditorGUI.indentLevel++;
                    
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.BeginHorizontal();
                    GUIContent[] callbackMode =
                    {
                        new GUIContent("Multiple callbacks", "Multiple callbacks will allow a callback to be sent more than once (for example, the remote version mismatch callback may be sent more than once, as it would be fired everytime a player on a newer version joins)"),
                        new GUIContent("Single callback", "Single callback will never fire a callback more than one time (This may be desired when using these callbacks to directly spawn notifications telling the user or master to rejoin)")
                    };

                    EditorGUILayout.LabelField(new GUIContent("Callback mode", "Hover over the possible callback options to see which one applies to your project."));
                    singleCallback = GUILayout.Toolbar(singleCallback ? 1 : 0, callbackMode) == 1;
                    EditorGUILayout.EndHorizontal();
                    
                    GUIContent versionMatch = new GUIContent("On version match", "Called when the world version (build number) on the local client matches that of the instance master");
                    GUIContent versionMismatch = new GUIContent("On version mismatch", "Called when the world version (build number) on the local client *doesn't* match that of the instance master");
                    GUIContent versionTimeout = new GUIContent("On version timeout", "Called when the world version (build number) of the instance master failed to be read (this can happen when the hierarchy changes significantly between versions)");

                    EditorGUILayout.BeginVertical("Helpbox");
                    onVersionMatch = EditorGUILayout.TextField(versionMatch, onVersionMatch);
                    onVersionMismatch = EditorGUILayout.TextField(versionMismatch, onVersionMismatch);
                    onVersionTimeout = EditorGUILayout.TextField(versionTimeout, onVersionTimeout);
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(6);

                    EditorGUILayout.BeginHorizontal();
                    sendNetworkedEvent = EditorGUILayout.Toggle(new GUIContent("Send networked event on mismatch", "When this is enabled, a networked event will be sent when any player on a newer build version joins the instance. " +
                        "You can use this to tell the master they should create a new instance."), sendNetworkedEvent);
                    
                    GUIContent[] eventTarget =
                    {
                        new GUIContent("To instance master"),
                        new GUIContent("To all players")
                    };

                    EditorGUI.BeginDisabledGroup(!sendNetworkedEvent);
                    sendToAll = GUILayout.Toolbar(sendToAll ? 1 : 0, eventTarget) == 1;
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();

                    if (sendNetworkedEvent)
                    {
                        EditorGUI.indentLevel++;
                        onVersionMismatchRemote = EditorGUILayout.TextField("On version mismatch (remote)", onVersionMismatchRemote);
                        EditorGUI.indentLevel--;
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(inspectorBehaviour, "Change BuildHelper Event Name");
                        inspectorBehaviour.onVersionMatch = onVersionMatch;
                        inspectorBehaviour.onVersionMismatch = onVersionMismatch;
                        inspectorBehaviour.onVersionTimeout = onVersionTimeout;
                        inspectorBehaviour.sendNetworkedEvent = sendNetworkedEvent;
                        inspectorBehaviour.onVersionMismatchRemote = onVersionMismatchRemote;
                        inspectorBehaviour.sendToAll = sendToAll;
                        inspectorBehaviour.singleCallback = singleCallback;
                    }

                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space(6);

                EditorGUI.BeginChangeCheck();
                allowUpdates = EditorGUILayout.Toggle(new GUIContent("Allow Updates", "When this is enabled, the moment the old master leaves, the world version number will be updated."), allowUpdates);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspectorBehaviour, "Change BuildHelper Event Target");
                    inspectorBehaviour.allowUpdates = allowUpdates;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawProgramVariableList()
        {
            EditorGUILayout.BeginVertical("Helpbox");
            EditorGUI.BeginChangeCheck();
            bool setProgramVariable =
                EditorGUILayout.Toggle("SetProgramVariable", inspectorBehaviour.setProgramVariable);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Change BuildHelper settings");
                inspectorBehaviour.setProgramVariable = setProgramVariable;
            }

            if (setProgramVariable)
            {
                //variableInstructions = ImportFromUdonBehaviour();
                EditorGUILayout.HelpBox("You can assign UdonBehaviours here to have the appropriate variables set on Start()", MessageType.Info);
                EditorGUILayout.BeginVertical("Helpbox");

                //begin header
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target UdonBehaviour", GUILayout.Width(160));
                EditorGUILayout.LabelField("Variable to write", GUILayout.Width(100));
                EditorGUILayout.LabelField("Target location", GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    Undo.RecordObject(inspectorBehaviour, "Add new behaviour to list");
                    ArrayUtility.Add(ref variableInstructions, new VariableInstruction());
                    ExportToUdonBehaviour(variableInstructions);
                }
                EditorGUILayout.EndHorizontal();
                //end header

                for (int index = 0; index < variableInstructions.Length; index++)
                {
                    EditorGUILayout.BeginHorizontal();
                    
                    EditorGUI.BeginChangeCheck();
                    
                    VariableInstruction instruction = variableInstructions[index];
                    instruction.targetBehaviour = (UdonBehaviour)EditorGUILayout.ObjectField(
                        instruction.targetBehaviour, typeof(UdonBehaviour),
                        true, GUILayout.Width(160));
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(inspectorBehaviour, "Modify behaviour");
                        ExportToUdonBehaviour(variableInstructions);
                    }

                    if (variableInstructions[index].targetBehaviour != null)
                    {
                        EditorGUI.BeginChangeCheck();
                        variableInstructions[index].source =
                            (VariableInstruction.Source)EditorGUILayout.EnumPopup(variableInstructions[index].source,
                                GUILayout.Width(100));

                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(inspectorBehaviour, "Modify source variable");
                            RegenerateValidVariables(index);
                            ExportToUdonBehaviour(variableInstructions);
                        }

                        if (variableInstructions[index].source != VariableInstruction.Source.none)
                        {
                            EditorGUI.BeginChangeCheck();
                            GUIStyle popupStyle = new GUIStyle(EditorStyles.popup)
                            {
                                richText = true,
                                normal = { textColor = new Color(1, 1, 1, 0) },
                                hover = { textColor = new Color(1, 1, 1, 0) },
                                focused = { textColor = new Color(1, 1, 1, 0) }
                            };

                            variableInstructions[index].variableIndex = EditorGUILayout.Popup(
                                variableInstructions[index].variableIndex, variableInstructions[index].VariableLabels,
                                popupStyle);

                            Rect labelRect = GUILayoutUtility.GetLastRect();
                            labelRect.x += 2;
                            GUIStyle labelStyle = new GUIStyle(GUI.skin.label) { richText = true };
                            if (variableInstructions[index].variableIndex != -1)
                            {
                                GUI.Label(labelRect, variableInstructions[index]
                                    .FancyVariableNames[variableInstructions[index].variableIndex], labelStyle);
                            }

                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(inspectorBehaviour, "Modify target variable");
                                ExportToUdonBehaviour(variableInstructions);
                            }
                        }
                    }

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        Undo.RecordObject(inspectorBehaviour, "Remove behaviour from behaviour list");
                        ArrayUtility.RemoveAt(ref variableInstructions, index);
                        ExportToUdonBehaviour(variableInstructions);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private TMP_Text tmp;
        private void DrawTMPEditor()
        {
            EditorGUILayout.BeginVertical("Helpbox");
            
            EditorGUI.BeginChangeCheck();
            bool useTMP = EditorGUILayout.Toggle("Print to TextMeshPro", inspectorBehaviour.useTMP);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(inspectorBehaviour, "Change BuildHelper TMP settings");
                inspectorBehaviour.useTMP = useTMP;
            }
            
            if (useTMP)
            {
                tmp = inspectorBehaviour.tmp;
                
                EditorGUI.BeginChangeCheck();

                tmp = (TMP_Text)EditorGUILayout.ObjectField("Target TMP_Text", tmp, typeof(TMP_Text), true);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(inspectorBehaviour, "Change target TMP");

                    if (tmp.GetType() == typeof(TextMeshPro))
                    {
                        inspectorBehaviour.tmp = (TextMeshPro)tmp;
                        inspectorBehaviour.tmpUGui = null;
                        inspectorBehaviour.isUgui = false;
                    }
                    else if (tmp.GetType() == typeof(TextMeshProUGUI))
                    {
                        inspectorBehaviour.tmp = null;
                        inspectorBehaviour.tmpUGui = (TextMeshProUGUI)tmp;
                        inspectorBehaviour.isUgui = true;
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void RegenerateValidVariables(int index)
        {
            GetValidVariables(variableInstructions[index].targetBehaviour, variableInstructions[index].source,
                out List<string> vars, out List<Type> types);

            string[] newValidNames = vars.ToArray();
            Type[] newValidTypes = types.ToArray();

            int newVariableIndex = variableInstructions[index].variableIndex < 0
                ? -1
                : Array.IndexOf(newValidNames,
                    variableInstructions[index].variableNames[variableInstructions[index].variableIndex]);

            if (newVariableIndex < 0)
                variableInstructions[index].variableIndex = -1;
            else
                variableInstructions[index].variableIndex =
                    newValidTypes[newVariableIndex] == variableInstructions[index]
                        .variableTypes[variableInstructions[index].variableIndex]
                        ? newVariableIndex
                        : -1;

            variableInstructions[index].variableNames = newValidNames;
            variableInstructions[index].variableTypes = newValidTypes;
            variableInstructions[index].PrepareLabels();

            ExportToUdonBehaviour(variableInstructions);
        }

        private VariableInstruction[] ImportFromUdonBehaviour()
        {
            inspectorBehaviour.UpdateProxy();

            VariableInstruction[] output = new VariableInstruction[0];
            
            for (int index = 0; index < inspectorBehaviour.targetBehaviours.Length; index++)
            {
                UdonBehaviour behaviour = (UdonBehaviour)inspectorBehaviour.targetBehaviours[index];

                GetValidVariables(behaviour, (VariableInstruction.Source) inspectorBehaviour.sourceEnum[index],
                    out List<string> vars, out List<Type> types);

                string[] newValidNames = vars.ToArray();
                Type[] newValidTypes = types.ToArray();
                int variableIndex = -1;

                if (inspectorBehaviour.targetVariableNames.Length > index)
                    variableIndex = Array.IndexOf(newValidNames, inspectorBehaviour.targetVariableNames[index]);

                VariableInstruction newInstruction = new VariableInstruction()
                {
                    targetBehaviour = behaviour,
                    variableNames = newValidNames,
                    variableTypes = newValidTypes,
                    variableIndex = variableIndex,
                    source = (VariableInstruction.Source)inspectorBehaviour.sourceEnum[index]
                };
                newInstruction.PrepareLabels();
                
                ArrayUtility.Add(ref output, newInstruction);
            }
            
            return output;
        }

        private void ExportToUdonBehaviour(VariableInstruction[] toExport)
        {
            inspectorBehaviour.UpdateProxy();
            inspectorBehaviour.targetBehaviours = new Component[toExport.Length];
            inspectorBehaviour.targetTypes = new string[toExport.Length];
            inspectorBehaviour.targetVariableNames = new string[toExport.Length];
            inspectorBehaviour.sourceEnum = new int[toExport.Length];

            for (int index = 0; index < toExport.Length; index++)
            {
                VariableInstruction instruction = toExport[index];
                if (instruction.targetBehaviour == null) continue;
                
                inspectorBehaviour.targetBehaviours[index] = instruction.targetBehaviour;
                inspectorBehaviour.sourceEnum[index] = (int)instruction.source;
                if (instruction.variableIndex == -1) continue;
                
                inspectorBehaviour.targetTypes[index] = instruction.variableTypes[instruction.variableIndex].ToString();
                inspectorBehaviour.targetVariableNames[index] = instruction.variableNames[instruction.variableIndex];
            }

            inspectorBehaviour.ApplyProxyModifications();
        }

        private static void GetValidVariables(UdonBehaviour udon, VariableInstruction.Source source, out List<string> vars, out List<Type> types)
        {
            vars = new List<string>();
            types = new List<Type>();
            if (udon == null) return;

            VRC.Udon.Common.Interfaces.IUdonSymbolTable symbolTable =
                udon.programSource.SerializedProgramAsset.RetrieveProgram().SymbolTable;

            List<string> programVariablesNames = symbolTable.GetSymbols().ToList();
            List<KeyValuePair<string, Type>> toSort = new List<KeyValuePair<string, Type>>();

            foreach (string variableName in programVariablesNames)
            {
                if (variableName.StartsWith("__")) continue;

                Type variableType = symbolTable.GetSymbolType(variableName);

                validTypesDictionary.TryGetValue(source, out Type[] validTypesArray);
                if (validTypesArray == null) continue;
                
                int typeIndex = Array.IndexOf(validTypesArray, variableType);
                if (typeIndex > -1)
                {
                    toSort.Add(new KeyValuePair<string, Type>(variableName, variableType));
                }
            }

            List<KeyValuePair<string, Type>> sorted = toSort.OrderBy(kvp => kvp.Key).ToList();

            foreach (KeyValuePair<string, Type> item in sorted)
            {
                vars.Add(item.Key);
                types.Add(item.Value);
            }
        }
    }
}