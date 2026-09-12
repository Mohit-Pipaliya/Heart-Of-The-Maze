using UnityEditor;
using UnityEngine;

namespace MalbersAnimations
{
    [AddComponentMenu("Malbers/Utilities/Tools/Surface Tag")]

    public class SurfaceTag : MonoBehaviour, ISurface
    {
        public SurfaceID surface;

        public SurfaceID Surface => surface;
    }

    // create an Inspector Editor
#if UNITY_EDITOR

    [UnityEditor.CustomEditor(typeof(SurfaceTag))]
    public class SurfaceTagEditor : UnityEditor.Editor
    {
        SerializedProperty surface;

        private void OnEnable()
        {
            surface = serializedObject.FindProperty("surface");
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(surface);
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}

