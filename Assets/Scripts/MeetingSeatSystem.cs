using System.Collections;
using UnityEngine;

/// <summary>
/// Guides the player to the meeting seat. The player walks there manually; a
/// nearby C prompt starts the existing sit, seated-idle, and stand-up sequence.
/// No trigger or automatic pathfinding is used.
/// </summary>
public class MeetingSeatSystem : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Seat ke saamne player ka manual approach marker. Yahin aane par C prompt dikhega.")]
    public Transform seatArrivalPoint;
    [Tooltip("Player baithte waqt jis direction mein face karega.")]
    public Transform seatFaceTarget;
    [Tooltip("Optional C prompt panel. Empty ho to built-in HUD prompt use hoga.")]
    public GameObject joinPromptUI;
    [Tooltip("Meeting UI tabhi dikhegi jab yeh generator power available report kare. Empty ho to scene se auto-find hoga.")]
    public GeneratorLightPuzzle generatorPowerSource;
    [Tooltip("Optional beacon. Empty chhodne par Seat Arrival Point par runtime particle beacon banega.")]
    public ParticleSystem seatArrivalBeacon;
    [Min(0.2f)] public float seatPromptRadius = 1.5f;

    [Header("Timing")]
    [Min(0.1f)] public float sitDownDuration = 2f;
    [Min(1f)] public float seatIdleDuration = 10f;
    [Min(0.1f)] public float standUpDuration = 2f;
    [Min(0f)] public float faceRotateDuration = 0.35f;

    [Header("Animator Parameters")]
    public string sitDownParam = "SitDown";
    public string seatIdleParam = "SeatIdle";
    public string standUpParam = "StandUp";

    private PlayerController _pc;
    private Animator _anim;
    private CharacterController _cc;
    private bool _playerAtSeat;
    private bool _busy;
    private bool _powerAvailable;

    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashWeaponState = Animator.StringToHash("WeaponState");

    private void Start()
    {
        if (generatorPowerSource == null)
            generatorPowerSource = FindFirstObjectByType<GeneratorLightPuzzle>();

        if (joinPromptUI != null)
            joinPromptUI.SetActive(false);

        if (seatArrivalPoint != null && seatArrivalBeacon == null)
            CreateSeatArrivalBeacon();

        UpdateBeacon(false);
    }

    private void Update()
    {
        if (_pc == null || _pc.IsDead || _pc != PlayerController.Instance)
            CachePlayer(PlayerController.Instance);

        _playerAtSeat = false;
        if (_pc != null && seatArrivalPoint != null)
        {
            Vector3 offset = seatArrivalPoint.position - _pc.transform.position;
            offset.y = 0f;
            _playerAtSeat = offset.sqrMagnitude <= seatPromptRadius * seatPromptRadius;
        }

        _powerAvailable = generatorPowerSource != null && generatorPowerSource.IsPowerAvailable;
        bool canJoin = _powerAvailable && _playerAtSeat && !_busy && _pc != null &&
                       !_pc.IsDead && !_pc.IsSequenceRunning;
        if (joinPromptUI != null)
            joinPromptUI.SetActive(canJoin);

        UpdateBeacon(!_busy && !_playerAtSeat && seatArrivalPoint != null);

        if (canJoin && JoinKeyPressed())
        {
            Debug.Log("[MeetingSeatSystem] C pressed at the seat point; starting the sit animation.", this);
            StartCoroutine(MeetingSequence());
        }
    }

    private void CachePlayer(PlayerController player)
    {
        if (player == null || (_pc != null && !_pc.IsDead && _pc != player))
            return;

        _pc = player;
        _anim = player.GetComponentInChildren<Animator>();
        _cc = player.CharacterController;
    }

    private IEnumerator MeetingSequence()
    {
        _busy = true;
        if (joinPromptUI != null) joinPromptUI.SetActive(false);
        UpdateBeacon(false);
        GameAudio.PlayUI();

        if (_pc != null)
            yield return StartCoroutine(_pc.UnequipCurrentWeaponForPickup());

        bool originalRootMotion = _anim != null && _anim.applyRootMotion;
        if (_anim != null) _anim.applyRootMotion = false;
        if (_pc != null) _pc.SetFrozen(true, lockWorldPosition: false);
        // SetFrozen enables root motion; this sequence places the player directly.
        if (_anim != null) _anim.applyRootMotion = false;

        if (_pc == null || seatArrivalPoint == null)
        {
            FinishSequence(originalRootMotion);
            yield break;
        }

        if (seatFaceTarget != null)
        {
            Vector3 facing = seatFaceTarget.position - seatArrivalPoint.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                yield return StartCoroutine(SmoothAlignToSeat(seatArrivalPoint.position, facing.normalized, faceRotateDuration));
        }

        if (_anim != null)
        {
            _anim.SetFloat(HashSpeed, 0f);
            _anim.SetInteger(HashWeaponState, 0);
            _anim.ResetTrigger(sitDownParam);
            _anim.SetTrigger(sitDownParam);
        }

        Quaternion seatRotation = GetSeatRotation();
        yield return HoldAtSeat(sitDownDuration, seatRotation);

        if (_anim != null) _anim.SetBool(seatIdleParam, true);
        yield return HoldAtSeat(seatIdleDuration, seatRotation);

        if (_anim != null)
        {
            _anim.SetBool(seatIdleParam, false);
            _anim.ResetTrigger(standUpParam);
            _anim.SetTrigger(standUpParam);
        }

        Vector3 standStart = seatArrivalPoint.position;
        Vector3 standEnd = standStart;
        if (seatFaceTarget != null)
        {
            Vector3 facing = seatFaceTarget.position - seatArrivalPoint.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                standEnd += facing.normalized * 0.8f;
        }

        float timer = 0f;
        while (timer < standUpDuration)
        {
            if (_pc != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, timer / Mathf.Max(0.01f, standUpDuration));
                _pc.transform.position = Vector3.Lerp(standStart, standEnd, t);
                _pc.transform.rotation = seatRotation;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        FinishSequence(originalRootMotion);
    }

    private IEnumerator HoldAtSeat(float duration, Quaternion seatRotation)
    {
        float timer = 0f;
        while (timer < duration)
        {
            if (_pc != null && seatArrivalPoint != null)
            {
                _pc.transform.position = seatArrivalPoint.position;
                _pc.transform.rotation = seatRotation;
            }
            timer += Time.deltaTime;
            yield return null;
        }
    }

    private Quaternion GetSeatRotation()
    {
        if (seatArrivalPoint != null && seatFaceTarget != null)
        {
            Vector3 facing = seatFaceTarget.position - seatArrivalPoint.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                return Quaternion.LookRotation(facing.normalized, Vector3.up);
        }
        return _pc != null ? _pc.transform.rotation : Quaternion.identity;
    }

    private void FinishSequence(bool originalRootMotion)
    {
        if (_anim != null) _anim.SetFloat(HashSpeed, 0f);
        if (_pc != null) _pc.SetFrozen(false);
        if (_cc != null) _cc.enabled = true;
        if (_anim != null) _anim.applyRootMotion = originalRootMotion;
        _busy = false;
        if (joinPromptUI != null) joinPromptUI.SetActive(false);
    }

    private void CreateSeatArrivalBeacon()
    {
        GameObject beacon = new GameObject("Meeting Seat Arrival Beacon");
        beacon.transform.SetParent(seatArrivalPoint, false);
        beacon.transform.position = seatArrivalPoint.position + Vector3.up * 0.12f;
        beacon.transform.rotation = Quaternion.identity;
        seatArrivalBeacon = beacon.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = seatArrivalBeacon.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.62f, 0.12f, 0.95f), new Color(1f, 0.94f, 0.58f, 0.9f));
        main.maxParticles = 90;

        ParticleSystem.EmissionModule emission = seatArrivalBeacon.emission;
        emission.rateOverTime = 18f;

        ParticleSystem.ShapeModule shape = seatArrivalBeacon.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 5f;
        shape.radius = 0.28f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule color = seatArrivalBeacon.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.62f, 0.12f), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
    }

    private void UpdateBeacon(bool visible)
    {
        if (seatArrivalBeacon == null) return;
        if (visible && !seatArrivalBeacon.isPlaying)
            seatArrivalBeacon.Play();
        else if (!visible && seatArrivalBeacon.isPlaying)
            seatArrivalBeacon.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable()
    {
        if (joinPromptUI != null) joinPromptUI.SetActive(false);
        UpdateBeacon(false);
    }

    private void OnGUI()
    {
        if (joinPromptUI == null && _powerAvailable && _playerAtSeat && !_busy && _pc != null && !_pc.IsDead && !_pc.IsSequenceRunning)
            GameHUDPrompt.DrawPromptBox("[C] Join Meeting");
        else if (!_playerAtSeat && !_busy && seatArrivalPoint != null && _pc != null && !_pc.IsDead)
            GameHUDPrompt.DrawPromptBox("Walk to the glowing meeting seat marker");
    }

    private static bool JoinKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.cKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.C);
#else
        return false;
#endif
    }

    private IEnumerator SmoothAlignToSeat(Vector3 targetPos, Vector3 targetForward, float duration)
    {
        if (_pc == null) yield break;

        Vector3 startPos = _pc.transform.position;
        Quaternion startRot = _pc.transform.rotation;
        Quaternion targetRot = Quaternion.LookRotation(targetForward, Vector3.up);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            Vector3 desiredPosition = Vector3.Lerp(startPos, targetPos, t);
            if (_cc != null && _cc.enabled)
                _cc.Move(desiredPosition - _pc.transform.position);
            else
                _pc.transform.position = desiredPosition;
            _pc.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        bool controllerWasEnabled = _cc != null && _cc.enabled;
        if (controllerWasEnabled) _cc.enabled = false;
        _pc.transform.position = targetPos;
        _pc.transform.rotation = targetRot;
        if (controllerWasEnabled) _cc.enabled = true;
    }
}
