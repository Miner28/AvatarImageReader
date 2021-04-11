using UnityEditor;
using UnityEngine;
using VRC.SDK3.Midi;
using VRC.SDKBase.Midi;

namespace VRC.SDK3.Editor
{
    [CustomEditor(typeof(VRCMidiListener))]
    public class VRCMidiListenerEditor : UnityEditor.Editor
    {
#if UNITY_STANDALONE_WIN
        [RuntimeInitializeOnLoadMethod]
        public static void InitializeMidi()
        {
            VRCMidiHandler.OnLog = (message) => Debug.Log(message);
            VRCMidiHandler.Initialize = () =>
            {
                return VRCMidiHandler.OpenMidiInput<VRCPortMidiInput>(
                    EditorPrefs.GetString(VRCMidiWindow.DEVICE_NAME_STRING));
            };
        }
#endif
    }
}