#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class ZombieAutoSetup
{
    [MenuItem("Tools/Heart Of The Maze/Setup Zombie AI on Selected GameObject", false, 50)]
    [MenuItem("GameObject/Heart Of The Maze/Setup Zombie AI", false, 10)]
    public static void SetupZombie()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Zombie Setup", "Kripya Hierarchy me zombie model ya GameObject select karein!", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(go, "Setup Zombie AI");

        // 1. Animator & Controller Setup
        Animator anim = go.GetComponent<Animator>();
        if (anim == null) anim = Undo.AddComponent<Animator>(go);

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animator/Zombie.controller");
        if (controller != null)
        {
            anim.runtimeAnimatorController = controller;
            if (controller is UnityEditor.Animations.AnimatorController ac)
            {
                ZombieAnimatorAutoSetup.ConfigureAnimatorController(ac);
            }
        }

        // 2. NavMeshAgent Setup
        NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
        if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(go);
        agent.speed = 1.6f;
        agent.angularSpeed = 260f;
        agent.acceleration = 14f;
        agent.stoppingDistance = 0.5f;
        agent.radius = 0.45f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;

        // 3. Capsule Collider Setup
        CapsuleCollider col = go.GetComponent<CapsuleCollider>();
        if (col == null) col = Undo.AddComponent<CapsuleCollider>(go);
        col.center = new Vector3(0f, 0.9f, 0f);
        col.radius = 0.45f;
        col.height = 1.8f;
        col.direction = 1;

        // 4. AudioSource Setup
        AudioSource audioSource = go.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = Undo.AddComponent<AudioSource>(go);
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 45f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        // 5. ZombieAI Script Setup
        ZombieAI zombie = go.GetComponent<ZombieAI>();
        if (zombie == null) zombie = Undo.AddComponent<ZombieAI>(go);
        zombie.maxHealth = 1000f;
        zombie.detectionRadius = 18f;
        zombie.attackRange = 2.2f;
        zombie.attackDamage = 15f;
        zombie.walkSpeed = 1.6f;
        zombie.runSpeed = 4.8f;
        zombie.wanderRadius = 18f;
        zombie.screamDuration = 1.9f;

        // Find Player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) zombie.player = playerObj.transform;

        EditorUtility.SetDirty(go);
        EditorUtility.DisplayDialog("Zombie AI Setup", $"Successfully configured Zombie AI on '{go.name}'!\n\n• Health: 1000\n• Walk & Wander: Active\n• Raad & Chase: Ready\n• Attack Range: 2.2m\n• Hit & Die animations: Ready", "OK");
    }
}
#endif
