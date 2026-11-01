using System.Collections;
using UnityEngine;

/// <summary>
/// SwordAttackEffect
/// =================
/// AAA-style sword attack VFX — attach this script to the Sword GameObject.
/// Spawns motion-blur slash trail, glowing impact sparks, and a hit-flash light.
///
/// ── HOW TO USE ──────────────────────────────────────────────────────────────
///  1. Attach this script to your Sword GameObject.
///  2. In Inspector, call TriggerAttack() from an Animation Event OR
///     let PlayerController call it via SwordAttackEffect.Instance.TriggerAttack().
///  3. Optionally assign a TrailRenderer on the sword tip object for the blade trail.
/// </summary>
public class SwordAttackEffect : MonoBehaviour
{
    public static SwordAttackEffect Instance { get; private set; }

    [Header("── Blade Trail ──")]
    [Tooltip("The tip of the sword blade — a TrailRenderer will be added here automatically if missing.")]
    public Transform bladeTip;
    [Tooltip("How long the motion-blur trail stays visible (seconds).")]
    public float trailDuration = 0.18f;
    public float trailStartWidth = 0.55f;
    public float trailEndWidth   = 0.0f;

    [Header("── Impact Sparks ──")]
    public int sparkCount = 28;
    public float sparkSpeed = 18f;
    public float sparkLifetime = 0.25f;

    [Header("── Flash Light ──")]
    public float flashRange     = 12f;
    public float flashIntensity = 4f;
    public float flashDuration  = 0.07f;
    public Color flashColor     = new Color(0.9f, 0.85f, 0.6f);

    [Header("── Audio SFX ──")]
    public AudioClip swingSound;
    public AudioClip fleshHitSound;
    public AudioClip clashSound;

    // Private
    private TrailRenderer _bladeTrail;
    private Coroutine _attackRoutine;
    private AudioSource _audioSource;

    private void Awake()
    {
        Instance = this;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0.3f;
        }

        AutoLoadAudio();
        SetupBladeTrail();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void AutoLoadAudio()
    {
        if (swingSound == null) swingSound = GameAudio.GetClip(GameAudio.SoundType.SwordSwing);
        if (fleshHitSound == null) fleshHitSound = GameAudio.GetClip(GameAudio.SoundType.SwordFlesh);
        if (clashSound == null) clashSound = GameAudio.GetClip(GameAudio.SoundType.SwordClash);
    }

    public static Material GetVFXMaterial(bool additive)
    {
        return VFXUtil.Get(additive);
    }

    // ── Blade Trail Setup ───────────────────────────────────────────────────

    private void SetupBladeTrail()
    {
        if (bladeTip == null) bladeTip = transform;

        _bladeTrail = bladeTip.GetComponent<TrailRenderer>();
        if (_bladeTrail == null)
            _bladeTrail = bladeTip.gameObject.AddComponent<TrailRenderer>();

        _bladeTrail.time             = 0.24f;
        _bladeTrail.startWidth       = 0.75f;
        _bladeTrail.endWidth         = 0.05f;
        _bladeTrail.minVertexDistance = 0.03f;
        _bladeTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _bladeTrail.receiveShadows   = false;

        // Sword slash: URP Additive
        _bladeTrail.material = GetVFXMaterial(true);
        VFXUtil.ApplyTexture(_bladeTrail.material);
        _bladeTrail.numCapVertices = 4;
        _bladeTrail.numCornerVertices = 4;

        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(3.0f, 3.0f, 3.2f), 0f),    // White hot cutting core
                new GradientColorKey(new Color(1.2f, 2.0f, 2.8f), 0.25f), // Electric silver cyan
                new GradientColorKey(new Color(0.15f, 0.55f, 1.4f), 0.65f), // Luminous razor blue
                new GradientColorKey(new Color(0.04f, 0.15f, 0.6f), 1f)     // Deep dissipation tail
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,    0f),
                new GradientAlphaKey(0.85f, 0.4f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        _bladeTrail.colorGradient = g;

        // Trail off by default — activated only during swing
        _bladeTrail.emitting = false;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Call this from PlayerController or Animation Event when a sword attack begins.</summary>
    public void TriggerAttack()
    {
        if (_attackRoutine != null)
            StopCoroutine(_attackRoutine);
        _attackRoutine = StartCoroutine(AttackRoutine());
    }

    /// <summary>Call this from PlayerController/Animation Event when sword connects with a target.</summary>
    public void TriggerImpact(Vector3 impactPoint, Vector3 impactNormal, bool isEnemy)
    {
        SpawnImpactEffect(impactPoint, impactNormal, isEnemy);
    }

    // ── Coroutines ─────────────────────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        if (_bladeTrail != null)
        {
            _bladeTrail.Clear();
            _bladeTrail.emitting = true;
        }

        SpawnFlashLight(transform.position);
        yield return new WaitForSeconds(0.28f);

        if (_bladeTrail != null)
            _bladeTrail.emitting = false;

        yield return new WaitForSeconds(trailDuration);
        _attackRoutine = null;
    }

    // ── Effects ────────────────────────────────────────────────────────────

    private void SpawnFlashLight(Vector3 pos)
    {
        VFXUtil.SpawnFadingLight(pos, flashColor, flashRange, flashIntensity, flashDuration);
    }

    private void SpawnImpactEffect(Vector3 pos, Vector3 normal, bool isEnemy)
    {
        if (isEnemy)
        {
            // AAA Flesh Cleave Impact: Dynamic vivid red flash + high-speed arterial blood spray + mist + bone sparks
            VFXUtil.SpawnFadingLight(pos, new Color(1.0f, 0.05f, 0.02f), 7.5f, 4.5f, 0.12f);
            
            // 1. High-velocity arterial blood splatter spray
            VFXUtil.Burst(pos, normal, new Color(0.85f, 0.05f, 0.02f, 1f),
                36, 10f, 26f, 0.08f, 0.30f, 0.14f, 0.42f, true, true, 2.2f, 45f);

            // 2. Lingering blood mist puff
            VFXUtil.Burst(pos, Vector3.up, new Color(0.65f, 0.04f, 0.03f, 0.65f),
                14, 1.4f, 4.5f, 0.28f, 0.65f, 0.35f, 0.85f, false, false, -0.05f, 65f);

            // 3. Bone/steel friction micro-sparks (visceral cutting sensation)
            VFXUtil.Burst(pos, normal, new Color(1f, 0.9f, 0.6f, 1f),
                12, 12f, 24f, 0.05f, 0.15f, 0.08f, 0.22f, true, true, 1.5f, 35f);
        }
        else
        {
            // AAA World Clash: Bright flash + sparkling ricochet embers + dust
            SpawnFlashLight(pos);
            VFXUtil.Burst(pos, normal, new Color(1f, 0.85f, 0.32f, 1f),
                sparkCount, 12f, 28f, 0.06f, 0.22f, 0.12f, 0.32f, true, true, 1.6f, 42f);
            VFXUtil.Burst(pos, Vector3.up, new Color(0.65f, 0.62f, 0.55f, 0.5f),
                14, 1.5f, 4.2f, 0.4f, 1.1f, 0.3f, 0.55f, false, false, -0.08f, 50f);
        }
    }
}
