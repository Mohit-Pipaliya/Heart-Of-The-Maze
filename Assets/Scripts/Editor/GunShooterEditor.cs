using UnityEditor;
using UnityEngine;
using TMPro;

[CustomEditor(typeof(GunShooter))]
public class GunShooterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GunShooter shooter = (GunShooter)target;

        EditorGUILayout.Space(8);
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("🔫 M4 AMMO & MAGAZINE SYSTEM", headerStyle);

        EditorGUILayout.HelpBox(
            "• Mag Capacity: 40 Bullets / Round.\n" +
            "• Max Carry: 120 Reserve Bullets.\n" +
            "• Press [R] to Reload (takes bullets from reserve to fill mag to 40).\n" +
            "• Press [E] to Pick Up 3D Magazine in world (attaches to empty holster socket).\n" +
            "• Press [M] to Change Magazine (plays animation, swaps mag, restocks 120 bullets!).",
            MessageType.Info);

        EditorGUILayout.Space(6);

        // One-Click Setup Button
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
        if (GUILayout.Button("⚡ ONE-CLICK SETUP (Sockets & Magazine)", GUILayout.Height(34)))
        {
            Undo.RecordObject(shooter, "Setup Magazine System");
            SetupMagazineSystem(shooter);
            EditorUtility.SetDirty(shooter);
        }

        GUI.backgroundColor = new Color(0.35f, 0.7f, 1.0f);
        if (GUILayout.Button("📦 SPAWN 3D MAGAZINE PICKUP IN SCENE", GUILayout.Height(30)))
        {
            SpawnMagazinePickupInScene(shooter);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);

        // Standard Inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("🎮 Live Simulation & Testing", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
        if (GUILayout.Button("🔄 Test Reload (R)", GUILayout.Height(28)))
        {
            if (Application.isPlaying)
            {
                shooter.StartCoroutine(shooter.ReloadRoutine());
            }
            else
            {
                int needed = shooter.maxMagCapacity - shooter.currentMagAmmo;
                int toLoad = Mathf.Min(needed, shooter.reserveAmmo);
                shooter.reserveAmmo -= toLoad;
                shooter.currentMagAmmo += toLoad;
                EditorUtility.SetDirty(shooter);
            }
        }

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.95f);
        if (GUILayout.Button("🔁 Test Mag Change (M)", GUILayout.Height(28)))
        {
            if (Application.isPlaying)
            {
                shooter.StartCoroutine(shooter.ChangeMagazineRoutine());
            }
            else
            {
                shooter.reserveAmmo = shooter.maxReserveAmmo;
                shooter.currentMagAmmo = shooter.maxMagCapacity;
                shooter.hasSpareMagazine = false;
                EditorUtility.SetDirty(shooter);
            }
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = Color.white;
    }

    [MenuItem("Tools/Heart of the Maze/Setup Magazine System")]
    public static void MenuSetupMagazineSystem()
    {
        GunShooter shooter = Object.FindFirstObjectByType<GunShooter>();
        if (shooter != null)
        {
            SetupMagazineSystem(shooter);
            Debug.Log("<color=green>[MagazineSystem]</color> Successfully configured Magazine System!");
        }
        else
        {
            Debug.LogWarning("GunShooter not found in active scene!");
        }
    }

    [MenuItem("Tools/Heart of the Maze/Spawn 3D Magazine Pickup")]
    public static void MenuSpawnMagazinePickup()
    {
        GunShooter shooter = Object.FindFirstObjectByType<GunShooter>();
        SpawnMagazinePickupInScene(shooter);
    }

    private static void SetupMagazineSystem(GunShooter shooter)
    {
        PlayerController player = shooter.GetComponentInParent<PlayerController>()
            ?? Object.FindFirstObjectByType<PlayerController>();

        GunEquipSystem gunSystem = shooter.GetComponentInParent<GunEquipSystem>()
            ?? shooter.GetComponent<GunEquipSystem>()
            ?? Object.FindFirstObjectByType<GunEquipSystem>();

        // 1. Ensure Magazine Holster Socket on Player
        if (player != null && shooter.magazineHolsterSocket == null)
        {
            Transform existingSocket = player.transform.Find("MagazineHolsterPoint");
            if (existingSocket == null)
            {
                GameObject sockObj = new GameObject("MagazineHolsterPoint");
                Undo.RegisterCreatedObjectUndo(sockObj, "Create MagazineHolsterPoint");
                sockObj.transform.SetParent(player.transform, false);
                sockObj.transform.localPosition = new Vector3(0.20f, 0.85f, -0.05f);
                sockObj.transform.localEulerAngles = new Vector3(15f, -10f, 0f);
                existingSocket = sockObj.transform;
            }
            shooter.magazineHolsterSocket = existingSocket;
            if (gunSystem != null) gunSystem.magazineHolsterSocket = existingSocket;
        }

        // 2. Ensure Magazine Socket on Gun
        if (gunSystem != null && gunSystem.gun != null && shooter.gunMagazineSocket == null)
        {
            Transform gunT = gunSystem.gun.transform;
            Transform existingGunMag = gunT.Find("GunMagazineSocket");
            if (existingGunMag == null)
            {
                GameObject magSock = new GameObject("GunMagazineSocket");
                Undo.RegisterCreatedObjectUndo(magSock, "Create GunMagazineSocket");
                magSock.transform.SetParent(gunT, false);
                magSock.transform.localPosition = new Vector3(0f, -0.05f, 0.10f);
                magSock.transform.localEulerAngles = Vector3.zero;
                existingGunMag = magSock.transform;
            }
            shooter.gunMagazineSocket = existingGunMag;
        }

        // 3. Ensure Left Hand Socket
        if (shooter.leftHandSocket == null)
        {
            shooter.leftHandSocket = shooter.GetLeftHandSocket();
        }

        // 4. Ensure 3D Model Prefab is linked to GunShooter
        if (shooter.magazinePrefab == null)
        {
            GameObject magFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/M4 Magazine/source/M4Mag.fbx");
            if (magFbx != null)
            {
                shooter.magazinePrefab = magFbx;
            }
        }

        // 4. Ensure UI Prompt "Press E to Pickup Magazine"
        EnsureMagazineUIPrompt();

        EditorUtility.SetDirty(shooter);
        if (gunSystem != null) EditorUtility.SetDirty(gunSystem);
    }

    private static GameObject EnsureMagazineUIPrompt()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return null;

        Transform existingPrompt = canvas.transform.Find("Press E to Pickup Magazine");
        if (existingPrompt != null) return existingPrompt.gameObject;

        // Clone from "Press E to Pickup M4" or "Press E to Pickup Sword"
        Transform samplePrompt = canvas.transform.Find("Press E to Pickup M4")
            ?? canvas.transform.Find("Press E to Pickup Sword");

        GameObject newPrompt;
        if (samplePrompt != null)
        {
            newPrompt = Instantiate(samplePrompt.gameObject, canvas.transform, false);
            newPrompt.name = "Press E to Pickup Magazine";
            TextMeshProUGUI tmp = newPrompt.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = "Press E to Pick Up Magazine";
            }
        }
        else
        {
            newPrompt = new GameObject("Press E to Pickup Magazine", typeof(RectTransform));
            newPrompt.transform.SetParent(canvas.transform, false);
            TextMeshProUGUI tmp = newPrompt.AddComponent<TextMeshProUGUI>();
            tmp.text = "Press E to Pick Up Magazine";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        newPrompt.SetActive(false);
        Undo.RegisterCreatedObjectUndo(newPrompt, "Create Magazine UI Prompt");
        return newPrompt;
    }

    private static void SpawnMagazinePickupInScene(GunShooter shooter)
    {
        Vector3 spawnPos = Vector3.zero;
        if (shooter != null)
        {
            spawnPos = shooter.transform.position + shooter.transform.forward * 1.8f + Vector3.up * 0.35f;
        }
        else
        {
            PlayerController p = Object.FindFirstObjectByType<PlayerController>();
            if (p != null) spawnPos = p.transform.position + p.transform.forward * 1.8f + Vector3.up * 0.35f;
        }

        // Create Magazine Pickup GameObject
        GameObject pickupObj = new GameObject("M4 Magazine Pickup");
        Undo.RegisterCreatedObjectUndo(pickupObj, "Spawn M4 Magazine Pickup");
        pickupObj.transform.position = spawnPos;

        BoxCollider col = pickupObj.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.2f, 1.2f, 1.2f);

        MagazinePickup pickupScript = pickupObj.AddComponent<MagazinePickup>();
        pickupScript.pickupPrompt = EnsureMagazineUIPrompt();

        // 3D Visual Mesh from User's M4 Magazine 3D Model
        GameObject magAsset = shooter != null && shooter.magazinePrefab != null
            ? shooter.magazinePrefab
            : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/M4 Magazine/source/M4Mag.fbx");

        GameObject magVisual;
        if (magAsset != null)
        {
            magVisual = (GameObject)PrefabUtility.InstantiatePrefab(magAsset, pickupObj.transform);
            magVisual.name = "Magazine_Visual";
            magVisual.transform.localPosition = Vector3.zero;
            magVisual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }
        else
        {
            magVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            magVisual.name = "Magazine_Visual";
            magVisual.transform.SetParent(pickupObj.transform, false);
            magVisual.transform.localScale = new Vector3(0.045f, 0.22f, 0.09f);
            Collider visualCol = magVisual.GetComponent<Collider>();
            if (visualCol != null) DestroyImmediate(visualCol);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Model/M4 Magazine/Materials/Mag_mat1.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Model/M4 Magazine/Materials/M_M4Magazine.mat");
            if (mat != null)
            {
                Renderer r = magVisual.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
        }

        Selection.activeGameObject = pickupObj;
        Debug.Log($"<color=green>[MagazineSystem]</color> Spawned M4 Magazine Pickup at {spawnPos}");
    }
}
