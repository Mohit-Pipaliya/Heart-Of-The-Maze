using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CelestialSystem
{
    /// <summary>
    /// Master time-of-day color grading and post-processing controller.
    /// Dynamically interpolates URP Volume Profile overrides across Dawn, Day, Dusk, and Night:
    ///   • Color Adjustments (Exposure, Contrast, Tint, Saturation)
    ///   • Shadows, Midtones, Highlights
    ///   • Bloom (tuned for day vs night star/moon luminescence)
    ///   • Vignette
    ///
    /// Multiplied by CelestialVolume.Visibility (fully zero when outside the box).
    /// Zero GC alloc in Update.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class TimeOfDayGrading : MonoBehaviour
    {
        [System.Serializable]
        public struct ProfileSettings
        {
            [Range(-2f, 2f)]   public float postExposure;
            [Range(-50f, 50f)] public float contrast;
            public Color colorFilter;
            [Range(-50f, 50f)] public float saturation;

            [Header("Shadows & Highlights")]
            public Vector4 shadowTint;
            public Vector4 highlightTint;

            [Header("Bloom & Vignette")]
            [Range(0f, 5f)] public float bloomIntensity;
            [Range(0f, 1f)] public float vignetteIntensity;

            public static ProfileSettings CreateDefaultDawn() => new ProfileSettings
            {
                postExposure = 0.2f,
                contrast = 14f,
                colorFilter = new Color(1.0f, 0.91f, 0.82f),
                saturation = 12f,
                shadowTint = new Vector4(0.92f, 0.85f, 0.98f, 0f),
                highlightTint = new Vector4(1.0f, 0.92f, 0.80f, 0f),
                bloomIntensity = 0.9f,
                vignetteIntensity = 0.24f
            };

            public static ProfileSettings CreateDefaultDay() => new ProfileSettings
            {
                postExposure = 0.0f,
                contrast = 18f,
                colorFilter = new Color(1.0f, 1.0f, 1.0f),
                saturation = 8f,
                shadowTint = new Vector4(0.94f, 0.96f, 1.0f, 0f),
                highlightTint = new Vector4(1.0f, 0.98f, 0.95f, 0f),
                bloomIntensity = 0.5f,
                vignetteIntensity = 0.20f
            };

            public static ProfileSettings CreateDefaultDusk() => new ProfileSettings
            {
                postExposure = -0.15f,
                contrast = 24f,
                colorFilter = new Color(1.0f, 0.82f, 0.68f),
                saturation = 16f,
                shadowTint = new Vector4(0.85f, 0.72f, 0.94f, 0f),
                highlightTint = new Vector4(1.0f, 0.78f, 0.65f, 0f),
                bloomIntensity = 1.3f,
                vignetteIntensity = 0.28f
            };

            public static ProfileSettings CreateDefaultNight() => new ProfileSettings
            {
                postExposure = -0.55f,
                contrast = 26f,
                colorFilter = new Color(0.76f, 0.85f, 1.0f),
                saturation = -10f,
                shadowTint = new Vector4(0.68f, 0.78f, 0.96f, 0f),
                highlightTint = new Vector4(0.82f, 0.90f, 1.0f, 0f),
                bloomIntensity = 1.8f,
                vignetteIntensity = 0.34f
            };
        }

        // ───────────────────────────────────────────────────────────────
        //  Inspector — References
        // ───────────────────────────────────────────────────────────────
        [Header("── Core References ─────────────────────────")]
        [Tooltip("The DayNightCycle driver. Auto-finds in scene if null.")]
        public DayNightCycle dayNightCycle;

        [Tooltip("The CelestialVolume boundary. Auto-finds in scene if null.")]
        public CelestialVolume celestialVolume;

        [Tooltip("The URP Volume containing the post-processing profile.")]
        public Volume postProcessVolume;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Profile Presets
        // ───────────────────────────────────────────────────────────────
        [Header("── Time of Day Color Grading Presets ────────")]
        public ProfileSettings dawnSettings  = ProfileSettings.CreateDefaultDawn();
        public ProfileSettings daySettings   = ProfileSettings.CreateDefaultDay();
        public ProfileSettings duskSettings  = ProfileSettings.CreateDefaultDusk();
        public ProfileSettings nightSettings = ProfileSettings.CreateDefaultNight();

        // ───────────────────────────────────────────────────────────────
        //  Cached Volume Components (Zero Alloc)
        // ───────────────────────────────────────────────────────────────
        private ColorAdjustments            _colorAdjustments;
        private Bloom                       _bloom;
        private Vignette                    _vignette;
        private ShadowsMidtonesHighlights   _shadowsMidtones;

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (dayNightCycle == null)
                dayNightCycle = FindFirstObjectByType<DayNightCycle>();

            if (celestialVolume == null)
                celestialVolume = FindFirstObjectByType<CelestialVolume>();

            if (postProcessVolume == null)
                postProcessVolume = GetComponent<Volume>();

            EnsureVolumeOverrides();
        }

        private void Start()
        {
            UpdateGrading();
        }

        private void Update()
        {
            UpdateGrading();
        }

        // ───────────────────────────────────────────────────────────────
        //  Volume Overrides Initialization
        // ───────────────────────────────────────────────────────────────
        private void EnsureVolumeOverrides()
        {
            if (postProcessVolume == null) return;

            // Use profile instance so we don't dirty disk asset during runtime
            VolumeProfile profile = postProcessVolume.profile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "CelestialSystem_RuntimeGradingProfile";
                postProcessVolume.profile = profile;
            }

            if (!profile.TryGet(out _colorAdjustments))
                _colorAdjustments = profile.Add<ColorAdjustments>(true);

            if (!profile.TryGet(out _bloom))
                _bloom = profile.Add<Bloom>(true);

            if (!profile.TryGet(out _vignette))
                _vignette = profile.Add<Vignette>(true);

            if (!profile.TryGet(out _shadowsMidtones))
                _shadowsMidtones = profile.Add<ShadowsMidtonesHighlights>(true);

            // Activate overrides
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.contrast.overrideState     = true;
            _colorAdjustments.colorFilter.overrideState  = true;
            _colorAdjustments.saturation.overrideState   = true;

            _bloom.intensity.overrideState = true;
            _vignette.intensity.overrideState = true;

            _shadowsMidtones.shadows.overrideState    = true;
            _shadowsMidtones.highlights.overrideState = true;
        }

        // ───────────────────────────────────────────────────────────────
        //  Core Grading Update
        // ───────────────────────────────────────────────────────────────
        private void UpdateGrading()
        {
            if (dayNightCycle == null || postProcessVolume == null) return;

            float visibility = celestialVolume != null ? celestialVolume.Visibility : 1.0f;

            // Modulate Volume weight by visibility
            postProcessVolume.weight = visibility;
            if (visibility <= 0.001f)
                return;

            float altitude = dayNightCycle.SunAltitude;
            bool isDay     = dayNightCycle.IsDay;

            // Calculate 4-way blend weights based on sun altitude and daytime progression
            // Sunrise/Dawn: SunAltitude between -4° and 12° in the morning (isDay and normalizedTime < 0.5)
            // Sunset/Dusk:  SunAltitude between -4° and 12° in the evening (isDay and normalizedTime >= 0.5)
            // Day:          SunAltitude > 12°
            // Night:        SunAltitude < -6°
            float timeNorm = dayNightCycle.CurrentTimeNormalized;
            bool isMorning = timeNorm < 0.5f;

            float dawnWeight  = 0f;
            float dayWeight   = 0f;
            float duskWeight  = 0f;
            float nightWeight = 0f;

            if (altitude > 14f)
            {
                dayWeight = 1.0f;
            }
            else if (altitude < -8f)
            {
                nightWeight = 1.0f;
            }
            else
            {
                // Transition zone: -8° to 14°
                float transitionFactor = Mathf.Clamp01((altitude + 8f) / 22f); // 0 at night, 1 at full day

                if (isMorning)
                {
                    dawnWeight  = Mathf.Sin(transitionFactor * Mathf.PI);
                    dayWeight   = transitionFactor * transitionFactor;
                    nightWeight = (1f - transitionFactor) * (1f - transitionFactor);
                }
                else
                {
                    duskWeight  = Mathf.Sin(transitionFactor * Mathf.PI);
                    dayWeight   = transitionFactor * transitionFactor;
                    nightWeight = (1f - transitionFactor) * (1f - transitionFactor);
                }
            }

            // Normalize blend weights
            float sum = dawnWeight + dayWeight + duskWeight + nightWeight;
            if (sum > 0.0001f)
            {
                dawnWeight  /= sum;
                dayWeight   /= sum;
                duskWeight  /= sum;
                nightWeight /= sum;
            }

            // Blend Settings
            float exp = dawnSettings.postExposure * dawnWeight +
                        daySettings.postExposure  * dayWeight +
                        duskSettings.postExposure * duskWeight +
                        nightSettings.postExposure * nightWeight;

            float cont = dawnSettings.contrast * dawnWeight +
                         daySettings.contrast  * dayWeight +
                         duskSettings.contrast * duskWeight +
                         nightSettings.contrast * nightWeight;

            Color filter = dawnSettings.colorFilter * dawnWeight +
                           daySettings.colorFilter  * dayWeight +
                           duskSettings.colorFilter * duskWeight +
                           nightSettings.colorFilter * nightWeight;

            float sat = dawnSettings.saturation * dawnWeight +
                        daySettings.saturation  * dayWeight +
                        duskSettings.saturation * duskWeight +
                        nightSettings.saturation * nightWeight;

            Vector4 shad = dawnSettings.shadowTint * dawnWeight +
                           daySettings.shadowTint  * dayWeight +
                           duskSettings.shadowTint * duskWeight +
                           nightSettings.shadowTint * nightWeight;

            Vector4 high = dawnSettings.highlightTint * dawnWeight +
                           daySettings.highlightTint  * dayWeight +
                           duskSettings.highlightTint * duskWeight +
                           nightSettings.highlightTint * nightWeight;

            float bloom = dawnSettings.bloomIntensity * dawnWeight +
                          daySettings.bloomIntensity  * dayWeight +
                          duskSettings.bloomIntensity * duskWeight +
                          nightSettings.bloomIntensity * nightWeight;

            float vig = dawnSettings.vignetteIntensity * dawnWeight +
                        daySettings.vignetteIntensity  * dayWeight +
                        duskSettings.vignetteIntensity * duskWeight +
                        nightSettings.vignetteIntensity * nightWeight;

            // Apply to URP volume overrides
            if (_colorAdjustments != null)
            {
                _colorAdjustments.postExposure.value = exp;
                _colorAdjustments.contrast.value     = cont;
                _colorAdjustments.colorFilter.value  = filter;
                _colorAdjustments.saturation.value   = sat;
            }

            if (_shadowsMidtones != null)
            {
                _shadowsMidtones.shadows.value    = shad;
                _shadowsMidtones.highlights.value = high;
            }

            if (_bloom != null)
                _bloom.intensity.value = bloom;

            if (_vignette != null)
                _vignette.intensity.value = vig;
        }
    }
}

