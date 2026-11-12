using UnityEngine;
using UnityEngine.Rendering;

namespace CelestialSystem
{
    /// <summary>
    /// Master controller for the Sun in the CelestialSystem.
    /// Manages:
    ///   • Directional Light (physically-based lux/intensity curve, color temperature gradient, shadow fade)
    ///   • Sun Disk Mesh / Transform (sky-dome positioning, billboard alignment, HLSL material driving)
    ///   • God Rays screen-space parameter feeding
    ///   • URP Lens Flare (SRP) attenuation
    ///   • Atmosphere / Fog tint synchronization
    ///
    /// Zero GC alloc in Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SunController : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Inspector — References
        // ───────────────────────────────────────────────────────────────
        [Header("── Core References ─────────────────────────")]
        [Tooltip("The DayNightCycle driver. Auto-finds in scene if null.")]
        public DayNightCycle dayNightCycle;

        [Tooltip("The CelestialVolume boundary. Auto-finds in scene if null.")]
        public CelestialVolume celestialVolume;

        [Tooltip("The Directional Light representing the Sun.")]
        public Light sunLight;

        [Tooltip("Renderer displaying the visible sun disk (uses CelestialSystem/SunDisk shader).")]
        public Renderer sunDiskRenderer;

        [Tooltip("Optional URP Lens Flare (SRP) component on or associated with the sun.")]
        public LensFlareComponentSRP lensFlare;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Lighting & Color
        // ───────────────────────────────────────────────────────────────
        [Header("── Sun Light Intensity & Lux ────────────────")]
        [Tooltip("Peak sun intensity at solar noon (Standard units: 1.0 - 2.0).")]
        [Range(0f, 5f)] public float maxLightIntensity = 1.25f;

        [Tooltip("Intensity curve as a function of Sun Altitude (-10° to 90°). X=0 is -10°, X=1 is 90°.")]
        public AnimationCurve intensityByAltitude = new AnimationCurve(
            new Keyframe(0.0f,  0.0f,   0.0f, 0.0f),  // -10°
            new Keyframe(0.10f, 0.0f,   0.0f, 1.5f),  // 0° horizon
            new Keyframe(0.25f, 0.45f,  1.5f, 1.5f),  // ~15° early morning
            new Keyframe(1.0f,  1.0f,   0.0f, 0.0f)   // 90° zenith
        );

        [Tooltip("Light color gradient as a function of Sun Altitude (0 = horizon/sunset, 1 = noon).")]
        public Gradient lightColorByAltitude;

        [Header("── Shadows ─────────────────────────────────")]
        [Tooltip("Base shadow strength at noon.")]
        [Range(0f, 1f)] public float shadowStrength = 0.85f;

        [Tooltip("Altitude (in degrees) below which shadow strength starts smoothly fading to 0.")]
        [Range(0f, 15f)] public float shadowFadeAltitude = 5.0f;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Sun Disk Visuals
        // ───────────────────────────────────────────────────────────────
        [Header("── Sun Disk (Sky Dome) ──────────────────────")]
        [Tooltip("Distance from the camera where the sun disk is placed (astronomical dome).")]
        [Range(100f, 5000f)] public float astronomicalDistance = 800f;

        [Tooltip("Uniform scale applied to the sun disk mesh.")]
        [Range(10f, 300f)] public float diskScale = 80f;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — God Rays & Lens Flare
        // ───────────────────────────────────────────────────────────────
        [Header("── God Rays & Atmosphere ───────────────────")]
        [Tooltip("Enable volumetric god ray data feed to the URP GodRaysRendererFeature.")]
        public bool enableGodRays = true;

        [Tooltip("Peak god ray strength multiplier (scales with altitude).")]
        [Range(0f, 3f)] public float godRaysStrength = 1.0f;

        [Tooltip("Base intensity for the SRP Lens Flare.")]
        [Range(0f, 5f)] public float baseLensFlareIntensity = 1.0f;

        [Header("── Fog Tint Synchronization ────────────────")]
        [Tooltip("Sync RenderSettings.fogColor with the sun/sky atmosphere.")]
        public bool syncFogColor = true;

        [Tooltip("Fog color gradient based on sun altitude (0 = sunset/dawn, 1 = day).")]
        public Gradient fogColorByAltitude;

        // ───────────────────────────────────────────────────────────────
        //  Cached Shader Property IDs (Zero Alloc)
        // ───────────────────────────────────────────────────────────────
        private static readonly int PropSunAltitude    = Shader.PropertyToID("_SunAltitude");
        private static readonly int PropVisibility     = Shader.PropertyToID("_Visibility");
        private static readonly int PropBaseColor      = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _mpb;
        private Camera _cachedCamera;

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (maxLightIntensity > 10f)
                maxLightIntensity = 1.25f;

            _mpb = new MaterialPropertyBlock();

            // Auto-resolve references if unassigned
            if (dayNightCycle == null)
                dayNightCycle = FindFirstObjectByType<DayNightCycle>();

            if (celestialVolume == null)
                celestialVolume = FindFirstObjectByType<CelestialVolume>();

            if (sunLight == null)
                sunLight = GetComponentInChildren<Light>();

            // Coordinate with existing scene directional lights to avoid 2 suns shining at once
            Light[] allSceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in allSceneLights)
            {
                if (l != null && l.type == LightType.Directional && l != sunLight)
                {
                    // Check if it belongs to MoonController
                    MoonController moonCtrl = FindFirstObjectByType<MoonController>();
                    if (moonCtrl != null && l == moonCtrl.moonLight) continue;

                    if (sunLight == null)
                    {
                        sunLight = l;
                        Debug.Log($"[SunController] Adopted existing scene directional light '{l.name}'.");
                    }
                    else
                    {
                        // Disable duplicate directional light to prevent double lighting and blown-out scenes
                        l.enabled = false;
                        Debug.Log($"[SunController] Disabled conflicting scene directional light '{l.name}'.");
                    }
                }
            }

            if (sunDiskRenderer == null)
                sunDiskRenderer = GetComponentInChildren<Renderer>();

            if (lensFlare == null)
                lensFlare = GetComponentInChildren<LensFlareComponentSRP>();

            InitDefaultGradients();
        }

        private void Start()
        {
            ResolveCamera();
            UpdateSun(0f); // Immediate update on start
        }

        private void LateUpdate()
        {
            UpdateSun(Time.deltaTime);
        }

        // ───────────────────────────────────────────────────────────────
        //  Core Sun Update
        // ───────────────────────────────────────────────────────────────
        private void UpdateSun(float dt)
        {
            if (dayNightCycle == null) return;

            float altitude = dayNightCycle.SunAltitude;
            Vector3 sunDir = dayNightCycle.SunWorldDirection;
            float visibility = celestialVolume != null ? celestialVolume.Visibility : 1.0f;

            // Normalized altitude: 0 at -10°, 1 at 90°
            float normAlt = Mathf.Clamp01((altitude + 10f) / 100f);

            // ── 1. Directional Light Direction ──
            if (sunLight != null)
            {
                // Directional light shines along -sunDir (from the sky down)
                if (sunDir.sqrMagnitude > 0.001f)
                    sunLight.transform.forward = -sunDir;

                // Evaluate intensity and color
                float intensityFactor = intensityByAltitude.Evaluate(normAlt);
                Color sunColor = lightColorByAltitude.Evaluate(Mathf.Clamp01(altitude / 70f));

                float currentIntensity = maxLightIntensity * intensityFactor * visibility;
                sunLight.intensity = currentIntensity;
                sunLight.color     = sunColor;

                // Shadow fading near the horizon
                if (altitude <= 0f || visibility <= 0.001f)
                {
                    sunLight.shadows = LightShadows.None;
                }
                else
                {
                    sunLight.shadows = LightShadows.Soft;
                    float shadowFade = Mathf.Clamp01(altitude / Mathf.Max(0.1f, shadowFadeAltitude));
                    sunLight.shadowStrength = shadowStrength * shadowFade * visibility;
                }

                // RenderSettings.sun safety: never leave pointing at a disabled light
                if (currentIntensity > 0.01f && visibility > 0.01f)
                {
                    if (RenderSettings.sun != sunLight)
                        RenderSettings.sun = sunLight;
                }
                else
                {
                    if (RenderSettings.sun == sunLight)
                        RenderSettings.sun = null;
                }
            }

            // ── 2. Sun Disk Mesh Positioning (Sky Dome) ──
            ResolveCamera();
            if (_cachedCamera != null && sunDiskRenderer != null)
            {
                Transform diskT = sunDiskRenderer.transform;
                Vector3 camPos  = _cachedCamera.transform.position;

                // Place sun disk at astronomical distance along sunDir (safely inside far clip plane)
                float far = _cachedCamera != null ? _cachedCamera.farClipPlane : 1000f;
                float effectiveDist = Mathf.Min(astronomicalDistance, far * 0.78f);
                diskT.position = camPos + sunDir * effectiveDist;
                // Face camera directly
                diskT.forward  = -sunDir;
                diskT.localScale = Vector3.one * diskScale;

                // Update Material Property Block (zero allocation)
                sunDiskRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(PropSunAltitude, altitude);
                _mpb.SetFloat(PropVisibility, visibility);
                sunDiskRenderer.SetPropertyBlock(_mpb);
            }

            // ── 3. Screen-Space God Rays Feed ──
            UpdateGodRaysData(sunDir, altitude, visibility);

            // ── 4. Lens Flare Attenuation ──
            if (lensFlare != null)
            {
                if (visibility <= 0.001f || altitude <= 0f)
                {
                    lensFlare.enabled = false;
                }
                else
                {
                    lensFlare.enabled = true;
                    float flareFade = Mathf.Clamp01(altitude / 10f);
                    lensFlare.intensity = baseLensFlareIntensity * flareFade * visibility;
                }
            }

            // ── 5. Fog Color Sync ──
            if (syncFogColor && RenderSettings.fog && visibility > 0.01f)
            {
                Color targetFog = fogColorByAltitude.Evaluate(Mathf.Clamp01((altitude + 5f) / 50f));
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFog, dt * 5f);
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  God Rays Calculation
        // ───────────────────────────────────────────────────────────────
        private void UpdateGodRaysData(Vector3 sunDir, float altitude, float visibility)
        {
            if (!enableGodRays || _cachedCamera == null)
            {
                GodRaysRendererFeature.SharedSunScreenPos = Vector4.zero;
                GodRaysRendererFeature.SharedGlobalVisibility = 0f;
                return;
            }

            // Project sun position to camera viewport coordinates
            Vector3 sunTarget = _cachedCamera.transform.position + sunDir * 1000f;
            Vector3 viewportPos = _cachedCamera.WorldToViewportPoint(sunTarget);

            // Is the sun in front of the camera near plane?
            bool inFront = viewportPos.z > 0f;

            // Distance from viewport center (0.5, 0.5)
            float distFromCenter = Vector2.Distance(new Vector2(viewportPos.x, viewportPos.y), new Vector2(0.5f, 0.5f));
            // Soft offscreen fade factor (falls off when sun goes beyond viewport boundaries)
            float onScreenMask = inFront ? Mathf.Clamp01(1.2f - distFromCenter) : 0f;

            // Altitude fade factor: god rays are dramatic between 0° and 35°, fading higher up and 0 below 0°
            float altFactor = 0f;
            if (altitude > 0f)
            {
                if (altitude < 30f)
                    altFactor = Mathf.Clamp01(altitude / 5f); // Fade in from 0° to 5°
                else
                    altFactor = Mathf.Clamp01(1f - (altitude - 30f) / 45f); // Subtle falloff above 30°
            }

            // Ray tint based on sun color
            Color rayTint = lightColorByAltitude.Evaluate(Mathf.Clamp01(altitude / 60f));

            GodRaysRendererFeature.SharedSunScreenPos = new Vector4(
                viewportPos.x,
                viewportPos.y,
                onScreenMask,
                altFactor * godRaysStrength
            );
            GodRaysRendererFeature.SharedRayTint = rayTint;
            GodRaysRendererFeature.SharedGlobalVisibility = visibility;
        }

        // ───────────────────────────────────────────────────────────────
        //  Camera Resolution
        // ───────────────────────────────────────────────────────────────
        private void ResolveCamera()
        {
            if (celestialVolume != null && celestialVolume.playerCamera != null)
            {
                _cachedCamera = celestialVolume.playerCamera;
                return;
            }

            if (_cachedCamera == null)
                _cachedCamera = Camera.main;
        }

        // ───────────────────────────────────────────────────────────────
        //  Default Gradients Setup
        // ───────────────────────────────────────────────────────────────
        private void OnValidate()
        {
            if (maxLightIntensity > 10f)
                maxLightIntensity = 1.25f;
        }

        private void InitDefaultGradients()
        {
            if (lightColorByAltitude == null || lightColorByAltitude.colorKeys.Length == 0)
            {
                lightColorByAltitude = new Gradient();
                lightColorByAltitude.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1.0f, 0.32f, 0.08f), 0.0f),  // Sunrise/sunset orange
                        new GradientColorKey(new Color(1.0f, 0.72f, 0.35f), 0.15f), // Golden hour
                        new GradientColorKey(new Color(1.0f, 0.95f, 0.88f), 0.45f), // Morning / Afternoon
                        new GradientColorKey(new Color(1.0f, 0.98f, 0.96f), 1.0f)   // Crisp Noon White
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 1.0f)
                    }
                );
            }

            if (fogColorByAltitude == null || fogColorByAltitude.colorKeys.Length == 0)
            {
                fogColorByAltitude = new Gradient();
                fogColorByAltitude.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(0.18f, 0.12f, 0.18f), 0.0f),  // Twilight purple
                        new GradientColorKey(new Color(0.85f, 0.42f, 0.22f), 0.12f), // Golden orange
                        new GradientColorKey(new Color(0.65f, 0.78f, 0.92f), 0.50f), // Soft day blue
                        new GradientColorKey(new Color(0.70f, 0.82f, 0.95f), 1.0f)   // Full day
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1.0f, 0.0f),
                        new GradientAlphaKey(1.0f, 1.0f)
                    }
                );
            }
        }
    }
}

