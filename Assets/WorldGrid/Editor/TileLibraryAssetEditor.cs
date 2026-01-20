#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using WorldGrid.Unity.Assets;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Editor.Tiles
{
    [CustomEditor(typeof(TileLibraryAsset))]
    public sealed class TileLibraryAssetEditor : UnityEditor.Editor
    {
        private bool _showEntries = true;
        private bool _showAtlasPreview = true;

        private ReorderableList _entries;
        private SerializedProperty _entriesProp;

        private const float ThumbSize = 48f;
        private const float Padding = 6f;
        private const float ColumnGap = 8f;

        private void OnEnable()
        {
            _entriesProp = serializedObject.FindProperty(nameof(TileLibraryAsset.entries));

            _entries = new ReorderableList(serializedObject, _entriesProp, true, true, true, true);

            _entries.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Tile Entries (Thumbnail + Full Entry)");
            };

            // Let Unity decide element height based on the full nested Entry property drawer.
            // This avoids all overlap issues (tags/properties/SerializeReference foldouts).
            _entries.elementHeightCallback = index =>
            {
                var el = _entriesProp.GetArrayElementAtIndex(index);
                float h = EditorGUI.GetPropertyHeight(el, includeChildren: true);
                return Mathf.Max(ThumbSize, h) + Padding * 2f;
            };

            _entries.drawElementCallback = (rect, index, active, focused) =>
            {
                var asset = (TileLibraryAsset)target;
                var el = _entriesProp.GetArrayElementAtIndex(index);

                rect.y += Padding;
                rect.height -= Padding * 2f;

                // Thumbnail on left
                var thumbRect = new Rect(rect.x, rect.y, ThumbSize, ThumbSize);

                // Full entry on right (Unity draws all child fields, including tags/properties lists)
                float entryX = rect.x + ThumbSize + ColumnGap;
                var entryRect = new Rect(entryX, rect.y, rect.width - (ThumbSize + ColumnGap), rect.height);

                DrawEntryThumbnail(thumbRect, asset, el);

                // Important: draw the whole entry so lists/foldouts work naturally.
                EditorGUI.PropertyField(entryRect, el, GUIContent.none, includeChildren: true);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw everything except entries using default drawing
            DrawPropertiesExcluding(serializedObject, nameof(TileLibraryAsset.entries));

            EditorGUILayout.Space(10);

            _showAtlasPreview = EditorGUILayout.Foldout(_showAtlasPreview, "Atlas Preview", true);
            if (_showAtlasPreview)
                DrawAtlasPreview((TileLibraryAsset)target);

            
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Auto-Populate Entries From Atlas Layout", GUILayout.MaxWidth(320)))
                {
                    var asset = (TileLibraryAsset)target;
                    asset.AutoPopulateEntriesFromAtlasLayout();
                    EditorUtility.SetDirty(asset);
                }

                if (GUILayout.Button("Clear Entries", GUILayout.MaxWidth(120)))
                {
                    var asset = (TileLibraryAsset)target;

                    if (EditorUtility.DisplayDialog(
                            "Clear Entries",
                            "This will remove all tile entries from this TileLibraryAsset.\n\nThis cannot be undone (except via Ctrl+Z). Continue?",
                            "Clear",
                            "Cancel"))
                    {
                        Undo.RecordObject(asset, "Clear Tile Entries");
                        asset.ClearEntries();
                        EditorUtility.SetDirty(asset);
                    }
                }

                GUILayout.FlexibleSpace();
            }


            EditorGUILayout.Space(10);

            _showEntries = EditorGUILayout.Foldout(_showEntries, "Tile Entries", true);
            if (_showEntries)
                _entries.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawAtlasPreview(TileLibraryAsset asset)
        {
            var tex = asset.atlasTexture;
            if (tex == null)
            {
                EditorGUILayout.HelpBox("No Atlas Texture assigned.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{tex.width} x {tex.height}px", GUILayout.MaxWidth(160));
                if (GUILayout.Button("Ping Texture", GUILayout.MaxWidth(100)))
                    EditorGUIUtility.PingObject(tex);
                if (GUILayout.Button("Select", GUILayout.MaxWidth(60)))
                    Selection.activeObject = tex;
            }

            float w = EditorGUIUtility.currentViewWidth - 40f;
            float aspect = (float)tex.height / tex.width;
            float h = Mathf.Clamp(w * aspect, 80f, 280f);

            Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.ExpandWidth(true));
            EditorGUI.DrawPreviewTexture(r, tex, null, ScaleMode.ScaleToFit);
        }

        private static void DrawEntryThumbnail(Rect rect, TileLibraryAsset asset, SerializedProperty entryProp)
        {
            var tex = asset.atlasTexture;
            if (tex == null)
            {
                EditorGUI.HelpBox(rect, "No\nAtlas", MessageType.Info);
                return;
            }

            var tileCoord = entryProp.FindPropertyRelative(nameof(TileLibraryAsset.Entry.tileCoord));
            var tileSpan = entryProp.FindPropertyRelative(nameof(TileLibraryAsset.Entry.tileSpan));
            var overrideUv = entryProp.FindPropertyRelative(nameof(TileLibraryAsset.Entry.overrideUv));
            var uvMin = entryProp.FindPropertyRelative(nameof(TileLibraryAsset.Entry.uvMin));
            var uvMax = entryProp.FindPropertyRelative(nameof(TileLibraryAsset.Entry.uvMax));

            RectUv uv;
            if (overrideUv.boolValue)
            {
                Vector2 min = uvMin.vector2Value;
                Vector2 max = uvMax.vector2Value;
                uv = new RectUv(min.x, min.y, max.x, max.y);
            }
            else
            {
                Vector2Int coord = tileCoord.vector2IntValue;
                Vector2Int span = tileSpan.vector2IntValue;

                uv = TileLibraryAsset.ComputeUvFromTileCoord(
                    tex.width, tex.height,
                    asset.tilePixelSize, asset.paddingPixels,
                    asset.originTopLeft,
                    coord, span
                );
            }

            var texCoords = new Rect(uv.UMin, uv.VMin, uv.UMax - uv.UMin, uv.VMax - uv.VMin);

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
            GUI.DrawTextureWithTexCoords(rect, tex, texCoords, true);
        }
    }
}
#endif
