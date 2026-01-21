#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using WorldGrid.Unity.Assets;

namespace WorldGrid.Unity.Editor
{
    //[CustomPropertyDrawer(typeof(TileLibraryAsset.Entry))]
    public sealed class TileLibraryEntryDrawer : PropertyDrawer
    {
        private static string s_ActivePropertyPath;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float baseH = EditorGUI.GetPropertyHeight(property, label, true);

            // Only the active (selected) entry gets preview height
            if (!IsActive(property))
                return baseH;

            if (!property.isExpanded)
                return baseH;

            return baseH
                + EditorGUIUtility.singleLineHeight   // "Preview" foldout
                + 6f
                + 110f; // preview square area
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float baseH = EditorGUI.GetPropertyHeight(property, label, true);
            var baseRect = new Rect(position.x, position.y, position.width, baseH);

            // Draw property normally
            EditorGUI.PropertyField(baseRect, property, label, true);

            // Detect click to mark active entry
            if (Event.current.type == EventType.MouseDown && baseRect.Contains(Event.current.mousePosition))
            {
                s_ActivePropertyPath = property.propertyPath;
            }

            // Only draw preview UI for active entry
            if (!IsActive(property) || !property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            float y = baseRect.yMax + 4f;

            // Preview foldout (always open for active entry)
            var foldRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(foldRect, "Preview", EditorStyles.boldLabel);

            y += EditorGUIUtility.singleLineHeight + 4f;

            var previewRect = new Rect(position.x + 16f, y, position.width - 32f, 110f);

            if (Event.current.type == EventType.Repaint)
                DrawPreview(previewRect, property);

            EditorGUI.EndProperty();
        }

        private static bool IsActive(SerializedProperty property)
        {
            return s_ActivePropertyPath == property.propertyPath;
        }

        private static void DrawPreview(Rect rect, SerializedProperty property)
        {
            var asset = property.serializedObject.targetObject as TileLibraryAsset;
            if (asset == null)
                return;

            GUI.Box(rect, GUIContent.none);

            rect = new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, rect.height - 12f);

            float s = Mathf.Min(rect.width, rect.height);
            var sq = new Rect(
                rect.x + (rect.width - s) * 0.5f,
                rect.y + (rect.height - s) * 0.5f,
                s,
                s
            );

            var colorProp = property.FindPropertyRelative("color");
            var coordProp = property.FindPropertyRelative("tileCoord");
            var spanProp = property.FindPropertyRelative("tileSpan");
            var overrideUvProp = property.FindPropertyRelative("overrideUv");
            var uvMinProp = property.FindPropertyRelative("uvMin");
            var uvMaxProp = property.FindPropertyRelative("uvMax");

            if (asset.atlasTexture != null)
            {
                Rect uvRect;

                if (overrideUvProp.boolValue)
                {
                    Vector2 uvMin = uvMinProp.vector2Value;
                    Vector2 uvMax = uvMaxProp.vector2Value;
                    uvRect = new Rect(uvMin.x, uvMin.y, uvMax.x - uvMin.x, uvMax.y - uvMin.y);
                }
                else if (asset.TryGetEffectiveAtlasSize(out int w, out int h))
                {
                    var uv = TileLibraryAsset.ComputeUvFromTileCoord(
                        w, h,
                        asset.tilePixelSize,
                        asset.paddingPixels,
                        asset.originTopLeft,
                        coordProp.vector2IntValue,
                        spanProp.vector2IntValue
                    );
                    uvRect = new Rect(uv.UMin, uv.VMin, uv.UMax - uv.UMin, uv.VMax - uv.VMin);
                }
                else
                {
                    return;
                }

                GUI.DrawTextureWithTexCoords(sq, asset.atlasTexture, uvRect, true);
            }
            else
            {
                EditorGUI.DrawRect(sq, colorProp.colorValue);
            }
        }
    }
}
#endif
