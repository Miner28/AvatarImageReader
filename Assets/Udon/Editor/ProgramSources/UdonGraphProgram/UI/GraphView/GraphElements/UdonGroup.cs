using System;

using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;
using System.Collections.Generic;
using UnityEditor;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{

    public class UdonGroup : Group, IUdonGraphElementDataProvider
    {

        public string uid;
        private CustomData _customData = new CustomData();
        private UdonGraph _graph;

        public static UdonGroup Create(string value, Rect position, UdonGraph graph)
        {
            var group = new UdonGroup("", graph);

            group.uid = Guid.NewGuid().ToString();

            // make sure rect size is not 0
            position.width = position.width > 0 ? position.width : 128;
            position.height = position.height > 0 ? position.height : 128;

            group._customData.uid = group.uid;
            group._customData.layout = position;
            group._customData.title = value;

            return group;
        }

        public static UdonGroup Create(UdonGraphElementData elementData, UdonGraph graph)
        {
            return new UdonGroup(elementData.jsonData, graph);
        }

        // current order of operations issue when creating a group from the context menu means this isn't set until first save. This allows us to force it.
        public void UpdateDataId()
        {
            _customData.uid = uid;
        }

        // Build a Group from jsonData, save to userData
        private UdonGroup(string jsonData, UdonGraph graph)
        {
            title = "Group";
            _graph = graph;

            if (!string.IsNullOrEmpty(jsonData))
            {
                EditorJsonUtility.FromJsonOverwrite(jsonData, _customData);
            }
        }

        public override void OnPersistentDataReady()
        {
            base.OnPersistentDataReady();
            if(_customData != null)
            {
                // Propagate data to useful places
                title = _customData.title;
                layer = _customData.layer;
                if (string.IsNullOrEmpty(_customData.uid))
                {
                    _customData.uid = Guid.NewGuid().ToString();
                }
                uid = _customData.uid;

                SetPosition(_customData.layout);

                // Add all elements from graph to self
                var childUIDs = _customData.containedElements;
                if (childUIDs != null)
                {
                    foreach (var item in childUIDs)
                    {
                        var childNode = _graph.GetElementByGuid(item);
                        if (childNode != null)
                        {
                            if (!ContainsElement(childNode))
                            {
                                AddElement(childNode);
                                // nodes can wind up behind the group since they're added first
                                childNode.BringToFront();
                            }
                        }
                    }
                }
            }
        }

        public bool containsElement(string uid)
        {
            return _customData.containedElements.Contains(uid);
        }

        public override void SetPosition(Rect newPos)
        {
            newPos = GraphElementExtension.GetSnappedRect(newPos);
            base.SetPosition(newPos);
        }

        // Save data to asset after new position set
        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            _customData.layout = GraphElementExtension.GetSnappedRect(GetPosition());
            this.SaveNewData();
        }

        // Save data to asset after rename
        protected override void OnGroupRenamed(string oldName, string newName)
        {
            // limit name to 100 characters
            title = newName.Substring(0, Mathf.Min(newName.Length, 100));
            _customData.title = title;
            this.SaveNewData();
        }

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            base.OnElementsAdded(elements);
            foreach (var element in elements)
            {
                if (!_customData.containedElements.Contains(element.persistenceKey))
                {
                    // don't add StackNode children directly to group, messes up reload
                    if(!(element.parent is UdonStackNode))
                    {
                        _customData.containedElements.Add(element.persistenceKey);
                    }
                }

                // Set group variable on UdonNodes
                if (element is UdonNode)
                {
                    (element as UdonNode).group = this;
                }
                if (element is UdonComment)
                {
                    (element as UdonComment).group = this;
                }
            }
            this.SaveNewData();
        }

        protected override void OnElementsRemoved(IEnumerable<GraphElement> elements)
        {
            base.OnElementsRemoved(elements);
            foreach (var element in elements)
            {
                if (_customData.containedElements.Contains(element.persistenceKey))
                {
                    _customData.containedElements.Remove(element.persistenceKey);
                    if(element is UdonNode)
                    {
                        (element as UdonNode).group = null;
                    }
                    else if (element is UdonComment)
                    {
                        (element as UdonComment).group = null;
                    }
                }
            }
            this.SaveNewData();
        }

        public override bool AcceptsElement(GraphElement element, ref string reasonWhyNotAccepted)
        {
            return base.AcceptsElement(element, ref reasonWhyNotAccepted);
        }

        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.UdonGroup, uid, EditorJsonUtility.ToJson(_customData));
        }

        public class CustomData
        {
            public string uid;
            public Rect layout;
            public List<string> containedElements = new List<string>();
            public string title;
            public int layer;
            public Color elementTypeColor;
        }
    }
}