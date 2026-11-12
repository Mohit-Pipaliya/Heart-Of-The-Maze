using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Master controller for the dynamic AAA cloud layer.
    /// Manages:
    ///   • Dual-layer clouds (Billowy Cumulus + Wispy Cirrus)
    ///   • Dynamic wind simulation (speed, angle, boiling turbulence)
    ///   • Cloud dome centering on player camera & frustum far-plane auto-adaptation
    ///   • Feeding sun and moon light colors, directions, and intensities
    ///   • Dynamic silver lining and golden hour rim lighting
    ///   • Coverage and density sliders
    ///   • CelestialVolume visibility fading
    ///
    /// Zero GC alloc in Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CloudController : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Inspector — References
        // ───────────────────────────────────────────────────────────────
        [Header("── Core References ─────────────────────────")]
        [Tooltip("The DayNightCycle driver. Auto-finds in scene if null.")]
        public DayNightCycle dayNightCycle;

        [Tooltip("The CelestialVolume boundary. Auto-finds in scene if null.")]
        public CelestialVolume celestialVolume;

        [Tooltip("SunController reference for lighting sync. Auto-finds if null.")]
        public SunController sunController;

        [Tooltip("MoonController reference for moonlight sync. Auto-finds if null.")]
        public MoonController moonController;

        [Tooltip("Renderer of the cloud layer mesh (using CelestialSystem/Clouds shader).")]
        public Renderer cloudRenderer;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Cloud Layer Dimensions
        // ───────────────────────────────────────────────────────────────
        [Header("── Dimensions & Camera Fitting ──────────────")]
        [Tooltip("Base radius of the cloud hemisphere.")]
        [Range(200f, 10000f)] public float cloudRadius = 850f;

        [Tooltip("Automatically fit cloud dome to camera farClipPlane * 0.85f to prevent frustum clipping.")]
        public bool autoAdaptToFarClip = true;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Cloud Appearance & Wind
        // ───────────────────────────────────────────────────────────────
        [Header("── Morphology & Wind (AAA Dynamic Atmosphere) ──")]
        [Tooltip("Cloud coverage (0 = clear sky, 0.42 = scattered cumulus, 0.85 = overcast).")]
        [Range(0f, 1f)] public float cloudCoverage = 0.42f;

        [Tooltip("Cloud opacity density.")]
        [Range(0.5f, 5f)] public float cloudDensity = 1.8f;

        [Tooltip("Wind speed multiplier (controls how fast clouds drift).")]
        [Range(0f, 0.3f)] public float windSpeed = 0.03f;

        [Tooltip("Wind direction angle in degrees (compass yaw heading: 0=N, 90=E, 180=S, 270=W).")]
        [Range(0f, 360f)] public float windDirectionAngle = 55f;

        [Tooltip("Micro-turbulence boiling speed (clouds dynamically morph and billow as they travel).")]
        [Range(0f, 0.3f)] public float turbulenceSpeed = 0.04f;

        [Header("── High-Altitude Cirrus Layer ──────────────")]
        [Tooltip("Visibility of high-altitude wispy cirrus cloud ribbons.")]
        [Range(0f, 1f)] public float cirrusOpacity = 0.22f;

        // ───────────────────────────────────────────────────────────────
        //  Public Read-Only Wind API (consumed by AmbientVFX & grass/trees)
        // ───────────────────────────────────────────────────────────────
        public Vector3 CurrentWindDirection { get; private set; } = new Vector3(0.82f, 0f, 0.57f);
        public float CurrentWindSpeed => windSpeed;

        // ───────────────────────────────────────────────────────────────
        //  Cached Shader Property IDs (Zero Alloc)
        // ───────────────────────────────────────────────────────────────
        private static readonly int PropSunDirection     = Shader.PropertyToID("_SunDirection");
        private static readonly int PropSunColor         = Shader.PropertyToID("_SunColor");
        private static readonly int PropSunAltitude      = Shader.PropertyToID("_SunAltitude");
        private static readonly int PropSunIntensity     = Shader.PropertyToID("_SunIntensity");
        private static readonly int PropMoonDirection    = Shader.PropertyToID("_MoonDirection");
        private static readonly int PropMoonColor        = Shader.PropertyToID("_MoonColor");
        private static readonly int PropMoonIntensity    = Shader.PropertyToID("_MoonIntensity");
        private static readonly int PropWindDirection    = Shader.PropertyToID("_WindDirection");
        private static readonly int PropWindSpeed        = Shader.PropertyToID("_WindSpeed");
        private static readonly int PropTurbulenceSpeed  = Shader.PropertyToID("_TurbulenceSpeed");
        private static readonly int PropCirrusOpacity    = Shader.PropertyToID("_CirrusOpacity");
        private static readonly int PropCoverage         = Shader.PropertyToID("_Coverage");
        private static readonly int PropDensity          = Shader.PropertyToID("_Density");
        private static readonly int PropVisibility       = Shader.PropertyToID("_Visibility");

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

            if (moonController == null)
                moonController = FindFirstObjectByType<MoonController>();

            if (cloudRenderer == null)
                cloudRenderer = GetComponent<Renderer>();
        }

        private void Start()
        {
            ResolveCamera();
            UpdateClouds();
        }

        private void LateUpdate()
        {
            UpdateClouds();
        }

        // ───────────────────────────────────────────────────────────────
        //  Core Update
        // ───────────────────────────────────────────────────────────────
        private void UpdateClouds()
        {
            if (dayNightCycle == null) return;

            ResolveCamera();

            // Center on camera & scale safely inside camera far clip plane
            if (_cachedCamera != null)
            {
                float far = _cachedCamera.farClipPlane;
                float effectiveRadius = autoAdaptToFarClip ? Mathf.Min(cloudRadius, far * 0.85f) : cloudRadius;

                transform.position = _cachedCamera.transform.position;
                transform.localScale = Vector3.one * (effectiveRadius * 2.0f);
            }

            if (cloudRenderer != null)
            {
                float visibility = celestialVolume != null ? celestialVolume.Visibility : 1.0f;

                // Sun data
                Vector3 sunDir = dayNightCycle.SunWorldDirection;
                Color sunCol = (sunController != null && sunController.sunLight != null)
                    ? sunController.sunLight.color
                    : Color.white;
                float sunInt = Mathf.Clamp01(dayNightCycle.SunAltitude / 15f);
                float sunAlt = dayNightCycle.SunAltitude;

                // Moon data
                Vector3 moonDir = dayNightCycle.MoonWorldDirection;
                Color moonCol = (moonController != null && moonController.moonLight != null)
                    ? moonController.moonLight.color
                    : new Color(0.72f, 0.84f, 1.0f);
                float moonInt = (moonController != null && moonController.moonLight != null)
                    ? Mathf.Clamp01(moonController.moonLight.intensity / 0.25f)
                    : 0.2f;

                // Wind vector from angle
                float rad = windDirectionAngle * Mathf.Deg2Rad;
                Vector3 wind3D = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                CurrentWindDirection = wind3D;
                Vector4 windVec = new Vector4(wind3D.x, 0f, wind3D.z, 0f);

                cloudRenderer.GetPropertyBlock(_mpb);
                _mpb.SetVector(PropSunDirection, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
                _mpb.SetColor(PropSunColor, sunCol);
                _mpb.SetFloat(PropSunAltitude, sunAlt);
                _mpb.SetFloat(PropSunIntensity, sunInt);

                _mpb.SetVector(PropMoonDirection, new Vector4(moonDir.x, moonDir.y, moonDir.z, 0f));
                _mpb.SetColor(PropMoonColor, moonCol);
                _mpb.SetFloat(PropMoonIntensity, moonInt);

                _mpb.SetVector(PropWindDirection, windVec);
                _mpb.SetFloat(PropWindSpeed, windSpeed);
                _mpb.SetFloat(PropTurbulenceSpeed, turbulenceSpeed);
                _mpb.SetFloat(PropCirrusOpacity, cirrusOpacity);
                _mpb.SetFloat(PropCoverage, cloudCoverage);
                _mpb.SetFloat(PropDensity, cloudDensity);
                _mpb.SetFloat(PropVisibility, visibility);

                cloudRenderer.SetPropertyBlock(_mpb);
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
