#if UNITY_EDITOR && !COMPILER_UDONSHARP

using UnityEngine;

namespace BocuD.BuildHelper
{
    public static class Logger
    {
        private const string prefix = "[<color=lime>BuildHelper</color>] ";
        
        public static void Log(string contents, Object context = null)
        {
            if (context != null) Debug.Log(prefix + contents, context);
            else Debug.Log(prefix + contents);
        }

        public static void LogWarning(string contents, Object context = null)
        {
            if (context != null) Debug.LogWarning(prefix + contents, context);
            else Debug.LogWarning(prefix + contents);
        }

        public static void LogError(string contents, Object context = null)
        {
            if (context != null) Debug.LogError(prefix + contents, context);
            else Debug.LogError(prefix + contents);
        }
    }
}

#endif