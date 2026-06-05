using UnityEditor;
using UnityEngine;
using Yumesora.MergeSponsor;

namespace Yumesora.MergeSponsor.Editor
{
    [CustomEditor(typeof(YumesoraSponsorTool))]
    public sealed class YumesoraSponsorToolEditor : UnityEditor.Editor
    {
        private SerializedProperty toolNameProperty;
        private SerializedProperty targetRootProperty;
        private SerializedProperty originalTextureProperty;
        private SerializedProperty texturePropertyProperty;
        private SerializedProperty autoApplyOnPlayProperty;
        private SerializedProperty includeInactiveRenderersProperty;
        private SerializedProperty autoResizeSponsorProperty;
        private SerializedProperty sponsorKitsProperty;

        private void OnEnable()
        {
            toolNameProperty = serializedObject.FindProperty("toolName");
            targetRootProperty = serializedObject.FindProperty("targetRoot");
            originalTextureProperty = serializedObject.FindProperty("originalTexture");
            texturePropertyProperty = serializedObject.FindProperty("textureProperty");
            autoApplyOnPlayProperty = serializedObject.FindProperty("autoApplyOnPlay");
            includeInactiveRenderersProperty = serializedObject.FindProperty("includeInactiveRenderers");
            autoResizeSponsorProperty = serializedObject.FindProperty("autoResizeSponsor");
            sponsorKitsProperty = serializedObject.FindProperty("sponsorKits");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            YumesoraSponsorTool tool = (YumesoraSponsorTool)target;

            EditorGUILayout.LabelField("YUMESORA Sponsor Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Applies merged sponsor textures only during Play/VRC upload by swapping temporary material instances. Source PNG files are not overwritten.",
                MessageType.Info);

            DrawTargetSection(tool);
            EditorGUILayout.Space(6f);
            DrawSponsorSection();
            EditorGUILayout.Space(6f);
            DrawStatusSection(tool);
            EditorGUILayout.Space(6f);
            DrawPlayModeActions(tool);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTargetSection(YumesoraSponsorTool tool)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(toolNameProperty, new GUIContent("Name"));
            EditorGUILayout.PropertyField(targetRootProperty, new GUIContent("Avatar Root"));
            EditorGUILayout.PropertyField(originalTextureProperty, new GUIContent("Original Texture"));
            EditorGUILayout.PropertyField(texturePropertyProperty, new GUIContent("Texture Property"));
            EditorGUILayout.PropertyField(autoApplyOnPlayProperty, new GUIContent("Apply On Play"));
            EditorGUILayout.PropertyField(includeInactiveRenderersProperty, new GUIContent("Include Inactive Renderers"));
            EditorGUILayout.PropertyField(autoResizeSponsorProperty, new GUIContent("Auto Resize Sponsor Kits"));

            int matchingSlots = tool.CountMatchingMaterialSlots();
            if (originalTextureProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign the original texture used by the avatar material.", MessageType.Warning);
            }
            else if (matchingSlots == 0)
            {
                EditorGUILayout.HelpBox("No material slot currently uses this original texture/property under the avatar root.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Matching material slots: " + matchingSlots, MessageType.None);
            }

            DrawEditorOnlyTagWarning(tool);

            EditorGUILayout.EndVertical();
        }

        private void DrawSponsorSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Sponsor Kits", EditorStyles.boldLabel);

            for (int i = 0; i < sponsorKitsProperty.arraySize; i++)
            {
                SerializedProperty kitProperty = sponsorKitsProperty.GetArrayElementAtIndex(i);
                SerializedProperty activeProperty = kitProperty.FindPropertyRelative("isActive");
                SerializedProperty nameProperty = kitProperty.FindPropertyRelative("displayName");
                SerializedProperty textureProperty = kitProperty.FindPropertyRelative("texture");
                SerializedProperty opacityProperty = kitProperty.FindPropertyRelative("opacity");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                activeProperty.boolValue = EditorGUILayout.Toggle(activeProperty.boolValue, GUILayout.Width(20f));
                EditorGUILayout.PropertyField(nameProperty, GUIContent.none);
                if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                {
                    sponsorKitsProperty.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(textureProperty, new GUIContent("Texture"));
                EditorGUILayout.PropertyField(opacityProperty, new GUIContent("Opacity"));
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("Add Sponsor Kit"))
            {
                int index = sponsorKitsProperty.arraySize;
                sponsorKitsProperty.InsertArrayElementAtIndex(index);

                SerializedProperty kitProperty = sponsorKitsProperty.GetArrayElementAtIndex(index);
                kitProperty.FindPropertyRelative("isActive").boolValue = true;
                kitProperty.FindPropertyRelative("displayName").stringValue = "Sponsor " + (index + 1);
                kitProperty.FindPropertyRelative("texture").objectReferenceValue = null;
                kitProperty.FindPropertyRelative("opacity").floatValue = 1f;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStatusSection(YumesoraSponsorTool tool)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

            string validationError;
            bool canBuild = tool.CanBuildMergedTexture(out validationError);
            if (canBuild)
            {
                EditorGUILayout.HelpBox("Ready. The merged texture will be created temporarily when Play starts.", MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(validationError, MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPlayModeActions(YumesoraSponsorTool tool)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Play Mode", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Temporary"))
                {
                    tool.ApplyTemporary();
                }

                if (GUILayout.Button("Restore"))
                {
                    tool.RestoreTemporary();
                }

                EditorGUILayout.EndHorizontal();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Manual apply/restore buttons are available while Unity is in Play Mode.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        [MenuItem("GameObject/YUMESORA/Create Sponsor Tool", false, 10)]
        [MenuItem("YUMESORA/Create Sponsor Tool")]
        public static void CreateSponsorTool(MenuCommand command)
        {
            Transform parent = ResolveSelectedParent(command);

            GameObject sponsorToolObject = new GameObject("YUMESORA Sponsor Tool");
            Undo.RegisterCreatedObjectUndo(sponsorToolObject, "Create Sponsor Tool");

            if (parent != null)
            {
                Undo.SetTransformParent(sponsorToolObject.transform, parent, "Parent Sponsor Tool");
                sponsorToolObject.transform.localPosition = Vector3.zero;
                sponsorToolObject.transform.localRotation = Quaternion.identity;
                sponsorToolObject.transform.localScale = Vector3.one;
            }

            sponsorToolObject.tag = "EditorOnly";

            YumesoraSponsorTool tool = sponsorToolObject.AddComponent<YumesoraSponsorTool>();
            tool.SetTargetRoot(parent != null ? parent : sponsorToolObject.transform);

            Selection.activeObject = sponsorToolObject;
        }

        private static Transform ResolveSelectedParent(MenuCommand command)
        {
            GameObject contextObject = command.context as GameObject;
            if (contextObject != null)
            {
                return contextObject.transform;
            }

            return Selection.activeTransform;
        }

        private void DrawEditorOnlyTagWarning(YumesoraSponsorTool tool)
        {
            if (tool.gameObject.CompareTag("EditorOnly"))
            {
                return;
            }

            EditorGUILayout.HelpBox("The Sponsor Tool object should use the EditorOnly tag so the helper object is not included in uploaded avatars.", MessageType.Warning);
            if (GUILayout.Button("Set EditorOnly Tag"))
            {
                Undo.RecordObject(tool.gameObject, "Set EditorOnly Tag");
                tool.gameObject.tag = "EditorOnly";
                EditorUtility.SetDirty(tool.gameObject);
            }
        }
    }

    [InitializeOnLoad]
    internal static class YumesoraSponsorToolPlayModeGuard
    {
        static YumesoraSponsorToolPlayModeGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            YumesoraSponsorTool[] tools = UnityEngine.Object.FindObjectsOfType<YumesoraSponsorTool>(true);
            for (int i = 0; i < tools.Length; i++)
            {
                tools[i].RestoreTemporary();
            }
        }
    }
}
