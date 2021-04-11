using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.UI;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;
using EditorUI = UnityEditor.Experimental.UIElements;
using EngineUI = UnityEngine.Experimental.UIElements;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    [Serializable]
    public class UdonPort : Port
    {
        public string FullName;
        private UdonNodeData _udonNodeData;
        private int _nodeValueIndex;

        private VisualElement _inputField;
        private VisualElement _inputFieldTypeLabel;

        private IArrayProvider _inspector;

        private bool _waitToReserialize = false;

        protected UdonPort(Orientation portOrientation, Direction portDirection, Capacity portCapacity, Type type) : base(portOrientation, portDirection, portCapacity, type)
        {
        }

        public static Port Create(string portName, Direction portDirection, IEdgeConnectorListener connectorListener, Type type, UdonNodeData data, int index, Orientation orientation = Orientation.Horizontal)
        {
            Capacity capacity = Capacity.Single;
            if(portDirection == Direction.Input && type == null || portDirection == Direction.Output && type != null)
            {
                capacity = Capacity.Multi;
            }
            var port = new UdonPort(orientation, portDirection, capacity, type)
            {
                m_EdgeConnector = new EdgeConnector<Edge>(connectorListener),
            };

            port.portName = portName;
            port._udonNodeData = data;
            port._nodeValueIndex = index;

            port.SetupPort();
            return port;
        }

        public int GetIndex()
        {
            return _nodeValueIndex;
        }

        private void SetupPort()
        {
            this.AddManipulator(m_EdgeConnector);

            tooltip = UdonGraphExtensions.FriendlyTypeName(portType);
            
            if (portType == null || direction == Direction.Output)
            {
                return;
            }

            if (TryGetValueObject(out object result, portType))
            {
                var field = UdonFieldFactory.CreateField(
                    portType,
                    result,
                    newValue => SetNewValue(newValue)
                );
            
                if(field != null)
                {
                    SetupField(field);
                }
            }

            if (_udonNodeData.fullName.StartsWith("Const"))
            {
                RemoveConnector();
            }

            UpdateLabel(connected);
        }

        // Made its own method for now as we have issues auto-converting between string and char in a TextField
        // TODO: refactor SetupField so we can do just the field.value part separately to combine with this
        private VisualElement SetupCharField()
        {
            TextField field = new TextField();
            field.AddToClassList("portField");
            if (TryGetValueObject(out object result))
            {
                field.value = UdonGraphExtensions.UnescapeLikeALiteral((char)result);
            }

            field.isDelayed = true;

            // Special handling for escaping char value
            field.OnValueChanged((e) =>
            {
                if(e.newValue[0] == '\\' && e.newValue.Length > 1)
                {
                    SetNewValue(UdonGraphExtensions.EscapeLikeALiteral(e.newValue.Substring(0, 2)));
                }
                else
                {
                    SetNewValue(e.newValue[0]);
                }
            });
            _inputField = field;

            // Add label, shown when input is connected. Not shown by default
            var friendlyName = UdonGraphExtensions.FriendlyTypeName(typeof(char)).FriendlyNameify();
            var label = new EngineUI.Label(friendlyName);
            _inputFieldTypeLabel = label;

            return _inputField;
        }

       private void SetupField(VisualElement field)
        {
            // Delay color fields so they don't break UI
            if(portType.IsAssignableFrom(typeof(EditorUI.ColorField)))
            {
                _waitToReserialize = true;
            }

            // Custom Event fields need their event names sanitized after input and their connectors removed
            if (_udonNodeData.fullName.CompareTo("Event_Custom") == 0)
            {
                var tfield = (TextField)field;
                tfield.OnValueChanged((e) =>
                {
                    string newValue = e.newValue.SanitizeVariableName();
                    tfield.value = newValue;
                    SetNewValue(newValue);
                });
                RemoveConnector();
            }

            // Add label, shown when input is connected. Not shown by default
            var friendlyName = UdonGraphExtensions.FriendlyTypeName(portType).FriendlyNameify();
            var label = new EngineUI.Label(friendlyName);
            _inputFieldTypeLabel = label;
            field.AddToClassList("portField");
            
            _inputField = field;
            Add(_inputField);
        }
        
        private void RemoveConnector()
        {
            this.Q("connector")?.RemoveFromHierarchy();
            this.Q(null, "connectorText")?.RemoveFromHierarchy();
        }

#pragma warning disable 0649 // variable never assigned
        private EngineUI.Button _editArrayButton;
        private void EditArray(Type elementType)
        {
            // Update Values when 'Save' is clicked
            if(_inspector != null)
            {
                // Update Values
                SetNewValue(_inspector.GetValues());

                // Remove Inspector
                _inspector.RemoveFromHierarchy();
                _inspector = null;

                // Update Button Text
                _editArrayButton.text = "Edit";
                return;
            }

            // Otherwise set up the inspector
            _editArrayButton.text = "Save";
            
            // Get value object, null is ok
            TryGetValueObject(out object value);

            // Create it new
            Type typedArrayInspector = (typeof(UdonArrayInspector<>)).MakeGenericType(elementType);
            _inspector = (Activator.CreateInstance(typedArrayInspector, value) as IArrayProvider);

            parent.Add(_inspector as VisualElement);
        }

        // Update elements on connect
        public override void Connect(Edge edge)
        {
            base.Connect(edge);

            // The below logic is just for Output ports
            if (edge.input.Equals(this)) return;

            // hide field, show label
            var input = ((UdonPort)edge.input);
            input.UpdateLabel(true);
            
            if (IsReloading())
            {
                return;
            }
            
            // update data
            if (portType == null)
            {
                // We are a flow port
                SetFlowUID(((UdonNode)input.node).uid);
            }
            else
            {
                // We are a value port, we need to send our info over to the OTHER node
                string myNodeUid = ((UdonNode)node).uid;
                input.SetDataFromNewConnection($"{myNodeUid}|{_nodeValueIndex}", input.GetIndex());
            }

            // in this case, we catch the method on the left node, with input as the right node
            SendReserializeEvent();
        }

        public override void OnStopEdgeDragging()
        {
            base.OnStopEdgeDragging();

            if (edgeConnector.edgeDragHelper.draggedPort == this)
            {
                if (capacity == Capacity.Single && connections.Count() > 0)
                {
                    // This port could only have one connection. Fixed in Reserialize, need to reload to show the change
                    this.Reload();
                }
            }
        }

        private void SetFlowUID(string newValue)
        {
            if (_udonNodeData.flowUIDs.Length <= _nodeValueIndex)
            {
                // If we don't have space for this flow value, create a new array
                // TODO: handle this elsewhere?
                var newFlowArray = new string[_nodeValueIndex + 1];
                for (int i = 0; i < _udonNodeData.flowUIDs.Length; i++)
                {
                    newFlowArray[i] = _udonNodeData.flowUIDs[i];
                }
                _udonNodeData.flowUIDs = newFlowArray;

                _udonNodeData.flowUIDs.SetValue(newValue, _nodeValueIndex);
            } 
            else
            {
                _udonNodeData.flowUIDs.SetValue(newValue, _nodeValueIndex);
            }
        }

        public bool IsReloading()
        {
            if(node is UdonNode)
            {
                return ((UdonNode)node).Graph.IsReloading;
            }
            else if(node is UdonStackNode)
            {
                return ((UdonStackNode)node).Graph.IsReloading;
            }
            else
            {
                return false;
            }
        }

        public void SetDataFromNewConnection(string uidAndPort, int index)
        {
            // can't do this for Reg stack nodes yet so skipping for demo
            if (_udonNodeData == null) return;

            if (_udonNodeData.nodeUIDs.Length <= _nodeValueIndex)
            {
                Debug.Log("Couldn't set it");
            }
            else
            {
                _udonNodeData.nodeUIDs.SetValue(uidAndPort, index);
            }
        }

        // Update elements on disconnect
        public override void Disconnect(Edge edge)
        {
            base.Disconnect(edge);

            // hide label, show field
            if(direction == Direction.Input)
            {
                UpdateLabel(false);
            }

            if (IsReloading())
            {
                return;
            }

            // update data
            if (direction == Direction.Output && portType == null)
            {
                // We are a flow port
                SetFlowUID("");
            }
            else if (direction == Direction.Input && portType != null)
            {
                // Direction is input
                // We are a value port
                SetDataFromNewConnection("", GetIndex());
            }

            // in this case, we catch the method on the left node, with input as the right node
            SendReserializeEvent();
        }

        public void UpdateLabel(bool isConnected)
        {
            // Port has a 'connected' bool but it doesn't seem to update, so passing 'isConnected' for now

            if (isConnected)
            {
                if (_inputField != null && Contains(_inputField))
                {
                    _inputField.RemoveFromHierarchy();
                }
                if (_inputFieldTypeLabel != null && !Contains(_inputFieldTypeLabel))
                {
                    Add(_inputFieldTypeLabel);
                }
                if(_editArrayButton != null && Contains(_editArrayButton))
                {
                    _editArrayButton.RemoveFromHierarchy();
                }
            }
            else
            {
                if (_inputField != null && !Contains(_inputField))
                {
                    Add(_inputField);
                }
                if (_inputFieldTypeLabel != null && Contains(_inputFieldTypeLabel))
                {
                    _inputFieldTypeLabel.RemoveFromHierarchy();
                }
                if(_editArrayButton != null && !Contains(_editArrayButton))
                {
                    Add(_editArrayButton);
                }
            }
        }

        private bool TryGetValueObject(out object result, Type type = null)
        {
            // Initialize out object
            result = null;

            // get container from node values
            SerializableObjectContainer container = _udonNodeData.nodeValues[_nodeValueIndex];
            
            // Null check, failure
            if (container == null)
                return false;
            
            // Deserialize into result, return failure on null
            result = container.Deserialize();

            // Strings will deserialize as null, that's ok
            if (type == null || type == typeof(string))
            {
                return true;
            }
            // any other type is not ok to be null
            else if (result == null)
            {
                return false;   
            }

            // Success - return true
            return type.IsInstanceOfType(result);
        }

        private void SetNewValue(object newValue)
        {
            _udonNodeData.nodeValues[_nodeValueIndex] = SerializableObjectContainer.Serialize(newValue, portType);   
            
            if (!_waitToReserialize)
            {
                SendReserializeEvent();
            }
        }

        private void SendReserializeEvent()
        {
            if (!IsReloading())
            {
                this.Reserialize();
            }
        }
    }
}