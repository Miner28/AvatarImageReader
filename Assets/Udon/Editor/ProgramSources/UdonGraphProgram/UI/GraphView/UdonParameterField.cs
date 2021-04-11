using EditorUI = UnityEditor.Experimental.UIElements;

using VRC.Udon.Graph;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonParameterField : EditorUI.GraphView.BlackboardField
    {
        private UdonGraph udonGraph;
        private UdonNodeData nodeData;
        public UdonNodeData Data => nodeData;

        public UdonParameterField(UdonGraph udonGraph, UdonNodeData nodeData)
        {
            this.udonGraph = udonGraph;
            this.nodeData = nodeData;

            // Get Definition or exit early
            UdonNodeDefinition definition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
            if (definition == null)
            {
                UnityEngine.Debug.LogWarning($"Couldn't create Parameter Field for {nodeData.fullName}");
                return;
            }

            this.text = (string)nodeData.nodeValues[(int)UdonParameterProperty.ValueIndices.name].Deserialize();
            this.typeText = UdonGraphExtensions.PrettyString(definition.name).FriendlyNameify();

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            this.Q("icon").AddToClassList("parameter-" + definition.type);
            this.Q("icon").visible = true;

            var textField = (TextField)this.Q("textField");
            textField.isDelayed = true;
        }

        void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", (a) => OpenTextEditor(), DropdownMenu.MenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", (a) => udonGraph.RemoveNodeData(nodeData), DropdownMenu.MenuAction.AlwaysEnabled);

            evt.StopPropagation();
        }

	}

}
