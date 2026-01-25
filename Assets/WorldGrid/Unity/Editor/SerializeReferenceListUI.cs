#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldGrid.Runtime.Tiles;

public static class SerializeReferenceListUI
{
    public static void DrawTilePropertyList(SerializedProperty listProp)
    {
        if (listProp == null || !listProp.isArray)
        {
            EditorGUILayout.HelpBox("Expected an array/list property.", MessageType.Error);
            return;
        }

        // Draw existing elements
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();

            string label = elem.managedReferenceValue != null
                ? elem.managedReferenceValue.GetType().Name
                : "(null)";

            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (GUILayout.Button("Set Type", GUILayout.Width(70)))
                ShowAddMenu(listProp, setExistingIndex: i);

            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                listProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();

            // Draw fields (only shows when managedReferenceValue is non-null)
            EditorGUILayout.PropertyField(elem, includeChildren: true);

            EditorGUILayout.EndVertical();
        }

        // Add new element
        if (GUILayout.Button("Add Property"))
            ShowAddMenu(listProp, setExistingIndex: null);
    }

    private static void ShowAddMenu(SerializedProperty listProp, int? setExistingIndex)
    {
        var menu = new GenericMenu();

        var types = TypeCache.GetTypesDerivedFrom<TileProperty>()
            .Where(t => !t.IsAbstract && !t.IsGenericType)
            .OrderBy(t => t.Name);

        foreach (var t in types)
        {
            menu.AddItem(new GUIContent(t.Name), false, () =>
            {
                int index;
                if (setExistingIndex.HasValue)
                {
                    index = setExistingIndex.Value;
                }
                else
                {
                    index = listProp.arraySize;
                    listProp.InsertArrayElementAtIndex(index);
                }

                var elem = listProp.GetArrayElementAtIndex(index);
                elem.managedReferenceValue = Activator.CreateInstance(t);

                listProp.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }
}
#endif
