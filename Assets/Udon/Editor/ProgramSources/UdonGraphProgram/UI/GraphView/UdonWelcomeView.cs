using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{

    public class UdonWelcomeView : VisualElement
    {
        private Button _openLastGraphButton;

        public UdonWelcomeView()
        {
            name = "udon-welcome";
            this.RegisterCallback<AttachToPanelEvent>(Initialize);
        }


        private void Initialize(AttachToPanelEvent evt)
        {
            // switch event to do some UI updates instead of initialization from here on out
            UnregisterCallback<AttachToPanelEvent>(Initialize);
        //    this.RegisterCallback<AttachToPanelEvent>(OnAttach);

            // Add Header
            Add(new TextElement()
            {
                name = "intro",
                text = "Udon Graph",
            });

            Add(new TextElement()
            {
                name = "header-message",
                text = "The Udon Graph is your gateway to creating amazing things in VRChat.\nCheck out the Readme and UdonExampleScene in the VRChat Examples folder to get started."
            });

            var mainContainer = new VisualElement()
            {
                name = "main",
            };

            Add(mainContainer);

            var template = EditorGUIUtility.Load("Assets/Udon/Editor/Resources/UdonChangelog.uxml") as VisualTreeAsset;
            var changelog = template.CloneTree(null);
            changelog.name = "changelog";
            mainContainer.Add(changelog);

            var column2 = new VisualContainer() { name = "column-2" };
            mainContainer.Add(column2);

            // Add Button for Last Graph
            if (!string.IsNullOrEmpty(Settings.LastGraphGuid))
            {
                _openLastGraphButton = new Button(() =>
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(Settings.LastGraphGuid);
                    var graphName = assetPath.Substring(assetPath.LastIndexOf("/") + 1).Replace(".asset", "");

                    // Find actual asset from guid
                    var asset = AssetDatabase.LoadAssetAtPath<UdonGraphProgramAsset>(assetPath);
                    if (asset != null)
                    {
                        var w = EditorWindow.GetWindow<UdonGraphWindow>("Udon Graph", true, typeof(SceneView));
                        // get reference to saved UdonBehaviour if possible
                        UdonBehaviour udonBehaviour = null;
                        string gPath = Settings.LastUdonBehaviourPath;
                        string sPath = Settings.LastUdonBehaviourScenePath;
                        if (!string.IsNullOrEmpty(gPath) && !string.IsNullOrEmpty(sPath))
                        {
                            var targetScene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(sPath);
                            if (targetScene != null && targetScene.isLoaded && targetScene.IsValid())
                            {
                                var targetObject = UnityEngine.GameObject.Find(gPath);
                                if (targetObject != null)
                                {
                                    udonBehaviour = targetObject.GetComponent<UdonBehaviour>();
                                }
                            }
                        }

                        // Initialize graph with restored udonBehaviour or null if not found / not saved
                        w.InitializeGraph(asset, udonBehaviour);
                    }
                });

                UpdateLastGraphButtonLabel();
                column2.Add(_openLastGraphButton);
            }

            var settingsTemplate = EditorGUIUtility.Load("Assets/Udon/Editor/Resources/UdonSettings.uxml") as VisualTreeAsset;
            var settings = settingsTemplate.CloneTree(null);
            settings.name = "settings";
            column2.Add(settings);

            // get reference to first settings section
            var section = settings.Q("section");

            // Add Grid Snap setting
            var gridSnapContainer = new VisualElement();
            gridSnapContainer.AddToClassList("settings-item-container");
            var gridSnapField = new IntegerField(3)
            {
                value = Settings.GridSnapSize
            };
            gridSnapField.OnValueChanged(e =>
            {
                Settings.GridSnapSize = e.newValue;
            });
            gridSnapContainer.Add(new Label("Grid Snap Size"));
            gridSnapContainer.Add(gridSnapField);
            section.Add(gridSnapContainer);
            var gridSnapLabel = new Label("Snap elements to a grid as you move them. 0 for No Snapping.");
            gridSnapLabel.AddToClassList("settings-label");
            section.Add(gridSnapLabel);

            // Add Search On Selected Node settings
            var searchOnSelectedNode = (new Toggle()
            {
                text = "Focus Search On Selected Node",
                value = Settings.SearchOnSelectedNodeRegistry,
            });
            searchOnSelectedNode.OnValueChanged((toggleEvent) =>
            {
                Settings.SearchOnSelectedNodeRegistry = toggleEvent.newValue;
            });
            section.Add(searchOnSelectedNode);
            var searchOnLabel = new Label("Highlight a node and press Spacebar to open a Search Window focused on nodes for that type. ");
            searchOnLabel.AddToClassList("settings-label");
            section.Add(searchOnLabel);

            // Add Search On Noodle Drop settings
            var searchOnNoodleDrop = (new Toggle()
            {
                text = "Search On Noodle Drop",
                value = Settings.SearchOnNoodleDrop,
            });
            searchOnNoodleDrop.OnValueChanged((toggleEvent) =>
            {
                Settings.SearchOnNoodleDrop = toggleEvent.newValue;
            });
            section.Add(searchOnNoodleDrop);
            var searchOnDropLabel = new Label("Drop a noodle into empty space to search for anything that can be connected.");
            searchOnDropLabel.AddToClassList("settings-label");
            section.Add(searchOnDropLabel);

            // Add UseNeonStyle setting
            var useNeonStyle = (new Toggle()
            {
                text = "Use Neon Style",
                value = Settings.UseNeonStyle,
            });
            useNeonStyle.OnValueChanged((toggleEvent) =>
            {
                Settings.UseNeonStyle = toggleEvent.newValue;
            });
            section.Add(useNeonStyle);
            var useNeonStyleLabel = new Label("Try out an experimental Neon Style. We will support User Styles in an upcoming version.");
            useNeonStyleLabel.AddToClassList("settings-label");
            section.Add(useNeonStyleLabel);

        }

        private void UpdateLastGraphButtonLabel()
        {
            if (_openLastGraphButton == null) return;

            string currentButtonAssetGuid = (string)_openLastGraphButton.userData;
            if (string.Compare(currentButtonAssetGuid, Settings.LastGraphGuid) != 0)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(Settings.LastGraphGuid);
                var graphName = assetPath.Substring(assetPath.LastIndexOf("/") + 1).Replace(".asset", "");

                _openLastGraphButton.userData = Settings.LastGraphGuid;
                _openLastGraphButton.text = $"Open {graphName}";
            }
        }

        private void OnAttach(AttachToPanelEvent evt)
        {
            UpdateLastGraphButtonLabel();
        }
    }
}