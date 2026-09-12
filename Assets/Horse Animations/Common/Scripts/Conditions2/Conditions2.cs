using MalbersAnimations.Scriptables;
using UnityEditor;
using UnityEngine;

namespace MalbersAnimations.Conditions
{
    [System.Serializable]
    public struct Conditions2
    {
        [SerializeReference] public ConditionCore[] conditions;

        public bool active;

        public Conditions2(int length)
        {
            conditions = new ConditionCore[length];
            active = true;
        }

        /// <summary>  Conditions can be used the list has some conditions </summary>
        public readonly bool Valid => active && conditions != null && conditions.Length > 0;

        public readonly bool Evaluate(Object target)
        {
            if (!Valid) return true; //by default return true

            if (conditions[0] == null)
            {
                Debug.LogError($"[Null] Condition not Allowed. Please Check your conditions.", target);
                return false;
            }

            bool result = conditions[0].Evaluate(target); //Get the first one

            for (int i = 1; i < conditions.Length; i++) //start from the 2nd one
            {
                try
                {
                    bool nextResult = conditions[i].Evaluate(target);
                    result = conditions[i].OrAnd ? (result || nextResult) : (result && nextResult);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Null] Condition Result [{i}] [{conditions[i].DynamicName}]. Please Check your conditions.", target);
                    Debug.LogException(e, target);
                }
            }
            return result;
        }

        public readonly void Gizmos(Component comp)
        {
            if (conditions == null || conditions.Length == 0) return;

            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i].DebugCondition) conditions[i].DrawGizmos(comp);
            }
        }
    }


    /*EXTRAS CONDITIONS Combos */
    [System.Serializable]
    public struct Conditions2Int
    {
        public Conditions2 conditions;
        public IntReference value;
    }

    [System.Serializable]
    public struct Conditions2Float
    {
        public Conditions2 conditions;
        public float value;
    }

    [System.Serializable]
    public struct Conditions2Bool
    {
        public Conditions2 conditions;
        public bool value;
    }

    [System.Serializable]
    public struct Conditions2String
    {
        public Conditions2 conditions;
        public string value;
    }


#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Conditions2))]
    public class Conditions2Drawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var active = property.FindPropertyRelative("active");
            EditorGUI.BeginProperty(position, label, property);

            var rect1 = new Rect(position);// rect1.width -= 12;
            var activeRect = new Rect(rect1.x + rect1.width - 65, position.y, 20, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(activeRect, active, GUIContent.none);

            label.text += active.boolValue ? "" : " (Disabled)";

            using (new EditorGUI.DisabledGroupScope(!active.boolValue))
                EditorGUI.PropertyField(rect1, property.FindPropertyRelative("conditions"), label, true);

            //Had to painted twice, otherwise it wont show in the place I wanted
            EditorGUI.PropertyField(activeRect, active, GUIContent.none);

            EditorGUI.EndProperty();
            EditorGUI.indentLevel = indent;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("conditions"), label);
        }
    }
#endif
}