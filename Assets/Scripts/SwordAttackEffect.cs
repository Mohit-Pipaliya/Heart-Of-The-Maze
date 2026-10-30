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
    private bool _isAttacking = false;
    private Texture2D _particleTex;
    private AudioSource _audioSource;

    private static Material _sharedAdditiveMat;
    private static Material _sharedAlphaMat;

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
        GenerateParticleTexture();
        SetupBladeTrail();
    }

    private void AutoLoadAudio()
    {
        if (swingSound == null) swingSound = GameAudio.GetClip(GameAudio.SoundType.SwordSwing);
        if (fleshHitSound == null) fleshHitSound = GameAudio.GetClip(GameAudio.SoundType.SwordFlesh);
        if (clashSound == null) clashSound = GameAudio.GetClip(GameAudio.SoundType.SwordClash);
    }

    // ── Texture ────────────────────────────────────────────────────────────

    private void GenerateParticleTexture()
    {
        _particleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 16f) / 16f;
                float dy = (y - 16f) / 16f;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                float a  = Mathf.Pow(Mathf.Clamp01(1f - d), 1.5f);
                _particleTex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        }
        _particleTex.Apply();
    }

    // ── URP VFX Material Helper ───────────────────────────────────────────

    public static Material GetVFXMaterial(bool additive)
    {
        if (additive && _sharedAdditiveMat != null) return _sharedAdditiveMat;
        if (!additive && _sharedAlphaMat != null) return _sharedAlphaMat;

        Material loaded = Resources.Load<Material>(additive ? "VFX/M_ParticleAdditive" : "VFX/M_ParticleAlpha");

#if UNITY_EDITOR
        if (loaded == null)
        {
            string path = additive 
                ? "Assets/Materials/VFX/M_ParticleAdditive.mat" 
                : "Assets/Materials/VFX/M_ParticleAlpha.mat";
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }
#endif

        if (loaded == null)
        {
            loaded = Resources.Load<Material>("VFX/M_TorchFire");
#if UNITY_EDITOR
            if (loaded == null)
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_TorchFire.mat");
#endif
        }

        if (loaded != null)
        {
            Material cloned = new Material(loaded);
            if (additive) _sharedAdditiveMat = cloned;
            else _sharedAlphaMat = cloned;
            return cloned;
        }

        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");

        Material m = new Material(s);
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", additive ? 2 : 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive) m.EnableKeyword("_BLENDMODE_ADD");
        else m.EnableKeyword("_BLENDMODE_ALPHA");
        m.renderQueue = 3000;
        m.SetOverrideTag("RenderType", "Transparent");

        if (additive) _sharedAdditiveMat = m;
        else _sharedAlphaMat = m;
        return m;
    }

    // ── Blade Trail Setup ───────────────────────────────────────────────────

    private void SetupBladeTrail()
    {
        if (bladeTip == null) bladeTip = transform;

        _bladeTrail = bladeTip.GetComponent<TrailRenderer>();
        if (_bladeTrail == null)
            _bladeTrail = bladeTip.gameObject.AddComponent<TrailRenderer>();

        _bladeTrail.time             = trailDuration;
        _bladeTrail.startWidth       = trailStartWidth;
        _bladeTrail.endWidth         = trailEndWidth;
        _bladeTrail.minVertexDistance = 0.04f;
        _bladeTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _bladeTrail.receiveShadows   = false;

        // Sword slash: URP Additive
        _bladeTrail.material = GetVFXMaterial(true);

        Gradient g = new Gradient();
        g.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f,   1f,   1f  ), 0f),    // White hot centre
                new GradientColorKey(new Color(0.6f, 0.85f,1f  ), 0.25f), // Blue-white
                new GradientColorKey(new Color(0.2f, 0.55f,1f  ), 0.65f), // Cyan
                new GradientColorKey(new Color(0.05f,0.2f, 0.6f), 1f)     // Deep blue tail
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.7f,  0.4f),
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
        if (_isAttacking) return;
        StartCoroutine(AttackRoutine());
    }

    /// <summary>Call this from PlayerController/Animation Event when sword connects with a target.</summary>
    public void TriggerImpact(Vector3 impactPoint, Vector3 impactNormal, bool isEnemy)
    {
        SpawnImpactEffect(impactPoint, impactNormal, isEnemy);
    }

    // ── Coroutines ─────────────────────────────────────────────────────────

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;

        // Play swing sound
        GameAudio.PlaySwordSwing(transform.position, _audioSource, 1.0f);

        // Enable blade trail for swing arc
        if (_bladeTrail != null)
        {
            _bladeTrail.Clear();
            _bladeTrail.emitting = true;
        }

        // Flash light at sword
        SpawnFlashLight(transform.position);

        // Wait for swing duration then fade trail out
        yield return new WaitForSeconds(0.25f);

        if (_bladeTrail != null)
            _bladeTrail.emitting = false;

        // Trail fades naturally over trailDuration
        yield return new WaitForSeconds(trailDuration);

        _isAttacking = false;
    }

    // ── Effects ────────────────────────────────────────────────────────────

    private void SpawnFlashLight(Vector3 pos)
    {
        GameObject lightObj = new GameObject("SwordFlash");
        lightObj.transform.position = pos;
        Light l = lightObj.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = flashColor;
        l.range     = flashRange;
        l.intensity = flashIntensity;
        Destroy(lightObj, flashDuration);
    }

    private void SpawnImpactEffect(Vector3 pos, Vector3 normal, bool isEnemy)
    {
        GameObject impactRoot = new GameObject("SwordImpact");
        impactRoot.transform.position = pos;
        impactRoot.transform.rotation = Quaternion.LookRotation(normal);

        // Play hit sound
        GameAudio.PlaySwordImpact(pos, isEnemy, 1.2f);

        // 1. Flash light
        SpawnFlashLight(pos);

        // 2. Sparks / blood burst
        ParticleSystem ps = impactRoot.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(12f, 28f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.gravityModifier = 1.8f;
        main.playOnAwake     = false;
        // Colour: enemy → blood red, world → white-gold sparks
        main.startColor = isEnemy
            ? new Color(0.9f, 0.05f, 0.05f, 1f)
            : new Color(1f,   0.85f, 0.3f,  1f);

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, sparkCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 40f;
        shape.radius    = 0.05f;

        var rend = impactRoot.GetComponent<ParticleSystemRenderer>();
        rend.material = GetVFXMaterial(true);
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 2.2f;

        // 3. Dust puff (for world hits)
        if (!isEnemy)
        {
            GameObject dustObj = new GameObject("DustPuff");
            dustObj.transform.SetParent(impactRoot.transform, false);
            ParticleSystem psDust = dustObj.AddComponent<ParticleSystem>();
            psDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var dm = psDust.main;
            dm.loop            = false;
            dm.startLifetime   = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            dm.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            dm.startSize       = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            dm.startColor      = new Color(0.65f, 0.62f, 0.58f, 0.5f);
            dm.gravityModifier = -0.08f;
            dm.playOnAwake     = false;
            var de = psDust.emission;
            de.rateOverTime = 0;
            de.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8, 14) });
            dustObj.GetComponent<ParticleSystemRenderer>().material = GetVFXMaterial(false);
            psDust.Play();
        }

        ps.Play();
        Destroy(impactRoot, 1.5f);
    }
}
