using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonProgramSourceView : VisualElement
    {
        private UdonProgramAsset _asset;

        private VisualContainer _assemblyContainer;
        private ScrollView _scrollView;
        private Label _assemblyHeader;
        private TextElement _assemblyField;

        public UdonProgramSourceView ()
        {
            // Create and add container and children to display latest Assembly
            _assemblyContainer = new VisualContainer() { name = "Container", };
            _assemblyHeader = new Label("Udon Assembly")
            {
                name = "Header",
            };

            _scrollView = new ScrollView();

            _assemblyField = new TextElement()
            {
                name = "AssemblyField",
            };

            _assemblyContainer.Add(_assemblyHeader);
            _assemblyContainer.Add(_scrollView);
            _assemblyContainer.Add(_assemblyField);
            _scrollView.SetContents(_assemblyField);

            Add(_assemblyContainer);
        }

        public void LoadAsset(UdonGraphProgramAsset asset)
        {
            _asset = asset;
        }

        public void SetText(string newValue)
        {
            _assemblyField.text = newValue;
        }

        public void Unload()
        {
            if(_asset != null)
            {
                _asset = null;
            }
        }
    }
}
