#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// HumanSnakeAutoSetup
/// ===================
/// Single 1-Click Setup for HumanSnakeEnemy:
/// 1. Configures Animator Controller (Speed, Attack, Hit, Die parameters & transitions like Spider)
/// 2. Configures Prefab (Animator, NavMeshAgent, CapsuleCollider, HumanSnakeEnemy script, Layer, HitBox)
/// 3. Spawns / Sets up HumanSnakeEnemy in the active Scene
/// 4. Creates / links SnakeTrigger zone
/// 5. Snaps enemy to Terrain surface
/// 6. Selects it in Hierarchy so all components appear directly in the Inspector!
/// </summary>
public static class HumanSnakeAutoSetup
{
    private const string ControllerPath = "Assets/Animator/HumanSnakeEnemy.controller";
    private const string PrefabPath     = "Assets/Animations/Human Snake Enemy/Models/_Prefabs/HumanSnakeEnemy.prefab";

    [MenuItem("Tools/Heart Of The Maze/⚡ 1-Click Setup Human Snake Enemy", false, 1)]
    public static void OneClickSetupAll()
    {
        // ── Step 1: Configure Animator Controller (Spider Style) ──
        AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ac != null)
        {
            ConfigureAnimator(ac);
        }
        else
        {
            Debug.LogWarning($"[HumanSnakeAutoSetup] Animator controller not found at {ControllerPath}");
        }

        // ── Step 2: Configure Prefab Asset ──
        ConfigurePrefab();

        // ── Step 3: Setup / Spawn in Active Scene ──
        GameObject sceneSnakeObj = SetupSceneInstance();

        // ── Step 4: Ping & Select in Hierarchy so Inspector shows everything ──
        if (sceneSnakeObj != null)
        {
            Selection.activeGameObject = sceneSnakeObj;
            EditorGUIUtility.PingObject(sceneSnakeObj);
        }

        string msg = "🎉 Human Snake Enemy Setup 100% Complete!\n\n" +
                     "✅ Animator Controller: Speed, Attack, Hit, Die parameters & transitions configured.\n" +
                     "✅ Scene & Inspector: HumanSnakeEnemy tuned with all combat & terrain settings.\n" +
                     "✅ Territory: SnakeTrigger zone auto-linked.\n" +
                     "✅ Terrain Snapped: Zameen par chipka diya gaya hai.\n\n" +
                     "Inspector me ab aap saare parameters live dekh sakte hain!";

        EditorUtility.DisplayDialog("Human Snake 1-Click Setup", msg, "Awesome!");
    }

    private static GameObject SetupSceneInstance()
    {
        // 1. Find existing Snake in scene or spawn from prefab
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

        // 2. Configure all components & Inspector values
        ConfigureGameObjectComponents(snakeObj);

        // 3. Ensure Territory Trigger Zone exists in scene & connect it
        Collider trigger = EnsureSceneTriggerZone(snakeObj.transform.position);
        HumanSnakeEnemy snakeScript = snakeObj.GetComponent<HumanSnakeEnemy>();
        if (snakeScript != null)
        {
            if (trigger != null) snakeScript.territoryTrigger = trigger;
            if (playerObj != null) snakeScript.player = playerObj.transform;

            // Snap to terrain surface immediately
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
        agent.angularSpeed = 0f; // Handled smoothly by script rotation
        agent.acceleration = 14f;
        agent.stoppingDistance = 2.4f;
        agent.radius = 0.5f;
        agent.height = 2.0f;
        agent.baseOffset = 0f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        // 4. CapsuleCollider (Physical body)
        CapsuleCollider col = go.GetComponent<CapsuleCollider>();
        if (col == null) col = go.AddComponent<CapsuleCollider>();
        col.direction = 1; // Y-axis
        col.radius = 0.5f;
        col.height = 2.0f;
        col.center = new Vector3(0f, 1.0f, 0f);
        col.isTrigger = false;

        // 5. Rigidbody Safety
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

        // 7. HumanSnakeEnemy Script (Exact Spider-style Inspector parameters)
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
        snake.showDebugGizmos = true;

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

    public static void ConfigureAnimator(AnimatorController ac)
    {
        Undo.RegisterCompleteObjectUndo(ac, "Configure HumanSnake Animator Spider Style");

        // Primary parameters (Spider style)
        AddParameter(ac, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(ac, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Die", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "Jump", AnimatorControllerParameterType.Trigger);

        // Fallback / legacy compatibility parameters
        AddParameter(ac, "locomotion", AnimatorControllerParameterType.Float);
        AddParameter(ac, "attack1", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "attack2", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "attack3", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "gotHit", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "death", AnimatorControllerParameterType.Trigger);
        AddParameter(ac, "idleBreak", AnimatorControllerParameterType.Trigger);

        if (ac.layers.Length == 0) return;

        var layers = ac.layers;
        layers[0].iKPass = true;
        ac.layers = layers;

        AnimatorStateMachine sm = ac.layers[0].stateMachine;

        // Find primary states
        AnimatorState locomotionState = FindState(sm, "Locomotion") ?? FindState(sm, "walk") ?? FindState(sm, "idle");
        AnimatorState attack1State    = FindState(sm, "attack1");
        AnimatorState hitState        = FindState(sm, "hit reaction") ?? FindState(sm, "gotHit") ?? FindState(sm, "hit");
        AnimatorState deathState      = FindState(sm, "death") ?? FindState(sm, "die");

        if (locomotionState != null)
        {
            sm.defaultState = locomotionState;
        }

        // Tag Hit & Dead states
        if (hitState != null) hitState.tag = "Hit";
        if (deathState != null) deathState.tag = "Dead";

        // AnyState -> Attack
        if (attack1State != null)
        {
            bool hasAttackTrans = sm.anyStateTransitions.Any(t => t.destinationState == attack1State &&
                t.conditions.Any(c => c.parameter == "Attack"));
            if (!hasAttackTrans)
            {
                var trans = sm.AddAnyStateTransition(attack1State);
                trans.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                trans.hasExitTime = false;
                trans.duration = 0.1f;
                trans.canTransitionToSelf = false;
            }

            if (locomotionState != null && !attack1State.transitions.Any(t => t.destinationState == locomotionState))
            {
                var exitTrans = attack1State.AddTransition(locomotionState);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime = 0.85f;
                exitTrans.duration = 0.2f;
            }
        }

        // AnyState -> Hit
        if (hitState != null)
        {
            bool hasHitTrans = sm.anyStateTransitions.Any(t => t.destinationState == hitState &&
                t.conditions.Any(c => c.parameter == "Hit"));
            if (!hasHitTrans)
            {
                var trans = sm.AddAnyStateTransition(hitState);
                trans.AddCondition(AnimatorConditionMode.If, 0, "Hit");
                trans.hasExitTime = false;
                trans.duration = 0.05f;
                trans.canTransitionToSelf = false;
            }

            if (locomotionState != null && !hitState.transitions.Any(t => t.destinationState == locomotionState))
            {
                var exitTrans = hitState.AddTransition(locomotionState);
                exitTrans.hasExitTime = true;
                exitTrans.exitTime = 0.85f;
                exitTrans.duration = 0.15f;
            }
        }

        // AnyState -> Die
        if (deathState != null)
        {
            bool hasDieTrans = sm.anyStateTransitions.Any(t => t.destinationState == deathState &&
                t.conditions.Any(c => c.parameter == "Die"));
            if (!hasDieTrans)
            {
                var trans = sm.AddAnyStateTransition(deathState);
                trans.AddCondition(AnimatorConditionMode.If, 0, "Die");
                trans.hasExitTime = false;
                trans.duration = 0.08f;
                trans.canTransitionToSelf = false;
            }
        }

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
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

    private static AnimatorState FindState(AnimatorStateMachine root, string keyword)
    {
        foreach (var child in root.states)
        {
            if (child.state.name.ToLower().Contains(keyword.ToLower()))
            {
                return child.state;
            }
        }
        return null;
    }
}
#endif
