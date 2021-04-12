﻿using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;
using System.Collections.Generic;
using VRC.Udon.Graph.Interfaces;
using System.Linq;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{

    public class UdonFullSearchWindow : UdonSearchWindowBase
    {

        static private List<SearchTreeEntry> _slowRegistryCache;
        #region ISearchWindowProvider

        override public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (_slowRegistryCache != null && _slowRegistryCache.Count > 0) return _slowRegistryCache;

            _slowRegistryCache = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Full Search"), 0)
            };

            var topRegistries = UdonEditorManager.Instance.GetTopRegistries();

            Texture2D icon = null;
            var groupEntries = new Dictionary<string, SearchTreeGroupEntry>();
            foreach (var topRegistry in topRegistries)
            {
                string topName = topRegistry.Key.Replace("NodeRegistry", "");

                if (topName != "Udon")
                {
                    _slowRegistryCache.Add(new SearchTreeGroupEntry(new GUIContent(topName), 1));
                }

                // get all registries, save into registryName > INodeRegistry Lookup
                var subRegistries = new Dictionary<string, INodeRegistry>();
                foreach (KeyValuePair<string, INodeRegistry> registry in topRegistry.Value.OrderBy(s => s.Key))
                {
                    string baseRegistryName = registry.Key.Replace("NodeRegistry", "").FriendlyNameify().ReplaceFirst(topName, "");
                    string registryName = baseRegistryName.UppercaseFirst();
                    subRegistries.Add(registryName, registry.Value);
                }

                // Go through each registry entry and add the top-level registry and associated array registry
                foreach (KeyValuePair<string, INodeRegistry> regEntry in subRegistries)
                {
                    INodeRegistry registry = regEntry.Value;
                    string registryName = regEntry.Key;

                    int level = 2;
                    // Special cases for Udon sub-levels, added at top
                    if (topName == "Udon")
                    {
                        level = 1;
                        if (registryName == "Event" || registryName == "Type")
                        {
                            registryName = $"{registryName}s";
                        }
                    }

                    if (!registryName.EndsWith("[]"))
                    {
                        // add Registry Level
                        var groupEntry = new SearchTreeGroupEntry(new GUIContent(registryName, icon), level) { userData = registry };
                        _slowRegistryCache.Add(groupEntry);
                    }

                    // Check for Array Type first
                    string regArrayType = $"{registryName}[]";
                    if (subRegistries.TryGetValue(regArrayType, out INodeRegistry arrayRegistry))
                    {
                        // we have a matching subRegistry, add that next
                        var arrayLevel = level + 1;
                        var arrayGroupEntry = new SearchTreeGroupEntry(new GUIContent(regArrayType, icon), arrayLevel) { userData = registry };
                        _slowRegistryCache.Add(arrayGroupEntry);

                        // Add all array entries
                        AddEntriesForRegistry(_slowRegistryCache, arrayRegistry, arrayLevel + 1);
                    }

                    AddEntriesForRegistry(_slowRegistryCache, registry, level + 1, true);

                }
            }
            return _slowRegistryCache;
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

        #endregion

    }
}