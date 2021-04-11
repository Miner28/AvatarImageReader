using System;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonMinimap : MiniMap
    {
        private CustomData _customData = new CustomData();
        private UdonGraph _graph;

        public UdonMinimap(UdonGraph graph)
        {
            _graph = graph;

            name = "UdonMap";
            maxWidth = 200;
            maxHeight = 100;
            anchored = false;
            SetPosition(_customData.layout);
        }

        private void SaveNewData()
        {
            if (!_graph.IsReloading)
            {
                _graph.SaveNewData();
            }
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

        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.Minimap, persistenceKey, JsonUtility.ToJson(_customData));
        }

        internal void LoadData(UdonGraphElementData data)
        {
            JsonUtility.FromJsonOverwrite(data.jsonData, _customData);
            SetPosition(_customData.layout);
            visible = _customData.visible;
        }

        public class CustomData
        {
            public bool visible = true;
            public Rect layout = new Rect(new Vector2(10, 20), Vector2.zero);
        }
    }
}