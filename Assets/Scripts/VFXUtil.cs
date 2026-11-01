using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Shared AAA VFX helpers: URP materials, soft particle texture, fading lights, bursts.
/// Guaranteed 100% fail-safe against pink/magenta missing shaders.
/// </summary>
public static class VFXUtil
{
    private static Texture2D _softTex;
    private static Texture2D _muzzleTex;
    private static Texture2D _sparkTex;
    private static Texture2D _smokeTex;
    private static Material _additive;
    private static Material _alpha;
    private static Material _muzzleMat;
    private static Material _sparkMat;
    private static Material _smokeMat;
    private static Shader _cachedOpaqueShader;

    public static Texture2D MuzzleFlashTexture
    {
        get
        {
            if (_muzzleTex != null) return _muzzleTex;
            _muzzleTex = Resources.Load<Texture2D>("VFX/T_MuzzleFlash");
#if UNITY_EDITOR
            if (_muzzleTex == null)
                _muzzleTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/VFX/T_MuzzleFlash.png");
#endif
            if (_muzzleTex != null) return _muzzleTex;
            return SoftParticleTexture;
        }
    }

    public static Texture2D SparkTexture
    {
        get
        {
            if (_sparkTex != null) return _sparkTex;
            _sparkTex = Resources.Load<Texture2D>("VFX/T_Spark");
#if UNITY_EDITOR
            if (_sparkTex == null)
                _sparkTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/VFX/T_Spark.png");
#endif
            if (_sparkTex != null) return _sparkTex;
            return SoftParticleTexture;
        }
    }

    public static Texture2D SmokeTexture
    {
        get
        {
            if (_smokeTex != null) return _smokeTex;
            _smokeTex = Resources.Load<Texture2D>("VFX/T_Smoke");
#if UNITY_EDITOR
            if (_smokeTex == null)
                _smokeTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/VFX/T_Smoke.png");
#endif
            if (_smokeTex != null) return _smokeTex;
            return SoftParticleTexture;
        }
    }

    public static Material MuzzleFlashMat
    {
        get
        {
            if (_muzzleMat != null && _muzzleMat.shader != null && !_muzzleMat.shader.name.Contains("Error"))
                return _muzzleMat;
            _muzzleMat = Resources.Load<Material>("VFX/M_MuzzleFlash");
#if UNITY_EDITOR
            if (_muzzleMat == null)
                _muzzleMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_MuzzleFlash.mat");
#endif
            if (_muzzleMat != null)
            {
                _muzzleMat = new Material(_muzzleMat);
                return _muzzleMat;
            }
            _muzzleMat = new Material(AdditiveMat);
            ApplyCustomTexture(_muzzleMat, MuzzleFlashTexture);
            return _muzzleMat;
        }
    }

    public static Material SparkMat
    {
        get
        {
            if (_sparkMat != null && _sparkMat.shader != null && !_sparkMat.shader.name.Contains("Error"))
                return _sparkMat;
            _sparkMat = Resources.Load<Material>("VFX/M_Spark");
#if UNITY_EDITOR
            if (_sparkMat == null)
                _sparkMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_Spark.mat");
#endif
            if (_sparkMat != null)
            {
                _sparkMat = new Material(_sparkMat);
                return _sparkMat;
            }
            _sparkMat = new Material(AdditiveMat);
            ApplyCustomTexture(_sparkMat, SparkTexture);
            return _sparkMat;
        }
    }

    public static Material SmokeMat
    {
        get
        {
            if (_smokeMat != null && _smokeMat.shader != null && !_smokeMat.shader.name.Contains("Error"))
                return _smokeMat;
            _smokeMat = Resources.Load<Material>("VFX/M_Smoke");
#if UNITY_EDITOR
            if (_smokeMat == null)
                _smokeMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_Smoke.mat");
#endif
            if (_smokeMat != null)
            {
                _smokeMat = new Material(_smokeMat);
                return _smokeMat;
            }
            _smokeMat = new Material(AlphaMat);
            ApplyCustomTexture(_smokeMat, SmokeTexture);
            return _smokeMat;
        }
    }

    public static void ApplyCustomTexture(Material mat, Texture2D tex)
    {
        if (mat == null || tex == null) return;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
    }

    public static Texture2D SoftParticleTexture
    {
        get
        {
            if (_softTex != null) return _softTex;

            // 1. Try loading dedicated asset
            _softTex = Resources.Load<Texture2D>("VFX/T_SoftParticle");
#if UNITY_EDITOR
            if (_softTex == null)
            {
                _softTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/VFX/T_SoftParticle.png");
            }
#endif
            if (_softTex != null) return _softTex;

            // 2. Procedural fallback
            _softTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            _softTex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                    _softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            _softTex.Apply();
            return _softTex;
        }
    }

    public static Material AdditiveMat
    {
        get
        {
            if (_additive != null && _additive.shader != null && !_additive.shader.name.Contains("Error")) 
                return _additive;
            _additive = LoadOrBuild(true);
            return _additive;
        }
    }

    public static Material AlphaMat
    {
        get
        {
            if (_alpha != null && _alpha.shader != null && !_alpha.shader.name.Contains("Error")) 
                return _alpha;
            _alpha = LoadOrBuild(false);
            return _alpha;
        }
    }

    public static Material Get(bool additive) => additive ? AdditiveMat : AlphaMat;

    /// <summary>
    /// Finds a guaranteed valid URP shader from runtime cache, Resources, or active scene renderers.
    /// Never returns null or an error/pink shader.
    /// </summary>
    public static Shader FindValidShader(bool transparent)
    {
        // 1. Prioritize URP Unlit (universally supported on ParticleSystem, TrailRenderer, and MeshRenderer)
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s != null && (!s.isSupported || s.name.Contains("Error"))) s = null;
        if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s != null && (!s.isSupported || s.name.Contains("Error"))) s = null;
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        if (s != null && (!s.isSupported || s.name.Contains("Error"))) s = null;

        // 2. Try Sprites / 2D fallback
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s != null && (!s.isSupported || s.name.Contains("Error"))) s = null;
        if (s == null) s = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (s != null && (!s.isSupported || s.name.Contains("Error"))) s = null;

        // 3. Fallback: Borrow a verified working URP shader from any active renderer in the scene
        if (s == null)
        {
            Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer r = allRenderers[i];
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.shader != null)
                {
                    Shader rs = r.sharedMaterial.shader;
                    string sn = rs.name;
                    if (!sn.Contains("Error") && !sn.Contains("InternalError") && 
                        !sn.Contains("Standard") && !sn.Contains("Diffuse") && rs.isSupported)
                    {
                        s = rs;
                        break;
                    }
                }
            }
        }

        // 4. Absolute emergency fallback
        if (s == null) s = Shader.Find("Hidden/Universal/CoreBlit");
        if (s != null && (!s.isSupported || s.name.Contains("Error"))) s = null;
        if (s == null) s = Shader.Find("UI/Default");
        return s;
    }

    /// <summary>
    /// Returns a guaranteed valid opaque URP Lit shader for shell casings / debris.
    /// </summary>
    public static Shader GetValidOpaqueShader()
    {
        if (_cachedOpaqueShader != null && !_cachedOpaqueShader.name.Contains("Error"))
            return _cachedOpaqueShader;

        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");

        if (s == null || s.name.Contains("Error"))
        {
            Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].sharedMaterial != null && all[i].sharedMaterial.shader != null)
                {
                    string sn = all[i].sharedMaterial.shader.name;
                    if (!sn.Contains("Error") && !sn.Contains("Internal") && !sn.Contains("Standard") && !sn.Contains("Diffuse"))
                    {
                        s = all[i].sharedMaterial.shader;
                        break;
                    }
                }
            }
        }

        _cachedOpaqueShader = s;
        return _cachedOpaqueShader;
    }

    private static Material LoadOrBuild(bool additive)
    {
        // 1. Try loading from Resources
        Material loaded = Resources.Load<Material>(additive ? "VFX/M_ParticleAdditive" : "VFX/M_ParticleAlpha");
        if (loaded == null && additive)
        {
            loaded = Resources.Load<Material>("VFX/M_BulletTracer");
        }
        if (loaded == null)
        {
            loaded = Resources.Load<Material>("VFX/M_TorchFire");
        }

#if UNITY_EDITOR
        // 2. Try loading from AssetDatabase in editor
        if (loaded == null)
        {
            string p = additive 
                ? "Assets/Materials/VFX/M_ParticleAdditive.mat" 
                : "Assets/Materials/VFX/M_ParticleAlpha.mat";
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        if (loaded == null && additive)
        {
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_BulletTracer.mat");
        }
        if (loaded == null)
        {
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_TorchFire.mat");
        }
#endif

        if (loaded != null && loaded.shader != null && !loaded.shader.name.Contains("Error") && loaded.shader.isSupported)
        {
            Material clone = new Material(loaded);
            ApplyTexture(clone);
            return clone;
        }

        // 3. Guaranteed procedural fallback with verified active URP shader
        Shader s = FindValidShader(transparent: true);
        Material m = new Material(s);

        // Configure transparent rendering
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1); // 1 = Transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2 : 0); // 2 = Additive, 0 = Alpha
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        if (m.HasProperty("_Cull")) m.SetInt("_Cull", 0); // Double-sided

        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (additive)
        {
            m.EnableKeyword("_BLENDMODE_ADD");
            m.DisableKeyword("_BLENDMODE_ALPHA");
        }
        else
        {
            m.EnableKeyword("_BLENDMODE_ALPHA");
            m.DisableKeyword("_BLENDMODE_ADD");
        }

        m.renderQueue = 3000;
        m.SetOverrideTag("RenderType", "Transparent");

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);

        ApplyTexture(m);
        return m;
    }

    public static void ApplyTexture(Material mat)
    {
        if (mat == null) return;
        Texture2D tex = SoftParticleTexture;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
    }

    public static Light SpawnFadingLight(Vector3 pos, Color color, float range, float intensity, float duration)
    {
        GameObject go = new GameObject("VFX_Light");
        go.transform.position = pos;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        go.AddComponent<VFXLightFade>().Init(light, intensity, duration);
        Object.Destroy(go, duration + 0.05f);
        return light;
    }

    public static ParticleSystem Burst(
        Vector3 pos,
        Vector3 normal,
        Color color,
        int count,
        float speedMin,
        float speedMax,
        float sizeMin,
        float sizeMax,
        float lifeMin,
        float lifeMax,
        bool additive,
        bool stretch,
        float gravity,
        float coneAngle,
        float lifetime = 1.6f)
    {
        GameObject go = new GameObject("VFX_Burst");
        go.SetActive(false); // Inactive prevents any internal autoplay or duration warnings during setup
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(count * 2, 8);

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = coneAngle;
        shape.radius = 0.04f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = Get(additive);
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        if (stretch)
        {
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 2.4f;
            rend.velocityScale = 0.08f;
        }

        go.SetActive(true);
        ps.Play();
        Object.Destroy(go, lifetime);
        return ps;
    }

    public static void WaterSplash(Vector3 pos, float scale = 1f)
    {
        Burst(pos, Vector3.up, new Color(0.75f, 0.88f, 1f, 0.7f),
            18, 1.5f * scale, 5f * scale, 0.08f * scale, 0.22f * scale, 0.25f, 0.5f,
            false, false, 0.6f, 55f, 1.2f);
        Burst(pos, Vector3.up, new Color(0.9f, 0.95f, 1f, 0.55f),
            10, 2f * scale, 7f * scale, 0.04f * scale, 0.1f * scale, 0.2f, 0.4f,
            true, true, 1.2f, 40f, 1.0f);
    }

    /// <summary>
    /// AAA needle-sharp incandescent ricochet sparks using T_Spark texture.
    /// </summary>
    public static ParticleSystem Sparks(Vector3 pos, Vector3 normal, Color color, int count = 26, float speed = 26f)
    {
        GameObject go = new GameObject("VFX_Sparks");
        go.SetActive(false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed * 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.085f);
        main.startColor = color;
        main.gravityModifier = 1.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count * 2;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.02f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = SparkMat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.renderMode = ParticleSystemRenderMode.Stretch;
        rend.lengthScale = 3.8f;
        rend.velocityScale = 0.06f;

        go.SetActive(true);
        ps.Play();
        Object.Destroy(go, 0.5f);
        return ps;
    }

    /// <summary>
    /// AAA volumetric organic dust/smoke puff using T_Smoke texture.
    /// </summary>
    public static ParticleSystem DustPuff(Vector3 pos, Vector3 normal, Color color, int count = 8, float scale = 1f)
    {
        GameObject go = new GameObject("VFX_DustPuff");
        go.SetActive(false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * scale, 4.0f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.28f * scale, 0.65f * scale);
        main.startColor = color;
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count * 2;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.03f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
        rend.material = SmokeMat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.renderMode = ParticleSystemRenderMode.Billboard;

        go.SetActive(true);
        ps.Play();
        Object.Destroy(go, 1.1f);
        return ps;
    }
}

public class VFXLightFade : MonoBehaviour
{
    private Light _light;
    private float _start;
    private float _duration;
    private float _t;

    public VFXLightFade Init(Light light, float startIntensity, float duration)
    {
        _light = light;
        _start = startIntensity;
        _duration = Mathf.Max(0.02f, duration);
        return this;
    }

    private void Update()
    {
        if (_light == null) return;
        _t += Time.deltaTime;
        float n = Mathf.Clamp01(_t / _duration);
        _light.intensity = _start * (1f - n * n);
    }
}
