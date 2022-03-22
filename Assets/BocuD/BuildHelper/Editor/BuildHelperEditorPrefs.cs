using UnityEditor;

namespace BocuD.BuildHelper
{
    [InitializeOnLoad]
    public static class BuildHelperEditorPrefs
    {
        private const string autoSavePath = "BuildHelperAutoSave";
        private const string asyncPublishPath = "BuildHelperAsyncPublish";
        private const string buildNumberPath = "BuildHelperNumberMode";
        private const string platformSwitchPath = "BuildHelperNumberMode";
        
        static BuildHelperEditorPrefs()
        {
            _autoSave = EditorPrefs.GetBool(autoSavePath);
            _useAsyncPublish = EditorPrefs.GetBool(asyncPublishPath);
            _buildNumberMode = EditorPrefs.GetInt(buildNumberPath);
            _platformSwitchMode = EditorPrefs.GetInt(platformSwitchPath);
        }

        private static bool _autoSave;
        public static bool AutoSave
        {
            set
            {
                if (_autoSave == value) return;
                
                _autoSave = value;
                EditorPrefs.SetBool(autoSavePath, value);
            }
            get => _autoSave;
        }

        private static bool _useAsyncPublish;
        public static bool UseAsyncPublish
        {
            set
            {
                if (_useAsyncPublish == value) return;
                
                _useAsyncPublish = value;
                EditorPrefs.SetBool(asyncPublishPath, value);
            }
            get => _useAsyncPublish;
        }
        
        private static int _buildNumberMode;
        public static int BuildNumberMode
        {
            set
            {
                if (_buildNumberMode == value) return;
                
                _buildNumberMode = value;
                EditorPrefs.SetInt(buildNumberPath, value);
            }
            get => _buildNumberMode;
        }

        private static int _platformSwitchMode;

        public static int PlatformSwitchMode
        {
            set
            {
                if (_platformSwitchMode == value) return;

                _platformSwitchMode = value;
                EditorPrefs.SetInt(platformSwitchPath, value);
            }

            get => _platformSwitchMode;
        }
    }
}