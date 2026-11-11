using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Controls the Star Dome and Milky Way.
    /// Manages:
    ///   • Astronomical dome centering around the player camera (zero parallax)
    ///   • Astronomical East-to-West celestial dome rotation around the celestial pole
    ///   • MaterialPropertyBlock updates for StarDome shader (sun altitude fade, box visibility)
    ///   • Optional Shuriken particle system driving for local atmospheric stars/dust
    ///
    /// Zero GC alloc in Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarField : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Inspector — References
        // ───────────────────────────────────────────────────────────────
        [Header("── Core References ─────────────────────────")]
        [Tooltip("The DayNightCycle driver. Auto-finds in scene if null.")]
        public DayNightCycle dayNightCycle;

        [Tooltip("The CelestialVolume boundary. Auto-finds in scene if null.")]
        public CelestialVolume celestialVolume;

        [Tooltip("Renderer of the Star Dome mesh (using CelestialSystem/StarDome shader).")]
        public Renderer starDomeRenderer;

        [Tooltip("Optional Shuriken ParticleSystem for additional star scintillation or shooting stars.")]
        public ParticleSystem starParticles;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Dimensions & Scale
        // ───────────────────────────────────────────────────────────────
        [Header("── Star Dome Dimensions ─────────────────────")]
        [Tooltip("Radius of the star dome sphere.")]
        [Range(200f, 10000f)] public float domeRadius = 1400f;

        // ───────────────────────────────────────────────────────────────
        //  Cached Shader Property IDs (Zero Alloc)
        // ───────────────────────────────────────────────────────────────
        private static readonly int PropSunAltitude = Shader.PropertyToID("_SunAltitude");
        private static readonly int PropVisibility  = Shader.PropertyToID("_Visibility");

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

            if (starDomeRenderer == null)
                starDomeRenderer = GetComponent<Renderer>();

            if (starParticles == null)
                starParticles = GetComponentInChildren<ParticleSystem>();
        }

        private void Start()
        {
            ResolveCamera();
            UpdateStarField();
        }

        private void LateUpdate()
        {
            UpdateStarField();
        }

        // ───────────────────────────────────────────────────────────────
        //  Core Star Update
        // ───────────────────────────────────────────────────────────────
        private void UpdateStarField()
        {
            if (dayNightCycle == null) return;

            ResolveCamera();

            // Center dome on camera & apply astronomical celestial pole rotation
            if (_cachedCamera != null)
            {
                transform.position = _cachedCamera.transform.position;
                transform.localScale = Vector3.one * domeRadius;
            }

            // Rotate around the celestial pole (East to West rotation)
            transform.rotation = dayNightCycle.StarDomeRotation;

            float sunAltitude = dayNightCycle.SunAltitude;
            float visibility  = celestialVolume != null ? celestialVolume.Visibility : 1.0f;

            // Update MaterialPropertyBlock
            if (starDomeRenderer != null)
            {
                starDomeRenderer.GetPropertyBlock(_mpb);
                _mpb.SetFloat(PropSunAltitude, sunAltitude);
                _mpb.SetFloat(PropVisibility, visibility);
                starDomeRenderer.SetPropertyBlock(_mpb);
            }

            // Optional particle system emission scaling
            if (starParticles != null)
            {
                // Stars fade in as sun goes below -5°
                float starFactor = Mathf.Clamp01((-sunAltitude - 5f) / 10f) * visibility;
                var emission = starParticles.emission;
                emission.enabled = starFactor > 0.01f;
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
