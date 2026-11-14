using UnityEditor;
using UnityEngine;

namespace MazeSystem.EditorTools
{
    [CustomEditor(typeof(MazeTransitionController))]
    public sealed class MazeTransitionControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var controller = (MazeTransitionController)target;

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🌙 MAZE TRANSITION CONTROLLER", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Sequences door closure, entrance fog wall, block travel & day opening.", EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space(4f);
                GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
                if (GUILayout.Button("⚡ 1-Click Auto Setup & Fill All Inspector Fields", GUILayout.Height(30f)))
                {
                    MazeOneClickSetup.OneClickSetup();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(4f);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("State", controller.State.ToString());
                    EditorGUILayout.TextField("Is Busy", controller.IsBusy ? "Yes" : "No");
                    EditorGUILayout.TextField("Queued Day / Night",
                        $"{(controller.HasQueuedDay ? "Day Queued" : "—")} / {(controller.HasQueuedNight ? "Night Queued" : "—")}");
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("manager"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fog"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gate"));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Night Transition Timing (Seconds)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("nightSafetyTimeout"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("doorRetryInterval"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fogFadeIn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blockSlideDuration"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swapSettleDelay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fogHoldAfterSwap"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fogFadeOut"));

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Day Transition Timing (Seconds)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dayDoorOpenDelay"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dayDoorTimeout"));

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("Manual Controls (Play Mode)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Trigger Night Swap", GUILayout.Height(24f)))
                        controller.HandleNightStart();
                    if (GUILayout.Button("Trigger Day Open", GUILayout.Height(24f)))
                        controller.HandleDayStart();
                }

                Repaint();
            }
        }
    }
}
