using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Controls the atmospheric sky dome mesh and its SkyGradient material.
    /// Centers the sky dome around the player camera to prevent parallax.
    /// Updates sun direction, altitude, twilight blends, and box visibility.
    ///
    /// Zero GC alloc in Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkyController : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Inspector — References
        // ───────────────────────────────────────────────────────────────
        [Header("── Core References ─────────────────────────")]
        [Tooltip("The DayNightCycle driver. Auto-finds in scene if null.")]
        public DayNightCycle dayNightCycle;

        [Tooltip("The CelestialVolume boundary. Auto-finds in scene if null.")]
        public CelestialVolume celestialVolume;

        [Tooltip("Renderer of the Sky Dome mesh (using CelestialSystem/SkyGradient shader).")]
        public Renderer skyRenderer;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Sky Scale & Transform
        // ───────────────────────────────────────────────────────────────
        [Header("── Sky Dome Dimensions ──────────────────────")]
        [Tooltip("Uniform scale applied to the sky dome mesh.")]
        [Range(200f, 10000f)] public float domeRadius = 1500f;

        [Tooltip("Follow camera position to eliminate parallax.")]
        public bool followCamera = true;

        // ───────────────────────────────────────────────────────────────
        //  Cached Shader Property IDs (Zero Alloc)
        // ───────────────────────────────────────────────────────────────
        private static readonly int PropSunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int PropSunAltitude  = Shader.PropertyToID("_SunAltitude");
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

            if (skyRenderer == null)
                skyRenderer = GetComponent<Renderer>();
        }

        private void Start()
        {
            ResolveCamera();
            UpdateSky();
        }

        private void LateUpdate()
        {
            UpdateSky();
        }

        // ───────────────────────────────────────────────────────────────
        //  Core Update
        // ───────────────────────────────────────────────────────────────
        private void UpdateSky()
        {
            if (dayNightCycle == null) return;

            ResolveCamera();

            // Center dome on camera & fit safely inside far clip plane
            if (followCamera && _cachedCamera != null)
            {
                float far = _cachedCamera.farClipPlane;
                float effectiveRadius = Mathf.Min(domeRadius, far * 0.92f);
                transform.position = _cachedCamera.transform.position;
                transform.localScale = Vector3.one * (effectiveRadius * 2.0f);
            }

            // Push properties to renderer via MaterialPropertyBlock
            if (skyRenderer != null)
            {
                float visibility = celestialVolume != null ? celestialVolume.Visibility : 1.0f;
                Vector3 sunDir   = dayNightCycle.SunWorldDirection;
                float altitude   = dayNightCycle.SunAltitude;

                skyRenderer.GetPropertyBlock(_mpb);
                _mpb.SetVector(PropSunDirection, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
                _mpb.SetFloat(PropSunAltitude, altitude);
                _mpb.SetFloat(PropVisibility, visibility);
                skyRenderer.SetPropertyBlock(_mpb);
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

