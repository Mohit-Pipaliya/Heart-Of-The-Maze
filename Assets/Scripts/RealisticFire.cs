using UnityEngine;

/// <summary>
/// RealisticFire: A self-contained, AAA multi-layered realistic fire system.
/// Direct auto-burn on Play (no lighter, no input needed).
/// Auto-constructs main flame, hot core, embers, smoke, flickering light, and 3D crackle audio.
/// Can be attached to any GameObject (Mashal, Campfire, Brazier, Wall Torch, Altar, etc.).
/// </summary>
[ExecuteAlways]
[SelectionBase]
public class RealisticFire : MonoBehaviour
{
    [Header("── Fire Master Settings ──")]
    [Tooltip("If true, fire automatically starts burning as soon as Game Play starts (no lighter needed).")]
    public bool autoIgniteOnStart = true;

    [Tooltip("Overall size multiplier for the entire fire, sparks, and light.")]
    [Range(0.2f, 5f)] public float fireScale = 1.0f;

    [Tooltip("Overall flame particle density and brightness multiplier.")]
    [Range(0.1f, 3f)] public float fireIntensity = 1.0f;

    [Tooltip("Position offset relative to this GameObject (e.g. height on top of torch or logs).")]
    public Vector3 fireOffset = new Vector3(0f, 0.15f, 0f);

    [Header("── Light Settings ──")]
    public bool enableLight = true;
    public float minLightIntensity = 1.8f;
    public float maxLightIntensity = 4.2f;
    public float lightRange = 8.5f;
    public Color lightBaseColor = new Color(1f, 0.55f, 0.15f);
    public Color lightHotColor = new Color(1f, 0.75f, 0.32f);
    [Range(0.5f, 12f)] public float flickerSpeed = 5.0f;

    [Header("── Particle Layers ──")]
    public bool enableEmbers = true;
    public bool enableSmoke = true;

    [Header("── 3D Audio Settings ──")]
    public bool enableSound = true;
    [Range(0f, 1f)] public float soundVolume = 0.55f;
    public float soundMinDistance = 1.5f;
    public float soundMaxDistance = 14f;
    [Tooltip("Optional custom fire crackle audio clip. If empty, a soft realistic crackle is generated automatically.")]
    public AudioClip customFireSound;

    [Header("── References (Auto-Populated) ──")]
    public Transform fireVFXRoot;
    public ParticleSystem mainFlamePS;
    public ParticleSystem coreFlamePS;
    public ParticleSystem embersPS;
    public ParticleSystem smokePS;
    public Light pointLight;
    public AudioSource audioSource;

    private float _randomSeed;
    private bool _isBurning = false;
    private float[] _baseEmissionRates;

    public bool IsBurning => _isBurning;

    private void Awake()
    {
        _randomSeed = Random.Range(0f, 100f);

        // Build or link the complete fire VFX rig if missing
        EnsureVFXRig();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            if (autoIgniteOnStart)
            {
                Ignite();
            }
            else
            {
                Extinguish();
            }
        }
        else
        {
            // In Edit mode, show preview
            ApplyVisualSettings();
        }
    }

    private void Update()
    {
        if (!_isBurning) return;

        // Dynamic realistic fire light flicker (two-frequency Perlin noise for non-repetitive organic flame dance)
        if (enableLight && pointLight != null)
        {
            float t = Time.time * flickerSpeed;
            float n1 = Mathf.PerlinNoise(t, _randomSeed);
            float n2 = Mathf.PerlinNoise(_randomSeed, t * 1.63f);
            float combinedNoise = (n1 * 0.65f) + (n2 * 0.35f);

            pointLight.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, combinedNoise) * fireIntensity * fireScale;
            pointLight.range = Mathf.Lerp(lightRange * 0.85f, lightRange * 1.15f, n1) * fireScale;
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

        if (mainFlamePS != null && !mainFlamePS.isPlaying) mainFlamePS.Play();
        if (coreFlamePS != null && !coreFlamePS.isPlaying) coreFlamePS.Play();
        if (embersPS != null && enableEmbers && !embersPS.isPlaying) embersPS.Play();
        if (smokePS != null && enableSmoke && !smokePS.isPlaying) smokePS.Play();

        if (pointLight != null)
        {
            pointLight.enabled = enableLight;
        }

        if (audioSource != null && enableSound)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        ApplyVisualSettings();
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
    }

    /// <summary>
    /// Dynamically update fire scale and intensity across particles and lights.
    /// </summary>
    public void SetIntensity(float intensity)
    {
        fireIntensity = Mathf.Clamp(intensity, 0.1f, 3f);
        ApplyVisualSettings();
    }

    /// <summary>
    /// Applies visual scale, emission rates, and positions across all components.
    /// </summary>
    public void ApplyVisualSettings()
    {
        if (fireVFXRoot != null)
        {
            fireVFXRoot.localPosition = fireOffset;
            fireVFXRoot.localScale = Vector3.one * fireScale;
        }

        // Apply intensity to emission modules
        if (mainFlamePS != null)
        {
            var em = mainFlamePS.emission;
            em.rateOverTime = 38f * fireIntensity;
        }
        if (coreFlamePS != null)
        {
            var em = coreFlamePS.emission;
            em.rateOverTime = 22f * fireIntensity;
        }
        if (embersPS != null)
        {
            var em = embersPS.emission;
            em.enabled = enableEmbers;
            em.rateOverTime = enableEmbers ? (16f * fireIntensity) : 0f;
        }
        if (smokePS != null)
        {
            var em = smokePS.emission;
            em.enabled = enableSmoke;
            em.rateOverTime = enableSmoke ? (8f * fireIntensity) : 0f;
        }

        if (pointLight != null)
        {
            pointLight.enabled = enableLight && _isBurning;
            pointLight.range = lightRange * fireScale;
            pointLight.color = lightBaseColor;
        }

        if (audioSource != null)
        {
            audioSource.volume = soundVolume;
            audioSource.minDistance = soundMinDistance;
            audioSource.maxDistance = soundMaxDistance;
        }
    }

    /// <summary>
    /// Automatically builds the multi-layered fire VFX rig if not already present.
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
                rootGo.transform.localPosition = fireOffset;
                fireVFXRoot = rootGo.transform;
            }
        }

        Material fireMat = LoadFireMaterial();

        // 2. Main Flame
        if (mainFlamePS == null)
        {
            Transform t = fireVFXRoot.Find("MainFlame");
            GameObject go = t != null ? t.gameObject : new GameObject("MainFlame");
            go.transform.SetParent(fireVFXRoot, false);
            go.transform.localPosition = Vector3.zero;

            mainFlamePS = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
            SetupMainFlame(mainFlamePS, fireMat);
        }

        // 3. Core Flame (Bright inner core)
        if (coreFlamePS == null)
        {
            Transform t = fireVFXRoot.Find("CoreFlame");
            GameObject go = t != null ? t.gameObject : new GameObject("CoreFlame");
            go.transform.SetParent(fireVFXRoot, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            coreFlamePS = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
            SetupCoreFlame(coreFlamePS, fireMat);
        }

        // 4. Embers & Floating Sparks
        if (embersPS == null)
        {
            Transform t = fireVFXRoot.Find("Embers");
            GameObject go = t != null ? t.gameObject : new GameObject("Embers");
            go.transform.SetParent(fireVFXRoot, false);
            go.transform.localPosition = Vector3.zero;

            embersPS = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
            SetupEmbers(embersPS, fireMat);
        }

        // 5. Smoke Plume
        if (smokePS == null)
        {
            Transform t = fireVFXRoot.Find("Smoke");
            GameObject go = t != null ? t.gameObject : new GameObject("Smoke");
            go.transform.SetParent(fireVFXRoot, false);
            go.transform.localPosition = new Vector3(0f, 0.25f, 0f);

            smokePS = go.GetComponent<ParticleSystem>() ?? go.AddComponent<ParticleSystem>();
            SetupSmoke(smokePS);
        }

        // 6. Point Light
        if (pointLight == null)
        {
            Transform t = fireVFXRoot.Find("FireLight");
            GameObject go = t != null ? t.gameObject : new GameObject("FireLight");
            go.transform.SetParent(fireVFXRoot, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            pointLight = go.GetComponent<Light>() ?? go.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = lightBaseColor;
            pointLight.intensity = (minLightIntensity + maxLightIntensity) * 0.5f;
            pointLight.range = lightRange;
            pointLight.shadows = LightShadows.Soft;
        }

        // 7. 3D Spatial Audio
        if (audioSource == null)
        {
            audioSource = fireVFXRoot.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = fireVFXRoot.gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1.0f; // True 3D sound
            audioSource.minDistance = soundMinDistance;
            audioSource.maxDistance = soundMaxDistance;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.volume = soundVolume;

            if (customFireSound != null)
            {
                audioSource.clip = customFireSound;
            }
            else
            {
                audioSource.clip = GenerateProceduralCrackleClip();
            }
        }
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = -0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 38f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.05f;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(new Color(1f, 0.62f, 0.12f), 0.2f),
                new GradientColorKey(new Color(1f, 0.22f, 0.02f), 0.65f),
                new GradientColorKey(new Color(0.2f, 0.04f, 0.01f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0.0f),
                new GradientAlphaKey(0.95f, 0.12f),
                new GradientAlphaKey(0.85f, 0.65f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        colOverLifetime.color = grad;

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
        noise.strength = 0.25f;
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.025f;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(new Color(1f, 0.88f, 0.45f), 0.4f),
                new GradientColorKey(new Color(1f, 0.45f, 0.05f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0.0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        colOverLifetime.color = grad;

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
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
        main.gravityModifier = -0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 16f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.08f;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0.0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.0f), 0.6f),
                new GradientColorKey(new Color(0.2f, 0.05f, 0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0.0f),
                new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        colOverLifetime.color = grad;

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
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = -0.06f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.06f;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.25f, 0.22f, 0.2f), 0.0f),
                new GradientColorKey(new Color(0.12f, 0.12f, 0.12f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0.0f),
                new GradientAlphaKey(0.28f, 0.2f),
                new GradientAlphaKey(0.18f, 0.6f),
                new GradientAlphaKey(0f, 1.0f)
            }
        );
        colOverLifetime.color = grad;

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
            // Low rumble
            roarPhase += (rnd.Next(60, 110) / (float)sampleRate) * Mathf.PI * 2f;
            float roar = Mathf.Sin(roarPhase) * 0.06f;

            // White noise hiss (air convection)
            float hiss = ((float)rnd.NextDouble() * 2f - 1f) * 0.04f;

            // Occasional pops & crackles
            float pop = 0f;
            if (rnd.NextDouble() < 0.0035) // Pop trigger
            {
                pop = ((float)rnd.NextDouble() * 2f - 1f) * 0.35f;
            }

            samples[i] = Mathf.Clamp(roar + hiss + pop, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Procedural_FireCrackle_Loop", lengthSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        _cachedProceduralCrackle = clip;
        return clip;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("GameObject/Effects/Realistic Fire (Auto-Lit)", false, 11)]
    public static void CreateRealisticFireObject(UnityEditor.MenuCommand menuCommand)
    {
        GameObject fireGo = new GameObject("RealisticFire");
        UnityEditor.GameObjectUtility.SetParentAndAlign(fireGo, menuCommand.context as GameObject);
        UnityEditor.Undo.RegisterCreatedObjectUndo(fireGo, "Create Realistic Fire (Auto-Lit)");

        RealisticFire rf = fireGo.AddComponent<RealisticFire>();
        rf.EnsureVFXRig();
        rf.ApplyVisualSettings();

        UnityEditor.Selection.activeObject = fireGo;
        Debug.Log("<color=orange><b>[RealisticFire]</b></color> Created Realistic Fire VFX! It will burn automatically on Game Play.");
    }
#endif
}
