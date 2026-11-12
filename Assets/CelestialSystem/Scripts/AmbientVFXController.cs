using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Local ambient particle VFX controller (Shuriken Particle System):
    ///   • Centers around the player camera to provide immediate atmospheric depth
    ///   • Day Dust Motes: subtle floating micro-particles illuminated by sunlight
    ///   • Night Fireflies: gentle organic bioluminescent particles with pulsation
    ///   • Smooth transition between day and night modes
    ///   • Automatic spawn rate scaling with CelestialVolume.Visibility
    ///
    /// Zero GC alloc in Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientVFXController : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Inspector — References
        // ───────────────────────────────────────────────────────────────
        [Header("── Core References ─────────────────────────")]
        [Tooltip("The DayNightCycle driver. Auto-finds in scene if null.")]
        public DayNightCycle dayNightCycle;

        [Tooltip("The CelestialVolume boundary. Auto-finds in scene if null.")]
        public CelestialVolume celestialVolume;

        [Tooltip("ParticleSystem for daytime dust motes.")]
        public ParticleSystem dayDustParticles;

        [Tooltip("ParticleSystem for nighttime fireflies.")]
        public ParticleSystem nightFireflyParticles;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Emission Settings
        // ───────────────────────────────────────────────────────────────
        [Header("── Dust Motes (Day) ─────────────────────────")]
        [Tooltip("Enable daytime floating dust motes.")]
        public bool enableDayDust = true;

        [Tooltip("Max spawn rate per second for day dust motes.")]
        [Range(0f, 200f)] public float dayDustSpawnRate = 45f;

        [Header("── Fireflies (Night) ────────────────────────")]
        [Tooltip("Enable nighttime bioluminescent fireflies.")]
        public bool enableNightFireflies = true;

        [Tooltip("Max spawn rate per second for night fireflies.")]
        [Range(0f, 60f)] public float nightFireflySpawnRate = 18f;

        // ───────────────────────────────────────────────────────────────
        //  Private Cached State
        // ───────────────────────────────────────────────────────────────
        private Camera _cachedCamera;

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (dayNightCycle == null)
                dayNightCycle = FindFirstObjectByType<DayNightCycle>();

            if (celestialVolume == null)
                celestialVolume = FindFirstObjectByType<CelestialVolume>();

            // Auto-detect children systems if not assigned
            ParticleSystem[] psList = GetComponentsInChildren<ParticleSystem>();
            if (psList.Length > 0 && dayDustParticles == null)
                dayDustParticles = psList[0];
            if (psList.Length > 1 && nightFireflyParticles == null)
                nightFireflyParticles = psList[1];
        }

        private void Start()
        {
            ResolveCamera();
            UpdateVFX();
        }

        private void LateUpdate()
        {
            UpdateVFX();
        }

        // ───────────────────────────────────────────────────────────────
        //  Core VFX Update
        // ───────────────────────────────────────────────────────────────
        private void UpdateVFX()
        {
            ResolveCamera();

            // Center on camera
            if (_cachedCamera != null)
                transform.position = _cachedCamera.transform.position;

            float visibility = celestialVolume != null ? celestialVolume.Visibility : 1.0f;
            float sunAltitude = dayNightCycle != null ? dayNightCycle.SunAltitude : 45f;

            // Day factor: 1 when altitude > 5°, 0 when altitude < -5°
            float dayFactor = Mathf.Clamp01((sunAltitude + 5f) / 10f);
            float nightFactor = 1.0f - dayFactor;

            // ── Update Day Dust ──
            if (dayDustParticles != null)
            {
                var emission = dayDustParticles.emission;
                if (!enableDayDust || visibility <= 0.001f || dayFactor <= 0.01f)
                {
                    emission.rateOverTime = 0f;
                }
                else
                {
                    emission.rateOverTime = dayDustSpawnRate * dayFactor * visibility;
                    if (!dayDustParticles.isPlaying)
                        dayDustParticles.Play();
                }
            }

            // ── Update Night Fireflies ──
            if (nightFireflyParticles != null)
            {
                var emission = nightFireflyParticles.emission;
                if (!enableNightFireflies || visibility <= 0.001f || nightFactor <= 0.01f)
                {
                    emission.rateOverTime = 0f;
                }
                else
                {
                    emission.rateOverTime = nightFireflySpawnRate * nightFactor * visibility;
                    if (!nightFireflyParticles.isPlaying)
                        nightFireflyParticles.Play();
                }
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

