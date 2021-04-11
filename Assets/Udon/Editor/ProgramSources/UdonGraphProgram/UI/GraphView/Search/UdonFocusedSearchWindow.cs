using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;
using System.Collections.Generic;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{

    public class UdonFocusedSearchWindow : UdonSearchWindowBase
    {

        public INodeRegistry targetRegistry;
        internal List<SearchTreeEntry> _fullRegistry;

        #region ISearchWindowProvider

        override public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {

            _fullRegistry = new List<SearchTreeEntry>();

            Texture2D icon = EditorGUIUtility.FindTexture("cs Script Icon");

            var registryName = GetSimpleNameForRegistry(targetRegistry);
            _fullRegistry.Add(new SearchTreeGroupEntry(new GUIContent($"{registryName} Search"), 0));

            // add Registry Level
            AddEntriesForRegistry(_fullRegistry, targetRegistry, 1, true);

            return _fullRegistry;
        }

        override public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            // should check for adding duplicate event nodes here, look at Legacy.UdonGraph.TryCreateNodeInstance
            // Assuming that if we've made it to this point we're allowed to add the node

            UdonNode node;
            // checking type so we can support selecting registries as well
            if (entry.userData is UdonNodeDefinition && !_graphView.IsDuplicateEventNode((entry.userData as UdonNodeDefinition).fullName))
            {
                node = UdonNode.CreateNode(entry.userData as UdonNodeDefinition, _graphView);
                _graphView.AddElement(node);
                node.SetPosition(new Rect(GetGraphPositionFromContext(context), Vector2.zero));
                node.Select(_graphView, false);

                // Do we need to do this to reserialize, etc?
                _graphView.ReSerializeData();
                return true;
            }
            else
            {
                return false;
            }
        }

        // TODO: move this to Extension
        private string GetSimpleNameForRegistry(INodeRegistry registry)
        {
            string registryName = registry.ToString().Replace("NodeRegistry", "").FriendlyNameify();
            registryName = registryName.Substring(registryName.LastIndexOf(".") + 1);
            registryName = registryName.Replace("UnityEngine", "");
            return registryName;
        }

        #endregion

    }
}