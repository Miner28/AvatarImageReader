using System;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonVariablesBlackboard : Blackboard, IUdonGraphElementDataProvider
    {
        private CustomData _customData = new CustomData();
        private UdonGraph _graph;

        public UdonVariablesBlackboard(UdonGraph graph)
        {
            _graph = graph;
            title = "Variables";
            name = "Parameters";
            scrollable = true;

            // Remove subtitle
            var subtitle = this.Query<Label>("subTitleLabel").AtIndex(0);
            if (subtitle != null)
            {
                subtitle.RemoveFromHierarchy();
            }

            // Improve resizer UI
            style.borderBottomWidth = 1;

            var resizer = this.Q(null, "resizer");
            if (resizer != null)
            {
                resizer.style.paddingTop = 0;
                resizer.style.paddingLeft = 0;
            }

            SetPosition(_customData.layout);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            _customData.visible = value;
            SaveNewData();
        }

        public override void UpdatePresenterPosition()
        {
            _customData.layout = GetPosition();
            SaveNewData();
        }

        private void SaveNewData()
        {
            if (!_graph.IsReloading)
            {
                _graph.SaveNewData();
            }
        }

        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.VariablesWindow, persistenceKey, JsonUtility.ToJson(_customData));
        }

        public class CustomData {
            public bool visible = true;
            public Rect layout = new Rect(10, 130, 200, 150);
        }

        internal void LoadData(UdonGraphElementData data)
        {
            JsonUtility.FromJsonOverwrite(data.jsonData, _customData);
            SetPosition(_customData.layout);
            this.visible = _customData.visible;
        }
    }

}