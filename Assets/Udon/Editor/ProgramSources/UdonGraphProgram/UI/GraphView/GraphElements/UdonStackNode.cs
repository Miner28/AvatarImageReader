using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;
using EditorUI = UnityEditor.Experimental.UIElements;

using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Graph;
using UnityEngine.Experimental.UIElements;
using UnityEditor;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{

    public class UdonStackNode : StackNode, IEdgeConnectorListener, IUdonGraphElementDataProvider
    {
        private UdonGraph _graph;
        private INodeRegistry _registry;
        public UdonPort refPort;
        private CustomData _customData = new CustomData();

        public UdonGraph Graph { get => _graph; private set { } }

        // Upgrade note - persistenceKey turns into viewDataKey in Unity 2019, this getter will make that transition easier
        public string uid { get => persistenceKey; set => persistenceKey = value; }

        // Called when restoring from asset
        public static UdonStackNode Create(UdonGraphElementData elementData, UdonGraph graph)
        {
            return new UdonStackNode(elementData.jsonData, graph);
        }

        // Called when creating from Search
        public static UdonStackNode Create(INodeRegistry registry, Vector2 position, UdonGraph graph)
        {
            var stackNode = new UdonStackNode("", graph);
            string registryName = registry.ToString();
            registryName = registryName.Substring(registryName.LastIndexOf('.') + 1);
            stackNode._customData.registryName = registryName;
            stackNode._customData.layout = new Rect(position, Vector2.zero);
            return stackNode;
        }

        private string GetSimpleNameForRegistry(INodeRegistry registry)
        {
            string registryName = registry.ToString().Replace("NodeRegistry", "").FriendlyNameify();
            registryName = registryName.Substring(registryName.LastIndexOf(".") + 1);
            registryName = registryName.Replace("UnityEngine", "");
            return registryName;
        }

        private UdonStackNode(string jsonData, UdonGraph graph)
        {
            _graph = graph;

            if (!string.IsNullOrEmpty(jsonData))
            {
                EditorJsonUtility.FromJsonOverwrite(jsonData, _customData);
            }
        }

        private bool _initialized = false;
        public override void OnPersistentDataReady()
        {
            if (_initialized) return;

            if(_customData != null)
            {
                // Exit early if we can't find the registry
                if (!UdonEditorManager.Instance.TryGetRegistry(_customData.registryName, out _registry))
                {
                    Debug.LogError($"Couldn't make Stack Node from Registry {_customData.registryName}");
                    return;
                }

                _customData.title = GetSimpleNameForRegistry(_registry);
                layer = _customData.layer;
                if (string.IsNullOrEmpty(_customData.uid))
                {
                    _customData.uid = Guid.NewGuid().ToString();
                }
                uid = _customData.uid;
                name = _customData.title;

                headerContainer.Add(new Label() { text = _customData.title });

                MakePorts(_registry);
                SetPosition(_customData.layout);
                UseDefaultStyling();

                // add all contained elements from graph to self
                var childUIDs = _customData.containedElements;
                if (childUIDs != null)
                {
                    foreach (var item in childUIDs)
                    {
                        var childNode = _graph.GetElementByGuid(item);
                        if (childNode != null)
                        {
                             AddElement(childNode);
                            // nodes can wind up behind the stacknode since they're added first
                            childNode.BringToFront();
                        }
                    }
                }

            }

            _initialized = true;
            this.SaveNewData();
        }

        private void MakePorts(INodeRegistry registry)
        {
            Dictionary<string, UdonNodeDefinition> objectMethods = new Dictionary<string, UdonNodeDefinition>();
           
            RefreshExpandedState();
            RefreshPorts();
        }

        public INodeRegistry GetRegistry()
        {
            return _registry;
        }

        public void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            foreach (var element in elements)
            {
                if (!_customData.containedElements.Contains(element.persistenceKey))
                {
                    _customData.containedElements.Add(element.persistenceKey);
                    Debug.Log($"StackNode {name} adding {element.name}");
                }
            }
            this.SaveNewData();
        }

        public void OnElementsRemoved(IEnumerable<GraphElement> elements)
        {
            foreach (var element in elements)
            {
                if (_customData.containedElements.Contains(element.persistenceKey))
                {
                    Debug.Log($"StackNode {name} removing {element.name}");
                    _customData.containedElements.Remove(element.persistenceKey);
                }
            }
            this.SaveNewData();
        }

        protected override bool AcceptsElement(GraphElement element, ref int proposedIndex, int maxIndex)
        {
            return true;
        }

        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            _customData.layout = GetPosition();
            this.SaveNewData();
        }

        #region IEdgeConnectorListener

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            Debug.Log($"OnDropOutside from {name}");
        }

        public void OnDrop(EditorUI.GraphView.GraphView graphView, Edge edge)
        {
            Debug.Log($"OnDrop from {name}");
        }

        #endregion

        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.UdonStackNode, uid, EditorJsonUtility.ToJson(_customData));
        }

        public class CustomData
        {
            public string uid;
            public Rect layout;
            public List<string> containedElements = new List<string>();
            public string registryName;
            public string title;
            public int layer;
            public Color elementTypeColor;
        }
    }
}