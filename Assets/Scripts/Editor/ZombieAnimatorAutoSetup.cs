#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// ZombieAnimatorAutoSetup
/// ======================
/// 1-Click tool to automatically configure Animator parameters, states, 
/// and transitions with conditions for the Zombie Animator Controller.
/// </summary>
public class ZombieAnimatorAutoSetup : EditorWindow
{
    public AnimatorController targetController;

    [MenuItem("Tools/Heart Of The Maze/1-Click Setup Zombie Animator", false, 1)]
    public static void OneClickSetup()
    {
        // 1. Try to load from default path
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animator/Zombie.controller");
        
        // 2. Fallback to active selection if not found
        if (controller == null && Selection.activeObject is AnimatorController selectedAc)
        {
            controller = selectedAc;
        }

        if (controller == null)
        {
            // Open window so user can assign
            ShowWindow();
            return;
        }

        ConfigureAnimatorController(controller);
    }

    [MenuItem("Tools/Heart Of The Maze/Zombie Animator Setup Window", false, 2)]
    public static void ShowWindow()
    {
        ZombieAnimatorAutoSetup window = GetWindow<ZombieAnimatorAutoSetup>("Zombie Animator Setup");
        window.minSize = new Vector2(380, 240);
        window.targetController = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animator/Zombie.controller");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("🧟 Zombie Animator 1-Click Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Ye tool aapke Zombie Animator Controller me saare Parameters (Speed, Raad, Run, Attack, Hit, Die, IsMoving, IsRunning, IsDead) aur Conditions wali Transitions automatically set kar deta hai.", MessageType.Info);
        
        GUILayout.Space(12);
        targetController = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", targetController, typeof(AnimatorController), false);

        GUILayout.Space(15);
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
        if (GUILayout.Button("⚡ Setup Animator Controller (1-Click)", GUILayout.Height(42)))
        {
            if (targetController != null)
            {
                ConfigureAnimatorController(targetController);
            }
            else
            {
                EditorUtility.DisplayDialog("Missing Controller", "Kripya 'Zombie.controller' select karein!", "OK");
            }
        }
        GUI.backgroundColor = Color.white;
    }

    public static void ConfigureAnimatorController(AnimatorController ac)
    {
        Undo.RegisterCompleteObjectUndo(ac, "Setup Zombie Animator Controller");

        // ── 1. Create Parameters ─────────────────────────────────────────────
        AddParameter(ac, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(ac, "IsMoving", AnimatorControllerParameterType.Bool);
        AddParameter(ac, "IsRunning", AnimatorControllerParameterType.Bool);
        AddParameter(ac, "IsSliding", AnimatorControllerParameterType.Bool);
        AddParameter(ac, "Slide", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Raad", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Die", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "IsDead", AnimatorControllerParameterType.Bool);
        AddParameter(ac, "Turn", AnimatorControllerParameterType.Float);

        // Enable IK Pass on Base Layer for procedural Foot IK and LookAt
        if (ac.layers.Length > 0)
        {
            var layers = ac.layers;
            layers[0].iKPass = true;
            ac.layers = layers;
        }

        AnimatorStateMachine root = ac.layers[0].stateMachine;

        // Enable IK On Feet across all states
        foreach (var cs in root.states)
        {
            cs.state.iKOnFeet = true;
        }

        // ── 2. Find States ───────────────────────────────────────────────────
        AnimatorState walkState   = FindState(root, "walk");
        AnimatorState raadState   = FindState(root, "raad") ?? FindState(root, "scream");
        AnimatorState runState    = FindState(root, "run");
        AnimatorState slideState  = FindState(root, "slide") ?? FindState(root, "crawl");
        AnimatorState attackState = FindState(root, "attack");
        AnimatorState hitState    = FindState(root, "hit");
        AnimatorState dieState    = FindState(root, "die") ?? FindState(root, "death");

        if (walkState == null)
        {
            EditorUtility.DisplayDialog("Setup Error", "Animator controller me 'Walk' state nahi mila!", "OK");
            return;
        }

        // Set Walk as default state
        root.defaultState = walkState;

        // ── 3. Reset Old Transitions ────────────────────────────────────────
        while (root.anyStateTransitions.Length > 0)
        {
            root.RemoveAnyStateTransition(root.anyStateTransitions[0]);
        }
        foreach (var childState in root.states)
        {
            while (childState.state.transitions.Length > 0)
            {
                childState.state.RemoveTransition(childState.state.transitions[0]);
            }
        }

        // ── 4. Build Transitions & Conditions ───────────────────────────────

        // ── TOP PRIORITY: AnyState -> Die (Death animation can NEVER be interrupted!) ──
        if (dieState != null)
        {
            dieState.tag = "Dead";
            
            var t1 = root.AddAnyStateTransition(dieState);
            t1.AddCondition(AnimatorConditionMode.If, 0, "Die");
            t1.hasExitTime = false;
            t1.duration = 0.08f;
            t1.canTransitionToSelf = false;

            var t2 = root.AddAnyStateTransition(dieState);
            t2.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            t2.hasExitTime = false;
            t2.duration = 0.08f;
            t2.canTransitionToSelf = false;
        }

        // A) Walk -> Raad (Player alert scream)
        if (walkState != null && raadState != null)
        {
            var t = walkState.AddTransition(raadState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Raad");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.canTransitionToSelf = false;
        }

        // B) Raad -> Run (Scream finished -> Start chasing)
        if (raadState != null && runState != null)
        {
            var t = raadState.AddTransition(runState);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.hasExitTime = true;
            t.exitTime = 0.88f;
            t.duration = 0.2f;
            t.canTransitionToSelf = false;
        }

        // C) Walk <-> Run (Locomotion transitions)
        if (walkState != null && runState != null)
        {
            // Walk -> Run
            var t1 = walkState.AddTransition(runState);
            t1.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
            t1.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t1.hasExitTime = false;
            t1.duration = 0.2f;
            t1.canTransitionToSelf = false;

            // Run -> Walk
            var t2 = runState.AddTransition(walkState);
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t2.hasExitTime = false;
            t2.duration = 0.25f;
            t2.canTransitionToSelf = false;
        }

        // D) Run -> Attack
        if (runState != null && attackState != null)
        {
            var t = runState.AddTransition(attackState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }

        // E) Walk -> Attack
        if (walkState != null && attackState != null)
        {
            var t = walkState.AddTransition(attackState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.hasExitTime = false;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }

        // F) Attack -> Run (After attack, if still chasing)
        if (attackState != null && runState != null)
        {
            var t = attackState.AddTransition(runState);
            t.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.hasExitTime = true;
            t.exitTime = 0.85f;
            t.duration = 0.2f;
            t.canTransitionToSelf = false;
        }

        // G) Attack -> Walk (After attack, if stopped running)
        if (attackState != null && walkState != null)
        {
            var t = attackState.AddTransition(walkState);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.hasExitTime = true;
            t.exitTime = 0.85f;
            t.duration = 0.25f;
            t.canTransitionToSelf = false;
        }

        // H) AnyState -> Hit (Reaction hit on receiving damage — only for upright zombies and NOT dead!)
        if (hitState != null)
        {
            hitState.tag = "Hit";
            var t = root.AddAnyStateTransition(hitState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");
            t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = false;

            // Hit -> Run (Resumes chase after hit reaction)
            if (runState != null)
            {
                var exitToRun = hitState.AddTransition(runState);
                exitToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");
                exitToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
                exitToRun.hasExitTime = true;
                exitToRun.exitTime = 0.75f;
                exitToRun.duration = 0.15f;
                exitToRun.canTransitionToSelf = false;
            }

            // Hit -> Slide (If wounded <= 600 HP)
            if (slideState != null)
            {
                var exitToSlide = hitState.AddTransition(slideState);
                exitToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");
                exitToSlide.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
                exitToSlide.hasExitTime = true;
                exitToSlide.exitTime = 0.75f;
                exitToSlide.duration = 0.15f;
                exitToSlide.canTransitionToSelf = false;
            }
        }

        // I) Slide Transitions (Health <= 600) — Once sliding, strictly STAY sliding on ground (unless Dead)
        if (slideState != null)
        {
            // AnyState -> Slide (STRICTLY when NOT dead!)
            var tSlide = root.AddAnyStateTransition(slideState);
            tSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");
            tSlide.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            tSlide.hasExitTime = false;
            tSlide.duration = 0.12f;
            tSlide.canTransitionToSelf = false;

            // Run -> Slide
            if (runState != null)
            {
                var tRunSlide = runState.AddTransition(slideState);
                tRunSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");
                tRunSlide.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
                tRunSlide.hasExitTime = false;
                tRunSlide.duration = 0.12f;
                tRunSlide.canTransitionToSelf = false;
            }

            // Walk -> Slide
            if (walkState != null)
            {
                var tWalkSlide = walkState.AddTransition(slideState);
                tWalkSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");
                tWalkSlide.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
                tWalkSlide.hasExitTime = false;
                tWalkSlide.duration = 0.12f;
                tWalkSlide.canTransitionToSelf = false;
            }
        }

        // ── 5. Save & Refresh ────────────────────────────────────────────────
        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();

        string msg = $"🎉 Zombie Animator Controller Successfully Configured!\n\n" +
                     $"• Parameters Created: Speed, IsMoving, IsRunning, Raad, Attack, Hit, Die, IsDead\n" +
                     $"• Transitions Connected:\n" +
                     $"   - Walk → Raad (Trigger: Raad)\n" +
                     $"   - Raad → Run (ExitTime)\n" +
                     $"   - Walk ↔ Run (IsRunning)\n" +
                     $"   - Run / Walk → Attack (Trigger: Attack)\n" +
                     $"   - Attack → Run / Walk (ExitTime)\n" +
                     $"   - AnyState → Hit (Trigger: Hit)\n" +
                     $"   - AnyState → Die (Trigger: Die / IsDead)";

        Debug.Log($"<color=#00FF88><b>[Zombie Animator Setup]</b></color> Successfully configured '{ac.name}'!");
        EditorUtility.DisplayDialog("Zombie Animator Setup", msg, "Awesome!");
    }

    private static void AddParameter(AnimatorController ac, string name, AnimatorControllerParameterType type)
    {
        if (!ac.parameters.Any(p => p.name == name))
        {
            ac.AddParameter(name, type);
        }
    }

    private static AnimatorState FindState(AnimatorStateMachine root, string keyword)
    {
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
#endif
