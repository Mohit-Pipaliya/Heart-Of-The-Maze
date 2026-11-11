using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Master controller for the Moon and Moonlight in the CelestialSystem.
    /// Manages:
    ///   • Moonlight (second Directional Light, cool blue-white, intensity by altitude & phase)
    ///   • Mutual shadow exclusion (only one of sun or moon casts shadows at any moment)
    ///   • Moon sphere mesh positioning (astronomical distance from camera)
    ///   • Moon illusion apparent size expansion near the horizon
    ///   • MaterialPropertyBlock driving of the PBR Moon shader
    ///
    /// Zero GC alloc in Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoonController : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Inspector — References
        // ───────────────────────────────────────────────────────────────
        [Header("── Core References ─────────────────────────")]
        [Tooltip("The DayNightCycle driver. Auto-finds in scene if null.")]
        public DayNightCycle dayNightCycle;

        [Tooltip("The CelestialVolume boundary. Auto-finds in scene if null.")]
        public CelestialVolume celestialVolume;

        [Tooltip("The SunController reference for mutual shadow coordination. Auto-finds if null.")]
        public SunController sunController;

        [Tooltip("Directional Light representing Moonlight.")]
        public Light moonLight;

        [Tooltip("Renderer displaying the 3D Moon sphere (using CelestialSystem/Moon shader).")]
        public Renderer moonRenderer;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Moonlight Settings
        // ───────────────────────────────────────────────────────────────
        [Header("── Moonlight Settings ───────────────────────")]
        [Tooltip("Peak moonlight intensity at full moon at zenith.")]
        [Range(0f, 10f)] public float maxMoonlightIntensity = 1.8f;

        [Tooltip("Light color of the moon.")]
        public Color moonlightColor = new Color(0.72f, 0.84f, 1.0f, 1.0f);

        [Tooltip("Shadow strength when casting moonlight shadows.")]
        [Range(0f, 1f)] public float moonlightShadowStrength = 0.65f;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Moon Mesh Visuals
        // ───────────────────────────────────────────────────────────────
        [Header("── Moon Mesh (Sky Dome) ──────────────────────")]
        [Tooltip("Distance from the camera where the moon is placed (astronomical dome).")]
        [Range(100f, 5000f)] public float astronomicalDistance = 780f;

        [Tooltip("Base scale of the moon mesh.")]
        [Range(5f, 200f)] public float baseMoonScale = 50f;

        [Tooltip("Apparent size multiplier when near the horizon (Moon Illusion).")]
        [Range(1f, 2f)] public float horizonIllusionMultiplier = 1.35f;

        // ───────────────────────────────────────────────────────────────
        //  Cached Shader Property IDs (Zero Alloc)
        // ───────────────────────────────────────────────────────────────
        private static readonly int PropSunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int PropMoonAltitude = Shader.PropertyToID("_MoonAltitude");
        private static readonly int PropPhaseAngle   = Shader.PropertyToID("_PhaseAngle");
        private static readonly int PropVisibility   = Shader.PropertyToID("_Visibility");

        private MaterialPropertyBlock _mpb;
        private Camera _cachedCamera;

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────
        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            if (dayNightCycle == null)
                dayNightCycle = FindFirstObjectByType<DayNightCycle>();

            if (celestialVolume == null)
                celestialVolume = FindFirstObjectByType<CelestialVolume>();

            if (sunController == null)
                sunController = FindFirstObjectByType<SunController>();

            if (moonLight == null)
                moonLight = GetComponentInChildren<Light>();

            if (moonRenderer == null)
                moonRenderer = GetComponentInChildren<Renderer>();
        }

        private void Start()
        {
            ResolveCamera();
            UpdateMoon(0f);
        }

        private void LateUpdate()
        {
            UpdateMoon(Time.deltaTime);
        }

        // ───────────────────────────────────────────────────────────────
        //  Core Moon Update
        // ───────────────────────────────────────────────────────────────
        private void UpdateMoon(float dt)
        {
            if (dayNightCycle == null) return;

            float altitude    = dayNightCycle.MoonAltitude;
            Vector3 moonDir   = dayNightCycle.MoonWorldDirection;
            Vector3 sunDir    = dayNightCycle.SunWorldDirection;
            float phaseAngle  = dayNightCycle.MoonPhaseAngle;
            float visibility  = celestialVolume != null ? celestialVolume.Visibility : 1.0f;

            // Illumination fraction: 0 (new moon) to 1 (full moon)
            float illumFraction = CelestialMath.MoonIlluminationFraction(phaseAngle);

            // ── 1. Moonlight Directional Light ──
            if (moonLight != null)
            {
                // Light shines along -moonDir
                if (moonDir.sqrMagnitude > 0.001f)
                    moonLight.transform.forward = -moonDir;

                // Altitude factor: 0 at horizon (0°), 1 above 30°
                float altFactor = Mathf.Clamp01(altitude / 25f);
                float targetIntensity = maxMoonlightIntensity * illumFraction * altFactor * visibility;

                moonLight.intensity = targetIntensity;
                moonLight.color     = moonlightColor;

                // ── Mutual Shadow Exclusion ──
                // Check if Sun is currently casting shadows
                bool sunCastingShadows = false;
                if (sunController != null && sunController.sunLight != null)
                {
                    sunCastingShadows = sunController.sunLight.shadows != LightShadows.None &&
                                        sunController.sunLight.intensity > 1.0f;
                }
                else
                {
                    sunCastingShadows = dayNightCycle.IsDay && dayNightCycle.SunAltitude > 0f;
                }

                // If sun casts shadows, moon NEVER casts shadows.
                // When sun is down and moon is above horizon with sufficient light, moon soft shadows activate!
                if (!sunCastingShadows && altitude > 2f && targetIntensity > 0.05f && visibility > 0.01f)
                {
                    moonLight.shadows = LightShadows.Soft;
                    moonLight.shadowStrength = moonlightShadowStrength * altFactor * visibility;
                }
                else
                {
                    moonLight.shadows = LightShadows.None;
                }
            }

            // ── 2. Moon Mesh Positioning & Scale (Sky Dome) ──
            ResolveCamera();
            if (_cachedCamera != null && moonRenderer != null)
            {
                Transform moonT = moonRenderer.transform;
                Vector3 camPos  = _cachedCamera.transform.position;

                // Position on celestial sphere
                moonT.position = camPos + moonDir * astronomicalDistance;

                // Apparent size enlargement near horizon (Moon Illusion)
                float horizonFactor = Mathf.Clamp01((16f - altitude) / 16f);
                float scaleMultiplier = Mathf.Lerp(1.0f, horizonIllusionMultiplier, horizonFactor);
                moonT.localScale = Vector3.one * (baseMoonScale * scaleMultiplier);

                // Align moon surface toward camera
                moonT.forward = -moonDir;

                // Push properties to MaterialPropertyBlock
                moonRenderer.GetPropertyBlock(_mpb);
                _mpb.SetVector(PropSunDirection, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
                _mpb.SetFloat(PropMoonAltitude, altitude);
                _mpb.SetFloat(PropPhaseAngle, phaseAngle);
                _mpb.SetFloat(PropVisibility, visibility);
                moonRenderer.SetPropertyBlock(_mpb);
            }
        }

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
    }
}
