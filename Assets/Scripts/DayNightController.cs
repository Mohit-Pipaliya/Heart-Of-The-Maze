using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// AAA Realistic Day & Night Controller.
/// Features:
///   • Unified 24-Hour Continuous Cycle: Seamless progression (0.0=Sunrise, 0.25=Noon, 0.50=Sunset, 0.75=Midnight).
///   • Real Sun & Moon Light Generation: Directional Light dynamically tracks Sun during day and Moon during night,
///     projecting authentic parallel rays and long soft shadows directly from the celestial source.
///   • Zero Moving Circular Shadows: All celestial bodies, cloud domes, stars, and corona halos have shadow-casting
///     explicitly disabled so zero artificial circular shadows can ever follow the player.
///   • Real-Life Inspired Cloud Canopy: 2200m high-altitude curved atmospheric sky dome using custom
///     Perlin-Worley multi-octave fractal noise with Mie forward scattering silver lining and dual wind drift.
///   • Sparkling Night Star Dome: 1200 elevated, high-visibility glittering stars (18m-48m) on the upper sky dome
///     shell (+500m base, pointing 100% UP) that smoothly fade in at dusk and sparkle throughout the night.
///   • True Trilight Atmospheric Illumination: Rich 3-color ambient lighting (Sky, Equator, Ground) giving deep,
///     natural skylight contrast in shadowed areas.
///   • True Cinematic Fade-In & Fade-Out: Sunlight and moonlight scale smoothly with altitude, fading to exact 0.0
///     at the horizon, with seamless continuous twilight blending and zero sudden light pops or color jumps.
/// </summary>
[ExecuteAlways]
[SelectionBase]
[DisallowMultipleComponent]
public class DayNightController : MonoBehaviour
{
    public static DayNightController Instance { get; private set; }

    [Header("── 1. Sky Arc & Horizon Setup ────────────────────")]
    [Tooltip("East reference point where Sun rises and Moon sets.")]
    [SerializeField] private Transform eastSunPoint;

    [Tooltip("West reference point where Sun sets and Moon rises.")]
    [SerializeField] private Transform westSunPoint;

    [Tooltip("Center of the celestial dome. Defaults to Glade/Maze/Player or midpoint between East & West.")]
    [SerializeField] private Transform worldCenterTarget;

    [Tooltip("When enabled, extends the East and West points to a grand horizon radius for a truly massive, realistic sky arc.")]
    [SerializeField] private bool autoExpandToHorizon = true;

    [Tooltip("Horizontal distance from the center to the horizon endpoints of the sky arc (in meters).")]
    [Range(1000f, 9000f)]
    [SerializeField] private float skyRadius = 3800f;

    [Tooltip("Altitude of the horizon points above the ground floor so sunrise/sunset is above mountains/trees.")]
    [Range(50f, 1500f)]
    [SerializeField] private float baseHorizonHeight = 350f;

    [Tooltip("Maximum height of the celestial arc at the apex (Zenith / High Noon) above horizon.")]
    [Range(500f, 6000f)]
    [SerializeField] private float arcHeight = 2600f;

    [Header("── 2. Sun & Moon 3D Models & Scales ──────────────")]
    [Tooltip("Sun prefab from Assets/Prefeb/Sun.prefab")]
    [SerializeField] private GameObject sunPrefab;

    [Tooltip("Moon prefab from Assets/Prefeb/Moon.prefab")]
    [SerializeField] private GameObject moonPrefab;

    [Tooltip("Scene instance of the Sun.")]
    [SerializeField] private Transform sunInstance;

    [Tooltip("Scene instance of the Moon.")]
    [SerializeField] private Transform moonInstance;

    [Tooltip("Scale of the Sun model in the sky.")]
    [SerializeField] private Vector3 sunScale = new Vector3(150f, 150f, 150f);

    [Tooltip("Scale of the Moon model in the sky (accounts for millimeter FBX unit scale).")]
    [SerializeField] private Vector3 moonScale = new Vector3(85000f, 85000f, 85000f);

    [Header("── 2b. Radiant Celestial Glow ─────────────────────")]
    [Tooltip("Enable radiant glowing corona halos around Sun and Moon.")]
    [SerializeField] private bool enableCelestialGlow = true;

    [Tooltip("Sun HDR Emission Glow Color.")]
    [SerializeField] private Color sunGlowColor = new Color(12.0f, 9.5f, 4.0f, 1f);

    [Tooltip("Moon HDR Emission Glow Color.")]
    [SerializeField] private Color moonGlowColor = new Color(3.5f, 4.2f, 6.0f, 1f);

    [Tooltip("Corona halo scale multiplier relative to celestial body scale.")]
    [Range(1.2f, 5f)]
    [SerializeField] private float coronaScaleMultiplier = 2.4f;

    [Header("── 3. Unified 24-Hour Cycle & Time Control ───────")]
    [Tooltip("Total duration of a full 24-hour Day/Night cycle in seconds.")]
    [Min(10f)]
    [SerializeField] private float fullCycleDuration = 240f;

    [Tooltip("Continuous 24-hour cycle time: 0.0=Sunrise, 0.25=Noon, 0.50=Sunset, 0.75=Midnight.")]
    [Range(0f, 1f)]
    [SerializeField] private float cycleTime = 0.10f; // Starts in beautiful morning daylight!

    [Tooltip("Advance time automatically in Play mode.")]
    [SerializeField] private bool autoAdvance = true;

    [Tooltip("Time speed multiplier for fast testing.")]
    [Range(0.1f, 50f)]
    [SerializeField] private float timeMultiplier = 1f;

    [Header("── 4. Directional Light & Realistic Shadows ──────")]
    [Tooltip("Directional Light illuminating the world.")]
    [SerializeField] private Light directionalLight;

    [Tooltip("Maximum intensity of the Sun at high noon.")]
    [Range(0.1f, 4f)]
    [SerializeField] private float sunMaxIntensity = 1.65f;

    [Tooltip("Maximum intensity of the Moon at midnight.")]
    [Range(0.01f, 1.5f)]
    [SerializeField] private float moonMaxIntensity = 0.38f;

    [Header("── 5. Atmospheric Sunlight & Moonlight Colors ────")]
    [Tooltip("Sunlight color at dawn sunrise.")]
    [SerializeField] private Color sunriseColor = new Color(1.0f, 0.58f, 0.22f, 1f);

    [Tooltip("Sunlight color at clear midday.")]
    [SerializeField] private Color dayColor = new Color(1.0f, 0.98f, 0.94f, 1f);

    [Tooltip("Sunlight color at golden hour.")]
    [SerializeField] private Color goldenHourColor = new Color(1.0f, 0.74f, 0.38f, 1f);

    [Tooltip("Sunlight color at crimson sunset.")]
    [SerializeField] private Color sunsetColor = new Color(1.0f, 0.40f, 0.12f, 1f);

    [Tooltip("Sunlight/Moonlight color during dusk twilight.")]
    [SerializeField] private Color twilightColor = new Color(0.65f, 0.42f, 0.70f, 1f);

    [Tooltip("Moonlight color at night.")]
    [SerializeField] private Color nightColor = new Color(0.55f, 0.72f, 1.0f, 1f);

    [Header("── 6. Real-Life Inspired Cloud Canopy (High Sky) ─")]
    [Tooltip("Enable realistic high-altitude sky cloud canopy. Never touches terrain!")]
    [SerializeField] private bool enableCloudCanopy = true;

    [Tooltip("Altitude of the cloud canopy above the ground floor.")]
    [Range(1000f, 5000f)]
    [SerializeField] private float cloudCanopyAltitude = 2200f;

    [Tooltip("Radius of the cloud canopy sky dome (meters).")]
    [Range(2000f, 9000f)]
    [SerializeField] private float cloudCanopyRadius = 5500f;

    [Tooltip("Cloud Quantity / Sky Coverage: Kitne clouds aakash me dikhenge (0.1 = Bahot kam badal, 0.52 = Normal scattered clouds, 0.85 = Overcast ghane badal)")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float cloudCoverage = 0.52f;

    [Tooltip("Cloud Thickness / Density: Badal kitne ghane / mote honge (0.5 = Halke patle badal, 1.35 = Normal thick, 3.5 = Bahot ghane dense badal)")]
    [Range(0.5f, 3.5f)]
    [SerializeField] private float cloudDensity = 1.35f;

    [Tooltip("Cloud Edge Softness: Badlo ke kinare kitne mulayam / feathery honge")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float cloudSoftness = 0.32f;

    [Tooltip("Cloud Scale: Badlo ka size (kam number = bade continuous formations, jyada number = chhote badal)")]
    [Range(0.2f, 3.0f)]
    [SerializeField] private float cloudScale = 0.65f;

    [Tooltip("Wind Speed: Hawa ke sath badal chalne ki speed")]
    [Range(0.0005f, 0.02f)]
    [SerializeField] private float cloudWindSpeed = 0.003f;

    public float CloudCoverage { get => cloudCoverage; set => cloudCoverage = Mathf.Clamp(value, 0.1f, 0.9f); }
    public float CloudDensity { get => cloudDensity; set => cloudDensity = Mathf.Clamp(value, 0.5f, 3.5f); }
    public float CloudSoftness { get => cloudSoftness; set => cloudSoftness = Mathf.Clamp(value, 0.05f, 0.5f); }
    public float CloudScale { get => cloudScale; set => cloudScale = Mathf.Clamp(value, 0.2f, 3.0f); }
    public float CloudWindSpeed { get => cloudWindSpeed; set => cloudWindSpeed = Mathf.Clamp(value, 0.0005f, 0.02f); }

    [Header("── 7. Elevated Stars Dome VFX (High Sky Only) ────")]
    [SerializeField] private ParticleSystem starsSystem;

    [Tooltip("Radius of the celestial star dome hemisphere (meters).")]
    [Range(2000f, 9000f)]
    [SerializeField] private float starDomeRadius = 4200f;

    // Public Events
    public event Action OnSunrise;
    public event Action OnSunset;
    public event Action OnDayStart;
    public event Action OnNightStart;

    // Backward-compatible properties
    public bool IsDay => cycleTime < 0.5f;
    public bool IsNight => cycleTime >= 0.5f;
    public float TimeNormalized => cycleTime < 0.5f ? (cycleTime / 0.5f) : ((cycleTime - 0.5f) / 0.5f);
    public float CycleTime { get => cycleTime; set => cycleTime = Mathf.Repeat(value, 1f); }
    public float DayDuration { get => fullCycleDuration * 0.5f; set => fullCycleDuration = Mathf.Max(10f, value * 2f); }
    public float NightDuration { get => fullCycleDuration * 0.5f; set => fullCycleDuration = Mathf.Max(10f, value * 2f); }
    public Transform EastPoint => eastSunPoint;
    public Transform WestPoint => westSunPoint;

    private Material _starMaterialInstance;
    private Material _cloudCanopyMaterialInstance;
    private GameObject _cloudCanopyObject;
    private Transform _sunCorona;
    private Transform _moonCorona;
    private bool _previousWasDay = true;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        try
        {
            CleanUpLegacyArtifacts();
            AutoFindSceneReferences();
            EnsureCameraFrustum();
            EnsureCelestialInstances();
            EnsureGlowHalos();
            EnsureRealisticSkyVisuals();
            UpdateCelestialState();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DayNightController] Awake init notice: " + ex.Message);
        }
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        try
        {
            CleanUpLegacyArtifacts();
            AutoFindSceneReferences();
            EnsureCameraFrustum();
            EnsureCelestialInstances();
            EnsureGlowHalos();
            EnsureRealisticSkyVisuals();
            UpdateCelestialState();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DayNightController] OnEnable init notice: " + ex.Message);
        }
    }

    private void Start()
    {
        try
        {
            CleanUpLegacyArtifacts();
            EnsureCameraFrustum();
            _previousWasDay = IsDay;
            UpdateCelestialState();

            // Immediate startup synchronization for the Maze Main Door
            SyncDoorsWithTime();
            StartCoroutine(StartupDoorSyncRoutine());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DayNightController] Start init notice: " + ex.Message);
        }
    }

    private System.Collections.IEnumerator StartupDoorSyncRoutine()
    {
        yield return null; // Wait 1 frame so all Awake and Start in scene execute
        SyncDoorsWithTime();
        yield return new WaitForSeconds(0.25f);
        SyncDoorsWithTime();
    }

    public void SyncDoorsWithTime()
    {
        if (IsDay)
        {
            NotifyMazeSystemDay();
        }
        else
        {
            NotifyMazeSystemNight();
        }
    }

    private void Update()
    {
        if (Application.isPlaying && autoAdvance)
        {
            AdvanceTime(Time.deltaTime * timeMultiplier);
        }

        // Failsafe check: instantly destroy any legacy cloud particles if they exist
        if (transform.Find("Clouds_Cumulus") != null || transform.Find("Clouds_Cirrus") != null || transform.Find("Clouds_Layer") != null)
        {
            CleanUpLegacyArtifacts();
        }

        UpdateCelestialState();
    }

    private void OnValidate()
    {
        fullCycleDuration = Mathf.Max(10f, fullCycleDuration);
        cycleTime = Mathf.Repeat(cycleTime, 1f);
        arcHeight = Mathf.Max(100f, arcHeight);
        skyRadius = Mathf.Max(500f, skyRadius);

        try
        {
            CleanUpLegacyArtifacts();

            if (eastSunPoint == null || westSunPoint == null || directionalLight == null)
                AutoFindSceneReferences();

            EnsureCameraFrustum();
            EnsureGlowHalos();
            UpdateCelestialState();

            bool currentIsDay = IsDay;
            if (currentIsDay != _previousWasDay)
            {
                _previousWasDay = currentIsDay;
                SyncDoorsWithTime();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DayNightController] OnValidate notice: " + ex.Message);
        }
    }

    private void EnsureCameraFrustum()
    {
        var cam = Camera.main;
        if (cam != null && cam.farClipPlane < 10000f)
        {
            cam.farClipPlane = 12000f;
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Continuous Time Progression & Event Triggers
    // ───────────────────────────────────────────────────────────────

    public void AdvanceTime(float deltaSeconds)
    {
        cycleTime = Mathf.Repeat(cycleTime + (deltaSeconds / fullCycleDuration), 1f);

        bool currentIsDay = IsDay;
        if (currentIsDay != _previousWasDay)
        {
            _previousWasDay = currentIsDay;
            if (currentIsDay)
            {
                OnSunrise?.Invoke();
                OnDayStart?.Invoke();
                NotifyMazeSystemDay();
            }
            else
            {
                OnSunset?.Invoke();
                OnNightStart?.Invoke();
                NotifyMazeSystemNight();
            }
        }
    }

    public void SetTimeOfDay(bool day, float normalizedProgress)
    {
        float p = Mathf.Clamp01(normalizedProgress);
        cycleTime = day ? (p * 0.5f) : (0.5f + p * 0.5f);
        UpdateCelestialState();
        _previousWasDay = day;
        if (day) NotifyMazeSystemDay();
        else NotifyMazeSystemNight();
    }

    [ContextMenu("Jump To Sunrise")]
    public void JumpToSunrise()
    {
        cycleTime = 0.03f;
        _previousWasDay = true;
        UpdateCelestialState();
        NotifyMazeSystemDay();
    }

    [ContextMenu("Jump To High Noon")]
    public void JumpToNoon()
    {
        cycleTime = 0.25f;
        _previousWasDay = true;
        UpdateCelestialState();
        NotifyMazeSystemDay();
    }

    [ContextMenu("Jump To Sunset")]
    public void JumpToSunset()
    {
        cycleTime = 0.52f;
        _previousWasDay = false;
        UpdateCelestialState();
        NotifyMazeSystemNight();
    }

    [ContextMenu("Jump To Midnight")]
    public void JumpToMidnight()
    {
        cycleTime = 0.75f;
        _previousWasDay = false;
        UpdateCelestialState();
        NotifyMazeSystemNight();
    }

    public void NotifyMazeSystemDay()
    {
        // 1. Trigger MazeBlockManager (ONLY the active block facing the Glade opens its door!)
        var mazeManager = FindFirstObjectByType<MazeSystem.MazeBlockManager>();
        if (mazeManager != null)
        {
            mazeManager.AutoAdvanceDayNight = false;
            mazeManager.DelayNightWhilePlayerInside = false;
            mazeManager.TriggerDay();
            mazeManager.OpenActiveDoor();
        }

        // 2. Unlock Glade Entrance Gate Blocker
        var gate = FindFirstObjectByType<MazeSystem.GladeEntranceGate>();
        if (gate != null)
        {
            gate.SetBlocked(false);
        }
    }

    public void NotifyMazeSystemNight()
    {
        // 1. Trigger MazeBlockManager (ONLY the active block facing the Glade closes its door!)
        var mazeManager = FindFirstObjectByType<MazeSystem.MazeBlockManager>();
        if (mazeManager != null)
        {
            mazeManager.AutoAdvanceDayNight = false;
            mazeManager.DelayNightWhilePlayerInside = false;
            mazeManager.TriggerNight();
            mazeManager.CloseActiveDoor();
        }

        // 2. Lock Glade Entrance Gate Blocker
        var gate = FindFirstObjectByType<MazeSystem.GladeEntranceGate>();
        if (gate != null)
        {
            gate.SetBlocked(true);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Grand Sky Arc Calculation
    // ───────────────────────────────────────────────────────────────

    public Vector3 GetWorldCenter()
    {
        if (worldCenterTarget != null)
            return worldCenterTarget.position;

        var player = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
        if (player != null) return player.transform.position;

        var maze = GameObject.Find("Maze System");
        if (maze != null) return maze.transform.position;

        if (eastSunPoint != null && westSunPoint != null)
        {
            Vector3 mid = (eastSunPoint.position + westSunPoint.position) * 0.5f;
            return mid;
        }

        return transform.position;
    }

    public void GetHorizonEndPoints(out Vector3 eastHorizon, out Vector3 westHorizon)
    {
        Vector3 center = GetWorldCenter();

        if (autoExpandToHorizon)
        {
            Vector3 eastDir = Vector3.right;
            if (eastSunPoint != null)
            {
                Vector3 diff = eastSunPoint.position - center;
                diff.y = 0f;
                if (diff.sqrMagnitude > 1f) eastDir = diff.normalized;
            }

            Vector3 westDir = -eastDir;
            if (westSunPoint != null)
            {
                Vector3 diff = westSunPoint.position - center;
                diff.y = 0f;
                if (diff.sqrMagnitude > 1f) westDir = diff.normalized;
            }

            float baseGroundY = center.y + baseHorizonHeight;
            eastHorizon = new Vector3(center.x + eastDir.x * skyRadius, baseGroundY, center.z + eastDir.z * skyRadius);
            westHorizon = new Vector3(center.x + westDir.x * skyRadius, baseGroundY, center.z + westDir.z * skyRadius);
        }
        else
        {
            eastHorizon = eastSunPoint != null ? eastSunPoint.position : center + Vector3.right * skyRadius;
            westHorizon = westSunPoint != null ? westSunPoint.position : center - Vector3.right * skyRadius;
        }
    }

    public Vector3 CalculateSkyArcPosition(float arcProgress)
    {
        GetHorizonEndPoints(out Vector3 east, out Vector3 west);
        Vector3 horizontal = Vector3.Lerp(east, west, arcProgress);
        float verticalOffset = Mathf.Sin(arcProgress * Mathf.PI) * arcHeight;
        return horizontal + Vector3.up * verticalOffset;
    }

    // ───────────────────────────────────────────────────────────────
    //  State Update: Continuous Fade-In & Fade-Out Across 24 Hours
    // ───────────────────────────────────────────────────────────────

    public void UpdateCelestialState()
    {
        Vector3 worldCenter = GetWorldCenter();
        Camera mainCam = Camera.main;
        Vector3 camPos = mainCam != null ? mainCam.transform.position : worldCenter;

        // ── 1. SUN MOVEMENT & VISIBILITY ───────────────────────────
        // Sun is in the sky during cycleTime in [0.0, 0.50]
        float sunArcProgress = cycleTime / 0.50f;
        Vector3 sunPos = CalculateSkyArcPosition(Mathf.Clamp01(sunArcProgress));

        // Smooth Sun Horizon Fade Envelope: Fades in smoothly at 0.0..0.06, fades out at 0.44..0.50
        float sunHorizonFade = 0f;
        if (cycleTime >= 0f && cycleTime <= 0.50f)
        {
            if (cycleTime < 0.06f)
                sunHorizonFade = Mathf.SmoothStep(0f, 1f, cycleTime / 0.06f);
            else if (cycleTime > 0.44f)
                sunHorizonFade = Mathf.SmoothStep(1f, 0f, (cycleTime - 0.44f) / 0.06f);
            else
                sunHorizonFade = 1f;
        }

        if (sunInstance != null)
        {
            bool showSun = sunHorizonFade > 0.005f;
            if (sunInstance.gameObject.activeSelf != showSun)
                sunInstance.gameObject.SetActive(showSun);

            sunInstance.position = sunPos;
            sunInstance.localScale = sunScale * Mathf.Max(0.1f, sunHorizonFade);

            Vector3 lookDir = (worldCenter - sunPos).normalized;
            if (lookDir.sqrMagnitude > 0.01f)
                sunInstance.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            if (_sunCorona != null)
            {
                _sunCorona.gameObject.SetActive(enableCelestialGlow && showSun);
                _sunCorona.position = sunPos;
                _sunCorona.localScale = Vector3.one * (sunScale.x * coronaScaleMultiplier * sunHorizonFade);
                _sunCorona.LookAt(camPos);
            }
        }

        // ── 2. MOON MOVEMENT & VISIBILITY ──────────────────────────
        // Moon is in the sky during cycleTime in [0.50, 1.00]
        float moonArcProgress = (cycleTime - 0.50f) / 0.50f;
        Vector3 moonPos = CalculateSkyArcPosition(Mathf.Clamp01(moonArcProgress));

        // Smooth Moon Horizon Fade Envelope: Fades in smoothly at 0.50..0.56, fades out at 0.94..1.00
        float moonHorizonFade = 0f;
        if (cycleTime >= 0.50f && cycleTime <= 1.0f)
        {
            if (cycleTime < 0.56f)
                moonHorizonFade = Mathf.SmoothStep(0f, 1f, (cycleTime - 0.50f) / 0.06f);
            else if (cycleTime > 0.94f)
                moonHorizonFade = Mathf.SmoothStep(1f, 0f, (cycleTime - 0.94f) / 0.06f);
            else
                moonHorizonFade = 1f;
        }

        if (moonInstance != null)
        {
            bool showMoon = moonHorizonFade > 0.005f;
            if (moonInstance.gameObject.activeSelf != showMoon)
                moonInstance.gameObject.SetActive(showMoon);

            moonInstance.position = moonPos;
            moonInstance.localScale = moonScale * Mathf.Max(0.1f, moonHorizonFade);

            Vector3 lookDir = (worldCenter - moonPos).normalized;
            if (lookDir.sqrMagnitude > 0.01f)
                moonInstance.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            if (_moonCorona != null)
            {
                _moonCorona.gameObject.SetActive(enableCelestialGlow && showMoon);
                _moonCorona.position = moonPos;
                _moonCorona.localScale = Vector3.one * (200f * coronaScaleMultiplier * moonHorizonFade);
                _moonCorona.LookAt(camPos);
            }
        }

        // ── 3. DIRECTIONAL LIGHT TRACKING & SHADOWS ────────────────
        if (directionalLight != null)
        {
            if (!directionalLight.enabled) directionalLight.enabled = true;
            directionalLight.shadows = LightShadows.Soft;
            directionalLight.shadowStrength = (cycleTime <= 0.50f) ? 0.95f : 0.78f;
            directionalLight.shadowNormalBias = 0.4f;
            directionalLight.shadowBias = 0.05f;

            // Continuous Sun Light Intensity (Scales naturally with Sun Height, fades to exact 0.0 at sunset)
            float sunAltitudeSin = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(sunArcProgress) * Mathf.PI));
            float sunCurrentIntensity = (cycleTime <= 0.50f) ? (sunMaxIntensity * sunAltitudeSin * sunHorizonFade) : 0f;

            // Continuous Moon Light Intensity (Scales naturally with Moon Height, fades to exact 0.0 at moonset)
            float moonAltitudeSin = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(moonArcProgress) * Mathf.PI));
            float moonCurrentIntensity = (cycleTime >= 0.50f) ? (moonMaxIntensity * moonAltitudeSin * moonHorizonFade) : 0f;

            // Direct Light Ray Vector & Color Interpolation
            Vector3 lightSourcePos;
            Vector3 lightForward;
            Color lightCol;
            float totalIntensity;

            if (cycleTime < 0.45f)
            {
                // Pure Daylight
                lightSourcePos = sunPos;
                lightForward = (worldCenter - sunPos).normalized;
                totalIntensity = sunCurrentIntensity;

                if (cycleTime < 0.08f)
                    lightCol = Color.Lerp(sunriseColor, dayColor, Mathf.SmoothStep(0f, 1f, cycleTime / 0.08f));
                else if (cycleTime > 0.38f)
                    lightCol = Color.Lerp(dayColor, goldenHourColor, Mathf.SmoothStep(0f, 1f, (cycleTime - 0.38f) / 0.07f));
                else
                    lightCol = dayColor;
            }
            else if (cycleTime < 0.55f)
            {
                // Seamless Twilight Dusk Blend (Sunset -> Night)
                float tDusk = (cycleTime - 0.45f) / 0.10f;
                lightSourcePos = Vector3.Lerp(sunPos, moonPos, tDusk);
                Vector3 dirSun = (worldCenter - sunPos).normalized;
                Vector3 dirMoon = (worldCenter - moonPos).normalized;
                lightForward = Vector3.Slerp(dirSun, dirMoon, tDusk);

                Color duskColor = Color.Lerp(sunsetColor, twilightColor, Mathf.SmoothStep(0f, 1f, tDusk));
                lightCol = Color.Lerp(duskColor, nightColor, Mathf.SmoothStep(0f, 1f, tDusk));
                totalIntensity = Mathf.Lerp(sunCurrentIntensity, moonCurrentIntensity, tDusk);
            }
            else if (cycleTime < 0.94f)
            {
                // Pure Moonlight
                lightSourcePos = moonPos;
                lightForward = (worldCenter - moonPos).normalized;
                lightCol = nightColor;
                totalIntensity = moonCurrentIntensity;
            }
            else
            {
                // Seamless Twilight Dawn Blend (Night -> Sunrise)
                float tDawn = (cycleTime - 0.94f) / 0.06f;
                lightSourcePos = Vector3.Lerp(moonPos, sunPos, tDawn);
                Vector3 dirMoon = (worldCenter - moonPos).normalized;
                Vector3 dirSun = (worldCenter - sunPos).normalized;
                lightForward = Vector3.Slerp(dirMoon, dirSun, tDawn);

                lightCol = Color.Lerp(nightColor, sunriseColor, Mathf.SmoothStep(0f, 1f, tDawn));
                totalIntensity = Mathf.Lerp(moonCurrentIntensity, sunCurrentIntensity, tDawn);
            }

            // Position directional light along line of sight so rays clearly generate from Sun/Moon
            directionalLight.transform.position = worldCenter - lightForward * 750f;
            Vector3 safeUp = Mathf.Abs(Vector3.Dot(lightForward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            directionalLight.transform.rotation = Quaternion.LookRotation(lightForward, safeUp);
            directionalLight.color = lightCol;
            directionalLight.intensity = totalIntensity;

            // ── 3b. RICH TRILIGHT AMBIENT LIGHTING ───────────────────
            Color skyAmb, eqAmb, gndAmb;

            if (cycleTime < 0.08f)
            {
                // Dawn sunrise
                float f = cycleTime / 0.08f;
                skyAmb = Color.Lerp(new Color(0.14f, 0.18f, 0.32f), new Color(0.40f, 0.32f, 0.45f), f);
                eqAmb = Color.Lerp(new Color(0.25f, 0.16f, 0.20f), new Color(0.60f, 0.35f, 0.18f), f);
                gndAmb = Color.Lerp(new Color(0.05f, 0.04f, 0.05f), new Color(0.18f, 0.12f, 0.08f), f);
            }
            else if (cycleTime < 0.40f)
            {
                // Midday crisp daylight
                skyAmb = new Color(0.38f, 0.48f, 0.65f);
                eqAmb = new Color(0.32f, 0.36f, 0.42f);
                gndAmb = new Color(0.16f, 0.18f, 0.15f);
            }
            else if (cycleTime < 0.52f)
            {
                // Crimson sunset
                float f = (cycleTime - 0.40f) / 0.12f;
                skyAmb = Color.Lerp(new Color(0.38f, 0.48f, 0.65f), new Color(0.45f, 0.22f, 0.32f), f);
                eqAmb = Color.Lerp(new Color(0.32f, 0.36f, 0.42f), new Color(0.65f, 0.30f, 0.12f), f);
                gndAmb = Color.Lerp(new Color(0.16f, 0.18f, 0.15f), new Color(0.15f, 0.09f, 0.06f), f);
            }
            else if (cycleTime < 0.60f)
            {
                // Dusk twilight
                float f = (cycleTime - 0.52f) / 0.08f;
                skyAmb = Color.Lerp(new Color(0.45f, 0.22f, 0.32f), new Color(0.06f, 0.09f, 0.16f), f);
                eqAmb = Color.Lerp(new Color(0.65f, 0.30f, 0.12f), new Color(0.04f, 0.06f, 0.10f), f);
                gndAmb = Color.Lerp(new Color(0.15f, 0.09f, 0.06f), new Color(0.02f, 0.025f, 0.035f), f);
            }
            else if (cycleTime < 0.92f)
            {
                // Midnight
                skyAmb = new Color(0.06f, 0.09f, 0.16f);
                eqAmb = new Color(0.04f, 0.06f, 0.10f);
                gndAmb = new Color(0.02f, 0.025f, 0.035f);
            }
            else
            {
                // Pre-dawn twilight
                float f = (cycleTime - 0.92f) / 0.08f;
                skyAmb = Color.Lerp(new Color(0.06f, 0.09f, 0.16f), new Color(0.14f, 0.18f, 0.32f), f);
                eqAmb = Color.Lerp(new Color(0.04f, 0.06f, 0.10f), new Color(0.25f, 0.16f, 0.20f), f);
                gndAmb = Color.Lerp(new Color(0.02f, 0.025f, 0.035f), new Color(0.05f, 0.04f, 0.05f), f);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyAmb;
            RenderSettings.ambientEquatorColor = eqAmb;
            RenderSettings.ambientGroundColor = gndAmb;

            if (RenderSettings.fog)
            {
                RenderSettings.fogColor = Color.Lerp(eqAmb, lightCol, 0.22f);
            }
        }

        // ── 4. CONTINUOUS STARS VISIBILITY ─────────────────────────
        // Stars smoothly fade in during dusk (0.46..0.56) and smoothly fade out during dawn (0.94..0.04)
        float starAlpha = 0f;
        if (cycleTime >= 0.46f && cycleTime <= 0.56f)
            starAlpha = Mathf.SmoothStep(0f, 1f, (cycleTime - 0.46f) / 0.10f);
        else if (cycleTime > 0.56f && cycleTime < 0.94f)
            starAlpha = 1f;
        else if (cycleTime >= 0.94f && cycleTime <= 1.0f)
            starAlpha = Mathf.SmoothStep(1f, 0f, (cycleTime - 0.94f) / 0.06f);
        else if (cycleTime < 0.04f)
            starAlpha = Mathf.SmoothStep(1f, 0f, (cycleTime + 0.06f) / 0.10f);
        else
            starAlpha = 0f;

        UpdateStarsVisibility(starAlpha);

        // ── 5. REALISTIC CLOUD CANOPY UPDATE ───────────────────────
        Vector3 activeCelestialPos = (cycleTime <= 0.50f) ? sunPos : moonPos;
        Color activeLightCol = directionalLight != null ? directionalLight.color : Color.white;
        UpdateCloudCanopyLighting(activeCelestialPos, activeLightCol);

        // Center Cloud Canopy and Star Dome directly above the active world/glade
        if (_cloudCanopyObject != null)
            _cloudCanopyObject.transform.position = worldCenter + Vector3.up * cloudCanopyAltitude;

        if (starsSystem != null)
            starsSystem.transform.position = worldCenter + Vector3.up * 500f; // High base: ZERO stars can touch ground!
    }

    private void UpdateCloudCanopyLighting(Vector3 lightPos, Color lightColor)
    {
        if (_cloudCanopyObject == null) return;
        var renderer = _cloudCanopyObject.GetComponent<MeshRenderer>();
        if (renderer == null || renderer.sharedMaterial == null) return;

        if (_cloudCanopyMaterialInstance == null || _cloudCanopyMaterialInstance.shader != renderer.sharedMaterial.shader)
        {
            _cloudCanopyMaterialInstance = new Material(renderer.sharedMaterial);
            renderer.material = _cloudCanopyMaterialInstance;
        }

        Vector3 sunDirection = (lightPos - GetWorldCenter()).normalized;
        _cloudCanopyMaterialInstance.SetVector("_SunDirection", sunDirection);
        _cloudCanopyMaterialInstance.SetFloat("_CloudCoverage", cloudCoverage);
        _cloudCanopyMaterialInstance.SetFloat("_CloudDensity", cloudDensity);
        _cloudCanopyMaterialInstance.SetFloat("_CloudSoftness", cloudSoftness);
        _cloudCanopyMaterialInstance.SetFloat("_Tiling", cloudScale);
        _cloudCanopyMaterialInstance.SetFloat("_WindSpeedX", cloudWindSpeed);
        _cloudCanopyMaterialInstance.SetFloat("_WindSpeedY", cloudWindSpeed * 0.5f);

        // 24-Hour Continuous Realistic Cloud Color Palette
        Color baseCloud;
        Color shadowCloud;

        if (cycleTime < 0.08f)
        {
            // Dawn golden peach
            float factor = cycleTime / 0.08f;
            baseCloud = Color.Lerp(new Color(1f, 0.78f, 0.58f, 0.95f), new Color(1f, 1f, 1f, 0.95f), factor);
            shadowCloud = Color.Lerp(new Color(0.40f, 0.28f, 0.45f, 1f), new Color(0.38f, 0.42f, 0.52f, 1f), factor);
        }
        else if (cycleTime < 0.38f)
        {
            // Pure daytime billowy white clouds
            baseCloud = new Color(1f, 1f, 1f, 0.95f);
            shadowCloud = new Color(0.38f, 0.42f, 0.52f, 1f);
        }
        else if (cycleTime < 0.50f)
        {
            // Sunset crimson & amber clouds
            float factor = (cycleTime - 0.38f) / 0.12f;
            baseCloud = Color.Lerp(new Color(1f, 1f, 1f, 0.95f), new Color(1f, 0.48f, 0.20f, 0.95f), factor);
            shadowCloud = Color.Lerp(new Color(0.38f, 0.42f, 0.52f, 1f), new Color(0.35f, 0.18f, 0.32f, 1f), factor);
        }
        else if (cycleTime < 0.58f)
        {
            // Twilight dusk silhouettes
            float factor = (cycleTime - 0.50f) / 0.08f;
            baseCloud = Color.Lerp(new Color(1f, 0.48f, 0.20f, 0.95f), new Color(0.22f, 0.26f, 0.40f, 0.88f), factor);
            shadowCloud = Color.Lerp(new Color(0.35f, 0.18f, 0.32f, 1f), new Color(0.06f, 0.08f, 0.16f, 1f), factor);
        }
        else if (cycleTime < 0.92f)
        {
            // Moonlit night clouds
            baseCloud = new Color(0.22f, 0.26f, 0.40f, 0.88f);
            shadowCloud = new Color(0.06f, 0.08f, 0.16f, 1f);
        }
        else
        {
            // Pre-dawn transition
            float factor = (cycleTime - 0.92f) / 0.08f;
            baseCloud = Color.Lerp(new Color(0.22f, 0.26f, 0.40f, 0.88f), new Color(1f, 0.78f, 0.58f, 0.95f), factor);
            shadowCloud = Color.Lerp(new Color(0.06f, 0.08f, 0.16f, 1f), new Color(0.40f, 0.28f, 0.45f, 1f), factor);
        }

        _cloudCanopyMaterialInstance.SetColor("_CloudColor", baseCloud);
        _cloudCanopyMaterialInstance.SetColor("_CloudShadowColor", shadowCloud);
        _cloudCanopyMaterialInstance.SetColor("_SunHighlightColor", lightColor * 1.6f);
    }

    private void UpdateStarsVisibility(float alpha)
    {
        if (starsSystem == null) return;

        var renderer = starsSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;

        // Failsafe: if particles were ever lost, firmly place them back on the upper dome
        if (starsSystem.particleCount < 600)
        {
            ConfigureElevatedStarsParticleSystem(starsSystem);
        }

        if (alpha <= 0.005f)
        {
            if (renderer.enabled) renderer.enabled = false;
            return;
        }

        if (!renderer.enabled)
        {
            renderer.enabled = true;
        }

        if (renderer.sharedMaterial != null)
        {
            if (_starMaterialInstance == null || _starMaterialInstance.shader != renderer.sharedMaterial.shader)
            {
                _starMaterialInstance = new Material(renderer.sharedMaterial);
                renderer.material = _starMaterialInstance;
            }

            Color starColor = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            if (_starMaterialInstance.HasProperty("_BaseColor"))
                _starMaterialInstance.SetColor("_BaseColor", starColor);
            if (_starMaterialInstance.HasProperty("_Color"))
                _starMaterialInstance.SetColor("_Color", starColor);
            if (_starMaterialInstance.HasProperty("_EmissionColor"))
                _starMaterialInstance.SetColor("_EmissionColor", new Color(1.4f, 1.4f, 1.8f) * alpha);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Scene Auto-Discovery & References
    // ───────────────────────────────────────────────────────────────

    public void AutoFindSceneReferences()
    {
        if (eastSunPoint == null)
        {
            eastSunPoint = FindTransformMatchingKeywords("east", "sun");
            if (eastSunPoint == null)
                eastSunPoint = FindTransformMatchingKeywords("east");
        }

        if (westSunPoint == null)
        {
            westSunPoint = FindTransformMatchingKeywords("west", "sun");
            if (westSunPoint == null)
                westSunPoint = FindTransformMatchingKeywords("west");
        }

        if (directionalLight == null)
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    directionalLight = l;
                    break;
                }
            }
        }

#if UNITY_EDITOR
        if (sunPrefab == null)
            sunPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Sun.prefab");
        if (moonPrefab == null)
            moonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Moon.prefab");
#endif
    }

    private Transform FindTransformMatchingKeywords(params string[] keywords)
    {
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in all)
        {
            string n = t.name.ToLowerInvariant();
            bool match = true;
            foreach (var kw in keywords)
            {
                if (!n.Contains(kw.ToLowerInvariant()))
                {
                    match = false;
                    break;
                }
            }
            if (match) return t;
        }
        return null;
    }

    // ───────────────────────────────────────────────────────────────
    //  Instances & Radiant Glow Halos Setup
    // ───────────────────────────────────────────────────────────────

    private void EnsureCelestialInstances()
    {
        if (sunInstance == null)
        {
            var existingSun = GameObject.Find("Sun_Celestial");
            if (existingSun != null)
            {
                sunInstance = existingSun.transform;
            }
            else if (sunPrefab != null)
            {
                GameObject go = Instantiate(sunPrefab, transform);
                go.name = "Sun_Celestial";
                StripColliders(go);
                sunInstance = go.transform;
            }
        }

        if (sunInstance != null)
        {
            DisableShadowCasting(sunInstance.gameObject);
            ApplyEmissionGlow(sunInstance.gameObject, sunGlowColor);
        }

        if (moonInstance == null)
        {
            var existingMoon = GameObject.Find("Moon_Celestial");
            if (existingMoon != null)
            {
                moonInstance = existingMoon.transform;
            }
            else if (moonPrefab != null)
            {
                GameObject go = Instantiate(moonPrefab, transform);
                go.name = "Moon_Celestial";
                StripColliders(go);
                moonInstance = go.transform;
            }
        }

        if (moonInstance != null)
        {
            DisableShadowCasting(moonInstance.gameObject);
            ApplyEmissionGlow(moonInstance.gameObject, moonGlowColor);
        }
    }

    private static void DisableShadowCasting(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    private void ApplyEmissionGlow(GameObject go, Color emission)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r.name.Contains("Corona")) continue;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor"))
                    m.SetColor("_EmissionColor", emission);
            }
        }
    }

    private void EnsureGlowHalos()
    {
        if (!enableCelestialGlow) return;

        if (_sunCorona == null)
        {
            var existing = transform.Find("Sun_CoronaGlow");
            if (existing != null)
            {
                _sunCorona = existing;
            }
            else
            {
                _sunCorona = CreateCoronaQuad("Sun_CoronaGlow", new Color(1f, 0.85f, 0.45f, 0.90f));
            }
        }

        if (_moonCorona == null)
        {
            var existing = transform.Find("Moon_CoronaGlow");
            if (existing != null)
            {
                _moonCorona = existing;
            }
            else
            {
                _moonCorona = CreateCoronaQuad("Moon_CoronaGlow", new Color(0.55f, 0.80f, 1f, 0.82f));
            }
        }
    }

    private Transform CreateCoronaQuad(string name, Color glowColor)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(transform, false);

        var col = quad.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        var renderer = quad.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Material baseMat = null;
#if UNITY_EDITOR
        baseMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_ParticleAdditive.mat");
#endif
        if (baseMat == null) baseMat = Resources.Load<Material>("VFX/M_ParticleAdditive");

        if (baseMat != null)
        {
            Material matInst = new Material(baseMat);
            if (matInst.HasProperty("_BaseColor")) matInst.SetColor("_BaseColor", glowColor);
            if (matInst.HasProperty("_Color")) matInst.SetColor("_Color", glowColor);
            renderer.material = matInst;
        }

        return quad.transform;
    }

    private static void StripColliders(GameObject go)
    {
        var cols = go.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            DestroyImmediate(c);
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Realistic Sky Cloud Canopy & Elevated Stars Generation
    // ───────────────────────────────────────────────────────────────

    public void EnsureCloudsAndStars() => EnsureRealisticSkyVisuals();

        public static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.Undo.DestroyObjectImmediate(obj);
#else
            DestroyImmediate(obj);
#endif
        }
    }

    public void EnsureRealisticSkyVisuals()
    {
        // 1. Purge all legacy particle cloud objects so ZERO smoke or fog ever touches the terrain!
        CleanUpLegacyArtifacts();

        // 2. Ensure Realistic High-Altitude Cloud Canopy
        if (enableCloudCanopy)
        {
            if (_cloudCanopyObject == null)
            {
                var existing = transform.Find("Realistic_Cloud_Canopy");
                if (existing != null)
                {
                    _cloudCanopyObject = existing.gameObject;
                }
                else
                {
                    _cloudCanopyObject = CreateRealisticCloudCanopyMesh();
                }
            }

            if (_cloudCanopyObject != null)
            {
                _cloudCanopyObject.SetActive(true);

                var mf = _cloudCanopyObject.GetComponent<MeshFilter>();
                if (mf == null) mf = _cloudCanopyObject.AddComponent<MeshFilter>();
                if (mf.sharedMesh == null) mf.sharedMesh = GenerateSkyDomeMesh(cloudCanopyRadius, 28, 28);

                var mr = _cloudCanopyObject.GetComponent<MeshRenderer>();
                if (mr == null) mr = _cloudCanopyObject.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;

                if (mr.sharedMaterial == null)
                {
                    Material cloudMat = null;
#if UNITY_EDITOR
                    cloudMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_RealisticClouds.mat");
#endif
                    if (cloudMat == null) cloudMat = Resources.Load<Material>("VFX/M_RealisticClouds");
                    if (cloudMat != null) mr.sharedMaterial = cloudMat;
                }
            }
        }

        // 3. Ensure Elevated Star Dome (Fixed in sky dome, ZERO ground particles!)
        if (starsSystem == null)
        {
            var existing = transform.Find("Stars_Layer");
            if (existing != null)
            {
                starsSystem = existing.GetComponent<ParticleSystem>();
            }
            else
            {
                GameObject starGo = new GameObject("Stars_Layer");
                starGo.transform.SetParent(transform, false);
                starsSystem = starGo.AddComponent<ParticleSystem>();
            }
        }

        if (starsSystem != null)
        {
            ConfigureElevatedStarsParticleSystem(starsSystem);
        }
    }

    public void CleanUpLegacyArtifacts()
    {
        try
        {
            // 1. Search and destroy any legacy particle cloud/fog children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                string n = child.name;
                if (n.Contains("Cumulus") || n.Contains("Cirrus") || n == "Clouds_Layer")
                {
                    SafeDestroy(child.gameObject);
                }
            }

            // 2. Search scene-wide for any unparented legacy cloud particle systems
            var allPs = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (var ps in allPs)
            {
                if (ps == null) continue;
                string n = ps.gameObject.name;
                if (n.Contains("Cumulus") || n.Contains("Cirrus") || n == "Clouds_Layer")
                {
                    SafeDestroy(ps.gameObject);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DayNightController] CleanUpLegacyArtifacts notice: " + ex.Message);
        }
    }

    private GameObject CreateRealisticCloudCanopyMesh()
    {
        GameObject canopy = new GameObject("Realistic_Cloud_Canopy");
        canopy.transform.SetParent(transform, false);

        var filter = canopy.AddComponent<MeshFilter>();
        var renderer = canopy.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // Generate curved sky canopy dome mesh
        filter.sharedMesh = GenerateSkyDomeMesh(cloudCanopyRadius, 28, 28);

        // Assign Realistic Cloud Material
        Material cloudMat = null;
#if UNITY_EDITOR
        cloudMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_RealisticClouds.mat");
#endif
        if (cloudMat == null) cloudMat = Resources.Load<Material>("VFX/M_RealisticClouds");
        if (cloudMat != null) renderer.sharedMaterial = cloudMat;

        return canopy;
    }

    private Mesh GenerateSkyDomeMesh(float radius, int rings, int sectors)
    {
        Mesh mesh = new Mesh();
        mesh.name = "RealisticCloudDome";

        int vertexCount = (rings + 1) * (sectors + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] triangles = new int[rings * sectors * 6];

        float maxPhi = 76f * Mathf.Deg2Rad;
        float ringStep = maxPhi / rings;
        float sectorStep = (Mathf.PI * 2f) / sectors;

        int vertIdx = 0;
        for (int r = 0; r <= rings; r++)
        {
            float phi = r * ringStep;
            float cosPhi = Mathf.Cos(phi);
            float sinPhi = Mathf.Sin(phi);

            for (int s = 0; s <= sectors; s++)
            {
                float theta = s * sectorStep;
                float cosTheta = Mathf.Cos(theta);
                float sinTheta = Mathf.Sin(theta);

                // Hemispherical canopy arching over the sky
                float x = sinPhi * cosTheta * radius;
                float z = sinPhi * sinTheta * radius;
                float y = -(1f - cosPhi) * (radius * 0.32f);

                vertices[vertIdx] = new Vector3(x, y, z);
                normals[vertIdx] = new Vector3(-sinPhi * cosTheta, -cosPhi, -sinPhi * sinTheta); // Point down towards ground
                
                // Planar UV projection across the sky dome: ZERO POLE PINCHING, completely uniform cloud scale!
                float u = (x / (radius * 1.9f)) + 0.5f;
                float v = (z / (radius * 1.9f)) + 0.5f;
                uvs[vertIdx] = new Vector2(u, v);
                vertIdx++;
            }
        }

        int tri = 0;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < sectors; s++)
            {
                int i0 = r * (sectors + 1) + s;
                int i1 = i0 + 1;
                int i2 = (r + 1) * (sectors + 1) + s;
                int i3 = i2 + 1;

                triangles[tri++] = i0;
                triangles[tri++] = i1;
                triangles[tri++] = i2;

                triangles[tri++] = i1;
                triangles[tri++] = i3;
                triangles[tri++] = i2;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private void ConfigureElevatedStarsParticleSystem(ParticleSystem ps)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) renderer = ps.gameObject.AddComponent<ParticleSystemRenderer>();

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Material starMat = null;
#if UNITY_EDITOR
        starMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_ParticleAdditive.mat");
#endif
        if (starMat == null) starMat = Resources.Load<Material>("VFX/M_ParticleAdditive");
        if (starMat != null) renderer.sharedMaterial = starMat;

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = Mathf.Infinity;
        main.startSpeed = 0f;
        main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        ps.transform.localPosition = Vector3.zero;

        // Clear any old particles that were emitted at (0,0,0) on the ground!
        ps.Clear();

        // Populate 1200 stars placed strictly in the high celestial dome
        int count = 1200;
        ParticleSystem.Particle[] stars = new ParticleSystem.Particle[count];
        var rng = new System.Random(42);

        float radius = starDomeRadius; // 4200m
        for (int i = 0; i < count; i++)
        {
            float azimuth = (float)(rng.NextDouble() * Math.PI * 2.0);
            // Elevation: 15° to 88° strictly above the horizon!
            float minElev = 0.22f;
            float maxElev = 0.98f;
            float elev = minElev + (float)(rng.NextDouble() * (maxElev - minElev));

            float cosElev = Mathf.Cos(elev * Mathf.PI * 0.5f);
            float sinElev = Mathf.Sin(elev * Mathf.PI * 0.5f);

            float x = radius * cosElev * Mathf.Cos(azimuth);
            float y = radius * sinElev; // Strictly positive high sky altitude (1200m to 4200m!)
            float z = radius * cosElev * Mathf.Sin(azimuth);

            stars[i].position = new Vector3(x, y, z);
            stars[i].startSize = 24f + (float)(rng.NextDouble() * 32f); // 24m to 56m glittering stars
            stars[i].startLifetime = float.MaxValue;
            stars[i].remainingLifetime = float.MaxValue;
            stars[i].velocity = Vector3.zero;

            float temp = (float)rng.NextDouble();
            Color starCol;
            if (temp < 0.70f)
                starCol = new Color(0.95f, 0.98f, 1.0f, 1f); // Diamond white
            else if (temp < 0.88f)
                starCol = new Color(1.0f, 0.92f, 0.80f, 1f); // Warm stellar gold
            else
                starCol = new Color(0.80f, 0.90f, 1.0f, 1f); // Celestial blue

            stars[i].startColor = starCol;
        }

        ps.SetParticles(stars, count);

        if (IsDay)
        {
            renderer.enabled = false;
        }
        else
        {
            renderer.enabled = true;
        }
    }

    // ───────────────────────────────────────────────────────────────
    //  Gizmos (Grand Celestial Arc Visualization in Scene View)
    // ───────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        GetHorizonEndPoints(out Vector3 east, out Vector3 west);
        Vector3 worldCenter = GetWorldCenter();

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(east, 35f);
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireSphere(west, 35f);

        Gizmos.color = (cycleTime <= 0.5f) ? new Color(1f, 0.75f, 0.2f, 0.85f) : new Color(0.5f, 0.7f, 1f, 0.85f);
        const int segments = 50;
        Vector3 prev = CalculateSkyArcPosition(0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 curr = CalculateSkyArcPosition(t);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }

        float p = (cycleTime <= 0.5f) ? (cycleTime / 0.5f) : ((cycleTime - 0.5f) / 0.5f);
        Vector3 currentPos = CalculateSkyArcPosition(p);
        Gizmos.color = (cycleTime <= 0.5f) ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(currentPos, 45f);
        Gizmos.DrawLine(currentPos, worldCenter);
    }
}
