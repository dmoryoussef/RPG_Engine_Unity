#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using WorldGrid.Unity.Assets;

namespace WorldGrid.Unity.Editor
{
    //[CustomEditor(typeof(TileLibraryAsset))]
    public sealed class TileLibraryAssetEditor : UnityEditor.Editor
    {
        SerializedProperty _atlasLayoutMode;

        private void OnEnable()
        {
            _atlasLayoutMode = serializedObject.FindProperty("atlasLayoutMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Clean fix: keep Unity's default inspector rendering
            // (retains labels, layout, and any existing per-entry preview/icon behavior).
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Add explanation without taking over drawing.
            DrawLayoutHelp();

            EditorGUILayout.Space(10);
            DrawTools();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLayoutHelp()
        {
            if (_atlasLayoutMode == null)
                return;

            var mode = (TileLibraryAsset.AtlasLayoutMode)_atlasLayoutMode.enumValueIndex;

            switch (mode)
            {
                case TileLibraryAsset.AtlasLayoutMode.FromTexture:
                    EditorGUILayout.HelpBox(
                        "From Texture:\n" +
                        "Atlas size + tile grid are derived from the assigned atlas texture.\n" +
                        "Use this for spritesheets.",
                        MessageType.Info);
                    break;

                case TileLibraryAsset.AtlasLayoutMode.ManualPixels:
                    EditorGUILayout.HelpBox(
                        "Manual Pixels:\n" +
                        "You specify atlas WIDTH/HEIGHT in pixels.\n" +
                        "The tile grid is derived from: (atlas pixels) / (tile size + padding).\n\n" +
                        "Use this when you know the atlas pixel size (or want a fixed atlas size) even if the texture isn't assigned yet.",
                        MessageType.Info);
                    break;

                case TileLibraryAsset.AtlasLayoutMode.ManualGrid:
                    EditorGUILayout.HelpBox(
                        "Manual Grid:\n" +
                        "You specify the tile grid (columns/rows or tile count).\n" +
                        "The atlas pixel size is derived from: (grid) * (tile size + padding).\n\n" +
                        "Use this for procedural/no-texture tile sets where you want explicit control over tile count.",
                        MessageType.Info);
                    break;
            }
        }

        private void DrawTools()
        {
            var asset = (TileLibraryAsset)target;

            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate / Auto-Populate Tiles", GUILayout.Height(28)))
                {
                    Undo.RecordObject(asset, "Auto-Populate Tile Entries");
                    asset.AutoPopulateEntriesFromAtlasLayout();
                    EditorUtility.SetDirty(asset);
                }

                if (GUILayout.Button("Clear Tiles", GUILayout.Height(28)))
                {
                    Undo.RecordObject(asset, "Clear Tile Entries");
                    asset.ClearEntries();
                    EditorUtility.SetDirty(asset);
                }
            }

            bool canComputeUvs = asset.TryGetEffectiveAtlasSize(out _, out _);
            using (new EditorGUI.DisabledScope(!canComputeUvs))
            {
                if (GUILayout.Button("Compute UVs (store to uvMin/uvMax)", GUILayout.Height(22)))
                {
                    Undo.RecordObject(asset, "Compute Tile UVs");
                    asset.ComputeAndStoreUvsForEntries();
                    EditorUtility.SetDirty(asset);
                }
            }
        }
    }
}
#endif
