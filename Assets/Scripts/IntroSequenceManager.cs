using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

/// <summary>Controls the underground lift wake-up sequence.</summary>
public class IntroSequenceManager : MonoBehaviour
{
    public enum SequenceState { Lying, GettingUp, WaitForInput, BoxRising, Gameplay }
    [Header("References")]
    [SerializeField] private GameObject player;
    [SerializeField] private Animator animator;
    [SerializeField] private LiftBox liftBox;
    [SerializeField] private IntroCameraController introCamera;
    [SerializeField] private Transform eyeCameraPoint;
    [SerializeField] private IntroPromptUI promptUI;
    [SerializeField] private Behaviour movementBehaviour;
    [Header("Sequence")]
    [SerializeField] private KeyCode goUpKey = KeyCode.U;
    [SerializeField] private string lyingState = "Lying";
    [SerializeField] private string gettingUpState = "GettingUp";
    [SerializeField] private string gettingUpTrigger = "StartGettingUp";
    [SerializeField] private string idleTrigger = "StartIdle";
    [SerializeField] private bool hasLyingAnimation;
    [SerializeField] private float cameraReturnDuration = 0.7f;
    public UnityEvent onLying = new UnityEvent();
    public UnityEvent onGettingUp = new UnityEvent();
    public UnityEvent onWaitForInput = new UnityEvent();
    public UnityEvent onBoxRising = new UnityEvent();
    public UnityEvent onGameplay = new UnityEvent();
    public SequenceState State { get; private set; }

    private CharacterController _characterController;
    private NavMeshAgent _agent;
    private bool _movementWasEnabled;
    private bool _controllerWasEnabled;
    private bool _agentWasEnabled;

    private void Awake()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");
        if (animator == null && player != null) animator = player.GetComponentInChildren<Animator>();
        if (liftBox == null) liftBox = FindFirstObjectByType<LiftBox>();
        if (introCamera == null) introCamera = FindFirstObjectByType<IntroCameraController>();
        if (eyeCameraPoint == null && introCamera != null) eyeCameraPoint = introCamera.transform;
        _characterController = player != null ? player.GetComponent<CharacterController>() : null;
        _agent = player != null ? player.GetComponent<NavMeshAgent>() : null;
        if (movementBehaviour == null && player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null) movementBehaviour = pc;
        }
        if (promptUI == null) promptUI = IntroPromptUI.GetOrCreate();
    }

    private void Start()
    {
        if (player == null || animator == null || liftBox == null)
        {
            Debug.LogError($"[{nameof(IntroSequenceManager)}] Assign/find the player, animator, and LiftBox before starting.", this);
            enabled = false;
            return;
        }
        SetState(SequenceState.Lying);
        _movementWasEnabled = movementBehaviour != null && movementBehaviour.enabled;
        _controllerWasEnabled = _characterController != null && _characterController.enabled;
        _agentWasEnabled = _agent != null && _agent.enabled;
        if (movementBehaviour != null) movementBehaviour.enabled = false;
        if (_characterController != null) _characterController.enabled = false;
        if (_agent != null) _agent.enabled = false;
        animator.applyRootMotion = false;
        if (introCamera != null) introCamera.BeginIntro();
        if (hasLyingAnimation)
            SetState(SequenceState.Lying);
        else
            SetState(SequenceState.GettingUp);
    }

    private void Update()
    {
        if (State == SequenceState.Lying && IsStateComplete(lyingState))
        {
            animator.SetTrigger(gettingUpTrigger);
            SetState(SequenceState.GettingUp);
        }
        else if (State == SequenceState.GettingUp && IsGettingUpComplete())
        {
            animator.ResetTrigger(idleTrigger);
            animator.SetTrigger(idleTrigger);
            SetState(SequenceState.WaitForInput);
            if (promptUI != null) promptUI.Show();
        }
        else if (State == SequenceState.WaitForInput && GetKeyDown(goUpKey))
        {
            StartCoroutine(RiseSequence());
        }
    }

    private bool IsGettingUpComplete()
    {
        if (animator.IsInTransition(0)) return false;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(gettingUpState) && state.normalizedTime >= 1f;
    }

    private bool IsStateComplete(string stateName)
    {
        if (animator.IsInTransition(0)) return false;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(stateName) && state.normalizedTime >= 1f;
    }

    private IEnumerator RiseSequence()
    {
        if (promptUI != null) promptUI.Hide();
        SetState(SequenceState.BoxRising);
        player.transform.SetParent(liftBox.transform, true);
        yield return liftBox.RaiseToTarget();
        player.transform.SetParent(null, true);
        if (introCamera != null) yield return introCamera.BlendBackToNormal(cameraReturnDuration);
        if (animator != null && !string.IsNullOrEmpty(idleTrigger))
        {
            animator.ResetTrigger(idleTrigger);
            animator.SetTrigger(idleTrigger);
        }
        if (_agent != null && _agentWasEnabled) _agent.enabled = true;
        if (_characterController != null && _controllerWasEnabled) _characterController.enabled = true;
        if (movementBehaviour != null && _movementWasEnabled) movementBehaviour.enabled = true;
        if (introCamera != null) introCamera.CompleteIntro();
        SetState(SequenceState.Gameplay);
    }

    private void SetState(SequenceState next)
    {
        State = next;
        Debug.Log($"[{nameof(IntroSequenceManager)}] State: {next}", this);
        switch (next)
        {
            case SequenceState.Lying: onLying.Invoke(); break;
            case SequenceState.GettingUp: onGettingUp.Invoke(); break;
            case SequenceState.WaitForInput: onWaitForInput.Invoke(); break;
            case SequenceState.BoxRising: onBoxRising.Invoke(); break;
            case SequenceState.Gameplay: onGameplay.Invoke(); break;
        }
    }

    private static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            var keyControl = keyboard.FindKeyOnCurrentKeyboardLayout(key.ToString());
            if (keyControl != null) return keyControl.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        return false;
#endif
    }
}
