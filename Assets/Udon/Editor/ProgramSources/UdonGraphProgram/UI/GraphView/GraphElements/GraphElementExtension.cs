using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView;

namespace UnityEditor.Experimental.UIElements.GraphView
{
    public static class GraphElementExtension
    {
        public static void Reserialize(this GraphElement element)
        {
            MarkDirty();

            var evt = new Event()
            {
                type = EventType.ExecuteCommand,
                commandName = UdonGraphCommands.Reserialize,
            };
            using (var e = ExecuteCommandEvent.GetPooled(evt))
            {
                element.SendEvent(e);
            }
        }

        public static void Reload(this GraphElement element)
        {
            var evt = new Event()
            {
                type = EventType.ExecuteCommand,
                commandName = UdonGraphCommands.Reload
            };
            using (var e = ExecuteCommandEvent.GetPooled(evt))
            {
                element.SendEvent(e);
            }
        }

        public static void SaveNewData(this GraphElement element)
        {
            var evt = new Event()
            {
                type = EventType.ExecuteCommand,
                commandName = UdonGraphCommands.SaveNewData
            };
            using (var e = ExecuteCommandEvent.GetPooled(evt))
            {
                element.SendEvent(e);
            }

            MarkDirty();
        }

        public static void MarkDirty()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        public static Vector2 GetSnappedPosition(Vector2 position)
        {
            // don't snap at 0 size
            var snap = Settings.GridSnapSize;
            if (snap == 0) return position;

            position.x = (float)System.Math.Round(position.x / snap) * snap;
            position.y = (float)System.Math.Round(position.y / snap) * snap;

            return position;
        } 

        public static Rect GetSnappedRect(Rect rect)
        {
            rect.position = GetSnappedPosition(rect.position);
            return rect;
        }
    }
}