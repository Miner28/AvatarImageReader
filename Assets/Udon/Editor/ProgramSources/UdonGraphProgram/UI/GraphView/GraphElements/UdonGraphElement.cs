using System;

using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonGraphElement : GraphElement
    {
        // Upgrade note - persistenceKey turns into viewDataKey in Unity 2019, this getter will make that transition easier
        public string uid { get => persistenceKey; set => persistenceKey = value; }
        internal UdonGraphElementType type = UdonGraphElementType.GraphElement;

        internal UdonGraphElement()
        {

        }

        // save this update to the asset
        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            this.Reserialize();
        }

    }

    public interface IUdonGraphElementDataProvider
    {
        UdonGraphElementData GetData();
    }

    [Serializable]
    public class GraphRect
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public GraphRect(Rect input)
        {
            this.x = Mathf.Round(input.x);
            this.y = Mathf.Round(input.y);
            this.width = Mathf.Round(input.width);
            this.height = Mathf.Round(input.height);
        }

        public Rect rect => new Rect(this.x, this.y, this.width, this.height);
    }
}