using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Unity.Profiling;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Attributes;
using VRC.Udon.Common.Enums;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.VM;
using Logger = VRC.Core.Logger;
using Object = UnityEngine.Object;

#if VRC_CLIENT
using VRC.Udon.Security;
#endif
#if UNITY_EDITOR && !VRC_CLIENT
using UnityEditor.SceneManagement;
#endif

namespace VRC.Udon
{
    public sealed class UdonBehaviour : AbstractUdonBehaviour, ISerializationCallbackReceiver
    {
        #region Odin Serialized Fields

        public IUdonVariableTable publicVariables = new UdonVariableTable();

        #endregion

        #region Serialized Public Fields

        [PublicAPI]
        // ReSharper disable once InconsistentNaming
        public bool SynchronizePosition;

        // ReSharper disable once InconsistentNaming
        [PublicAPI]
        public readonly bool SynchronizeAnimation = false; //We don't support animation sync yet, coming soon.

        // ReSharper disable once InconsistentNaming
        [PublicAPI] public bool AllowCollisionOwnershipTransfer = true;

        #endregion

        #region Serialized Private Fields

        [SerializeField] private AbstractSerializedUdonProgramAsset serializedProgramAsset;

#if UNITY_EDITOR && !VRC_CLIENT
        [SerializeField]
        public AbstractUdonProgramSource programSource;

#endif

        #endregion

        #region Public Fields and Properties

        [PublicAPI] public static Action<UdonBehaviour, IUdonProgram> OnInit { get; set; } = null;

        [PublicAPI]
        public static Action<UdonBehaviour, NetworkEventTarget, string> SendCustomNetworkEventHook { get; set; } = null;

        [PublicAPI]
        [ExcludeFromUdonWrapper]
        public override bool IsNetworkingSupported
        {
            get => _isNetworkingSupported;
            set
            {
                if (_initialized)
                {
                    throw new InvalidOperationException(
                        "IsNetworkingSupported cannot be changed after the UdonBehaviour has been initialized.");
                }

                _isNetworkingSupported = value;
            }
        }

        public override bool IsInteractive => _hasInteractiveEvents;

        public override int NetworkID { get; set; }

        internal int UpdateOrder => _program?.UpdateOrder ?? 0;

        #endregion

        #region Private Fields

        private UdonManager _udonManager;
        private IUdonProgram _program;
        private IUdonVM _udonVM;
        private bool _isReady;
        private int _debugLevel;
        private bool _hasError;
        private bool _hasDoneStart;
        private bool _initialized;
        private bool _isNetworkingSupported = false;

        private bool _hasInteractiveEvents;
        private bool _hasUpdateEvent;
        private bool _hasLateUpdateEvent;
        private bool _hasFixedUpdateEvent;
        private readonly Dictionary<string, List<uint>> _eventTable = new Dictionary<string, List<uint>>();

        private readonly Dictionary<(string eventName, string symbolName), string> _symbolNameCache =
            new Dictionary<(string, string), string>();

        private static ProfilerMarker _managedUpdateProfilerMarker =
            new ProfilerMarker("UdonBehaviour.ManagedUpdate()");

        private static ProfilerMarker _managedLateUpdateProfilerMarker =
            new ProfilerMarker("UdonBehaviour.ManagedLateUpdate()");

        private static ProfilerMarker _managedFixedUpdateProfilerMarker =
            new ProfilerMarker("UdonBehaviour.ManagedFixedUpdate()");

        #endregion

        #region Editor Only

#if UNITY_EDITOR && !VRC_CLIENT
        public void RunEditorUpdate(ref bool dirty)
        {
            if(programSource == null)
            {
                return;
            }

            programSource.RunEditorUpdate(this, ref dirty);

            if(!dirty)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

#endif

        #endregion

        #region Private Methods

        private bool LoadProgram()
        {
            if (serializedProgramAsset == null)
            {
                return false;
            }

            _program = serializedProgramAsset.RetrieveProgram();

            IUdonSymbolTable symbolTable = _program?.SymbolTable;
            IUdonHeap heap = _program?.Heap;
            if (symbolTable == null || heap == null)
            {
                return false;
            }

            foreach (string variableSymbol in publicVariables.VariableSymbols)
            {
                if (!symbolTable.HasAddressForSymbol(variableSymbol))
                {
                    continue;
                }

                uint symbolAddress = symbolTable.GetAddressFromSymbol(variableSymbol);

                if (!publicVariables.TryGetVariableType(variableSymbol, out Type declaredType))
                {
                    continue;
                }

                publicVariables.TryGetVariableValue(variableSymbol, out object value);
                if (declaredType == typeof(GameObject) || declaredType == typeof(UdonBehaviour) ||
                    declaredType == typeof(Transform))
                {
                    if (value == null)
                    {
                        value = new UdonGameObjectComponentHeapReference(declaredType);
                        declaredType = typeof(UdonGameObjectComponentHeapReference);
                    }
                }

                heap.SetHeapVariable(symbolAddress, value, declaredType);
            }

            return true;
        }

        private void ProcessEntryPoints()
        {
            if (_program.EntryPoints.HasExportedSymbol("_interact"))
            {
                _hasInteractiveEvents = true;
            }

            if (_program.EntryPoints.HasExportedSymbol("_update"))
            {
                _hasUpdateEvent = true;
            }

            if (_program.EntryPoints.HasExportedSymbol("_lateUpdate"))
            {
                _hasLateUpdateEvent = true;
            }

            if (_program.EntryPoints.HasExportedSymbol("_fixedUpdate"))
            {
                _hasFixedUpdateEvent = true;
            }

            RegisterUpdate();

            _eventTable.Clear();
            foreach (string entryPoint in _program.EntryPoints.GetExportedSymbols())
            {
                uint address = _program.EntryPoints.GetAddressFromSymbol(entryPoint);

                if (!_eventTable.ContainsKey(entryPoint))
                {
                    _eventTable.Add(entryPoint, new List<uint>());
                }

                _eventTable[entryPoint].Add(address);

                _udonManager.RegisterInput(this, entryPoint, true);
            }
        }

        private bool ResolveUdonHeapReferences(IUdonSymbolTable symbolTable, IUdonHeap heap)
        {
            bool success = true;
            foreach (string symbolName in symbolTable.GetSymbols())
            {
                uint symbolAddress = symbolTable.GetAddressFromSymbol(symbolName);
                object heapValue = heap.GetHeapVariable(symbolAddress);
                if (!(heapValue is UdonBaseHeapReference udonBaseHeapReference))
                {
                    continue;
                }

                if (!ResolveUdonHeapReference(heap, symbolAddress, udonBaseHeapReference))
                {
                    success = false;
                }
            }

            return success;
        }

        private bool ResolveUdonHeapReference(IUdonHeap heap, uint symbolAddress,
            UdonBaseHeapReference udonBaseHeapReference)
        {
            switch (udonBaseHeapReference)
            {
                case UdonGameObjectComponentHeapReference udonGameObjectComponentHeapReference:
                {
                    Type referenceType = udonGameObjectComponentHeapReference.type;
                    if (referenceType == typeof(GameObject))
                    {
                        heap.SetHeapVariable(symbolAddress, gameObject);
                        return true;
                    }
                    else if (referenceType == typeof(Transform))
                    {
                        heap.SetHeapVariable(symbolAddress, gameObject.transform);
                        return true;
                    }
                    else if (referenceType == typeof(UdonBehaviour))
                    {
                        heap.SetHeapVariable(symbolAddress, this);
                        return true;
                    }
                    else if (referenceType == typeof(Object))
                    {
                        heap.SetHeapVariable(symbolAddress, this);
                        return true;
                    }
                    else
                    {
                        Logger.Log(
                            $"Unsupported GameObject/Component reference type: {udonBaseHeapReference.GetType().Name}. Only GameObject, Transform, and UdonBehaviour are supported.",
                            _debugLevel,
                            this);

                        return false;
                    }
                }
                default:
                {
                    Logger.Log($"Unknown heap reference type: {udonBaseHeapReference.GetType().Name}", _debugLevel,
                        this);
                    return false;
                }
            }
        }

        #endregion

        #region Managed Unity Events

        internal void ManagedUpdate()
        {
            using (_managedUpdateProfilerMarker.Auto())
            {
                if (!_hasDoneStart && _isReady)
                {
                    _hasDoneStart = true;
                    RunEvent("_onEnable");
                    RunEvent("_start");
                    if (!_hasUpdateEvent)
                    {
                        _udonManager.UnregisterUdonBehaviourUpdate(this);
                    }
                }

                RunEvent("_update");
            }
        }

        internal void ManagedLateUpdate()
        {
            using (_managedLateUpdateProfilerMarker.Auto())
            {
                RunEvent("_lateUpdate");
            }
        }

        internal void ManagedFixedUpdate()
        {
            using (_managedFixedUpdateProfilerMarker.Auto())
            {
                RunEvent("_fixedUpdate");
            }
        }

        #endregion

        #region Unity Events

        public void OnAnimatorIK(int layerIndex)
        {
            RunEvent("_onAnimatorIk", ("index", layerIndex));
        }

        public void OnAnimatorMove()
        {
            RunEvent("_onAnimatorMove");
        }

        public void OnAudioFilterRead(float[] data, int channels)
        {
            RunEvent("_onAudioFilterRead", ("data", data), ("channels", channels));
        }

        public void OnBecameInvisible()
        {
            RunEvent("_onBecameInvisible");
        }

        public void OnBecameVisible()
        {
            RunEvent("_onBecameVisible");
        }

        public void OnCollisionEnter(Collision other)
        {
            var player = VRCPlayerApi.GetPlayerByGameObject(other.gameObject);
            if (player != null)
            {
                RunEvent("_onPlayerCollisionEnter", ("player", player));
            }
            else
            {
                RunEvent("_onCollisionEnter", ("other", other));
            }
        }

        public void OnCollisionEnter2D(Collision2D other)
        {
            RunEvent("_onCollisionEnter2D", ("other", other));
        }

        public void OnCollisionExit(Collision other)
        {
            var player = VRCPlayerApi.GetPlayerByGameObject(other.gameObject);
            if (player != null)
            {
                RunEvent("_onPlayerCollisionExit", ("player", player));
            }
            else
            {
                RunEvent("_onCollisionExit", ("other", other));
            }
        }

        public void OnCollisionExit2D(Collision2D other)
        {
            RunEvent("_onCollisionExit2D", ("other", other));
        }

        public void OnCollisionStay(Collision other)
        {
            var player = VRCPlayerApi.GetPlayerByGameObject(other.gameObject);
            if (player != null)
            {
                RunEvent("_onPlayerCollisionStay", ("player", player));
            }
            else
            {
                RunEvent("_onCollisionStay", ("other", other));
            }
        }

        public void OnCollisionStay2D(Collision2D other)
        {
            RunEvent("_onCollisionStay2D", ("other", other));
        }

        public void OnDestroy()
        {
            if (_program == null) return;
            
            foreach (string entryPoint in _program.EntryPoints.GetExportedSymbols())
            {
                uint address = _program.EntryPoints.GetAddressFromSymbol(entryPoint);

                if (!_eventTable.ContainsKey(entryPoint))
                {
                    _eventTable.Add(entryPoint, new List<uint>());
                }

                _eventTable[entryPoint].Add(address);
                _udonManager.RegisterInput(this, entryPoint, false);
            }

            RunEvent("_onDestroy");
        }

        public void OnDisable()
        {
            UnregisterUpdate();

            RunEvent("_onDisable");
        }

        public void OnDrawGizmos()
        {
            RunEvent("_onDrawGizmos");
        }

        public void OnDrawGizmosSelected()
        {
            RunEvent("_onDrawGizmosSelected");
        }

        public void OnEnable()
        {
            if (_initialized)
            {
                RegisterUpdate();
            }

            RunEvent("_onEnable");
        }

        public void OnJointBreak(float breakForce)
        {
            RunEvent("_onJointBreak", ("force", breakForce));
        }

        public void OnJointBreak2D(Joint2D brokenJoint)
        {
            RunEvent("_onJointBreak2D", ("joint", brokenJoint));
        }

        public void OnMouseDown()
        {
            RunEvent("_onMouseDown");
        }

        public void OnMouseDrag()
        {
            RunEvent("_onMouseDrag");
        }

        public void OnMouseEnter()
        {
            RunEvent("_onMouseEnter");
        }

        public void OnMouseExit()
        {
            RunEvent("_onMouseExit");
        }

        public void OnMouseOver()
        {
            RunEvent("_onMouseOver");
        }

        public void OnMouseUp()
        {
            RunEvent("_onMouseUp");
        }

        public void OnMouseUpAsButton()
        {
            RunEvent("_onMouseUpAsButton");
        }

        public void OnParticleCollision(GameObject other)
        {
            var player = VRCPlayerApi.GetPlayerByGameObject(other.gameObject);
            if (player != null)
            {
                RunEvent("_onPlayerParticleCollision", ("player", player));
            }
            else
            {
                RunEvent("_onParticleCollision", ("other", other));
            }
        }

        public void OnParticleTrigger()
        {
            RunEvent("_onParticleTrigger");
        }

        public void OnPostRender()
        {
            RunEvent("_onPostRender");
        }

        public void OnPreCull()
        {
            RunEvent("_onPreCull");
        }

        public void OnPreRender()
        {
            RunEvent("_onPreRender");
        }

        public void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (!_eventTable.ContainsKey("_onRenderImage") || _eventTable["_onRenderImage"].Count == 0)
            {
                Graphics.Blit(src, dest);
                return;
            }

            RunEvent("_onRenderImage", ("src", src), ("dest", dest));
        }

        public void OnRenderObject()
        {
            RunEvent("_onRenderObject");
        }

        public void OnTransformChildrenChanged()
        {
            RunEvent("_onTransformChildrenChanged");
        }

        public void OnTransformParentChanged()
        {
            RunEvent("_onTransformParentChanged");
        }

        public void OnTriggerEnter(Collider other)
        {
            var player = VRCPlayerApi.GetPlayerByGameObject(other.gameObject);
            if (player != null)
            {
                RunEvent("_onPlayerTriggerEnter", ("player", player));
            }
            else
            {
                RunEvent("_onTriggerEnter", ("other", other));
            }
        }

        public void OnTriggerEnter2D(Collider2D other)
        {
            RunEvent("_onTriggerEnter2D", ("other", other));
        }

        public void OnTriggerExit(Collider other)
        {
            var player = VRCPlayerApi.GetPlayerByGameObject(other.gameObject);
            if (player != null)
            {
                RunEvent("_onPlayerTriggerExit", ("player", player));
            }
            else
            {
                RunEvent("_onTriggerExit", ("other", other));
            }
        }

        public void OnTriggerExit2D(Collider2D other)
        {
            RunEvent("_onTriggerExit2D", ("other", other));
        }

        public void OnTriggerStay(Collider other)
        {
            var player = VRCPlayerApi.GetPlayerByGameObject(other.gameObject);
            if (player != null)
            {
                RunEvent("_onPlayerTriggerStay", ("player", player));
            }
            else
            {
                RunEvent("_onTriggerStay", ("other", other));
            }
        }

        public void OnTriggerStay2D(Collider2D other)
        {
            RunEvent("_onTriggerStay2D", ("other", other));
        }

        public void OnValidate()
        {
            RunEvent("_onValidate");
        }

        public void OnWillRenderObject()
        {
            RunEvent("_onWillRenderObject");
        }

        #endregion

        #region VRCSDK Events

#if VRC_CLIENT
        [PublicAPI]
        private void OnNetworkReady()
        {
            _isReady = true;
        }
#endif

        //Called through Interactable interface
        public override void Interact()
        {
            RunEvent("_interact");
        }

        public override void OnDrop()
        {
            RunEvent("_onDrop");
        }

        public override void OnPickup()
        {
            RunEvent("_onPickup");
        }

        public override void OnPickupUseDown()
        {
            RunEvent("_onPickupUseDown");
        }

        public override void OnPickupUseUp()
        {
            RunEvent("_onPickupUseUp");
        }

        //Called via delegate by UdonSync
        [PublicAPI]
        public void OnPreSerialization()
        {
            RunEvent("_onPreSerialization");
        }

        //Called via delegate by UdonSync
        [PublicAPI]
        public void OnDeserialization()
        {
            RunEvent("_onDeserialization");
        }

        #endregion

        #region RunProgram Methods

        [PublicAPI]
        public override void RunProgram(string eventName)
        {
            if (_program == null)
            {
                return;
            }

            if (!_program.EntryPoints.GetExportedSymbols().Contains(eventName))
            {
                return;
            }

            uint address = _program.EntryPoints.GetAddressFromSymbol(eventName);
            RunProgram(address);
        }

        private void RunProgram(uint entryPoint)
        {
            if (_hasError)
            {
                return;
            }

            if (_udonVM == null)
            {
                return;
            }

            uint originalAddress = _udonVM.GetProgramCounter();
            UdonBehaviour originalExecuting = _udonManager.currentlyExecuting;

            _udonVM.SetProgramCounter(entryPoint);
            _udonManager.currentlyExecuting = this;

            _udonVM.DebugLogging = _udonManager.DebugLogging;

            try
            {
                uint result = _udonVM.Interpret();
                if (result != 0)
                {
                    Logger.LogError($"Udon VM execution errored, this UdonBehaviour will be halted.", _debugLevel,
                        this);
                    _hasError = true;
                    enabled = false;
                }
            }
            catch (UdonVMException error)
            {
                Logger.LogError(
                    $"An exception occurred during Udon execution, this UdonBehaviour will be halted.\n{error}",
                    _debugLevel, this);
                _hasError = true;
                enabled = false;
            }

            _udonManager.currentlyExecuting = originalExecuting;
            if (originalAddress < 0xFFFFFFFC)
            {
                _udonVM.SetProgramCounter(originalAddress);
            }
        }

        [PublicAPI]
        public ImmutableArray<string> GetPrograms()
        {
            return _program?.EntryPoints.GetExportedSymbols() ?? ImmutableArray<string>.Empty;
        }

        #endregion

        #region Serialization

        [SerializeField] private string serializedPublicVariablesBytesString;

        [SerializeField] private List<Object> publicVariablesUnityEngineObjects;

        [SerializeField] private DataFormat publicVariablesSerializationDataFormat = DataFormat.Binary;

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            DeserializePublicVariables();
        }

        private void DeserializePublicVariables()
        {
            byte[] serializedPublicVariablesBytes =
                Convert.FromBase64String(serializedPublicVariablesBytesString ?? "");
            publicVariables = SerializationUtility.DeserializeValue<IUdonVariableTable>(
                serializedPublicVariablesBytes,
                publicVariablesSerializationDataFormat,
                publicVariablesUnityEngineObjects
            ) ?? new UdonVariableTable();

            // Validate that the type of the value can actually be cast to the declaredType to avoid InvalidCastExceptions later.
            foreach (string publicVariableSymbol in publicVariables.VariableSymbols.ToArray())
            {
                if (!publicVariables.TryGetVariableValue(publicVariableSymbol, out object value))
                {
                    continue;
                }

                if (value == null)
                {
                    continue;
                }

                if (!publicVariables.TryGetVariableType(publicVariableSymbol, out Type declaredType))
                {
                    continue;
                }

                if (declaredType.IsInstanceOfType(value))
                {
                    continue;
                }

                if (declaredType.IsValueType)
                {
                    publicVariables.TrySetVariableValue(publicVariableSymbol, Activator.CreateInstance(declaredType));
                }
                else
                {
                    publicVariables.TrySetVariableValue(publicVariableSymbol, null);
                }
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            SerializePublicVariables();
        }

        private void SerializePublicVariables()
        {
            byte[] serializedPublicVariablesBytes = SerializationUtility.SerializeValue(publicVariables,
                publicVariablesSerializationDataFormat, out publicVariablesUnityEngineObjects);
            serializedPublicVariablesBytesString = Convert.ToBase64String(serializedPublicVariablesBytes);
        }

        #endregion

        #region IUdonBehaviour Interface

        public override void RunEvent(string eventName, params (string symbolName, object value)[] programVariables)
        {
            if (!_isReady)
            {
                return;
            }

            if (!_hasDoneStart)
            {
                return;
            }

            if (!_eventTable.TryGetValue(eventName, out List<uint> entryPoints))
            {
                return;
            }

            //TODO: Replace with a non-boxing interface before exposing to users
            foreach ((string symbolName, object value) in programVariables)
            {
                SetEventVariable(eventName, symbolName, value);
            }

            foreach (uint entryPoint in entryPoints)
            {
                RunProgram(entryPoint);
            }

            foreach ((string symbolName, object _) in programVariables)
            {
                SetProgramVariable(symbolName, null);
            }
        }

        public override void RunInputEvent(string eventName, UdonInputEventArgs args)
        {
            if (!_isReady)
            {
                return;
            }

            if (!_hasDoneStart)
            {
                return;
            }

            if (!_program.EntryPoints.GetExportedSymbols().Contains(eventName))
            {
                return;
            }

            // Set value arg
            switch (args.eventType)
            {
                case UdonInputEventType.AXIS:
                    SetEventVariable(eventName, "floatValue", args.floatValue);
                    break;
                case UdonInputEventType.BUTTON:
                    SetEventVariable(eventName, "boolValue", args.boolValue);
                    break;
            }

            // Set event args
            SetEventVariable(eventName, "args", args);
            RunProgram(eventName);
        }

        private void SetEventVariable<T>(string eventName, string symbolName, T value)
        {
            if (!_symbolNameCache.TryGetValue((eventName, symbolName), out string newSymbolName))
            {
                newSymbolName = $"{eventName.Substring(1)}{char.ToUpper(symbolName.First())}{symbolName.Substring(1)}";
                _symbolNameCache.Add((eventName, symbolName), newSymbolName);
            }

            SetProgramVariable(newSymbolName, value);
        }

        public override void InitializeUdonContent()
        {
            if (_initialized)
            {
                return;
            }

            SetupLogging();

            _udonManager = UdonManager.Instance;
            if (_udonManager == null)
            {
                enabled = false;
                Logger.LogError(
                    $"Could not find the UdonManager; the UdonBehaviour on '{gameObject.name}' will not run.",
                    _debugLevel, this);
                return;
            }

            if (!LoadProgram())
            {
                enabled = false;
                Logger.Log(
                    $"Could not load the program; the UdonBehaviour on '{gameObject.name}' will not run.", _debugLevel,
                    this);

                return;
            }

            IUdonSymbolTable symbolTable = _program?.SymbolTable;
            IUdonHeap heap = _program?.Heap;
            if (symbolTable == null || heap == null)
            {
                enabled = false;
                Logger.Log($"Invalid program; the UdonBehaviour on '{gameObject.name}' will not run.",
                    _debugLevel, this);
                return;
            }

            if (!ResolveUdonHeapReferences(symbolTable, heap))
            {
                enabled = false;
                Logger.Log(
                    $"Failed to resolve a GameObject/Component Reference; the UdonBehaviour on '{gameObject.name}' will not run.",
                    _debugLevel, this);
                return;
            }

            _udonVM = _udonManager.ConstructUdonVM();

            if (_udonVM == null)
            {
                enabled = false;
                Logger.LogError($"No UdonVM; the UdonBehaviour on '{gameObject.name}' will not run.",
                    _debugLevel, this);
                return;
            }

            _udonVM.LoadProgram(_program);

            ProcessEntryPoints();

#if !VRC_CLIENT
            _isReady = true;
#else
            if(!_isNetworkingSupported)
            {
                _isReady = true;
            }
#endif

            _initialized = true;

            RunOnInit();
        }

        [PublicAPI]
        public void RunOnInit()
        {
            if (OnInit == null)
            {
                return;
            }

            try
            {
                OnInit(this, _program);
            }
            catch (Exception exception)
            {
                enabled = false;
                Logger.LogError(
                    $"An exception '{exception.Message}' occurred during initialization; the UdonBehaviour on '{gameObject.name}' will not run. Exception:\n{exception}",
                    _debugLevel,
                    this
                );
            }
        }

        private void RegisterUpdate()
        {
            if (_udonManager == null)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_hasUpdateEvent || !_hasDoneStart)
            {
                _udonManager.RegisterUdonBehaviourUpdate(this);
            }

            if (_hasLateUpdateEvent)
            {
                _udonManager.RegisterUdonBehaviourLateUpdate(this);
            }

            if (_hasFixedUpdateEvent)
            {
                _udonManager.RegisterUdonBehaviourFixedUpdate(this);
            }
        }

        private void UnregisterUpdate()
        {
            if (_udonManager == null)
            {
                return;
            }

            if (_hasUpdateEvent)
            {
                _udonManager.UnregisterUdonBehaviourUpdate(this);
            }

            if (_hasLateUpdateEvent)
            {
                _udonManager.UnregisterUdonBehaviourLateUpdate(this);
            }

            if (_hasFixedUpdateEvent)
            {
                _udonManager.UnregisterUdonBehaviourFixedUpdate(this);
            }
        }

        #region IUdonEventReceiver and IUdonSyncTarget Interface

        #region IUdonEventReceiver Only

        public override void SendCustomEvent(string eventName)
        {
            RunProgram(eventName);
        }

        public override void SendCustomNetworkEvent(NetworkEventTarget target, string eventName)
        {
#if UNITY_EDITOR
            SendCustomEvent(eventName);
#else
            SendCustomNetworkEventHook?.Invoke(this, target, eventName);
#endif
        }

        public override void SendCustomEventDelayedSeconds(string eventName, float delaySeconds, EventTiming eventTiming = EventTiming.Update)
        {
            UdonManager.Instance.ScheduleDelayedEvent(this, eventName, delaySeconds, eventTiming);
        }

        public override void SendCustomEventDelayedFrames(string eventName, int delayFrames, EventTiming eventTiming = EventTiming.Update)
        {
            UdonManager.Instance.ScheduleDelayedEvent(this, eventName, delayFrames, eventTiming);
        }

        #endregion

        #region IUdonSyncTarget

        public override IUdonSyncMetadataTable SyncMetadataTable => _program?.SyncMetadataTable;

        #endregion

        #region Shared

        public override Type GetProgramVariableType(string symbolName)
        {
            if (!_program.SymbolTable.HasAddressForSymbol(symbolName))
            {
                return null;
            }

            uint symbolAddress = _program.SymbolTable.GetAddressFromSymbol(symbolName);
            return _program.Heap.GetHeapVariableType(symbolAddress);
        }

        public override void SetProgramVariable<T>(string symbolName, T value)
        {
            if (_program == null)
            {
                return;
            }

            if (!_program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return;
            }

            _program.Heap.SetHeapVariable<T>(symbolAddress, value);
        }

        public override void SetProgramVariable(string symbolName, object value)
        {
            if (_program == null)
            {
                return;
            }

            if (!_program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return;
            }

            _program.Heap.SetHeapVariable(symbolAddress, value);
        }

        public override T GetProgramVariable<T>(string symbolName)
        {
            if (_program == null)
            {
                return default;
            }

            if (!_program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return default;
            }

            return _program.Heap.GetHeapVariable<T>(symbolAddress);
        }

        public override object GetProgramVariable(string symbolName)
        {
            if (_program == null)
            {
                return null;
            }

            if (!_program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return null;
            }

            return _program.Heap.GetHeapVariable(symbolAddress);
        }

        public override bool TryGetProgramVariable<T>(string symbolName, out T value)
        {
            value = default;
            if (_program == null)
            {
                return false;
            }

            if (!_program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return false;
            }

            return _program.Heap.TryGetHeapVariable(symbolAddress, out value);
        }

        public override bool TryGetProgramVariable(string symbolName, out object value)
        {
            value = null;
            if (_program == null)
            {
                return false;
            }

            if (!_program.SymbolTable.TryGetAddressFromSymbol(symbolName, out uint symbolAddress))
            {
                return false;
            }

            return _program.Heap.TryGetHeapVariable(symbolAddress, out value);
        }

        #endregion

        #endregion

        #endregion

        #region Logging Methods

        private void SetupLogging()
        {
            _debugLevel = GetType().GetHashCode();
            if (Logger.DebugLevelIsDescribed(_debugLevel))
            {
                return;
            }

            Logger.DescribeDebugLevel(_debugLevel, "UdonBehaviour");
            Logger.AddDebugLevel(_debugLevel);
        }

        #endregion

        #region Manual Initialization Methods

        [PublicAPI]
        public void AssignProgramAndVariables(AbstractSerializedUdonProgramAsset compiledAsset,
            IUdonVariableTable variables)
        {
            serializedProgramAsset = compiledAsset;
            publicVariables = variables;
        }

        #endregion
    }
}
