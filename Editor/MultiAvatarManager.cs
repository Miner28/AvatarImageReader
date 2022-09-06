using UnityEditor;
using UnityEngine;
using VRC.Core;

#if VRCHAT_API_TOOLS_IMPORTED
using BocuD.VRChatApiTools;
#endif

namespace AvatarImageReader.Editor
{
    public class MultiAvatarManager : EditorWindow
    {
        public static void SpawnEditor(RuntimeDecoderEditor prefabEditor)
        {
            MultiAvatarManager window = GetWindow<MultiAvatarManager>();
            window.titleContent = new GUIContent("Linked Avatar Editor");
            window.minSize = new Vector2(450, 400);
            window.prefabEditor = prefabEditor;
        }

        public RuntimeDecoderEditor prefabEditor;
        private Vector2 scrollView;

#if VRCHAT_API_TOOLS_IMPORTED
        private void OnGUI()
        {
            EditorGUILayout.LabelField($"Multi Avatar Manager for {prefabEditor.reader.name}");
            scrollView = EditorGUILayout.BeginScrollView(scrollView);

            bool ownershipError = false;

            for (int a = 0; a < prefabEditor.reader.linkedAvatars.Length; a++)
            {
                VRChatApiToolsGUI.DrawBlueprintInspector(prefabEditor.reader.linkedAvatars[a], true, () =>
                {
                    if (GUILayout.Button("Change Avatar"))
                    {
                        BlueprintPicker.BlueprintSelector<ApiAvatar>(avatar => prefabEditor.AvatarSelected(avatar, a));
                    }

                    if (GUILayout.Button("Remove Avatar"))
                    {
                        ArrayUtility.RemoveAt(ref prefabEditor.reader.linkedAvatars, a);
                        prefabEditor.OnLinkedAvatarsUpdated();
                    }
                });

                if (VRChatApiTools.blueprintCache.TryGetValue(prefabEditor.reader.linkedAvatars[a], out ApiModel m))
                {
                    if (m is ApiAvatar avatar && avatar.authorId != APIUser.CurrentUser.id)
                    {
                        ownershipError = true;
                    }
                }
            }

            if (ownershipError)
            {
                EditorGUILayout.HelpBox("One or more avatars isn't owned by the currently logged in user",
                    MessageType.Error);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current avatar count: ", prefabEditor.reader.linkedAvatars.Length.ToString());
            if (GUILayout.Button("Add new Avatar"))
            {
                BlueprintPicker.BlueprintSelector<ApiAvatar>(avatar => prefabEditor.AvatarSelected(avatar, prefabEditor.reader.linkedAvatars.Length));
            }
            EditorGUILayout.EndHorizontal();
        }
#endif
    }
}
