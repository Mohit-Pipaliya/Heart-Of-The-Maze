using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public class IntroSequenceSetupWindow : EditorWindow
{
    private GameObject player;
    private GameObject box;
    private Object gettingUpAsset;
    private Object idleAsset;
    private Object lyingAsset;
    private float riseDistance = 25f;
    private float riseDuration = 8f;

    [MenuItem("Tools/Intro Sequence/Auto Setup")]
    private static void Open() => GetWindow<IntroSequenceSetupWindow>("Intro Sequence Setup");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Assign your player, lift box, and imported animation FBX files or clips. Setup creates and wires the runtime sequence.", MessageType.Info);
        player = (GameObject)EditorGUILayout.ObjectField("Player", player, typeof(GameObject), true);
        box = (GameObject)EditorGUILayout.ObjectField("Box", box, typeof(GameObject), true);
        gettingUpAsset = EditorGUILayout.ObjectField("Getting Up", gettingUpAsset, typeof(Object), false);
        idleAsset = EditorGUILayout.ObjectField("Idle", idleAsset, typeof(Object), false);
        lyingAsset = EditorGUILayout.ObjectField("Optional Lying", lyingAsset, typeof(Object), false);
        riseDistance = EditorGUILayout.FloatField("Rise Distance", riseDistance);
        riseDuration = EditorGUILayout.FloatField("Rise Duration", riseDuration);
        using (new EditorGUI.DisabledScope(player == null || box == null || gettingUpAsset == null || idleAsset == null))
            if (GUILayout.Button("Setup", GUILayout.Height(32))) Setup();
    }

    private void Setup()
    {
        AnimationClip getUp = ResolveClip(gettingUpAsset);
        AnimationClip idle = ResolveClip(idleAsset);
        AnimationClip lying = lyingAsset != null ? ResolveClip(lyingAsset) : null;
        if (getUp == null || idle == null) { EditorUtility.DisplayDialog("Intro Setup", "Could not find Getting Up and Idle clips.", "OK"); return; }
        WarnAnimationImport(gettingUpAsset, getUp);
        SetLoop(getUp, false); SetLoop(idle, true); if (lying != null) SetLoop(lying, true);
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null) animator = player.AddComponent<Animator>();
        animator.applyRootMotion = false;
        EnsureAnimatorController(animator, getUp, idle, lying);
        Transform eye = null;
        if (animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Head) != null)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            eye = head.Find("EyeCameraPoint");
            if (eye == null) { var go = new GameObject("EyeCameraPoint"); eye = go.transform; eye.SetParent(head, false); eye.localPosition = new Vector3(0f, 0.08f, 0.12f); }
        }
        else
        {
            eye = player.transform.Find("EyeCameraPoint");
            if (eye == null) { var go = new GameObject("EyeCameraPoint"); eye = go.transform; eye.SetParent(player.transform, false); eye.localPosition = new Vector3(0f, 1.65f, 0.08f); }
            Debug.LogWarning("[Intro Setup] Player rig is not Humanoid; EyeCameraPoint was placed at estimated eye height.", player);
        }
        var target = box.transform.Find("BoxTargetPoint");
        if (target == null) { var go = new GameObject("BoxTargetPoint"); target = go.transform; target.SetParent(box.transform.parent, true); }
        target.position = box.transform.position + Vector3.up * riseDistance;
        var lift = box.GetComponent<LiftBox>() ?? box.AddComponent<LiftBox>();
        Set(lift, "boxRoot", box.transform); Set(lift, "targetPoint", target); Set(lift, "riseDistance", riseDistance); Set(lift, "riseDuration", riseDuration);
        var camera = Camera.main;
        var camRig = eye.GetComponent<IntroCameraController>() ?? eye.gameObject.AddComponent<IntroCameraController>();
        camRig.Configure(camera, player.transform, camera != null ? camera.GetComponent<CameraController>() : null);
        var manager = player.GetComponent<IntroSequenceManager>() ?? player.AddComponent<IntroSequenceManager>();
        Set(manager, "player", player); Set(manager, "animator", animator); Set(manager, "liftBox", lift); Set(manager, "introCamera", camRig); Set(manager, "eyeCameraPoint", eye);
        Set(manager, "hasLyingAnimation", lying != null);
        var movement = player.GetComponent<PlayerController>();
        Set(manager, "movementBehaviour", movement);
        Set(manager, "promptUI", null);
        EditorUtility.SetDirty(animator); EditorUtility.SetDirty(manager); EditorUtility.SetDirty(lift); EditorUtility.SetDirty(camRig);
        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorUtility.DisplayDialog("Intro Setup", "Intro sequence is set up. Save the scene and follow the setup guide.", "OK");
    }

    private static AnimationClip ResolveClip(Object source)
    {
        if (source is AnimationClip clip) return clip;
        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.Contains("__preview__"));
    }

    private static void WarnAnimationImport(Object source, AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path)) path = AssetDatabase.GetAssetPath(clip);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        if (importer.animationType != ModelImporterAnimationType.Human)
            Debug.LogWarning($"[Intro Setup] '{source.name}' is not imported as Humanoid. Set Rig > Animation Type to Humanoid and Apply for better retargeting.", source);
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var item in clips)
            if (item.name == clip.name && !item.lockRootHeightY)
                Debug.LogWarning($"[Intro Setup] '{clip.name}' has Root Transform Position (Y) Bake Into Pose disabled. Enable it in the FBX Animation import settings to reduce vertical drift.", source);
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return;
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        foreach (var item in clips) if (item.name == clip.name) item.loopTime = loop;
        importer.clipAnimations = clips; importer.SaveAndReimport();
    }

    private static void EnsureAnimatorController(Animator animator, AnimationClip getUp, AnimationClip idle, AnimationClip lying)
    {
        const string dir = "Assets/Animations"; const string path = dir + "/IntroAnimator.controller";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Animations");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller != null) AssetDatabase.DeleteAsset(path);
        controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = controller.layers[0].stateMachine;
        var getState = sm.AddState("GettingUp"); getState.motion = getUp;
        var idleState = sm.AddState("Idle"); idleState.motion = idle;
        AnimatorState lyingState = null;
        if (lying != null) { lyingState = sm.AddState("Lying"); lyingState.motion = lying; sm.defaultState = lyingState; }
        else sm.defaultState = getState;
        var trans = getState.AddTransition(idleState); trans.hasExitTime = false; trans.hasFixedDuration = true; trans.AddCondition(AnimatorConditionMode.If, 0, "StartIdle");
        controller.AddParameter("StartIdle", AnimatorControllerParameterType.Trigger);
        if (lyingState != null)
        {
            var wake = lyingState.AddTransition(getState); wake.hasExitTime = false; wake.exitTime = 1f; wake.AddCondition(AnimatorConditionMode.If, 0, "StartGettingUp");
            controller.AddParameter("StartGettingUp", AnimatorControllerParameterType.Trigger);
        }
        animator.runtimeAnimatorController = controller;
        AssetDatabase.SaveAssets();
    }

    private static void Set(Object target, string property, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(property); if (p == null) return;
        if (value is Object obj) p.objectReferenceValue = obj; else if (value is float f) p.floatValue = f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
