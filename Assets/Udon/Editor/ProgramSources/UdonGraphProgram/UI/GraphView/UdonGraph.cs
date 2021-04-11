using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityGraph = UnityEditor.Experimental.UIElements.GraphView;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;
using UnityEngine.Experimental.UIElements;
using UnityEditor.SceneManagement;
using VRC.Udon.EditorBindings;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public static class UdonGraphCommands
    {
        public const string Reserialize = "Reserialize";
        public const string Reload = "Reload";
        public const string SaveNewData = "SaveNewData";
    }

    public class UdonGraph : UnityGraph.GraphView
    {

        private GridBackground _background;
        private UdonMinimap _map;
        private UdonVariablesBlackboard _blackboard;

        // copied over from Legacy.UdonGraph,
        public UdonGraphProgramAsset graphProgramAsset;
        public UdonBehaviour _udonBehaviour;
        public UdonGraphData graphData;

        // Tracking variables
        private List<string> _variablePopupOptions = new List<string>();
        private List<UdonNodeData> _variableNodes = new List<UdonNodeData>();

        private Vector2 lastMousePosition;
        private VisualElement mouseTipContainer;
        private TextElement mouseTip;
        private Vector2 mouseTipOffset = new Vector2(20, -22);

        private UdonSearchManager _searchManager;

        private bool _reloading = false;
        
        private bool _dragging = false;

        public bool IsReloading => _reloading;

        // Enable stuff from NodeGraphProcessor
        private UdonGraphWindow _window;

        public List<string> GetVariableNames { get => _variablePopupOptions; private set { } }
        public List<UdonNodeData> GetVariableNodes { get => _variableNodes; private set { } }

        public UdonGraph(UdonGraphWindow window)
        {
            _window = window;

            this.StretchToParentSize();
            SetupBackground();
            SetupMap();
            SetupBlackboard();
            SetupZoom(0.2f, 3);
            SetupDragAndDrop();

            this.AddManipulator(new UnityGraph.ContentDragger());
            this.AddManipulator(new UnityGraph.SelectionDragger());
            this.AddManipulator(new UnityGraph.RectangleSelector());

            mouseTipContainer = new VisualElement()
            {
                name = "mouse-tip-container",
            };
            Add(mouseTipContainer);
            mouseTip = new TextElement()
            {
                name = "mouse-tip",
                visible = true,
            };
            SetMouseTip("");
            mouseTipContainer.Add(mouseTip);

            // This event is used to send a "Reserialize" command from updated port fields
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);

            // Save last known mouse position for better pasting. Is there a performance hit for this?
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            _searchManager = new UdonSearchManager(this, window);

            elementsInsertedToStackNode = OnStackNodeInsert;
            elementsRemovedFromStackNode = OnStackNodeRemove;
            graphViewChanged = OnViewChanged;
            serializeGraphElements = OnSerializeGraphElements;
            unserializeAndPaste = OnUnserializeAndPaste;
            canPasteSerializedData = CheckCanPaste;
            viewTransformChanged = OnViewTransformChanged;
        }

        private void OnViewTransformChanged(UnityGraph.GraphView graphView)
        {
            graphProgramAsset.viewTransform.position = this.viewTransform.position;
            graphProgramAsset.viewTransform.scale = this.viewTransform.scale.x;
            EditorUtility.SetDirty(graphProgramAsset);
        }

        private bool CheckCanPaste(string pasteData)
        {
            UdonNodeData[] copiedNodeDataArray;
            try
            {
                copiedNodeDataArray = JsonUtility
                    .FromJson<SerializableObjectContainer.ArrayWrapper<UdonNodeData>>(UdonGraphExtensions.UnZipString(pasteData))
                    .value;
            }
            catch
            {
                //oof ouch that's not valid data
                return false;
            }
            return true;
        }

        public void Initialize(UdonGraphProgramAsset asset, UdonBehaviour udonBehaviour)
        {
            if (graphProgramAsset != null)
                SaveGraphToDisk();
            
            graphProgramAsset = asset;
            if (udonBehaviour != null)
            {
                _udonBehaviour = udonBehaviour;
            }
            
            graphData = new UdonGraphData(graphProgramAsset.GetGraphData());

            DoDelayedReload();
            EditorApplication.update += DelayedRestoreViewFromData;

            // When pressing ctrl-s, we save the graph
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += _ => SaveGraphToDisk();
        }

        private void DelayedRestoreViewFromData()
        {
            EditorApplication.update -= DelayedRestoreViewFromData;
            UpdateViewTransform(graphProgramAsset.viewTransform.position, Vector3.one * graphProgramAsset.viewTransform.scale);
        }

        public void SaveGraphToDisk()
        {
            if (graphProgramAsset == null)
                return;

            EditorUtility.SetDirty(graphProgramAsset);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.target == this && evt.keyCode == KeyCode.Tab)
            {
                var screenPosition = GUIUtility.GUIToScreenPoint(evt.originalMousePosition);
                nodeCreationRequest(new NodeCreationContext() { screenMousePosition = screenPosition, target = this });
                evt.StopImmediatePropagation();
            }
            else if (evt.keyCode == KeyCode.A && evt.ctrlKey)
            {
                // Select every graph element
                ClearSelection();
                foreach (var element in graphElements.ToList())
                {
                    AddToSelection(element);
                }
            }
        }

        public bool GetBlackboardVisible()
        {
            return _blackboard.visible;
        }

        public bool GetMinimapVisible()
        {
            return _map.visible;
        }

        public void ToggleShowVariables(bool value)
        {
            _blackboard.SetVisible(value);
        }

        public void ToggleShowMiniMap(bool value)
        {
            _map.SetVisible(value);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is UnityGraph.GraphView || evt.target is UdonNode)
            {
                // Create a Group, enclosing any selected nodes
                evt.menu.AppendAction("Create Group", (m) => {

                    UdonGroup group = UdonGroup.Create("Group", GetRectFromMouse(), this);
                    Undo.RecordObject(graphProgramAsset, "Add Group");
                    AddElement(group);
                    group.UpdateDataId();

                    foreach (ISelectable item in selection)
                    {
                        if (item is UdonNode)
                        {
                            group.AddElement(item as UdonNode);
                        }
                        else if(item is UdonComment)
                        {
                            group.AddElement(item as UdonComment);
                        }
                    }

                    SaveNewData();

                }, DropdownMenu.MenuAction.AlwaysEnabled);
                var selectedItems = selection.Where(i=>i is UdonNode || i is UdonComment).ToList();
                if (selectedItems.Count > 0)
                {
                    evt.menu.AppendAction("Remove From Group", (m) =>
                    {
                        Undo.RecordObject(graphProgramAsset, "Remove Items from Group");
                        int count = selectedItems.Count;
                        for (int i = count - 1; i >=0; i--)
                        {
                            if(selectedItems.ElementAt(i) is UdonNode)
                            {
                                UdonNode node = selectedItems.ElementAt(i) as UdonNode;
                                if(node.group != null) node.group.RemoveElement(node);
                            }
                            else if (selectedItems.ElementAt(i) is UdonComment)
                            {
                                UdonComment comment = selectedItems.ElementAt(i) as UdonComment;
                                if(comment.group != null) comment.group.RemoveElement(comment);
                            }

                        }

                        ReSerializeData();
                        Reload();
                    }, DropdownMenu.MenuAction.AlwaysEnabled);
                }

                // Create a Comment
                evt.menu.AppendAction("Create Comment", (m) => {
                    UdonComment comment = UdonComment.Create("Comment", GetRectFromMouse(), this);
                    Undo.RecordObject(graphProgramAsset, "Add Comment");
                    AddElement(comment);
                    SaveNewData();
                }, DropdownMenu.MenuAction.AlwaysEnabled);

                evt.menu.AppendSeparator();
            }

            base.BuildContextualMenu(evt);
        }

        private Rect GetRectFromMouse()
        {
            return new Rect(contentViewContainer.WorldToLocal(lastMousePosition), Vector2.zero);
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            lastMousePosition = evt.mousePosition;
            MoveMouseTip(lastMousePosition);

        }

        private void MoveMouseTip(Vector2 position)
        {
            if (mouseTipContainer.visible)
            {
                var layout = mouseTipContainer.layout;
                layout.position = position + mouseTipOffset;
                mouseTipContainer.layout = layout;
            }
        }

        public bool IsDuplicateEventNode(string fullName)
        {
            if (fullName.StartsWith("Event_") &&
                (fullName != "Event_Custom"))
            {
                if (this.Query(fullName).ToList().Count > 0)
                {
                    Debug.LogWarning(
                            $"Can't create more than one {fullName} node, try managing your flow with a Block node instead!");
                    return true;
                }
            }
            return false;
        }

        private string OnSerializeGraphElements(IEnumerable<GraphElement> selection)
        {

            Bounds bounds = new Bounds();
            bool startedBounds = false;
            List<UdonNodeData> nodeData = new List<UdonNodeData>();
            List<UdonNodeData> variables = new List<UdonNodeData>();
            foreach (var item in selection)
            {
                // Only serializing UdonNode for now
                if (item is UdonNode)
                {
                    UdonNode node = (UdonNode)item;
                    // Calculate bounding box to enclose all items
                    if (!startedBounds)
                    {
                        bounds = new Bounds(node.data.position, Vector3.zero);
                        startedBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(node.data.position);
                    }

                    // Handle Get/Set Variables
                    if (node.data.fullName == "Get_Variable" || node.data.fullName == "Set_Variable")
                    {
                        // make old-school get-variable node data from existing variable
                        var targetUid = node.data.nodeValues[0].Deserialize();
                        var matchingNode = GetVariableNodes.First(v => v.uid == (string)targetUid);
                        if (matchingNode != null && !variables.Contains(matchingNode))
                        {
                            variables.Add(matchingNode);
                        }
                    }

                    nodeData.Add(new UdonNodeData(node.data));
                }
            }

            // Add variables at beginning of list so they get created first
            nodeData.InsertRange(0, variables);

            // Go through each item and offset its position by the center of the group (normalizes the coordinates around 0,0)
            var offset = new Vector2(bounds.center.x, bounds.center.y);
            foreach (UdonNodeData data in nodeData)
            {
                var ogPosition = data.position;
                data.position -= offset;
            }

            string result = UdonGraphExtensions.ZipString(JsonUtility.ToJson(
                new SerializableObjectContainer.ArrayWrapper<UdonNodeData>(nodeData.ToArray())));

            return result;
        }

        private void OnUnserializeAndPaste(string operationName, string pasteData)
        {
            ClearSelection();

            UdonNodeData[] copiedNodeDataArray;
            // Note: CheckCanPaste already does this check but it doesn't cost much to do it twice
            try
            {
                copiedNodeDataArray = JsonUtility
                    .FromJson<SerializableObjectContainer.ArrayWrapper<UdonNodeData>>(UdonGraphExtensions.UnZipString(pasteData))
                    .value;
            }
            catch
            {
                //oof ouch that's not valid data
                return;
            }

            var copiedNodeDataList = new List<UdonNodeData>();
            // Add new variables if needed
            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                if (copiedNodeDataArray[i].fullName.StartsWith("Variable_"))
                {
                    if (!graphData.nodes.Exists(n => n.uid == copiedNodeDataArray[i].uid))
                    {
                        // check for conflicting variable names
                        int nameIndex = (int)UdonParameterProperty.ValueIndices.name;
                        string varName = (string)copiedNodeDataArray[i].nodeValues[nameIndex].Deserialize();
                        if (GetVariableNames.Contains(varName))
                        {
                            // if we already have a variable with that name, find a new name and serialize it into the data
                            varName = GetUnusedVariableNameLike(varName);
                            copiedNodeDataArray[i].nodeValues[nameIndex] = SerializableObjectContainer.Serialize(varName);
                        }
                        graphData.nodes.Add(copiedNodeDataArray[i]);
                    }
                }
                else if(IsDuplicateEventNode(copiedNodeDataArray[i].fullName))
                {
                    // don't add duplicate event nodes
                }
                else
                {
                    copiedNodeDataList.Add(copiedNodeDataArray[i]);
                }
            }

            // Remove duplicate events

            RefreshVariables();

            // copy modified list back to array
            copiedNodeDataArray = copiedNodeDataList.ToArray();

            _reloading = true;
            var graphMousePosition = GetRectFromMouse().position;
            List<UdonNode> pastedNodes = new List<UdonNode>();
            Dictionary<string, string> uidMap = new Dictionary<string, string>();
            UdonNodeData[] newNodeDataArray = new UdonNodeData[copiedNodeDataArray.Length];

            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                UdonNodeData nodeData = copiedNodeDataArray[i];
                newNodeDataArray[i] = new UdonNodeData(graphData, nodeData.fullName)
                {
                    position = nodeData.position + graphMousePosition,
                    uid = Guid.NewGuid().ToString(),
                    nodeUIDs = new string[nodeData.nodeUIDs.Length],
                    nodeValues = nodeData.nodeValues,
                    flowUIDs = new string[nodeData.flowUIDs.Length]
                };

                uidMap.Add(nodeData.uid, newNodeDataArray[i].uid);
            }

            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                UdonNodeData nodeData = copiedNodeDataArray[i];
                UdonNodeData newNodeData = newNodeDataArray[i];

                for (int j = 0; j < newNodeData.nodeUIDs.Length; j++)
                {
                    if (uidMap.ContainsKey(nodeData.nodeUIDs[j].Split('|')[0]))
                    {
                        newNodeData.nodeUIDs[j] = uidMap[nodeData.nodeUIDs[j].Split('|')[0]];
                    }
                }

                for (int j = 0; j < newNodeData.flowUIDs.Length; j++)
                {
                    if (uidMap.ContainsKey(nodeData.flowUIDs[j].Split('|')[0]))
                    {
                        newNodeData.flowUIDs[j] = uidMap[nodeData.flowUIDs[j].Split('|')[0]];
                    }
                }

                UdonNode udonNode = UdonNode.CreateNode(newNodeDataArray[i], this);
                if (udonNode != null)
                {
                    graphData.nodes.Add(newNodeDataArray[i]);
                    pastedNodes.Add(udonNode);
                }
            }
            _reloading = false;

            Reload();

            // Select all newly-pasted nodes after reload
            // TODO: figure out why this doesn't work!
            foreach (var item in pastedNodes)
            {
                AddToSelection(item as GraphElement);
            }
        }

        // This is needed to properly clear the selection in some cases (like deleting a stack node) even though it doesn't appear to do anything
        public override void ClearSelection()
        {
            base.ClearSelection();
        }

        public void MarkSceneDirty()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        private GraphViewChange OnViewChanged(GraphViewChange changes)
        {
            bool dirty = false;

            // Remove node from Data when removed from Graph
            if (!_reloading && changes.elementsToRemove != null && changes.elementsToRemove.Count > 0)
            {
                var newElementsData = graphProgramAsset.graphElementData.ToList();

                foreach (var element in changes.elementsToRemove)
                {
                    if (element is UdonNode)
                    {
                        var nodeData = ((UdonNode)element).data;
                        graphData.RemoveNode(nodeData);
                        continue;
                    }

                    if (element is Edge)
                    {
                        Undo.RecordObject(graphProgramAsset, $"delete-{element.name}");
                        continue;
                    }

                    if (element is UdonParameterField)
                    {
                        var pField = element as UdonParameterField;
                        if (graphData.nodes.Contains(pField.Data))
                        {
                            graphData.nodes.Remove(pField.Data);
                            DoDelayedReload();
                        }
                    }

                    // not an UdonNode or Edge, it's an element serialized in graphElementData
                    var graphElement = newElementsData.Find(e => e.uid.CompareTo(element.persistenceKey) == 0);
                    if (graphElement != null)
                    {
                        Undo.RecordObject(graphProgramAsset, $"delete-{element.name}");
                        newElementsData.Remove(graphElement);
                    }
                }

                if (newElementsData.Count < graphProgramAsset.graphElementData.Length)
                {
                    graphProgramAsset.graphElementData = newElementsData.ToArray();
                }

                ClearSelection();
                dirty = true;
            }

            if (dirty)
            {
                MarkSceneDirty();
                EditorApplication.update += DelayedReserialize;
            }

            return changes;
        }

        #region StackNode change listeners
        private void OnStackNodeInsert(StackNode node, int index, IEnumerable<GraphElement> elements)
        {
            (node as UdonStackNode).OnElementsAdded(elements);
        }

        private void OnStackNodeRemove(StackNode node, IEnumerable<GraphElement> elements)
        {
            (node as UdonStackNode).OnElementsRemoved(elements);
        }
        #endregion

        public void DoDelayedCompile()
        {
            EditorApplication.update += DelayedCompile;
        }

        private void DelayedCompile()
        {
            EditorApplication.update -= DelayedCompile;
            graphProgramAsset.RefreshProgram();
        }
        
        public void DoDelayedReload()
        {
            EditorApplication.update += DelayedReload;
        }
        
        void DelayedReload()
        {
            EditorApplication.update -= DelayedReload;
            Reload();
        }

        private void SetupBackground()
        {
            _background = new GridBackground
            {
                name = "bg"
            };
            Insert(0, _background);
            _background.StretchToParentSize();
        }

        private void SetupBlackboard()
        {
            _blackboard = new UdonVariablesBlackboard(this);

            _blackboard.addItemRequested = BlackboardAddVariable;
            _blackboard.editTextRequested = BlackboardEditVariableName;
            _blackboard.SetPosition(new Rect(10, 130, 200, 150));
            Add(_blackboard);
        }

        private void BlackboardEditVariableName(Blackboard b, VisualElement v, string newValue)
        {
            UdonParameterField field = (UdonParameterField) v;
            Undo.RecordObject(graphProgramAsset, "Rename Variable");
            
            // Sanitize value for variable name
            string newVariableName = newValue.SanitizeVariableName();
            newVariableName = GetUnusedVariableNameLike(newVariableName);
            field.Data.nodeValues[(int)UdonParameterProperty.ValueIndices.name] = SerializableObjectContainer.Serialize(newVariableName);
            
            // Todo: intelligently reload only nodes that use this variable
            DoDelayedReserialize();
            DoDelayedReload();
        }

        private void BlackboardAddVariable(Blackboard obj)
        {
            var screenPosition = GUIUtility.GUIToScreenPoint(lastMousePosition);
            _searchManager.OpenVariableSearch(screenPosition);
        }

        public void OpenPortSearch(Type type, Vector2 position, Port output, Direction direction)
        {
            _searchManager.OpenPortSearch(type, position, output, direction);
        }

        private void SetupMap()
        {
            _map = new UdonMinimap(this);
            Add(_map);
        }

        private bool _reserializeNextUpdate = false;
        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            switch (evt.commandName)
            {
                case UdonGraphCommands.Reload:
                    Reload();
                    break;
                case UdonGraphCommands.Reserialize:
                    if (!_reloading && !_reserializeNextUpdate)
                    {
                        _reserializeNextUpdate = true;
                        EditorApplication.update += DelayedReserialize;
                    }
                    break;
                case UdonGraphCommands.SaveNewData:
                    if (!_reloading)
                    {
                        SaveNewData();
                    }
                    break;
                default:
                    break;
            }

        }

        // collects all serialization requests from a single frame and batches them for next frame
        private void DelayedReserialize()
        {
            _reserializeNextUpdate = false;
            EditorApplication.update -= DelayedReserialize;
            ReSerializeData(true);
        }

        public void DoDelayedReserialize()
        {
            // This check keeps them from piling up
            if (!_reserializeNextUpdate)
            {
                EditorApplication.update += DelayedReserialize;   
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var result = ports.ToList().Where(
                    port => port.direction != startPort.direction
                    && port.node != startPort.node
                    && port.portType.IsReallyAssignableFrom(startPort.portType)
                    && (port.capacity == Port.Capacity.Multi || port.connections.Count() == 0)
                ).ToList();
            return result;
        }

        public void Reload()
        {
            _reloading = true;

            // Is there a better place to do this?
            string customStyle = "UdonGraphNeonStyle";
            if (Settings.UseNeonStyle && !HasStyleSheetPath(customStyle))
            {
                AddStyleSheetPath(customStyle);
            }
            else if (!Settings.UseNeonStyle && HasStyleSheetPath(customStyle))
            {
                RemoveStyleSheetPath(customStyle);
            }
            
            Undo.undoRedoPerformed -= OnUndoRedo; //Remove old handler if present to prevent duplicates, doesn't cause errors if not present
            Undo.undoRedoPerformed += OnUndoRedo;

            // Clear out Blackboard here
            _blackboard.Clear();
            
            // clear existing elements, probably need to update to only clear nodes and edges
            DeleteElements(graphElements.ToList());

            RefreshVariables();
            
            // add all nodes to graph
            for (int i = graphData.nodes.Count - 1; i >= 0; i--)
            {
                UdonNodeData nodeData = graphData.nodes[i];

                // Check for Node type - create nodes, separate out Variables
                if (nodeData.fullName.StartsWithCached("Variable_"))
                {
                    UdonNodeDefinition definition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
                    if (definition != null)
                    {
                        _blackboard.Insert(0, new BlackboardRow(new UdonParameterField(this, nodeData), new UdonParameterProperty(this, definition, nodeData)));
                    }
                }
                else if (nodeData.fullName.StartsWithCached("Comment"))
                {
                    // one way conversion from Comment Node > Comment Group
                    var commentString = nodeData.nodeValues[0].Deserialize();
                    if (commentString != null)
                    {
                        var comment = UdonComment.Create((string)commentString, new Rect(nodeData.position, Vector2.zero), this);
                        AddElement(comment);
                    }

                    // Remove from data, no longer a node
                    graphData.nodes.RemoveAt(i);
                }
                else
                {
                    UdonNode udonNode = UdonNode.CreateNode(nodeData, this);
                    AddElement(udonNode);
                    if (udonNode != null) continue;
                    Debug.Log($"Removing null node '{nodeData.fullName}'");
                    graphData.nodes.RemoveAt(i);
                }
            }
            //return;
            // reconnect nodes
            nodes.ForEach((genericNode) =>
            {
                UdonNode udonNode = (UdonNode)genericNode;
                udonNode.RestoreConnections();
            });

            // Add all Graph Elements
            if (graphProgramAsset.graphElementData != null)
            {
                foreach (var elementData in graphProgramAsset.graphElementData)
                {

                    GraphElement element = RestoreElementFromData(elementData);
                    if (element != null)
                    {
                        AddElement(element);
                    }

                }
            }
            
            ReSerializeData(false);
            _reloading = false;
        }

        // TODO: create generic to restore any supported element from UdonGraphElementData?
        private GraphElement RestoreElementFromData(UdonGraphElementData data)
        {
            switch (data.type)
            {
                case UdonGraphElementType.GraphElement:
                    {
                        return null;
                    }

                case UdonGraphElementType.UdonStackNode:
                    {
                        return UdonStackNode.Create(data, this);
                    }

                case UdonGraphElementType.UdonGroup:
                    {
                        return UdonGroup.Create(data, this);
                    }

                case UdonGraphElementType.UdonComment:
                    {
                        return UdonComment.Create(data, this);
                    }
                case UdonGraphElementType.Minimap:
                    {
                        _map.LoadData(data);
                        return null;
                    }
                case UdonGraphElementType.VariablesWindow:
                    {
                        _blackboard.LoadData(data);
                        return null;
                    }
                default:
                    return null;
            }
        }

        private void OnUndoRedo()
        {
            graphData = new UdonGraphData(graphProgramAsset.GetGraphData());
            Reload();
        }

        private void RefreshVariables()
        {
            _variableNodes = graphData.nodes
                .Where(n => n.fullName.StartsWithCached("Variable_")).Where(n => n.nodeValues.Length > 1 && n.nodeValues[1] != null).ToList();
            _variablePopupOptions =
                _variableNodes.Select(s => (string)s.nodeValues[1].Deserialize()).ToList();
        }

        // Returns UID of newly created variable
        public string AddNewVariable(string typeName = "Variable_SystemString", string variableName = "", bool isPublic = false)
        {
            // Figure out unique variable name to use
            string newVariableName = string.IsNullOrEmpty(variableName) ? "newVariable" : variableName;
            newVariableName = GetUnusedVariableNameLike(newVariableName);

            string newVarUid = Guid.NewGuid().ToString();
            UdonNodeData newNodeData = new UdonNodeData(graphData, typeName)
            {
                uid = newVarUid,
                nodeUIDs = new string[5],
                nodeValues = new[]
                                {
                    SerializableObjectContainer.Serialize(default),
                    SerializableObjectContainer.Serialize(newVariableName, typeof(string)),
                    SerializableObjectContainer.Serialize(isPublic, typeof(bool)),
                    SerializableObjectContainer.Serialize(false, typeof(bool)),
                    SerializableObjectContainer.Serialize("none", typeof(string))
                },
                position = Vector2.zero
            };

            graphData.nodes.Add(newNodeData);
            ReSerializeData(false);
            Reload();

            return newVarUid;
        }

        public void RemoveNodeData(UdonNodeData nodeData)
        {
            if (graphData.nodes.Contains(nodeData))
            {
                graphData.nodes.Remove(nodeData);
            }
            Reload();
        }

        public void ReSerializeData(bool saveNewData = true)
        {
            // While reloading, we delete and re-add all the data, so we don't want to serialize during that time.
            if (_reloading)
            {
                return;
            }

            if (graphProgramAsset == null)
            {
                Debug.LogError("Can't Reserialize without an asset");
                return;
            }

            SerializedObject serializedGraphProgramAsset = new SerializedObject((UdonGraphProgramAsset)graphProgramAsset);
            SerializedProperty graphDataProperty = serializedGraphProgramAsset.FindProperty("graphData");
            SerializedProperty nodesProperty = graphDataProperty.FindPropertyRelative("nodes");

            if (nodesProperty.arraySize > graphData.nodes.Count)
            {
                nodesProperty.ClearArray();
            }

            for (int i = 0; i < graphData.nodes.ToList().Count; i++)
            {
                if (nodesProperty.arraySize < graphData.nodes.Count)
                {
                    nodesProperty.InsertArrayElementAtIndex(i);
                }

                SerializedProperty nodeProperty = nodesProperty.GetArrayElementAtIndex(i);

                SerializedProperty fullNameProperty = nodeProperty.FindPropertyRelative("fullName");
                fullNameProperty.stringValue = graphData.nodes[i].fullName;

                SerializedProperty uidProperty = nodeProperty.FindPropertyRelative("uid");
                uidProperty.stringValue = graphData.nodes[i].uid;

                SerializedProperty positionProperty = nodeProperty.FindPropertyRelative("position");
                positionProperty.vector2Value = graphData.nodes[i].position;

                SerializedProperty nodeUIDsProperty = nodeProperty.FindPropertyRelative("nodeUIDs");
                while (nodeUIDsProperty.arraySize > graphData.nodes[i].nodeUIDs.Length)
                {
                    nodeUIDsProperty.DeleteArrayElementAtIndex(nodeUIDsProperty.arraySize - 1);
                }

                for (int j = 0; j < graphData.nodes[i].nodeUIDs.Length; j++)
                {
                    if (nodeUIDsProperty.arraySize < graphData.nodes[i].nodeUIDs.Length)
                    {
                        nodeUIDsProperty.InsertArrayElementAtIndex(j);
                        nodeUIDsProperty.GetArrayElementAtIndex(j).stringValue = "";
                    }

                    SerializedProperty nodeUIDProperty = nodeUIDsProperty.GetArrayElementAtIndex(j);
                    nodeUIDProperty.stringValue = graphData.nodes[i].nodeUIDs[j];
                }

                SerializedProperty flowUIDsProperty = nodeProperty.FindPropertyRelative("flowUIDs");
                while (flowUIDsProperty.arraySize > graphData.nodes[i].flowUIDs.Length)
                {
                    flowUIDsProperty.DeleteArrayElementAtIndex(flowUIDsProperty.arraySize - 1);
                }

                for (int j = 0; j < graphData.nodes[i].flowUIDs.Length; j++)
                {
                    if (flowUIDsProperty.arraySize < graphData.nodes[i].flowUIDs.Length)
                    {
                        flowUIDsProperty.InsertArrayElementAtIndex(j);
                        flowUIDsProperty.GetArrayElementAtIndex(j).stringValue = "";
                    }

                    SerializedProperty flowUIDProperty = flowUIDsProperty.GetArrayElementAtIndex(j);
                    flowUIDProperty.stringValue = graphData.nodes[i].flowUIDs[j];
                }

                SerializedProperty nodeValuesProperty = nodeProperty.FindPropertyRelative("nodeValues");
                while (nodeValuesProperty.arraySize > graphData.nodes[i].nodeValues.Length)
                {
                    nodeValuesProperty.DeleteArrayElementAtIndex(nodeValuesProperty.arraySize - 1);
                }

                for (int j = 0; j < graphData.nodes[i].nodeValues.Length; j++)
                {
                    if (nodeValuesProperty.arraySize < graphData.nodes[i].nodeValues.Length)
                    {
                        nodeValuesProperty.InsertArrayElementAtIndex(j);
                        nodeValuesProperty.GetArrayElementAtIndex(j).FindPropertyRelative("unityObjectValue").objectReferenceValue = null;
                        nodeValuesProperty.GetArrayElementAtIndex(j).FindPropertyRelative("stringValue").stringValue = "";
                    }

                    SerializedProperty nodeValueProperty = nodeValuesProperty.GetArrayElementAtIndex(j);

                    if (graphData.nodes[i].nodeValues[j] == null)
                    {
                        continue;
                    }
                    object nodeValue = graphData.nodes[i].nodeValues[j].Deserialize();
                    if (nodeValue != null)
                    {
                        if (nodeValue is UnityEngine.Object value)
                        {
                            if (value != null)
                            {
                                nodeValueProperty.FindPropertyRelative("unityObjectValue").objectReferenceValue =
                                    graphData.nodes[i].nodeValues[j].unityObjectValue;
                            }
                        }
                    }
                    nodeValueProperty.FindPropertyRelative("stringValue").stringValue =
                        graphData.nodes[i].nodeValues[j].stringValue;
                }

                var baseNode = GetNodeByGuid(graphData.nodes[i].uid);
                if (baseNode != null)
                {
                    UdonNode node = baseNode as UdonNode;
                    node.data = graphData.nodes[i];
                }
            }

            serializedGraphProgramAsset.ApplyModifiedProperties();

            if (saveNewData)
            {
                SaveNewData();
            }

            if (graphProgramAsset is AbstractUdonProgramSource udonProgramSource)
            {
                UdonEditorManager.Instance.QueueAndRefreshProgram(udonProgramSource);
            }
        }

        // Copied from source code, this is what happens when you press 'a' on the keyboard
        public void Recenter()
        {
            Rect rectToFit;
            Vector3 frameTranslation = Vector3.zero;
            Vector3 frameScaling = Vector3.one;

            rectToFit = CalculateRectToFitAll(contentViewContainer);
            CalculateFrameTransform(rectToFit, layout, 30, out frameTranslation, out frameScaling);

            Matrix4x4.TRS(frameTranslation, Quaternion.identity, frameScaling);

            UpdateViewTransform(frameTranslation, frameScaling);

            contentViewContainer.MarkDirtyRepaint();
        }

        public void SaveNewData()
        {
            List<UdonGraphElementData> elementData = new List<UdonGraphElementData>();
            graphElements.ForEach((element) =>
            {
                // save data from each element that can provide UdonGraphElementData
                if (element is IUdonGraphElementDataProvider)
                {
                    var data = ((IUdonGraphElementDataProvider)element).GetData();
                    elementData.Add(data);
                }
            });
            // add blackboard data
            elementData.Add(_blackboard.GetData());
            elementData.Add(_map.GetData());

            // Save new data to asset
            if (graphProgramAsset != null)
            {
                graphProgramAsset.graphElementData = elementData.ToArray();
                EditorUtility.SetDirty(graphProgramAsset);
                AssetDatabase.SaveAssets();
            }
        }

        #region Drag and Drop Support

        private void SetupDragAndDrop()
        {
            RegisterCallback<DragEnterEvent>(OnDragEnter);
            RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragExitedEvent>(OnDragExited);
            RegisterCallback<DragLeaveEvent>((e)=>OnDragExited(null));
        }

        private void OnDragEnter(DragEnterEvent e)
        {
            OnDragEnter(e.mousePosition, e.ctrlKey);
        }

        private void OnDragEnter(Vector2 mousePosition, bool ctrlKey)
        {
            MoveMouseTip(mousePosition);

            var dragData = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;
            _dragging = false;

            if (dragData != null)
            {
                // Handle drag from exposed parameter view
                if (dragData.OfType<UdonParameterField>().Any())
                {
                    _dragging = true;
                    SetMouseTip(ctrlKey ? 
                        "Set Variable" : 
                        "Get Variable\n+Ctrl: Set Variable"
                    );
                }
            }

            if (DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences[0] != null)
            {
                var target = DragAndDrop.objectReferences[0];
                switch (target)
                {
                    case GameObject g:
                    case Component c:
                    {
                        string type = GetDefinitionNameForType(target.GetType());
                        if (UdonEditorManager.Instance.GetNodeDefinition(type) != null)
                        {
                            _dragging = true;
                        }
                        break;
                    }
                }
            }

            if (_dragging)
            {
                DragAndDrop.visualMode = ctrlKey ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Copy;
            }
        }

        private void OnDragUpdated(DragUpdatedEvent e)
        {
            if (_dragging)
            {
                MoveMouseTip(e.mousePosition);
                DragAndDrop.visualMode = e.ctrlKey ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Copy;
            }
            else
            {
                OnDragEnter(e.mousePosition, e.ctrlKey);
            }
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            if (!_dragging) return;
            var graphMousePosition = this.contentViewContainer.WorldToLocal(e.mousePosition);
            var draggedVariables = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;

            if (draggedVariables != null)
            {
                // Handle Drop of Variables
                var parameters = draggedVariables.OfType<UdonParameterField>();
                if (parameters.Any())
                {
                    RefreshVariables();
                    var nodes = new List<UdonNode>();
                    foreach (var parameter in parameters)
                    {
                        // Make Setter if ctrl is held, otherwise make Getter
                        UdonNode udonNode = MakeVariableNode(parameter.Data.uid, graphMousePosition, !e.ctrlKey);
                        AddElement(udonNode);
                    }

                    ReSerializeData();
                }
            }

            // Handle Drop of single GameObjects and Assets
            if (DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences[0] != null)
            {
                var target = DragAndDrop.objectReferences[0];
                switch (target)
                {
                    case Component c:
                        SetupDraggedObject(c, graphMousePosition);
                    break;

                    case GameObject g:
                        SetupDraggedObject(g, graphMousePosition);
                    break;
                }
            }

            _dragging = false;
        }

        private void OnDragExited(DragExitedEvent e)
        {
            SetMouseTip("");
            _dragging = false;
        }

        #endregion

        public UdonNode MakeVariableNode(string selectedUid, Vector2 graphMousePosition, bool isGetter)
        {
            string defName = isGetter ? "Get_Variable" : "Set_Variable";
            var definition = UdonEditorManager.Instance.GetNodeDefinition(defName);
            var nodeData = this.graphData.AddNode(definition.fullName);
            nodeData.nodeValues = new SerializableObjectContainer[1];
            nodeData.nodeUIDs = new string[1];
            nodeData.nodeValues[0] = SerializableObjectContainer.Serialize(selectedUid);
            nodeData.position = graphMousePosition;

            var udonNode = UdonNode.CreateNode(nodeData, this);
            return udonNode;
        }

        public string GetUnusedVariableNameLike(string newVariableName)
        {
            RefreshVariables();

            while (GetVariableNames.Contains(newVariableName))
            {
                char lastChar = newVariableName[newVariableName.Length - 1];
                if(char.IsDigit(lastChar))
                {
                    string newLastChar = (int.Parse(lastChar.ToString()) + 1).ToString();
                    newVariableName = newVariableName.Substring(0, newVariableName.Length - 1) + newLastChar;
                } 
                else
                {
                    newVariableName = $"{newVariableName}_1";   
                }
            }
            return newVariableName;
        }

        private void SetMouseTip(string message)
        {
            if (mouseTipContainer.visible)
            {
                mouseTip.text = message;
            }
        }

        private void LinkAfterCompile(string variableName, object target)
        {
            UdonAssemblyProgramAsset.AssembleDelegate listener = null;
            listener = (success, assembly) =>
            {
                if (!success) return;

                //TODO: get actual variable name in case it was auto-changed on add
                var result = _udonBehaviour.publicVariables.TrySetVariableValue(variableName, target);
                if (result)
                {
                    graphProgramAsset.OnAssemble -= listener;
                }
            };

            graphProgramAsset.OnAssemble += listener;
            EditorUtility.SetDirty(graphProgramAsset);
            AssetDatabase.SaveAssets();
            graphProgramAsset.RefreshProgram();
        }

        private string GetDefinitionNameForType(Type t)
        {
            string variableType = $"Variable_{t}".SanitizeVariableName();
            variableType = variableType.Replace("UdonBehaviour", "CommonInterfacesIUdonEventReceiver");
            return variableType;
        }

        private void SetupDraggedObject(UnityEngine.Object o, Vector2 graphMousePosition)
        {
            // Ensure variable type is allowed
            
            // create new Component variable and add to graph
            string variableType = GetDefinitionNameForType(o.GetType());
            string variableName = GetUnusedVariableNameLike(o.name.SanitizeVariableName());

            SetMouseTip($"Made {variableName}");

            string uid = AddNewVariable(variableType, variableName, true);
            RefreshVariables();

            object target = o;
            // Cast component to expected type
            if (o is Component) target = Convert.ChangeType(o, o.GetType());
            var variableNode = MakeVariableNode(uid, graphMousePosition, true);
            AddElement(variableNode);
            variableNode.Reserialize();

            LinkAfterCompile(variableName, target);
        }

        [Serializable]
        public class ViewTransformData
        {
            public Vector2 position = Vector2.zero;
            public float scale = 1f;
        }
    }
}
