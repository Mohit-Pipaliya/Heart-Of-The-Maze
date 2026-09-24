using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;

public class SpiderAnimatorAutoSetup : EditorWindow
{
    public AnimatorController controller;

    [MenuItem("Tools/Spider/Auto Setup Animator")]
    public static void ShowWindow()
    {
        GetWindow<SpiderAnimatorAutoSetup>("Spider Animator Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Spider Animator 'One-Click' Setup", EditorStyles.boldLabel);
        
        controller = (AnimatorController)EditorGUILayout.ObjectField("Spider Animator Controller", controller, typeof(AnimatorController), false);

        GUILayout.Space(10);
        if (GUILayout.Button("Setup Animator Automatically", GUILayout.Height(40)))
        {
            if (controller != null)
            {
                SetupController(controller);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Pehle apna SpiderAnimations.controller upar assign karein!", "OK");
            }
        }
        
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Ye script aapke Spider Animator ki sabhi galti transitions ko delete karke, naye parameters banayegi aur properly unhe connect karegi.", MessageType.Info);
    }

    private void SetupController(AnimatorController ac)
    {
        // 1. Parameters banayein
        AddParameter(ac, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(ac, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Die", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Jump", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = ac.layers[0].stateMachine;

        // Sabhi purani transitions clear karein
        foreach (var state in root.states)
        {
            state.state.transitions = new AnimatorStateTransition[0];
            state.state.tag = ""; // Reset tags
        }
        root.anyStateTransitions = new AnimatorStateTransition[0];

        // 2. States ko dhoondhe (naam ke hisaab se)
        AnimatorState idleWalkState = FindState(root, "walk");
        AnimatorState runState = FindState(root, "run");
        AnimatorState attackState = FindState(root, "attack");
        AnimatorState hitState = FindState(root, "hit");
        AnimatorState deadState = FindState(root, "dead") ?? FindState(root, "die");
        AnimatorState jumpState = FindState(root, "jump");

        if (idleWalkState != null) root.defaultState = idleWalkState;

        // 3. Movement Transitions (Walk <-> Run)
        if (idleWalkState != null && runState != null)
        {
            // Walk to Run
            var trans = idleWalkState.AddTransition(runState);
            trans.AddCondition(AnimatorConditionMode.Greater, 2.5f, "Speed"); // WalkSpeed(2) se zyada ho to Run
            trans.hasExitTime = false;
            trans.duration = 0.2f;

            // Run to Walk
            var transBack = runState.AddTransition(idleWalkState);
            transBack.AddCondition(AnimatorConditionMode.Less, 2.5f, "Speed");
            transBack.hasExitTime = false;
            transBack.duration = 0.2f;
        }

        // 4. AnyState Transitions for Actions
        if (attackState != null)
        {
            var trans = root.AddAnyStateTransition(attackState);
            trans.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            trans.hasExitTime = false;

            var exitTrans = attackState.AddTransition(idleWalkState);
            exitTrans.hasExitTime = true; // Attack pura hone ke baad wapas walk me jaye
            exitTrans.exitTime = 0.9f;
        }

        if (hitState != null)
        {
            hitState.tag = "Hit"; // Script ko batane ke liye ki enemy Hit ho raha hai
            var trans = root.AddAnyStateTransition(hitState);
            trans.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            trans.hasExitTime = false;

            var exitTrans = hitState.AddTransition(idleWalkState);
            exitTrans.hasExitTime = true;
            exitTrans.exitTime = 0.9f;
        }

        if (jumpState != null)
        {
            var trans = root.AddAnyStateTransition(jumpState);
            trans.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            trans.hasExitTime = false;

            var exitTrans = jumpState.AddTransition(idleWalkState);
            exitTrans.hasExitTime = true;
            exitTrans.exitTime = 0.8f;
        }

        if (deadState != null)
        {
            var trans = root.AddAnyStateTransition(deadState);
            trans.AddCondition(AnimatorConditionMode.If, 0, "Die");
            trans.hasExitTime = false;
            // No exit transition, dead is final state
        }

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Success", "Aapka Spider Animator Controller successfully setup ho gaya hai! \nIsme Speed, Attack, Hit, Die aur Jump properly connect ho gaye hain.", "Great!");
    }

    private void AddParameter(AnimatorController ac, string name, AnimatorControllerParameterType type)
    {
        if (!ac.parameters.Any(p => p.name == name))
        {
            ac.AddParameter(name, type);
        }
    }

    private AnimatorState FindState(AnimatorStateMachine root, string keyword)
    {
        // Find state containing keyword (case-insensitive)
        foreach (var childState in root.states)
        {
            if (childState.state.name.ToLower().Contains(keyword.ToLower()))
            {
                return childState.state;
            }
        }
        return null;
    }
}
