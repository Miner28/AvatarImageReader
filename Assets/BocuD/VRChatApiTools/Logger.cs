#if UNITY_EDITOR && !COMPILER_UDONSHARP

using UnityEngine;

namespace BocuD.VRChatApiTools
{
    public static class Logger
    {
        public static VRChatApiToolsUploadStatus statusWindow;

        private const string prefix = "[<color=lime>VRChatApiTools</color>] ";
        
        public static void Log(string contents, Object context = null)
        {
            if(statusWindow) statusWindow.AddLog($"<color=grey>{contents}</color>");
            
            if (context != null) Debug.Log(prefix + contents, context);
            else Debug.Log(prefix + contents);
        }

        public static void LogWarning(string contents, Object context = null)
        {
            if(statusWindow) statusWindow.AddLog($"<color=yellow>{contents}</color>");
            
            if (context != null) Debug.LogWarning(prefix + contents, context);
            else Debug.LogWarning(prefix + contents);
        }

        public static void LogError(string contents, Object context = null)
        {
            if(statusWindow) statusWindow.AddLog($"<color=red>{contents}</color>");
            
            if (context != null) Debug.LogError(prefix + contents, context);
            else Debug.LogError(prefix + contents);
        }
    }
}
#endif