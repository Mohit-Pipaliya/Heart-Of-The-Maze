using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LadderClimbSystem))]
public class LadderClimbSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LadderClimbSystem ladder = (LadderClimbSystem)target;

        EditorGUILayout.Space(6);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.2f, 0.85f, 1f);

        EditorGUILayout.LabelField("🪜 4-Trigger Ladder Climb System", titleStyle);
        EditorGUILayout.Space(4);

        // Status Box
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"State: {ladder.CurrentState}", EditorStyles.boldLabel);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField($"Climb Progress: {(ladder.ClimbProgress * 100f):F1}%");

            if (ladder.PlayerTransform != null)
            {
                Vector3 playerPos = ladder.PlayerTransform.position;
                Vector3 t1Pos = ladder.GetTriggerPos(ladder.trigger1_BottomGround);
                Vector3 t2Pos = ladder.GetTriggerPos(ladder.trigger2_BottomMount);
                Vector3 t4Pos = ladder.GetDeckFloorPos();

                float distBottom = Mathf.Min(
                    Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(t1Pos.x, t1Pos.z)),
                    Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(t2Pos.x, t2Pos.z))
                );
                float distTop = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(t4Pos.x, t4Pos.z));

                EditorGUILayout.LabelField($"Player Y: {playerPos.y:F2}m");
                EditorGUILayout.LabelField($"Dist to Ground (T1/T2): {distBottom:F1}m {(distBottom <= 25f && playerPos.y < 50f ? "✅ [C] Climb Up Available" : "")}");
                EditorGUILayout.LabelField($"Dist to Platform (T4): {distTop:F1}m {(distTop <= 25f && playerPos.y >= 50f ? "✅ [C] Climb Down Available" : "")}");
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // One-Click Link Button for User's WatchTower Triggers
        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.4f);
        if (GUILayout.Button("🎯 Auto-Link WatchTower 4 Triggers & Reset", GUILayout.Height(36)))
        {
            Undo.RegisterFullObjectHierarchyUndo(ladder.gameObject, "Auto Link WatchTower Triggers");

            ladder.ForceLinkWatchTowerTriggers();
            EditorUtility.SetDirty(ladder);

            Debug.Log($"[LadderClimbSystem] Linked Triggers -> " +
                      $"T1: {ladder.trigger1_BottomGround?.name} @ {ladder.GetTriggerPos(ladder.trigger1_BottomGround)}, " +
                      $"T2: {ladder.trigger2_BottomMount?.name} @ {ladder.GetTriggerPos(ladder.trigger2_BottomMount)}, " +
                      $"T3: {ladder.trigger3_TopMount?.name} @ {ladder.GetTriggerPos(ladder.trigger3_TopMount)}, " +
                      $"T4: {ladder.trigger4_TopGround?.name} @ {ladder.GetDeckFloorPos()}");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);

        // Show live calculated coordinates
        if (ladder.trigger1_BottomGround != null || ladder.trigger2_BottomMount != null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📍 Trigger World Positions:", EditorStyles.miniBoldLabel);
            if (ladder.trigger1_BottomGround != null)
                EditorGUILayout.LabelField($" T1 (Ground Prompt): {ladder.GetTriggerPos(ladder.trigger1_BottomGround)}");
            if (ladder.trigger2_BottomMount != null)
                EditorGUILayout.LabelField($" T2 (Bottom Rung): {ladder.GetTriggerPos(ladder.trigger2_BottomMount)}");
            if (ladder.trigger3_TopMount != null)
                EditorGUILayout.LabelField($" T3 (Top Rung): {ladder.GetTriggerPos(ladder.trigger3_TopMount)}");
            if (ladder.trigger4_TopGround != null)
                EditorGUILayout.LabelField($" T4 (Platform Floor): {ladder.GetDeckFloorPos()}");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("⚠️ Ladder par SOLID Box Collider MAT lagana! Sirf 4 Triggers (Is Trigger = ON) lagane hain.", MessageType.Info);
        EditorGUILayout.Space(8);

        DrawDefaultInspector();
    }
}
