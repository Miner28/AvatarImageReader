using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;
using EditorUI = UnityEditor.Experimental.UIElements;
using EngineUI = UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonParameterProperty : EngineUI.VisualElement
    {
        protected UdonGraph graph;
        protected UdonNodeData nodeData;
        protected UdonNodeDefinition definition;

        //public ExposedParameter parameter { get; private set; }

        public EngineUI.Toggle isPublic { get; private set; }
        public EngineUI.Toggle isSynced { get; private set; }
        public EngineUI.VisualElement defaultValueContainer { get; private set; }
        public EditorUI.PopupField<string> syncField { get; private set; }
        private EngineUI.VisualElement _inputField;

        public enum ValueIndices
        {
            value = 0,
            name = 1,
            isPublic = 2,
            isSynced = 3,
            syncType = 4
        }

        private static SerializableObjectContainer[] GetDefaultNodeValues()
        {
            return new[]
            {
                SerializableObjectContainer.Serialize("", typeof(string)),
                SerializableObjectContainer.Serialize("newVariableName", typeof(string)),
                SerializableObjectContainer.Serialize(false, typeof(bool)),
                SerializableObjectContainer.Serialize(false, typeof(bool)),
                SerializableObjectContainer.Serialize("none", typeof(string))
            };
        }

        // 0 = Value, 1 = name, 2 = public, 3 = synced, 4 = syncType
        public UdonParameterProperty(UdonGraph graphView, UdonNodeDefinition definition, UdonNodeData nodeData)
        {
            this.graph = graphView;
            this.definition = definition;
            this.nodeData = nodeData;

            // Make sure the incoming nodeData has the right number of nodeValues (super old graphs didn't have sync info)
            if (this.nodeData.nodeValues.Length != 5)
            {
                this.nodeData.nodeValues = GetDefaultNodeValues();
                for (int i = 0; i < nodeData.nodeValues.Length; i++)
                {
                    this.nodeData.nodeValues[i] = nodeData.nodeValues[i];
                }
            }

            // Public Toggle
            isPublic = new EngineUI.Toggle
            {
                text = "public",
                value = (bool) GetValue(ValueIndices.isPublic)
            };
            isPublic.OnValueChanged(e => { SetNewValue(e.newValue, ValueIndices.isPublic); });
            Add(isPublic);

            // Is Synced Field
            isSynced = new EngineUI.Toggle
            {
                text = "synced",
                value = (bool) GetValue(ValueIndices.isSynced),
            };

            isSynced.OnValueChanged(e =>
            {
                SetNewValue(e.newValue, ValueIndices.isSynced);
                syncField.visible = e.newValue;
            });
            Add(isSynced);

            // Sync Field, add to isSynced
            List<string> choices = new List<string>()
            {
                "none", "linear", "smooth"
            };
            syncField = new EditorUI.PopupField<string>(choices, 0)
            {
                visible = isSynced.value
            };
            syncField.OnValueChanged(e => { SetNewValue(e.newValue, ValueIndices.syncType); });
            isSynced.Add(syncField);

            // Container to show/edit Default Value
            var friendlyName = UdonGraphExtensions.FriendlyTypeName(definition.type).FriendlyNameify();
            defaultValueContainer = new EngineUI.VisualElement
            {
                new EngineUI.Label("default value") {name = "default-value-label"}
            };

            // Generate Default Value Field
            var value = TryGetValueObject(out object result);
            _inputField = UdonFieldFactory.CreateField(
                definition.type,
                result,
                newValue => SetNewValue(newValue, ValueIndices.value)
            );
            if (_inputField != null)
            {
                defaultValueContainer.Add(_inputField);
                Add(defaultValueContainer);
            }
        }

        private object GetValue(ValueIndices index)
        {
            if ((int) index >= nodeData.nodeValues.Length)
            {
                Debug.LogWarning($"Can't get {index} from {definition.name} variable");
                return null;
            }

            return nodeData.nodeValues[(int) index].Deserialize();
        }

        private bool TryGetValueObject(out object result)
        {
            result = null;

            var container = nodeData.nodeValues[0];
            if (container == null)
            {
                return false;
            }

            result = container.Deserialize();
            if (result == null)
            {
                return false;
            }

            return true;
        }

        private void SetNewValue(object newValue, ValueIndices index)
        {
            nodeData.nodeValues[(int) index] = SerializableObjectContainer.Serialize(newValue);
            graph.ReSerializeData();
            graph.SaveGraphToDisk();
        }

        // Convenience wrapper for field types that don't need special initialization
        private EngineUI.VisualElement SetupField<TField, TType>()
            where TField : EngineUI.VisualElement, EngineUI.INotifyValueChanged<TType>, new()
        {
            var field = new TField();
            return SetupField<TField, TType>(field);
        }

        // Works for any TextValueField types, needs to know fieldType and object type
        private EngineUI.VisualElement SetupField<TField, TType>(TField field)
            where TField : EngineUI.VisualElement, EngineUI.INotifyValueChanged<TType>
        {
            field.AddToClassList("portField");
            if (TryGetValueObject(out object result))
            {
                field.value = (TType) result;
            }

            field.OnValueChanged((e) => SetNewValue(e.newValue, ValueIndices.value));
            _inputField = field;
            return _inputField;
        }
    }
}