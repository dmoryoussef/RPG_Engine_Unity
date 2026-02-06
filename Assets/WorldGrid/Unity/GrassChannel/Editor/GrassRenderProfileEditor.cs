#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    [CustomEditor(typeof(Grass.GrassRenderProfile))]
    public sealed class GrassRenderProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty _orientationMode;
        private SerializedProperty _meshXY;
        private SerializedProperty _meshXZ;
        private SerializedProperty _useXZPlane;

        private SerializedProperty _verticalSegments;
        private SerializedProperty _useThirdQuad;

        private SerializedProperty _materialTemplate;
        private SerializedProperty _mainTextureOverride;
        private SerializedProperty _renderQueueOverride;

        private SerializedProperty _castShadows;
        private SerializedProperty _receiveShadows;
        private SerializedProperty _emissionStrength;

        private void OnEnable()
        {
            _orientationMode = serializedObject.FindProperty("orientationMode");
            _meshXY = serializedObject.FindProperty("meshXY");
            _meshXZ = serializedObject.FindProperty("meshXZ");
            _useXZPlane = serializedObject.FindProperty("useXZPlane");

            _verticalSegments = serializedObject.FindProperty("verticalSegments");
            _useThirdQuad = serializedObject.FindProperty("useThirdQuad");

            _materialTemplate = serializedObject.FindProperty("materialTemplate");
            _mainTextureOverride = serializedObject.FindProperty("mainTextureOverride");
            _renderQueueOverride = serializedObject.FindProperty("renderQueueOverride");

            _castShadows = serializedObject.FindProperty("castShadows");
            _receiveShadows = serializedObject.FindProperty("receiveShadows");
            _emissionStrength = serializedObject.FindProperty("emissionStrength");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var profile = (Grass.GrassRenderProfile)target;

            DrawOrientationBlock(profile);
            EditorGUILayout.Space(8);

            DrawMeshGenBlock(profile);
            EditorGUILayout.Space(8);

            DrawAssetsBlock();
            EditorGUILayout.Space(8);

            DrawShadowsBlock(profile);
            EditorGUILayout.Space(8);

            // Draw the rest (density/scale/wind/etc.) in the default layout
            DrawPropertiesExcluding(
                serializedObject,
                "m_Script",
                "orientationMode", "meshXY", "meshXZ", "useXZPlane",
                "verticalSegments", "useThirdQuad",
                "materialTemplate", "mainTextureOverride", "renderQueueOverride",
                "castShadows", "receiveShadows", "emissionStrength"
            );

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawOrientationBlock(Grass.GrassRenderProfile profile)
        {
            EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_orientationMode, new GUIContent("Mode"));
            EditorGUILayout.PropertyField(_useXZPlane, new GUIContent("Use XZ Plane (shader)"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync UseXZPlane From Mode"))
                {
                    Undo.RecordObject(profile, "Sync UseXZPlane From Mode");
                    profile.SyncUseXZPlaneFromOrientation();
                    EditorUtility.SetDirty(profile);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(_meshXY, new GUIContent("Mesh XY (2D)"));
            EditorGUILayout.PropertyField(_meshXZ, new GUIContent("Mesh XZ (3D)"));

            if (profile.EffectiveMesh == null)
            {
                EditorGUILayout.HelpBox("EffectiveMesh is null for the current mode. Assign a mesh or generate one below.", MessageType.Warning);
            }
        }

        private void DrawMeshGenBlock(Grass.GrassRenderProfile profile)
        {
            EditorGUILayout.LabelField("Mesh Generation", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_verticalSegments, new GUIContent("Vertical Segments"));
            EditorGUILayout.PropertyField(_useThirdQuad, new GUIContent("Use 3rd Quad (45°)"));

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate XY Mesh"))
                {
                    GrassClumpMeshGenerator2D.GenerateIntoProfile(profile);
                }

                if (GUILayout.Button("Generate XZ Mesh"))
                {
                    GrassClumpMeshGenerator3D.GenerateIntoProfile(profile);
                }
            }

            if (GUILayout.Button("Generate Both Meshes"))
            {
                GrassClumpMeshGenerator2D.GenerateIntoProfile(profile);
                GrassClumpMeshGenerator3D.GenerateIntoProfile(profile);
            }

            EditorGUILayout.HelpBox(
                "Changing mesh generation settings does not affect existing meshes until you regenerate.",
                MessageType.Info
            );
        }

        private void DrawAssetsBlock()
        {
            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_materialTemplate, new GUIContent("Material Template"));
            EditorGUILayout.PropertyField(_mainTextureOverride, new GUIContent("MainTex Override"));
            EditorGUILayout.PropertyField(_renderQueueOverride, new GUIContent("Render Queue Override"));
        }

        private void DrawShadowsBlock(Grass.GrassRenderProfile profile)
        {
            EditorGUILayout.LabelField("Shadows / Lighting", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_castShadows, new GUIContent("Cast Shadows"));
            EditorGUILayout.PropertyField(_receiveShadows, new GUIContent("Receive Shadows"));
            EditorGUILayout.PropertyField(_emissionStrength, new GUIContent("Emission Strength"));

            if (profile.emissionStrength >= 0.95f)
            {
                EditorGUILayout.HelpBox(
                    "EmissionStrength is near 1 (unlit look). Receiving shadows may not be visible. " +
                    "Set EmissionStrength closer to 0 to see lighting/shadows (requires shader support).",
                    MessageType.Info
                );
            }
        }
    }
}
#endif
