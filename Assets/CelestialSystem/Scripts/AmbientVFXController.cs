using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Local ambient particle VFX controller (Shuriken Particle System):
    ///   • Centers around the player camera to provide immediate atmospheric depth
    ///   • Dynamic Wind Simulation: dust motes and airborne debris visibly streak across
    ///     the camera aligned with CloudController's wind vector and speed
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

        [Tooltip("CloudController reference for global wind synchronization.")]
        public CloudController cloudController;

        [Tooltip("ParticleSystem for daytime wind dust motes.")]
        public ParticleSystem dayDustParticles;

        [Tooltip("ParticleSystem for nighttime fireflies.")]
        public ParticleSystem nightFireflyParticles;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Wind & Particle Settings
        // ───────────────────────────────────────────────────────────────
        [Header("── Wind Simulation (Airborne Motes) ─────────")]
        [Tooltip("Enable wind velocity forcing on ambient particles.")]
        public bool enableWindPhysics = true;

        [Tooltip("Multiplier converting cloud wind speed to airborne particle velocity.")]
        [Range(0.5f, 20f)] public float windSpeedFactor = 6.0f;

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

            if (cloudController == null)
                cloudController = FindFirstObjectByType<CloudController>();

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

            // Calculate wind velocity vector
            Vector3 windDir = (cloudController != null) ? cloudController.CurrentWindDirection : new Vector3(0.7f, 0f, 0.7f);
            float windSpeed = (cloudController != null) ? cloudController.CurrentWindSpeed : 0.04f;
            Vector3 particleWindVelocity = windDir * (windSpeed * windSpeedFactor * 40f);

            // ── Update Day Dust (Visibly blows with the wind) ──
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

                    if (enableWindPhysics)
                    {
                        var vel = dayDustParticles.velocityOverLifetime;
                        vel.enabled = true;
                        vel.space = ParticleSystemSimulationSpace.World;
                        vel.x = new ParticleSystem.MinMaxCurve(particleWindVelocity.x * 0.7f, particleWindVelocity.x * 1.3f);
                        vel.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.35f);
                        vel.z = new ParticleSystem.MinMaxCurve(particleWindVelocity.z * 0.7f, particleWindVelocity.z * 1.3f);
                    }

                    if (!dayDustParticles.isPlaying)
                        dayDustParticles.Play();
                }
            }

            // ── Update Night Fireflies (Gentle nocturnal drift) ──
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

                    if (enableWindPhysics)
                    {
                        var vel = nightFireflyParticles.velocityOverLifetime;
                        vel.enabled = true;
                        vel.space = ParticleSystemSimulationSpace.World;
                        vel.x = new ParticleSystem.MinMaxCurve(particleWindVelocity.x * 0.2f - 0.3f, particleWindVelocity.x * 0.2f + 0.3f);
                        vel.y = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
                        vel.z = new ParticleSystem.MinMaxCurve(particleWindVelocity.z * 0.2f - 0.3f, particleWindVelocity.z * 0.2f + 0.3f);
                    }

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
