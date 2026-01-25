#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldGrid.Runtime.Tiles;

public static class TilePropertyEditorUI
{
    public static void DrawTileProperties(SerializedProperty propertiesListProp)
    {
        if (propertiesListProp == null)
        {
            EditorGUILayout.HelpBox("Properties field not found. Ensure entry has [SerializeReference] List<TileProperty> properties.", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

        // Existing items
        for (int i = 0; i < propertiesListProp.arraySize; i++)
        {
            var elem = propertiesListProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();

            string typeName = elem.managedReferenceValue != null ? elem.managedReferenceValue.GetType().Name : "(null)";
            EditorGUILayout.LabelField(typeName, EditorStyles.boldLabel);

            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                propertiesListProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();

            // Draw fields of the managed reference
            EditorGUILayout.PropertyField(elem, includeChildren: true);

            EditorGUILayout.EndVertical();
        }

        // Add menu
        if (GUILayout.Button("Add Property"))
        {
            var menu = new GenericMenu();

            // Find all non-abstract TileProperty types
            var types = TypeCache.GetTypesDerivedFrom<TileProperty>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .OrderBy(t => t.Name);

            foreach (var t in types)
            {
                menu.AddItem(new GUIContent(t.Name), false, () =>
                {
                    int idx = propertiesListProp.arraySize;
                    propertiesListProp.InsertArrayElementAtIndex(idx);

                    var newElem = propertiesListProp.GetArrayElementAtIndex(idx);
                    newElem.managedReferenceValue = Activator.CreateInstance(t);

                    propertiesListProp.serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
