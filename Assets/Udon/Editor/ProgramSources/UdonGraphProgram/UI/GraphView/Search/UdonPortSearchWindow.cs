using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;
using System.Collections.Generic;
using System.Linq;
using System;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{

    public class UdonPortSearchWindow : UdonSearchWindowBase
    {

        internal List<SearchTreeEntry> _fullRegistry;

        #region ISearchWindowProvider

        public Type typeToSearch;
        public Port startingPort;
        public Direction direction;

        public class VariableInfo
        {
            public string uid;
            public bool isGetter;

            public VariableInfo(string uid, bool isGetter)
            {
                this.uid = uid;
                this.isGetter = isGetter;
            }
        }

        override public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            _fullRegistry = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent($"{direction.ToString()} Search"), 0)
            };

            var defsToAdd = new Dictionary<string, List<UdonNodeDefinition>>();
            var registries = UdonEditorManager.Instance.GetNodeRegistries();
            foreach (var item in registries)
            {
                var definitions = item.Value.GetNodeDefinitions().ToList();

                var registryName = item.Key.FriendlyNameify().Replace("NodeRegistry", "");
                defsToAdd.Add(registryName, new List<UdonNodeDefinition>());

                foreach (var def in definitions)
                {
                    var collection = direction == Direction.Input ? def.Inputs : def.Outputs;
                    if(collection.Any(p=>p.type == typeToSearch))
                    {
                        defsToAdd[registryName].Add(def);
                    }
                }
            }

            var variables = _graphView.GetVariableNodes;

            // Add Getters and Setters for matched variable types
            Texture2D icon = EditorGUIUtility.FindTexture("GameManager Icon");
            string typeToSearchSimple = typeToSearch.ToString().Replace(".", "");
            foreach (var item in variables)
            {
                string variableSimpleName = item.fullName.Replace("Variable_", "");
                string getOrSet = direction == Direction.Output ? "Get" : "Set";
                if(variableSimpleName == typeToSearchSimple)
                {
                    string customVariableName = item.nodeValues[1].Deserialize().ToString();
                    _fullRegistry.Add(new SearchTreeEntry(new GUIContent($"{getOrSet} {customVariableName}", icon))
                    {
                        level = 1,
                        userData = new VariableInfo(item.uid, direction == Direction.Output),
                    });
                }
            }

            foreach (var item in defsToAdd)
            {
                // Skip empty lists
                if (item.Value.Count == 0) continue;

                _fullRegistry.Add(new SearchTreeGroupEntry(new GUIContent(item.Key), 1));
                AddEntries(_fullRegistry, item.Value, 2);
            }

            return _fullRegistry;
        }

        override public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            UdonNode node;
            // checking type so we can support selecting registries as well
            if (entry.userData is UdonNodeDefinition && !_graphView.IsDuplicateEventNode((entry.userData as UdonNodeDefinition).fullName))
            {
                node = UdonNode.CreateNode(entry.userData as UdonNodeDefinition, _graphView);
                _graphView.AddElement(node);
                var position = GetGraphPositionFromContext(context);
                position.x -= 140; // this offset is added for the search window, remove it for the node
                node.SetPosition(new Rect(position, Vector2.zero));
                node.Select(_graphView, false);
                var collection = direction == Direction.Input ? node.portsIn : node.portsOut;
                var port = collection.FirstOrDefault(p => p.Value.portType == typeToSearch).Value;
                if(port != null)
                {
                    var e = startingPort.ConnectTo(port);
                    _graphView.AddElement(e);
                }

                // Do we need to do this to reserialize, etc?
                _graphView.ReSerializeData();
                return true;
            }
            else if(entry.userData is VariableInfo)
            {
                var data = entry.userData as VariableInfo;
                var position = GetGraphPositionFromContext(context);
                position.x -= 140; // this offset is added for the search window, remove it for the node

                UdonNode udonNode = _graphView.MakeVariableNode(data.uid, position, data.isGetter);
                _graphView.AddElement(udonNode);
                var collection = direction == Direction.Input ? udonNode.portsIn : udonNode.portsOut;
                var port = collection.First().Value;
                if (port != null)
                {
                    var e = startingPort.ConnectTo(port);
                    _graphView.AddElement(e);
                }
                _graphView.ReSerializeData();
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion

    }
}