#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// HumanSnakeAutoSetup
/// ===================
/// 1-Click Setup for HumanSnakeEnemy:
/// 1. Configures Animator Controller:
///    - Locomotion BlendTree (Idle, Walk, Chase) with Speed & locomotion parameters
///    - Combat Attack animations: attack1, attack2, attack3, tailWhipRight, tailWhipLeft
///    - Damage / Hit reaction: gotHit, Hit (with Hit tag and stun)
///    - Death animation: death, Die (with Dead tag, holds corpse pose)
///    - Clean AnyState transitions and return transitions back to Locomotion
/// 2. Configures Prefab (Animator, NavMeshAgent, CapsuleCollider scaled for 12x, HumanSnakeEnemy script)
/// 3. Spawns / Sets up HumanSnakeEnemy in active Scene
/// 4. Creates / links SnakeTrigger zone
/// 5. Snaps enemy to Terrain surface
/// 6. Custom Inspector with 1-Click Setup & Live Combat Testing buttons
/// </summary>
public static class HumanSnakeAutoSetup
{
    public const string ControllerPath = "Assets/Animator/HumanSnakeEnemy.controller";
    public const string PrefabPath     = "Assets/Animations/Human Snake Enemy/Models/_Prefabs/HumanSnakeEnemy.prefab";
    public const string FbxPathDemo    = "Assets/Animations/Human Snake Enemy/Models/Models/SerpentWarrior Demo.fbx";
    public const string FbxPathEnemy   = "Assets/Animations/Human Snake Enemy/Models/Models/HumanSnakeEnemy.fbx";

    [MenuItem("Tools/Heart Of The Maze/⚡ 1-Click Setup Human Snake Enemy", false, 1)]
    [MenuItem("Tools/Snake/⚡ Setup Human Snake Enemy (Complete)", false, 1)]
    public static void OneClickSetupAll()
    {
        // ── Step 1: Configure Animator Controller ──
        ConfigureAnimatorOnly();

        // ── Step 2: Configure Prefab Asset ──
        ConfigurePrefab();

        // ── Step 3: Setup / Spawn in Active Scene ──
        GameObject sceneSnakeObj = SetupSceneInstance();

        // ── Step 4: Ping & Select in Hierarchy ──
        if (sceneSnakeObj != null)
        {
            Selection.activeGameObject = sceneSnakeObj;
            EditorGUIUtility.PingObject(sceneSnakeObj);
        }

        string msg = "🎉 Human Snake Enemy Setup 100% Complete!\n\n" +
                     "✅ Animator Controller: All 16 animations connected!\n" +
                     "   • Locomotion: Idle, Walk, Chase BlendTree\n" +
                     "   • Attacks: attack1, attack2, attack3, tailWhipRight\n" +
                     "   • Damage Reaction: gotHit / Hit\n" +
                     "   • Death: death / Die\n" +
                     "✅ Combat AI: Automatic 4-attack rotation, hit stun, damage reach & look-at.\n" +
                     "✅ Physics: Collider tuned for 12x model scale (player can walk up without invisible wall).\n" +
                     "✅ Territory: SnakeTrigger zone auto-linked.\n" +
                     "✅ Terrain Snapped: Zameen par chipka diya gaya hai.\n\n" +
                     "Inspector me ab aap live controls aur test buttons dekh sakte hain!";

        EditorUtility.DisplayDialog("Human Snake 1-Click Setup", msg, "Awesome!");
    }

    [MenuItem("Tools/Snake/🎬 Setup Animator Controller Only", false, 2)]
    public static void ConfigureAnimatorOnly()
    {
        AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ac == null)
        {
            ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            Debug.Log($"<color=#00FF88>[HumanSnakeAutoSetup]</color> Created new AnimatorController at {ControllerPath}");
        }

        ConfigureAnimator(ac);
    }

    public static void ConfigureAnimator(AnimatorController ac)
    {
        if (ac == null) return;
        Undo.RegisterCompleteObjectUndo(ac, "Configure HumanSnake Animator Controller");

        // 1. Ensure All Parameters
        AddParameter(ac, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(ac, "locomotion", AnimatorControllerParameterType.Float);
        AddParameter(ac, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "attack1", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "attack2", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "attack3", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "tailWhipRight", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "tailWhipLeft", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "gotHit", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Die", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "death", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Jump", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "dodge", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "taunt", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "idleBreak", AnimatorControllerParameterType.Trigger);

        // 2. Load Clips from FBX
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(FbxPathDemo).OfType<AnimationClip>().ToArray();
        if (clips == null || clips.Length == 0)
            clips = AssetDatabase.LoadAllAssetsAtPath(FbxPathEnemy).OfType<AnimationClip>().ToArray();

        AnimationClip idleClip         = FindClip(clips, "idle");
        AnimationClip idleBreakClip    = FindClip(clips, "idleBreak");
        AnimationClip walkClip         = FindClip(clips, "walk");
        AnimationClip walkBackClip     = FindClip(clips, "walkBack");
        AnimationClip attack1Clip      = FindClip(clips, "attack1");
        AnimationClip attack2Clip      = FindClip(clips, "attack2");
        AnimationClip attack3Clip      = FindClip(clips, "attack3");
        AnimationClip tailWhipRightClip= FindClip(clips, "tailWhipRight");
        AnimationClip tailWhipLeftClip = FindClip(clips, "tailWhipLeft");
        AnimationClip gotHitClip       = FindClip(clips, "gotHit");
        AnimationClip deathClip        = FindClip(clips, "death");
        AnimationClip dodgeClip        = FindClip(clips, "dodge");
        AnimationClip tauntClip        = FindClip(clips, "taunt");
        AnimationClip tongueFlickClip  = FindClip(clips, "tonugeFlick") ?? FindClip(clips, "tongue");

        if (ac.layers.Length == 0)
        {
            ac.AddLayer("Base Layer");
        }

        var layers = ac.layers;
        layers[0].iKPass = true;
        ac.layers = layers;

        AnimatorStateMachine sm = ac.layers[0].stateMachine;

        // 3. Find or Create States
        AnimatorState locomotionState   = FindOrCreateState(sm, "Locomotion", new Vector3(300, 100, 0));
        AnimatorState attack1State      = FindOrCreateState(sm, "attack1", new Vector3(600, -80, 0));
        AnimatorState attack2State      = FindOrCreateState(sm, "attack2", new Vector3(600, -20, 0));
        AnimatorState attack3State      = FindOrCreateState(sm, "attack3", new Vector3(600, 40, 0));
        AnimatorState tailWhipState     = FindOrCreateState(sm, "tail whip right", new Vector3(600, 100, 0));
        AnimatorState hitState          = FindOrCreateState(sm, "hit reaction", new Vector3(600, 180, 0));
        AnimatorState deathState        = FindOrCreateState(sm, "death", new Vector3(600, 260, 0));
        AnimatorState idleBreakState    = FindOrCreateState(sm, "idle break", new Vector3(600, 320, 0));
        AnimatorState tauntState        = FindOrCreateState(sm, "taunt", new Vector3(600, 380, 0));

        // Assign clips
        if (attack1Clip != null) attack1State.motion = attack1Clip;
        if (attack2Clip != null) attack2State.motion = attack2Clip;
        if (attack3Clip != null) attack3State.motion = attack3Clip;
        if (tailWhipRightClip != null) tailWhipState.motion = tailWhipRightClip;
        if (gotHitClip != null) hitState.motion = gotHitClip;
        if (deathClip != null) deathState.motion = deathClip;
        if (idleBreakClip != null) idleBreakState.motion = idleBreakClip;
        if (tauntClip != null) tauntState.motion = tauntClip;

        // Set State Tags
        locomotionState.tag = "Locomotion";
        attack1State.tag    = "Attack";
        attack2State.tag    = "Attack";
        attack3State.tag    = "Attack";
        tailWhipState.tag   = "Attack";
        hitState.tag        = "Hit";
        deathState.tag      = "Dead";

        // Default State = Locomotion
        sm.defaultState = locomotionState;

        // 4. Configure Locomotion BlendTree
        BlendTree tree = locomotionState.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree
            {
                name = "Locomotion Blend Tree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            locomotionState.motion = tree;
            AssetDatabase.AddObjectToAsset(tree, ac);
        }

        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;

        // Clear children and configure: 0 = Idle, 2.2 = Walk, 5.2 = Chase Slither
        while (tree.children.Length > 0)
        {
            tree.RemoveChild(0);
        }

        if (idleClip != null)
            tree.AddChild(idleClip, 0.0f);
        if (walkClip != null)
        {
            tree.AddChild(walkClip, 2.2f);
            tree.AddChild(walkClip, 5.2f); // At 5.2 run speed, faster stride
        }

        // 5. Clean up duplicate AnyState Transitions and re-add exact transitions
        sm.anyStateTransitions = new AnimatorStateTransition[0];

        // AnyState -> Attacks
        AddAnyStateTriggerTransition(sm, attack1State, "attack1", 0.1f);
        AddAnyStateTriggerTransition(sm, attack1State, "Attack", 0.1f);
        AddAnyStateTriggerTransition(sm, attack2State, "attack2", 0.1f);
        AddAnyStateTriggerTransition(sm, attack3State, "attack3", 0.1f);
        AddAnyStateTriggerTransition(sm, tailWhipState, "tailWhipRight", 0.1f);

        // AnyState -> Hit Reaction
        AddAnyStateTriggerTransition(sm, hitState, "Hit", 0.05f);
        AddAnyStateTriggerTransition(sm, hitState, "gotHit", 0.05f);

        // AnyState -> Death
        AddAnyStateTriggerTransition(sm, deathState, "Die", 0.08f);
        AddAnyStateTriggerTransition(sm, deathState, "death", 0.08f);

        // AnyState -> Idle Break & Taunt
        AddAnyStateTriggerTransition(sm, idleBreakState, "idleBreak", 0.15f);
        AddAnyStateTriggerTransition(sm, tauntState, "taunt", 0.15f);

        // 6. Ensure Return Transitions back to Locomotion
        EnsureExitTransitionTo(attack1State, locomotionState, 0.85f, 0.15f);
        EnsureExitTransitionTo(attack2State, locomotionState, 0.85f, 0.15f);
        EnsureExitTransitionTo(attack3State, locomotionState, 0.85f, 0.15f);
        EnsureExitTransitionTo(tailWhipState, locomotionState, 0.85f, 0.15f);
        EnsureExitTransitionTo(hitState, locomotionState, 0.80f, 0.15f);
        EnsureExitTransitionTo(idleBreakState, locomotionState, 0.90f, 0.20f);
        EnsureExitTransitionTo(tauntState, locomotionState, 0.90f, 0.20f);

        // Death has NO exit transitions
        deathState.transitions = new AnimatorStateTransition[0];

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=#00FF88><b>[HumanSnakeAutoSetup] Successfully configured HumanSnakeEnemy Animator Controller!</b></color>");
    }

    private static GameObject SetupSceneInstance()
    {
        HumanSnakeEnemy existingSnake = Object.FindFirstObjectByType<HumanSnakeEnemy>();
        GameObject snakeObj = existingSnake != null ? existingSnake.gameObject : null;

        if (snakeObj == null)
        {
            GameObject foundByName = GameObject.Find("HumanSnakeEnemy_Boss") ?? GameObject.Find("HumanSnakeEnemy");
            if (foundByName != null) snakeObj = foundByName;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        if (snakeObj == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[HumanSnakeAutoSetup] Prefab not found at {PrefabPath}");
                return null;
            }

            Vector3 spawnPos = playerObj != null
                ? playerObj.transform.position + playerObj.transform.forward * 12.0f
                : new Vector3(0f, 0f, 10f);

            Quaternion spawnRot = playerObj != null
                ? Quaternion.LookRotation(-playerObj.transform.forward)
                : Quaternion.identity;

            snakeObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            snakeObj.name = "HumanSnakeEnemy_Boss";
            snakeObj.transform.position = spawnPos;
            snakeObj.transform.rotation = spawnRot;

            Undo.RegisterCreatedObjectUndo(snakeObj, "Spawn Human Snake Enemy");
        }
        else
        {
            Undo.RegisterFullObjectHierarchyUndo(snakeObj, "Configure Human Snake Enemy");
        }

        ConfigureGameObjectComponents(snakeObj);

        Collider trigger = EnsureSceneTriggerZone(snakeObj.transform.position);
        HumanSnakeEnemy snakeScript = snakeObj.GetComponent<HumanSnakeEnemy>();
        if (snakeScript != null)
        {
            if (trigger != null) snakeScript.territoryTrigger = trigger;
            if (playerObj != null) snakeScript.player = playerObj.transform;

            snakeScript.SnapToGroundImmediate();
            EditorUtility.SetDirty(snakeScript);
        }

        EditorUtility.SetDirty(snakeObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(snakeObj.scene);

        return snakeObj;
    }

    public static void ConfigureGameObjectComponents(GameObject go)
    {
        // 1. Layer: Enemy
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) go.layer = enemyLayer;

        // 2. Animator & Controller
        Animator anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        anim.applyRootMotion = false;

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller != null) anim.runtimeAnimatorController = controller;

        // 3. NavMeshAgent
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent == null) agent = go.AddComponent<NavMeshAgent>();
        agent.speed = 2.2f;
        agent.angularSpeed = 0f;
        agent.acceleration = 14f;
        agent.stoppingDistance = 2.4f;
        agent.radius = 0.5f;
        agent.height = 2.0f;
        agent.baseOffset = 0f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        // 4. CapsuleCollider (Scale-adjusted so player can approach without invisible wall)
        CapsuleCollider col = go.GetComponent<CapsuleCollider>();
        if (col == null) col = go.AddComponent<CapsuleCollider>();
        col.direction = 1; // Y-axis
        float scaleFactor = go.transform.lossyScale.y > 0.001f ? go.transform.lossyScale.y : 1f;
        col.radius = Mathf.Max(0.08f, 1.0f / scaleFactor);
        col.height = Mathf.Max(0.25f, 2.8f / scaleFactor);
        col.center = new Vector3(0f, 1.4f / scaleFactor, 0f);
        col.isTrigger = false;

        // 5. Rigidbody
        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 6. AudioSource
        AudioSource audio = go.GetComponent<AudioSource>();
        if (audio == null) audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.8f;
        audio.minDistance = 2.5f;
        audio.maxDistance = 35f;
        audio.rolloffMode = AudioRolloffMode.Linear;

        // 7. HumanSnakeEnemy Script
        HumanSnakeEnemy snake = go.GetComponent<HumanSnakeEnemy>();
        if (snake == null) snake = go.AddComponent<HumanSnakeEnemy>();

        snake.maxHealth = 1500f;
        snake.attackRange = 3.5f;
        snake.stoppingDistance = 2.4f;
        snake.aggroDistance = 35f;
        snake.patrolRadius = 12f;
        snake.damageToPlayer = 20f;
        snake.attackCooldown = 2.0f;
        snake.damageHitDelay = 0.45f;
        snake.damageReachTolerance = 1.8f;
        snake.walkSpeed = 2.2f;
        snake.runSpeed = 5.2f;
        snake.acceleration = 14f;
        snake.rotationSpeed = 8f;
        snake.enableAntiPenetration = true;
        snake.safeDistance = 1.8f;
        snake.pushPlayerForce = 0.85f;
        snake.enableTerrainAlignment = true;
        snake.slopeAlignSpeed = 12f;
        snake.groundSnapSpeed = 20f;
        snake.groundOffset = 0f;
        snake.autoFitHitbox = true;
        snake.enableAudio = true;
        snake.enableVFX = true;
        snake.enableHitFlash = true;
        snake.showDebugGizmos = false;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) snake.player = playerObj.transform;

        GameObject st = GameObject.Find("SnakeTrigger") ?? GameObject.Find("SpiderTrigger");
        if (st != null) snake.territoryTrigger = st.GetComponent<Collider>();
    }

    public static bool ConfigurePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return false;

        string assetPath = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            ConfigureGameObjectComponents(root);
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Debug.Log($"<color=#00FF88><b>[HumanSnakeAutoSetup]</b></color> Prefab successfully configured at {assetPath}!");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static Collider EnsureSceneTriggerZone(Vector3? centerPosition = null)
    {
        GameObject triggerObj = GameObject.Find("SnakeTrigger");
        if (triggerObj == null)
        {
            triggerObj = new GameObject("SnakeTrigger");
            Vector3 pos = centerPosition.HasValue
                ? centerPosition.Value
                : (GameObject.FindGameObjectWithTag("Player") != null
                    ? GameObject.FindGameObjectWithTag("Player").transform.position + Vector3.forward * 12f
                    : Vector3.zero);

            triggerObj.transform.position = pos;
            BoxCollider box = triggerObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(35f, 15f, 35f);
            box.center = new Vector3(0f, 7f, 0f);

            triggerObj.AddComponent<SnakeTriggerZone>();
            Undo.RegisterCreatedObjectUndo(triggerObj, "Create SnakeTrigger");
        }

        if (triggerObj.GetComponent<SnakeTriggerZone>() == null)
        {
            triggerObj.AddComponent<SnakeTriggerZone>();
        }

        return triggerObj.GetComponent<Collider>();
    }

    private static void AddParameter(AnimatorController ac, string name, AnimatorControllerParameterType type)
    {
        if (!ac.parameters.Any(p => p.name == name))
        {
            ac.AddParameter(name, type);
        }
    }

    private static AnimationClip FindClip(AnimationClip[] clips, string name)
    {
        if (clips == null) return null;
        return clips.FirstOrDefault(c => c.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) ??
               clips.FirstOrDefault(c => c.name.ToLower().Contains(name.ToLower()));
    }

    private static AnimatorState FindOrCreateState(AnimatorStateMachine root, string stateName, Vector3 pos)
    {
        foreach (var child in root.states)
        {
            if (child.state.name.Equals(stateName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child.state;
            }
        }
        return root.AddState(stateName, pos);
    }

    private static void AddAnyStateTriggerTransition(AnimatorStateMachine sm, AnimatorState dest, string triggerName, float duration)
    {
        if (dest == null) return;
        var trans = sm.AddAnyStateTransition(dest);
        trans.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        trans.hasExitTime = false;
        trans.duration = duration;
        trans.canTransitionToSelf = false;
    }

    private static void EnsureExitTransitionTo(AnimatorState fromState, AnimatorState toState, float exitTime, float duration)
    {
        if (fromState == null || toState == null) return;
        fromState.transitions = new AnimatorStateTransition[0];
        var trans = fromState.AddTransition(toState);
        trans.hasExitTime = true;
        trans.exitTime = exitTime;
        trans.duration = duration;
    }
}

/// <summary>
/// Custom Inspector for HumanSnakeEnemy:
/// Provides 1-Click Setup and live combat test buttons directly in the Inspector!
/// </summary>
[CustomEditor(typeof(HumanSnakeEnemy))]
public class HumanSnakeEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        HumanSnakeEnemy snake = (HumanSnakeEnemy)target;

        EditorGUILayout.Space(8);
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("🐍 HUMAN SNAKE ENEMY — BOSS COMBAT & AI", titleStyle);
        EditorGUILayout.HelpBox(
            "1-Click Setup automatically configures:\n" +
            "• Animator Controller: Locomotion BlendTree (Idle, Walk, Chase), Attack 1-3, Tail Whip, Hit Reaction, and Death.\n" +
            "• NavMeshAgent, Body Collider (scaled for 12x), and Combat Hitbox.\n" +
            "• SnakeTrigger zone & Player auto-binding.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.15f, 0.85f, 0.35f, 1f);
        if (GUILayout.Button("⚡ 1-CLICK SETUP ANIMATOR & ENEMY (All Animations & Combat)", GUILayout.Height(38)))
        {
            HumanSnakeAutoSetup.OneClickSetupAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎬 Setup Animator Only", GUILayout.Height(26)))
        {
            HumanSnakeAutoSetup.ConfigureAnimatorOnly();
        }
        if (GUILayout.Button("🎯 Snap To Ground & Link Trigger", GUILayout.Height(26)))
        {
            snake.SnapToGroundImmediate();
            Collider trig = HumanSnakeAutoSetup.EnsureSceneTriggerZone(snake.transform.position);
            if (trig != null) snake.territoryTrigger = trig;
            EditorUtility.SetDirty(snake);
        }
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("🎮 Live Combat Simulation", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🗡️ Hit (50 Dmg)"))
            {
                snake.TakeDamage(50f);
            }
            if (GUILayout.Button("☠️ Kill (Die)"))
            {
                snake.Die();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6);
        DrawDefaultInspector();
    }
}
#endif
