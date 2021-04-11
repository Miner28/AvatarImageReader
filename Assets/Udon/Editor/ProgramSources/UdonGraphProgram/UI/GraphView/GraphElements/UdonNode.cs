using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;
using EditorUI = UnityEditor.Experimental.UIElements;
using EngineUI = UnityEngine.Experimental.UIElements;

using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;
using UnityEditor;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonNode : Node, IEdgeConnectorListener
    {
        // name is inherited from parent VisualElement class
        public Type type;
        public GameObject gameObject;
        private UdonGraph _graphView;
        private EditorUI.PopupField<string> _popup;
        public UdonNodeDefinition definition;
        public UdonNodeData data;
        public Dictionary<int, UdonPort> portsIn;
        public Dictionary<int, UdonPort> portsOut;
        public List<UdonPort> portsFlowIn;
        public List<UdonPort> portsFlowOut;
        private INodeRegistry _registry;
        public UdonGroup group;

        // Overload handling
        private IList<UdonNodeDefinition> overloadDefinitions;
        private readonly Dictionary<UdonNodeDefinition, string> _optionNameCache = new Dictionary<UdonNodeDefinition, string>();
        private readonly Dictionary<UdonNodeDefinition, string> _cleanerOptionNameCache = new Dictionary<UdonNodeDefinition, string>();

        public UdonGraph Graph { get => _graphView; private set { } }
        public INodeRegistry Registry { get => _registry; private set { } }

        private readonly string[] _specialFlows =
        {
            "Block",
            "Branch",
            "For",
            "Foreach",
            "While",
            "Is_Valid",
        };

        // Upgrade note - persistenceKey turns into viewDataKey in Unity 2019, this getter will make that transition easier
        public string uid { get => persistenceKey; set => persistenceKey = value; }

        // Called when creating from Asset
        public static UdonNode CreateNode(UdonNodeData nodeData, UdonGraph view)
        {
            UdonNodeDefinition definition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
            if (definition == null)
            {
                Debug.LogError($"Cannot create node {nodeData.fullName} because there is no matching Node Definition");
                return null;
            }
            return CreateNode(definition, view, nodeData);
        }

        // Called when creating from scratch
        public static UdonNode CreateNode(UdonNodeDefinition definition, UdonGraph view, UdonNodeData nodeData = null)
        {
           return new UdonNode(definition, view, nodeData);
        }

        // Constructor is private to force all paths through Static factory method
        private UdonNode(UdonNodeDefinition nodeDefinition, UdonGraph view, UdonNodeData nodeData = null)
        {
            _graphView = view;
            definition = nodeDefinition;

            var registry = UdonGraphExtensions.GetRegistryForDefinition(nodeDefinition);
            if(registry != null)
            {
                this._registry = registry;
            }
            else
            {
                Debug.LogWarning($"Couldn't find registry for {nodeDefinition.fullName}");
            }

            VisualContainer titleContainer = new VisualContainer()
            {
                name = "title-container",
            };
            this.Q("title").Insert(0, titleContainer);

            titleContainer.Add(this.Q("title-label"));
            
            var subtitle = new EngineUI.Label("")
            {
                name = "subtitle",
            };
            bool skipSubtitle = (
                _specialFlows.Contains(nodeDefinition.fullName)
                || nodeDefinition.fullName.EndsWith("et_Variable")
                || nodeDefinition.fullName.StartsWithCached("Const_")
            );
            
            if (!skipSubtitle)
            {
                titleContainer.Insert(0, subtitle);
            }

            // Set Title
            var displayTitle = UdonGraphExtensions.PrettyString(nodeDefinition.name).FriendlyNameify();
            if (displayTitle == "Const_VRCUdonCommonInterfacesIUdonEventReceiver")
            {
                displayTitle = "UdonBehaviour";
            }
            else if(displayTitle == "==" || displayTitle == "!=" || displayTitle == "+")
            {
                displayTitle = $"{nodeDefinition.type.Name} {displayTitle}";
            }

            if (displayTitle.StartsWith("Op ")) 
                displayTitle = displayTitle.Replace("Op ", "");
            
            title = displayTitle;

            name = nodeDefinition.fullName;
            elementTypeColor = UnityEngine.Random.ColorHSV(0.5f, 0.6f, 0.1f, 0.2f, 0.8f, 0.9f);

            string className = nodeDefinition.name.Split(' ').FirstOrDefault().Split('_').FirstOrDefault();
            AddToClassList(className);
            // Null is a type here, so handle it special
            if(nodeDefinition.type == null)
            {
                AddToClassList("null");
            }
            else
            {
                AddToClassList(nodeDefinition.type.Namespace);
                AddToClassList(nodeDefinition.type.Name);
            }
            AddToClassList(displayTitle.Replace(" ", "").ToLowerFirstChar());
            if (nodeDefinition.fullName.Contains('_'))
            {
                AddToClassList(nodeDefinition.fullName.Substring(0, nodeDefinition.fullName.IndexOf('_')));
            }

            if (!skipSubtitle)
            {
                if (nodeDefinition.fullName.StartsWith("Event_"))
                {
                    subtitle.text = "Event";
                }
                else
                {
                    subtitle.text = className;
                    // temp title shenanigans
                    int firstSplit = nodeDefinition.fullName.IndexOf("__")+2;
                    if (firstSplit > 1)
                    {
                        int lastSplit = nodeDefinition.fullName.IndexOf("__", firstSplit);
                        int stringLength = (lastSplit > -1) ? lastSplit - firstSplit : nodeDefinition.fullName.Length - firstSplit;
                        string line2 = nodeDefinition.fullName.Substring(firstSplit, stringLength).Replace("_", " ").UppercaseFirst();
                        if (line2.StartsWith("Op "))
                        {
                            line2 = line2.Replace("Op ", "");
                            subtitle.text = nodeDefinition.type.Name;
                        }
                        title = line2;
                    }
                    else
                    {
                        //TODO: handle class names not found
                        //Debug.Log($"Couldn't find classname for {nodeDefinition.fullName}");
                    }
                }   
            }

            // Create or validate nodeData
            if (nodeData == null)
            {
                data = _graphView.graphData.AddNode(nodeDefinition.fullName);
                PopulateDefaultValues();
                ValidateNodeData();
            } 
            else
            {
                data = nodeData;
                ValidateNodeData();
                SetPosition(new Rect(data.position.x, data.position.y, 0, 0));
            }

            uid = data.uid;

            // Fill in all fields, etc and add to the graph view
            if (UdonGraphExtensions.ShouldShowDocumentationLink(definition))
            {
                DrawHelpButton();
            }

            // Show overloads for nodes EXCEPT type, those have too many entries and break Unity UI
            if (!nodeDefinition.fullName.StartsWith("Type_"))
            {
                RefreshPopup();
            }
            LayoutPorts(nodeDefinition);

            view.MarkSceneDirty();
        }

        private void DrawHelpButton()
        {
            EngineUI.Button helpButton = new EngineUI.Button(ShowNodeDocs)
            {
                name = "help-button",
            };
            helpButton.Add(new EngineUI.TextElement()
            {
                name = "icon",
                text = "?"
            });
            titleButtonContainer.Add(helpButton);
        }

        private void ShowNodeDocs()
        {
            string url = UdonGraphExtensions.GetDocumentationLink(definition);
            if (!string.IsNullOrEmpty(url))
            {
                Help.BrowseURL(url);
            }
        }

        public override void SetPosition(Rect newPos)
        {
            newPos.position = GraphElementExtension.GetSnappedPosition(newPos.position);
            base.SetPosition(newPos);
            data.position = newPos.position;
        }

        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            this.Reserialize();
        }

        private void RefreshPopup()
        {
            // Get overloads, draw them if we have more than one signature for this method
            overloadDefinitions = CacheOverloads();
            if (overloadDefinitions != null && overloadDefinitions.Count > 1)
            {
                // Get index of currently selected (could cache this on node instead)
                // TODO: switch to just reading this from Popup, which probably stores it
                int currentIndex = 0;
                for (int i = 0; i < overloadDefinitions.Count; i++)
                {
                    if (overloadDefinitions.ElementAt(i).fullName != name)
                    {
                        continue;
                    }

                    currentIndex = i;
                    break;
                }

                // Build dropdown list
                List<string> options = new List<string>();
                for (int i = 0; i < overloadDefinitions.Count; i++)
                {
                    UdonNodeDefinition nodeDefinition = overloadDefinitions.ElementAt(i);
                    if (!_optionNameCache.TryGetValue(nodeDefinition, out string optionName))
                    {
                        optionName = nodeDefinition.fullName;
                        // don't add overload types that take pointers, not supported
                        string[] splitOptionName = optionName.Split(new[] { "__" }, StringSplitOptions.None);
                        if (splitOptionName.Length >= 3)
                        {
                            optionName = $"({splitOptionName[2].Replace("_", ", ")})";
                        }
                        optionName = optionName.FriendlyNameify();
                        _optionNameCache.Add(nodeDefinition, optionName);
                    }

                    if (!_cleanerOptionNameCache.TryGetValue(nodeDefinition, out string cleanerOptionName))
                    {
                        cleanerOptionName =
                            optionName.Replace("UnityEngine", "").Replace("System", "").Replace("Variable_", "");
                        _cleanerOptionNameCache.Add(nodeDefinition, cleanerOptionName);
                    }

                    options.Add(cleanerOptionName);
                    // optionName is what was used as the tooltip. Do we need the tooltip?
                }

                // Clear out old one
                if (inputContainer.Contains(_popup))
                {
                    inputContainer.Remove(_popup);
                }

                _popup = new EditorUI.PopupField<string>(options, currentIndex);
                _popup.OnValueChanged((e) =>
                {
                    // TODO - store data in the dropdown and use formatListItemCallback?
                    data.fullName = overloadDefinitions.ElementAt(_popup.index).fullName;
                    _graphView.Reload();
                });
                inputContainer.Add(_popup);
            }
        }

        private List<UdonNodeDefinition> CacheOverloads()
        {
            string baseIdentifier = name;
            string[] splitBaseIdentifier = baseIdentifier.Split(new[] { "__" }, StringSplitOptions.None);
            if (splitBaseIdentifier.Length >= 2)
            {
                baseIdentifier = $"{splitBaseIdentifier[0]}__{splitBaseIdentifier[1]}__";
            }

            if (baseIdentifier.StartsWithCached("Const_"))
            {
                return null;
            }

            if (baseIdentifier.StartsWithCached("Type_"))
            {
                baseIdentifier = "Type_";
            }

            if (baseIdentifier.StartsWithCached("Variable_"))
            {
                baseIdentifier = "Variable_";
            }

            // This used to be cached on graph instead of calculated per-node
            // TODO: cache this somewhere, maybe UdonEditorManager? Is that worth it for performance?
            IEnumerable<UdonNodeDefinition> matchingNodeDefinitions =
                UdonEditorManager.Instance.GetNodeDefinitions(baseIdentifier);

            var result = new List<UdonNodeDefinition>();
            foreach (var definition in matchingNodeDefinitions)
            {
                // don't add definitions with pointer parameters, not supported in Udon
                if (!definition.fullName.Contains('*'))
                {
                    result.Add(definition);
                }
            }
            return result;
        }

        internal void RestoreConnections()
        {
            RestoreInputs();
            RestoreFlows();
        }

        private void RestoreFlows()
        {
            for (int i = 0; i < data.flowUIDs.Length; i++)
            {
                // skip if flow uid is empty
                string nodeUID = data.flowUIDs[i];
                if (string.IsNullOrEmpty(nodeUID))
                {
                    continue;
                }

                // Find connected node via Graph
                UdonNode connectedNode = _graphView.GetNodeByGuid(nodeUID) as UdonNode;
                if (connectedNode == null)
                {
                    Debug.Log($"Couldn't find node with GUID {nodeUID}");
                    continue;
                }
                
                // Trying to move a Block's flow that was left at the end to the beginning
                if (portsFlowOut != null && i >= portsFlowOut.Count)
                {
                    Debug.LogWarning($"Trying to restore flow to {connectedNode.name} from a non-existent port, skipping");
                    
                    for (int j = 0; j < data.flowUIDs.Length; j++)
                    {
                        bool didRestoreFlow = false;
                        if (string.IsNullOrEmpty(data.flowUIDs[j]))
                        {
                            data.flowUIDs[j] = data.flowUIDs[i];
                            data.flowUIDs[i] = "";
                            didRestoreFlow = true;
                        }
                        if (didRestoreFlow)
                        {
                            RestoreFlows();
                        }
                    }
                    
                    continue;
                }

                UdonPort sourcePort = null;
                // Edge case, but its possible that this is null in broken graphs
                // Skip if we can't find the source port
                if (portsFlowOut != null)
                {
                    sourcePort = portsFlowOut.Count > 1 ? portsFlowOut[i] : portsFlowOut.FirstOrDefault();
                    if (sourcePort == null)
                    {
                        Debug.LogError($"Failed to find output flow port for node {uid}");
                        // clear the flow uid, user will have to reconnect by hand
                        data.flowUIDs[i] = "";
                        continue;
                    }
                }
                else
                {
                    Debug.LogError($"Failed to find output flow port for node {uid}");
                    // clear the flow uid, user will have to reconnect by hand
                    data.flowUIDs[i] = "";
                    continue;
                }


                UdonPort destPort = null;
                // Edge case, but its possible that this is null in broken graphs
                if(connectedNode.portsFlowIn != null)
                {
                    destPort = connectedNode.portsFlowIn.FirstOrDefault();
                    if (destPort == null)
                    {
                        Debug.LogError($"Failed to find input flow port node node {nodeUID}");
                        // clear the flow uid, user will have to reconnect by hand
                        data.flowUIDs[i] = "";
                        continue;
                    }
                }
                else
                {
                    Debug.LogError($"Failed to find input flow port node node {nodeUID}");
                    // clear the flow uid, user will have to reconnect by hand
                    data.flowUIDs[i] = "";
                    continue;
                }

                // Passed the tests! ready to connect
                var edge = sourcePort.ConnectTo(destPort);
                edge.AddToClassList("flow");
                _graphView.AddElement(edge);
            }
        }

        private void RestoreInputs()
        {
            for (int i = 0; i < definition.Inputs.Count; i++)
            {
                // Skip to next input if we don't have a node to check at this index
                if (data.nodeUIDs.Length <= i)
                {
                    continue;
                }

                // Skip to next input if we have a bad node reference
                if (string.IsNullOrEmpty(data.nodeUIDs[i]))
                {
                    continue;
                }

                // get otherIndex. not 100% sure what this refers to yet, maybe a port index?
                string[] splitUID = data.nodeUIDs[i].Split('|');
                string nodeUID = splitUID[0];
                int otherIndex = 0;
                if (splitUID.Length > 1)
                {
                    otherIndex = int.Parse(splitUID[1]);
                }
                // Skip if we don't have a good uid for the other node
                if (string.IsNullOrEmpty(nodeUID))
                {
                    continue;
                }

                // Find connected node via Graph
                UdonNode connectedNode = _graphView.GetNodeByGuid(nodeUID) as UdonNode;
                if (connectedNode == null)
                {
                    Debug.Log($"Couldn't find node with GUID {nodeUID}");
                }

                // No matching port for this data, skip
                if (!portsIn.TryGetValue(i, out UdonPort destPort))
                {
                    Debug.LogError($"Failed to find input data slot (index {i}) for node {uid} {data.fullName}");
                    continue;
                }

                // Copied from Legacy, not sure what conditions would cause this
                if (otherIndex < 0 || connectedNode.portsOut.Keys.Count <= otherIndex)
                {
                    otherIndex = 0;
                }

                // skip if we can't find the sourcePort - comment better once you understand what this is exactly
                if (!connectedNode.portsOut.TryGetValue(otherIndex, out UdonPort sourcePort))
                {
                    Debug.LogError($"Failed to find output data slot for node {nodeUID}");
                    continue;
                }

                // Passed the tests! ready to connect
                var edge = sourcePort.ConnectTo(destPort);
                _graphView.AddElement(edge);
            }
        }

        // Legacy, haven't gone through yet
        void ValidateNodeData()
        {
            bool modifiedData = false;
            for (int i = 0; i < data.nodeValues.Length; i++)
            {
                if (definition.Inputs.Count <= i)
                {
                    continue;
                }

                Type expectedType = definition.Inputs[i].type;

                if (data.nodeValues[i] == null)
                {
                    continue;
                }

                object value = data.nodeValues[i].Deserialize();
                if (value == null)
                {
                    continue;
                }

                if (!expectedType.IsInstanceOfType(value))
                {
                    data.nodeValues[i] = SerializableObjectContainer.Serialize(null, expectedType);
                    modifiedData = true;
                }
            }

            if (modifiedData)
            {
                _graphView.ReSerializeData();
            }
        }

        void PopulateDefaultValues()
        {
            // No default values so I'm just...making them?
            int count = definition.Inputs.Count;

            data.nodeValues = new SerializableObjectContainer[count];
            data.nodeUIDs = new string[count];
            for (int i = 0; i < count; i++)
            {
                object value = definition.defaultValues.Count > i ? definition.defaultValues[i] : default;
                data.nodeValues[i] = SerializableObjectContainer.Serialize(value, definition.Inputs[i].type);
            }
        }

        private enum VariableNodeType { Get, Set};

        // renamed from MakePorts to match Legacy implementation for now
        private void LayoutPorts(UdonNodeDefinition udonNodeDefinition)
        {
            SetupFlowPorts(udonNodeDefinition);

            // Don't setup in ports for Get_Variable node types, instead add variable popup
            if (name.CompareTo("Get_Variable") == 0)
            {
                AddVariablePopup(VariableNodeType.Get);
            }
            else
            {
                // Add Variable popup and in-ports for Set_Variable
                if (name.CompareTo("Set_Variable") == 0)
                {
                    AddVariablePopup(VariableNodeType.Set);
                }

                SetupInPorts(udonNodeDefinition);
            }
            
            SetupOutPorts(udonNodeDefinition);

            RefreshExpandedState();
            RefreshPorts();
        }

        // TODO: Test this again after we have the new graph serializing the addition of nodes
        private void AddVariablePopup(VariableNodeType varType)
        {
            // Legacy method of determining currently selected index
            // TODO: upgrade this logic path from the legacy method of determining Variable indices
            // Get Variable nodes only have one value, get it and deserialize
            var value = data.nodeValues[0].Deserialize();

            // Make copy of options so we can add the Create New Variable option
            List<string> options = new List<string>(_graphView.GetVariableNames)
            {
                "Create New Variable"
            };

            // Get value of selected node in rather roundabout way
            int currentIndex = _graphView.GetVariableNodes
                .IndexOf(_graphView.GetVariableNodes.FirstOrDefault(v => v.uid == (string)value));

            if (currentIndex < 0)
            {
                Debug.LogWarning($"Node {name} didn't have a variable assigned, removing");
                _graphView.graphData.RemoveNode(data);
                _graphView.RemoveElement(this);
                _graphView.Reload();
                return;
            }

            // Create popup, set current value and set function to update data when it's changed.
            var popup = new EditorUI.PopupField<string>(options, currentIndex);
            popup.OnValueChanged((e) => {
                // Test whether we've selected an existing variable or the 'Create New Variable' option
                if(popup.index < _graphView.GetVariableNames.Count)
                {
                    // not currently using event value, which is variable name. Instead using legacy method of comparing index to graph variable nodes array index
                    string newUid = _graphView.GetVariableNodes[popup.index].uid;
                    // Get Variable nodes only have one entry, so index is 0 below
                    SetNewValue(newUid, 0);
                    // include 'Set' in title for Set Variable, just name for Get Variable
                    title = (varType == VariableNodeType.Set ? "Set " : "") + e.newValue;
                    _graphView.Reload();
                }
                else
                {
                    // User selected 'Create New Variable'
                     string newUid = _graphView.AddNewVariable();
                    SetNewValue(newUid, 0, typeof(string));
                    // TODO: see if we can remove the need for a second reload here
                    _graphView.Reload();
                }
            });

            string startingUid = _graphView.GetVariableNodes[currentIndex].uid;
            SetNewValue(startingUid, 0);

            // include 'Set' in title for Set Variable, just name for Get Variable
            title = (varType == VariableNodeType.Set ? "Set " : "") + _graphView.GetVariableNames[currentIndex];

            // Add newly created popup to node
            inputContainer.Add(popup);
        }

        public void SetNewValue(object newValue, int index, Type inType = null)
        {
            data.nodeValues[index] = SerializableObjectContainer.Serialize(newValue, inType);
            this.Reserialize();
        }

        private void SetupOutPorts(UdonNodeDefinition udonNodeDefinition)
        {
            portsOut = new Dictionary<int, UdonPort>();
            for (int i = 0; i < udonNodeDefinition.Outputs.Count; i++)
            {
                var item = udonNodeDefinition.Outputs[i];

                // Convert object type to variable type for Get_Variable nodes, or run them through the SlotTypeConverter for all other nodes
                Type type = (udonNodeDefinition.fullName.Contains("Get_Variable")) ? 
                    GetTypeForDefinition(udonNodeDefinition) : 
                    UdonGraphExtensions.SlotTypeConverter(item.type, udonNodeDefinition.fullName);

                string label = UdonGraphExtensions.FriendlyTypeName(type).FriendlyNameify();
                if (label == "IUdonEventReceiver")
                {
                    label = "UdonBehaviour";
                }

                if (item.name != null) label = $"{label} {item.name}";
                UdonPort port = (UdonPort) UdonPort.Create(label, Direction.Output, this, type, data, i);
                outputContainer.Add(port);
                portsOut[i] = port;
            }
        }

        private void SetupInPorts(UdonNodeDefinition udonNodeDefinition)
        {
            portsIn = new Dictionary<int, UdonPort>();

            // Expand node data to hold values for all inputs
            data.Resize(udonNodeDefinition.Inputs.Count);

            int startIndex = 0;
            // Skip first input for Set_Variable since that's the eventName which is set via dropdown
            if (name.CompareTo("Set_Variable") == 0)
            {
                startIndex = 1;
            }

            // Skip inputs for Null and This nodes
            if (name.Contains("Const_Null") || name.Contains("Const_This"))
            {
                return;
            }

            for (int index = startIndex; index < udonNodeDefinition.Inputs.Count; index++)
            {
                UdonNodeParameter input = udonNodeDefinition.Inputs[index];
                string label = "";
                // TODO: Ask Cubed what this does? Or figure it out.
                if (udonNodeDefinition.Inputs.Count > index && index >= 0)
                {
                    label = udonNodeDefinition.Inputs[index].name;
                }

                if (label == "IUdonEventReceiver")
                {
                    label = "UdonBehaviour";
                }


                label = label.FriendlyNameify();
                string typeName = UdonGraphExtensions.FriendlyTypeName(input.type);
                // skip over types with pointers. Should remove these from included overloads in the first place!
                if (typeName.Contains('*'))
                {
                    continue;
                }

                // Convert object type to variable type for Set_Variable nodes, or run them through the SlotTypeConverter for all other nodes
                Type type = (udonNodeDefinition.fullName.Contains("Set_Variable")) ?
                    type = GetTypeForDefinition(udonNodeDefinition) : 
                    UdonGraphExtensions.SlotTypeConverter(input.type, udonNodeDefinition.fullName);
                
                // not 100% sure if I should use label or typeName here
                UdonPort p = UdonPort.Create(label, Direction.Input, this, type, data, index) as UdonPort;
                inputContainer.Add(p);
                portsIn.Add(index, p);
            }

        }

        private Type GetTypeForDefinition(UdonNodeDefinition udonNodeDefinition)
        {
            string targetUid = data.nodeValues[0].Deserialize().ToString();
            UdonNodeData varData = _graphView.GetVariableNodes.Where(n => n.uid == targetUid).FirstOrDefault();
            if (varData != null)
            {
                var targetDefinition = UdonEditorManager.Instance.GetNodeDefinition(varData.fullName);
                if (targetDefinition != null)
                {
                    return UdonGraphExtensions.SlotTypeConverter(targetDefinition.type, udonNodeDefinition.fullName);
                }
            }
            // if we fail, return generic object type
            return typeof(object);
        }

        private void SetupFlowPorts(UdonNodeDefinition udonNodeDefinition)
        {
            if (udonNodeDefinition.flow)
            {
                portsFlowIn = new List<UdonPort>();
                portsFlowOut = new List<UdonPort>();

                string label = "";

                int inFlowIndex = -1;
                int outFlowIndex = -1;
                // don't add input flow for events, they're called from above
                if (!udonNodeDefinition.fullName.StartsWith("Event_"))
                {
                    label = udonNodeDefinition.inputFlowNames.Count > 0 ? udonNodeDefinition.inputFlowNames[0] : "";
                    AddFlowPort(Direction.Input, label, ++inFlowIndex);
                }

                // add output flow
                label = udonNodeDefinition.outputFlowNames.Count > 0 ? udonNodeDefinition.outputFlowNames[0] : "";
                AddFlowPort(Direction.Output, label, ++outFlowIndex);
                if (_specialFlows.Contains(udonNodeDefinition.fullName))
                {
                    label = udonNodeDefinition.outputFlowNames.Count > 1 ? udonNodeDefinition.outputFlowNames[1] : "";
                    AddFlowPort(Direction.Output, label, ++outFlowIndex);
                }

                // Add the number of output flows we need for a Block
                if (udonNodeDefinition.fullName == "Block")
                {
                    data.flowUIDs = data.flowUIDs.Where(f => !string.IsNullOrEmpty(f)).ToArray();
                    int connectedFlows = data.flowUIDs.Length;
                    if (connectedFlows > 1)
                    {
                        for (int i = 0; i < connectedFlows - 1; i++)
                        {
                            AddFlowPort(Direction.Output, "", ++outFlowIndex);
                        }
                    }
                }
            }
        }

        private void AddFlowPort(Direction d, string label, int index)
        {
            UdonPort p = (UdonPort) UdonPort.Create(label, d, this, null, data, index);
            p.AddToClassList("flow");
            if(d == Direction.Input)
            {
                inputContainer.Add(p);
                portsFlowIn.Add(p);
            }
            else
            {
                outputContainer.Add(p);
                portsFlowOut.Add(p);
            }
        }

        private bool HasRecursiveFlow(Port fromSlot, Port toSlot)
        {
            // No need to check connections to value slots
            if (toSlot.portType != null) return false;

            // Check out ports of node being connected TO
            foreach (var port in (toSlot.node as UdonNode).portsFlowOut)
            {
                // if any of its ports connect to fromSlot, it's recursive. using foreach for convenience, should be just one edge
                foreach (var edge in port.connections)
                {
                    // if this connection goes to the node that started this all, then it's recursion
                    if(edge.input.node == fromSlot.node)
                    {
                        return true;
                    }
                    // Need to run this recursively to check all ports
                    if(HasRecursiveFlow(fromSlot, edge.input))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        #region IEdgeConnectorListener
        public void OnDrop(UnityEditor.Experimental.UIElements.GraphView.GraphView graphView, Edge edge)
        {
            if (edge.output != null && edge.input != null && !HasRecursiveFlow(edge.output, edge.input))
            {
                edge.output.Connect(edge);
                edge.input.Connect(edge);
                graphView.AddElement(edge);

                // Reload block nodes after new connections
                if(definition.fullName == "Block")
                {
                    this.Reload();
                }
            }
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            if (!Settings.SearchOnNoodleDrop) return;

            if (edge.output != null && edge.output.portType != null)
            {
                _graphView.OpenPortSearch(edge.output.portType, position, edge.output, Direction.Input);
            }
            else if (edge.input != null && edge.input.portType != null)
            {
                _graphView.OpenPortSearch(edge.input.portType, position, edge.input, Direction.Output);
            }
        }

        #endregion
    }
}
