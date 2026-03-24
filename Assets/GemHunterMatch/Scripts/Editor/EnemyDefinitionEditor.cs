using System;
using UnityEditor;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Кастомный Editor для EnemyDefinition. Добавляет выпадающий список выбора типов IEnemyAction
    /// при добавлении элементов в Actions, так как SerializeReference не показывает типы в Inspector.
    /// </summary>
    [CustomEditor(typeof(EnemyDefinition))]
    public class EnemyDefinitionEditor : UnityEditor.Editor
    {
        private static readonly Type[] ActionTypes =
        {
            typeof(IntervalAttackAction),
            typeof(DefenseStanceAction)
        };

        private static readonly string[] ActionTypeNames =
        {
            "Interval Attack",
            "Defense Stance"
        };

        private int addActionTypeIndex;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty enemyNameProperty = serializedObject.FindProperty("EnemyName");
            SerializedProperty maxHealthProperty = serializedObject.FindProperty("MaxHealth");
            SerializedProperty actionsProperty = serializedObject.FindProperty("Actions");

            EditorGUILayout.PropertyField(enemyNameProperty);
            EditorGUILayout.PropertyField(maxHealthProperty);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            for (int i = 0; i < actionsProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = actionsProperty.GetArrayElementAtIndex(i);
                bool deleted = DrawActionElement(elementProperty, actionsProperty, i);
                if (deleted) i--;
            }

            EditorGUILayout.BeginHorizontal();
            addActionTypeIndex = EditorGUILayout.Popup("Add Action", addActionTypeIndex, ActionTypeNames);
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                actionsProperty.InsertArrayElementAtIndex(actionsProperty.arraySize);
                SerializedProperty newElement = actionsProperty.GetArrayElementAtIndex(actionsProperty.arraySize - 1);
                newElement.managedReferenceValue = Activator.CreateInstance(ActionTypes[addActionTypeIndex]);
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private bool DrawActionElement(SerializedProperty elementProperty, SerializedProperty actionsProperty, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            object currentValue = elementProperty.managedReferenceValue;
            string typeLabel = currentValue != null ? currentValue.GetType().Name : "None";

            EditorGUILayout.BeginHorizontal();
            bool expanded = elementProperty.isExpanded;
            elementProperty.isExpanded = EditorGUILayout.Foldout(expanded, $"[{index}] {typeLabel}", true);

            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                actionsProperty.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return true;
            }

            EditorGUILayout.EndHorizontal();

            if (elementProperty.isExpanded && currentValue != null)
            {
                EditorGUI.indentLevel++;
                SerializedProperty iterator = elementProperty.Copy();
                SerializedProperty endProperty = elementProperty.GetEndProperty();

                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(iterator, endProperty))
                            break;

                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    while (iterator.NextVisible(false));
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            return false;
        }
    }
}
