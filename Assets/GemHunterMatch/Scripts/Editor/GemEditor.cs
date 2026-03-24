using System;
using UnityEditor;
using UnityEngine;

namespace Match3
{
    /// <summary>
    /// Кастомный Editor для Gem. Добавляет выпадающий список выбора типа IGemEffect,
    /// так как стандартный SerializeReference не показывает типы в Inspector.
    /// </summary>
    [CustomEditor(typeof(Gem))]
    public class GemEditor : UnityEditor.Editor
    {
        private static readonly Type[] EffectTypes =
        {
            typeof(AttackEffect),
            typeof(DefenseEffect),
            typeof(HealEffect),
            typeof(BloodEffect),
            typeof(GoldEffect)
        };

        private static readonly string[] EffectTypeNames =
        {
            "None",
            "Attack",
            "Defense",
            "Heal",
            "Blood",
            "Gold"
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty gemTypeProperty = serializedObject.FindProperty("GemType");
            SerializedProperty effectProperty = serializedObject.FindProperty("Effect");
            SerializedProperty matchEffectPrefabsProperty = serializedObject.FindProperty("MatchEffectPrefabs");
            SerializedProperty uiSpriteProperty = serializedObject.FindProperty("UISprite");

            EditorGUILayout.PropertyField(gemTypeProperty);

            // Выпадающий список для Effect
            DrawEffectSelector(effectProperty);

            EditorGUILayout.PropertyField(matchEffectPrefabsProperty);
            EditorGUILayout.PropertyField(uiSpriteProperty);

            // Рисуем остальные свойства через DrawDefaultInspector, но только скрытые/наши
            DrawPropertiesExcluding(serializedObject, "m_Script", "GemType", "Effect", "MatchEffectPrefabs", "UISprite", "CurrentMatch");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEffectSelector(SerializedProperty effectProperty)
        {
            object currentValue = effectProperty.managedReferenceValue;
            int selectedIndex = 0;

            if (currentValue != null)
            {
                Type currentType = currentValue.GetType();
                for (int i = 0; i < EffectTypes.Length; i++)
                {
                    if (currentType == EffectTypes[i])
                    {
                        selectedIndex = i + 1;
                        break;
                    }
                }
            }

            int newSelectedIndex = EditorGUILayout.Popup("Effect", selectedIndex, EffectTypeNames);

            if (newSelectedIndex != selectedIndex)
            {
                if (newSelectedIndex == 0)
                {
                    effectProperty.managedReferenceValue = null;
                }
                else
                {
                    effectProperty.managedReferenceValue = Activator.CreateInstance(EffectTypes[newSelectedIndex - 1]);
                }
            }

            if (effectProperty.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                SerializedProperty iterator = effectProperty.Copy();
                SerializedProperty endProperty = effectProperty.GetEndProperty();

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
        }
    }
}
