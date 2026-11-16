using UnityEngine;

/// <summary>
/// RealisticFire: A fully customizable, AAA multi-layered realistic fire system.
/// Every single element can be tuned directly from the Unity Inspector:
/// - Size, Scale & Position Offset (Auto-compensates for parent models like 1500x Wall Torch)
/// - Flame Shape, Height, Width, Speed & Wind Turbulence
/// - Flame Colors & Gradient (Hot Core, Mid Flame, Outer Tips)
/// - Flying Embers / Sparks (Count, Speed, Size, Color)
/// - Smoke Plume (Density, Rise Speed, Size, Color)
/// - Dynamic Flickering Point Light (Range, Intensity, Colors, Flicker Speed)
/// - 3D Crackling Audio (Volume, Distance, Procedural / Custom Clip)
/// </summary>
[ExecuteAlways]
[SelectionBase]
public class RealisticFire : MonoBehaviour
{
    // ═════════════════════════════════════════════════════════════════════════════
    // 1. MASTER SETTINGS
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── 1. Master Settings ──")]
    [Tooltip("If true, fire automatically starts burning as soon as Game Play starts (no lighter needed).")]
    public bool autoIgniteOnStart = true;

    [Tooltip("Overall size multiplier for the entire fire flame, sparks, and light.")]
    [Range(0.05f, 10f)] public float fireScale = 1.0f;

    [Tooltip("Overall flame particle density and brightness multiplier.")]
    [Range(0.1f, 5f)] public float fireIntensity = 1.0f;

    [Tooltip("Position offset relative to this GameObject (in real world meters).")]
    public Vector3 fireOffset = new Vector3(0f, 0.05f, 0f);

    // ═════════════════════════════════════════════════════════════════════════════
    // 2. FLAME SHAPE & DYNAMICS
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── 2. Flame Shape & Dynamics ──")]
    [Tooltip("Height multiplier for the flames (taller or shorter flame tongues).")]
    [Range(0.2f, 4f)] public float flameHeight = 1.0f;

    [Tooltip("Width / Thickness of the flame base and cone angle.")]
    [Range(0.2f, 4f)] public float flameWidth = 1.0f;

    [Tooltip("Speed at which flames rise upward (upward draft).")]
    [Range(0.2f, 4f)] public float flameSpeed = 1.0f;

    [Tooltip("Wind noise turbulence (how much the flame flickers, dances and twists).")]
    [Range(0f, 3f)] public float flameTurbulence = 0.25f;

    // ═════════════════════════════════════════════════════════════════════════════
    // 3. FLAME COLORS & GRADIENT
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── 3. Flame Colors & Tint ──")]
    [Tooltip("Inner hottest core flame color (bright white/pale yellow).")]
    public Color flameCoreColor = new Color(1.0f, 1.0f, 0.95f, 1.0f);

    [Tooltip("Mid flame body color (vibrant fiery orange).")]
    public Color flameMidColor = new Color(1.0f, 0.62f, 0.12f, 1.0f);

    [Tooltip("Outer flame tips color (deep burning red/dark orange).")]
    public Color flameTipColor = new Color(1.0f, 0.22f, 0.02f, 1.0f);

    [Tooltip("Optional custom material for the fire. If null, uses authentic URP realistic fire material.")]
    public Material customFireMaterial;

    // ═════════════════════════════════════════════════════════════════════════════
    // 4. EMBERS & FLYING SPARKS
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── 4. Embers / Flying Sparks ──")]
    [Tooltip("Toggle floating glowing embers and sparks.")]
    public bool enableEmbers = true;

    [Tooltip("Number of sparks emitted per second.")]
    [Range(0f, 60f)] public float embersRate = 16f;

    [Tooltip("Speed at which sparks fly upward into the air.")]
    [Range(0.5f, 8f)] public float embersSpeed = 2.8f;

    [Tooltip("Size of glowing spark particles.")]
    [Range(0.01f, 0.2f)] public float embersSize = 0.04f;

    [Tooltip("Color of glowing sparks.")]
    public Color embersColor = new Color(1.0f, 0.85f, 0.35f, 1.0f);

    // ═════════════════════════════════════════════════════════════════════════════
    // 5. SMOKE PLUME
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── 5. Smoke Plume ──")]
    [Tooltip("Toggle rising smoke plume.")]
    public bool enableSmoke = true;

    [Tooltip("Smoke emission rate / density.")]
    [Range(0f, 40f)] public float smokeRate = 8f;

    [Tooltip("How fast smoke rises into the air.")]
    [Range(0.2f, 4f)] public float smokeRiseSpeed = 1.0f;

    [Tooltip("Size of smoke puffs.")]
    [Range(0.1f, 3f)] public float smokeSize = 0.5f;

    [Tooltip("Color and opacity of smoke.")]
    public Color smokeColor = new Color(0.22f, 0.20f, 0.18f, 0.30f);

    // ═════════════════════════════════════════════════════════════════════════════
    // 6. DYNAMIC POINT LIGHT
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── 6. Dynamic Flickering Light ──")]
    [Tooltip("Enable dynamic point light cast onto surrounding environment.")]
    public bool enableLight = true;

    [Tooltip("Master Light Brightness/Intensity. Drag slider in Inspector to make fire brighter or dimmer!")]
    [Range(0f, 30f)] public float lightIntensity = 3.5f;

    [Tooltip("Light illumination radius in real world meters (how far light spreads).")]
    [Range(0.5f, 50f)] public float lightRange = 8.5f;

    [Tooltip("How much the light flickers in percentage (0 = steady light, 1 = dramatic flickering).")]
    [Range(0f, 1f)] public float flickerAmount = 0.35f;

    [Tooltip("How fast the flame light dances and flickers.")]
    [Range(0.5f, 15f)] public float flickerSpeed = 5.0f;

    [Tooltip("Base warm flame light color.")]
    public Color lightBaseColor = new Color(1f, 0.55f, 0.15f);

    [Tooltip("Hot flicker highlight color.")]
    public Color lightHotColor = new Color(1f, 0.75f, 0.32f);

    public float minLightIntensity
    {
        get => lightIntensity * Mathf.Clamp01(1f - flickerAmount);
        set => lightIntensity = value;
    }

    public float maxLightIntensity
    {
        get => lightIntensity * (1f + flickerAmount);
        set => lightIntensity = value;
    }

    // ═════════════════════════════════════════════════════════════════════════════
    // 7. 3D SPATIAL AUDIO
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── 7. 3D Spatial Audio ──")]
    [Tooltip("Enable 3D realistic wood crackling audio loop.")]
    public bool enableSound = true;

    [Range(0f, 1f)] public float soundVolume = 0.55f;
    public float soundMinDistance = 1.5f;
    public float soundMaxDistance = 14f;

    [Tooltip("Optional custom crackle audio clip. If empty, uses procedural crackle sound.")]
    public AudioClip customFireSound;

    // ═════════════════════════════════════════════════════════════════════════════
    // 8. REFERENCES (AUTO-POPULATED)
    // ═════════════════════════════════════════════════════════════════════════════
    [Header("── References (Auto-Populated) ──")]
    public Transform fireVFXRoot;
    public ParticleSystem mainFlamePS;
    public ParticleSystem coreFlamePS;
    public ParticleSystem embersPS;
    public ParticleSystem smokePS;
    public Light pointLight;
    public AudioSource audioSource;

    private float _randomSeed;
    [SerializeField] private bool _isBurning = false;

    public bool IsBurning => _isBurning;

    private void Awake()
    {
        _randomSeed = Random.Range(0f, 100f);
        if (Application.isPlaying)
        {
            EnsureVFXRig();
        }
    }

    private void OnEnable()
    {
        EnsureVFXRig();

        if (Application.isPlaying)
        {
            if (autoIgniteOnStart)
            {
                Ignite();
            }
        }
        else
        {
            ApplyVisualSettings();
        }
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            if (autoIgniteOnStart && !_isBurning)
            {
                Ignite();
            }
            else if (!autoIgniteOnStart)
            {
                Extinguish();
            }
        }
        else
        {
            ApplyVisualSettings();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            Extinguish();
        }
    }

    private void Update()
    {
        // Light flicker calculation
        if (_isBurning && enableLight && pointLight != null)
        {
            float t = Time.time * flickerSpeed;
            float n1 = Mathf.PerlinNoise(t, _randomSeed);
            float n2 = Mathf.PerlinNoise(_randomSeed, t * 1.63f);
            float combinedNoise = (n1 * 0.65f) + (n2 * 0.35f);

            // Dynamic organic flicker around master lightIntensity
            float flickerFactor = 1f + (combinedNoise - 0.5f) * 2f * flickerAmount;
            pointLight.intensity = lightIntensity * flickerFactor * fireScale;
            pointLight.range = Mathf.Lerp(lightRange * 0.88f, lightRange * 1.12f, n1) * fireScale;
            pointLight.color = Color.Lerp(lightBaseColor, lightHotColor, n2);
        }
    }

    private void OnValidate()
    {
        ApplyVisualSettings();
    }

    /// <summary>
    /// Starts burning the realistic fire.
    /// </summary>
    public void Ignite()
    {
        _isBurning = true;

        EnsureVFXRig();

        if (fireVFXRoot != null)
        {
            fireVFXRoot.gameObject.SetActive(true);
        }

        PlayParticle(mainFlamePS);
        PlayParticle(coreFlamePS);
        if (enableEmbers) PlayParticle(embersPS);
        if (enableSmoke) PlayParticle(smokePS);

        if (pointLight != null)
        {
            pointLight.enabled = enableLight;
        }

        if (audioSource != null && enableSound)
        {
            if (audioSource.clip == null && Application.isPlaying)
            {
                audioSource.clip = customFireSound != null ? customFireSound : GenerateProceduralCrackleClip();
            }
            if (!audioSource.isPlaying && audioSource.clip != null)
            {
                audioSource.Play();
            }
        }

        ApplyVisualSettings();
    }

    private void PlayParticle(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.gameObject.SetActive(true);
        var em = ps.emission;
        em.enabled = true;
        if (!ps.isPlaying) ps.Play();
    }

    /// <summary>
    /// Extinguishes the fire immediately.
    /// </summary>
    public void Extinguish()
    {
        _isBurning = false;

        if (mainFlamePS != null) mainFlamePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (coreFlamePS != null) coreFlamePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (embersPS != null) embersPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (smokePS != null) smokePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (pointLight != null)
        {
            pointLight.enabled = false;
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        ApplyVisualSettings();
    }

    /// <summary>
    /// Applies visual scale, emission rates, gradient colors, and positions across all components.
    /// Automatically normalizes parent scale so fire is physically consistent in world space.
    /// </summary>
    public void ApplyVisualSettings()
    {
        if (fireVFXRoot != null)
        {
            // Compensate for parent transform lossyScale so the fire is ALWAYS true world scale,
            // regardless of whether parent is scaled 1x, 15x, or 1500x!
            Vector3 lossy = transform.lossyScale;
            float invX = (Mathf.Abs(lossy.x) > 0.00001f) ? (1f / lossy.x) : 1f;
            float invY = (Mathf.Abs(lossy.y) > 0.00001f) ? (1f / lossy.y) : 1f;
            float invZ = (Mathf.Abs(lossy.z) > 0.00001f) ? (1f / lossy.z) : 1f;

            fireVFXRoot.localScale = new Vector3(invX, invY, invZ) * fireScale;
            fireVFXRoot.localPosition = new Vector3(fireOffset.x * invX, fireOffset.y * invY, fireOffset.z * invZ);
        }

        Material fireMat = customFireMaterial != null ? customFireMaterial : LoadFireMaterial();

        // 1. Main Flame
        if (mainFlamePS != null)
        {
            var em = mainFlamePS.emission;
            em.enabled = _isBurning;
            em.rateOverTime = 38f * fireIntensity;

            var main = mainFlamePS.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f * flameHeight, 1.15f * flameHeight);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * flameSpeed, 2.8f * flameSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * flameWidth, 0.65f * flameWidth);

            var shape = mainFlamePS.shape;
            shape.angle = 14f * flameWidth;
            shape.radius = 0.05f * flameWidth;

            var noise = mainFlamePS.noise;
            noise.enabled = flameTurbulence > 0.01f;
            noise.strength = flameTurbulence;

            var col = mainFlamePS.colorOverLifetime;
            col.enabled = true;
            col.color = CreateFlameGradient(flameCoreColor, flameMidColor, flameTipColor);

            var rend = mainFlamePS.GetComponent<ParticleSystemRenderer>();
            if (rend != null && fireMat != null) rend.material = fireMat;
        }

        // 2. Core Flame (Bright Inner Core)
        if (coreFlamePS != null)
        {
            var em = coreFlamePS.emission;
            em.enabled = _isBurning;
            em.rateOverTime = 22f * fireIntensity;

            var main = coreFlamePS.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f * flameHeight, 0.75f * flameHeight);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f * flameSpeed, 2.2f * flameSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f * flameWidth, 0.4f * flameWidth);

            var shape = coreFlamePS.shape;
            shape.angle = 6f * flameWidth;
            shape.radius = 0.025f * flameWidth;

            var col = coreFlamePS.colorOverLifetime;
            col.enabled = true;
            col.color = CreateCoreGradient(flameCoreColor, flameMidColor);

            var rend = coreFlamePS.GetComponent<ParticleSystemRenderer>();
            if (rend != null && fireMat != null) rend.material = fireMat;
        }

        // 3. Embers / Sparks
        if (embersPS != null)
        {
            var em = embersPS.emission;
            em.enabled = enableEmbers && _isBurning;
            em.rateOverTime = enableEmbers ? (embersRate * fireIntensity) : 0f;

            var main = embersPS.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(embersSpeed * 0.7f, embersSpeed * 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(embersSize * 0.5f, embersSize * 1.5f);

            var col = embersPS.colorOverLifetime;
            col.enabled = true;
            col.color = CreateEmbersGradient(embersColor);

            var rend = embersPS.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.material = fireMat ?? VFXUtil.SparkMat;
            }
        }

        // 4. Smoke Plume
        if (smokePS != null)
        {
            var em = smokePS.emission;
            em.enabled = enableSmoke && _isBurning;
            em.rateOverTime = enableSmoke ? (smokeRate * fireIntensity) : 0f;

            var main = smokePS.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(smokeRiseSpeed * 0.6f, smokeRiseSpeed * 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(smokeSize * 0.6f, smokeSize * 1.4f);

            var col = smokePS.colorOverLifetime;
            col.enabled = true;
            col.color = CreateSmokeGradient(smokeColor);

            var rend = smokePS.GetComponent<ParticleSystemRenderer>();
            if (rend != null) rend.material = VFXUtil.SmokeMat;
        }

        // 5. Point Light
        if (pointLight != null)
        {
            pointLight.enabled = enableLight && (_isBurning || !Application.isPlaying);
            pointLight.range = lightRange * fireScale;
            pointLight.intensity = lightIntensity * fireScale;
            pointLight.color = lightBaseColor;
        }

        // 6. Audio
        if (audioSource != null)
        {
            audioSource.volume = soundVolume;
            audioSource.minDistance = soundMinDistance;
            audioSource.maxDistance = soundMaxDistance;
        }
    }

    private Gradient CreateFlameGradient(Color core, Color mid, Color tip)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(core, 0.0f),
                new GradientColorKey(mid, 0.22f),
                new GradientColorKey(tip, 0.65f),
                new GradientColorKey(new Color(tip.r * 0.25f, tip.g * 0.2f, tip.b * 0.1f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0.0f),
                new GradientAlphaKey(0.95f, 0.12f),
                new GradientAlphaKey(0.85f, 0.65f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        return grad;
    }

    private Gradient CreateCoreGradient(Color core, Color mid)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(core, 0.0f),
                new GradientColorKey(Color.Lerp(core, mid, 0.5f), 0.4f),
                new GradientColorKey(mid, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0.0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        return grad;
    }

    private Gradient CreateEmbersGradient(Color c)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(c, 0.0f),
                new GradientColorKey(c, 0.6f),
                new GradientColorKey(new Color(c.r * 0.4f, c.g * 0.15f, 0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0.0f),
                new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        return grad;
    }

    private Gradient CreateSmokeGradient(Color c)
    {
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(c.r, c.g, c.b), 0.0f),
                new GradientColorKey(new Color(c.r * 0.6f, c.g * 0.6f, c.b * 0.6f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0.0f),
                new GradientAlphaKey(c.a, 0.2f),
                new GradientAlphaKey(c.a * 0.6f, 0.6f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        return grad;
    }

    /// <summary>
    /// Automatically builds or links the multi-layered fire VFX rig.
    /// </summary>
    [ContextMenu("Rebuild Fire VFX")]
    public void EnsureVFXRig()
    {
        // 1. Root Container
        if (fireVFXRoot == null)
        {
            Transform existing = transform.Find("FireVFX_Rig");
            if (existing != null)
            {
                fireVFXRoot = existing;
            }
            else
            {
                GameObject rootGo = new GameObject("FireVFX_Rig");
                rootGo.transform.SetParent(transform, false);
                fireVFXRoot = rootGo.transform;
            }
        }

        Material fireMat = customFireMaterial != null ? customFireMaterial : LoadFireMaterial();

        // 2. Main Flame
        if (mainFlamePS == null)
        {
            Transform t = fireVFXRoot.Find("MainFlame");
            GameObject go = t != null ? t.gameObject : null;
            if (go == null)
            {
                go = new GameObject("MainFlame");
                go.transform.SetParent(fireVFXRoot, false);
                go.transform.localPosition = Vector3.zero;
            }
            mainFlamePS = go.GetComponent<ParticleSystem>();
            if (mainFlamePS == null) mainFlamePS = go.AddComponent<ParticleSystem>();
            SetupMainFlame(mainFlamePS, fireMat);
        }

        // 3. Core Flame
        if (coreFlamePS == null)
        {
            Transform t = fireVFXRoot.Find("CoreFlame");
            GameObject go = t != null ? t.gameObject : null;
            if (go == null)
            {
                go = new GameObject("CoreFlame");
                go.transform.SetParent(fireVFXRoot, false);
                go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            }
            coreFlamePS = go.GetComponent<ParticleSystem>();
            if (coreFlamePS == null) coreFlamePS = go.AddComponent<ParticleSystem>();
            SetupCoreFlame(coreFlamePS, fireMat);
        }

        // 4. Embers
        if (embersPS == null)
        {
            Transform t = fireVFXRoot.Find("Embers");
            GameObject go = t != null ? t.gameObject : null;
            if (go == null)
            {
                go = new GameObject("Embers");
                go.transform.SetParent(fireVFXRoot, false);
                go.transform.localPosition = Vector3.zero;
            }
            embersPS = go.GetComponent<ParticleSystem>();
            if (embersPS == null) embersPS = go.AddComponent<ParticleSystem>();
            SetupEmbers(embersPS, fireMat);
        }

        // 5. Smoke
        if (smokePS == null)
        {
            Transform t = fireVFXRoot.Find("Smoke");
            GameObject go = t != null ? t.gameObject : null;
            if (go == null)
            {
                go = new GameObject("Smoke");
                go.transform.SetParent(fireVFXRoot, false);
                go.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            }
            smokePS = go.GetComponent<ParticleSystem>();
            if (smokePS == null) smokePS = go.AddComponent<ParticleSystem>();
            SetupSmoke(smokePS);
        }

        // 6. Point Light
        if (pointLight == null)
        {
            Transform t = fireVFXRoot.Find("FireLight");
            GameObject go = t != null ? t.gameObject : null;
            if (go == null)
            {
                go = new GameObject("FireLight");
                go.transform.SetParent(fireVFXRoot, false);
                go.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            }
            pointLight = go.GetComponent<Light>();
            if (pointLight == null) pointLight = go.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = lightBaseColor;
            pointLight.intensity = lightIntensity * fireScale;
            pointLight.range = lightRange;
            pointLight.shadows = LightShadows.Soft;
        }

        // 7. 3D Spatial Audio
        if (audioSource == null)
        {
            audioSource = fireVFXRoot.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = fireVFXRoot.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = soundMinDistance;
            audioSource.maxDistance = soundMaxDistance;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.volume = soundVolume;

            if (customFireSound != null)
            {
                audioSource.clip = customFireSound;
            }
            else if (Application.isPlaying)
            {
                audioSource.clip = GenerateProceduralCrackleClip();
            }
        }

        ApplyVisualSettings();
    }

    private Material LoadFireMaterial()
    {
        Material mat = Resources.Load<Material>("VFX/M_TorchFire");
        if (mat == null)
        {
#if UNITY_EDITOR
            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_TorchFire.mat");
#endif
        }

        if (mat == null)
        {
            mat = VFXUtil.AdditiveMat;
        }

        return mat;
    }

    private void SetupMainFlame(ParticleSystem ps, Material fireMat)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f * flameHeight, 1.15f * flameHeight);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * flameSpeed, 2.8f * flameSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f * flameWidth, 0.65f * flameWidth);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = -0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 38f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f * flameWidth;
        shape.radius = 0.05f * flameWidth;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        colOverLifetime.color = CreateFlameGradient(flameCoreColor, flameMidColor, flameTipColor);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = flameTurbulence;
        noise.frequency = 1.6f;
        noise.scrollSpeed = 1.1f;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend != null && fireMat != null)
        {
            rend.material = fireMat;
        }
    }

    private void SetupCoreFlame(ParticleSystem ps, Material fireMat)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f * flameHeight, 0.75f * flameHeight);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f * flameSpeed, 2.2f * flameSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f * flameWidth, 0.4f * flameWidth);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f * flameWidth;
        shape.radius = 0.025f * flameWidth;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        colOverLifetime.color = CreateCoreGradient(flameCoreColor, flameMidColor);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.4f, 1f),
            new Keyframe(1f, 0.1f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend != null && fireMat != null)
        {
            rend.material = fireMat;
        }
    }

    private void SetupEmbers(ParticleSystem ps, Material fireMat)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(embersSpeed * 0.7f, embersSpeed * 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(embersSize * 0.5f, embersSize * 1.5f);
        main.gravityModifier = -0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = embersRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.08f;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        colOverLifetime.color = CreateEmbersGradient(embersColor);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 2.0f;
        noise.scrollSpeed = 1.4f;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            rend.material = fireMat ?? VFXUtil.SparkMat;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 2.0f;
        }
    }

    private void SetupSmoke(ParticleSystem ps)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(smokeRiseSpeed * 0.6f, smokeRiseSpeed * 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(smokeSize * 0.6f, smokeSize * 1.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = -0.06f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = smokeRate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.06f;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        colOverLifetime.color = CreateSmokeGradient(smokeColor);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(1f, 1.8f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 0.9f;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            rend.material = VFXUtil.SmokeMat;
        }
    }

    /// <summary>
    /// Generates a subtle, procedural crackling fire loop audio clip.
    /// </summary>
    private static AudioClip _cachedProceduralCrackle;
    private static AudioClip GenerateProceduralCrackleClip()
    {
        if (_cachedProceduralCrackle != null) return _cachedProceduralCrackle;

        int sampleRate = 22050;
        int lengthSamples = sampleRate * 3; // 3-second seamless loop
        float[] samples = new float[lengthSamples];
        System.Random rnd = new System.Random(42);

        // Low frequency roar + intermittent subtle wood pops
        float roarPhase = 0f;
        for (int i = 0; i < lengthSamples; i++)
        {
            float t = (float)i / sampleRate;

            // Warm low-mid flame hum
            roarPhase += 2f * Mathf.PI * 65f / sampleRate;
            float hum = Mathf.Sin(roarPhase) * 0.15f;

            // Perlin-style noise for flickering gust
            float gust = (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.12f;

            // Random subtle wood snap/crackle
            float pop = 0f;
            if (rnd.NextDouble() < 0.0035) // infrequent pops
            {
                pop = (float)(rnd.NextDouble() * 2.0 - 1.0) * 0.55f;
            }

            samples[i] = Mathf.Clamp(hum + gust + pop, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("FireCrackleProcedural", lengthSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        _cachedProceduralCrackle = clip;
        return clip;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("GameObject/Effects/Realistic Fire (Auto-Lit)", false, 10)]
    public static void CreateFireInScene(UnityEditor.MenuCommand menuCommand)
    {
        GameObject go = new GameObject("Realistic Fire");
        RealisticFire fire = go.AddComponent<RealisticFire>();
        fire.EnsureVFXRig();

        UnityEditor.GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Realistic Fire");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
