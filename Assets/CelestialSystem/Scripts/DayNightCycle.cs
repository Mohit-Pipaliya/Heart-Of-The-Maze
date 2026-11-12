using System;
using UnityEngine;
using UnityEngine.Events;

namespace CelestialSystem
{
    /// <summary>
    /// Master time-keeper for the day/night cycle.
    /// Independent day and night durations with piecewise time remapping.
    /// All celestial math is delegated to CelestialMath (pure static).
    /// No GC alloc per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayNightCycle : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Enums
        // ───────────────────────────────────────────────────────────────
        public enum StartPhase { Day, Night }
        public enum MoonPhaseMode { Auto, Manual }
        public enum MoonTrackingMode { NightOrbit, AstronomicalRealtime }

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Time
        // ───────────────────────────────────────────────────────────────
        [Header("── Time Settings ──────────────────────────")]
        [Tooltip("Real-world seconds for one full day (sun: sunrise → sunset).")]
        [Min(1f)] public float dayDuration  = 300f;

        [Tooltip("Real-world seconds for one full night (sun: sunset → next sunrise).")]
        [Min(1f)] public float nightDuration = 120f;

        [Tooltip("Transition blend window at dawn (seconds). Used by listeners for effect fading.")]
        [Min(0f)] public float dawnTransition = 10f;

        [Tooltip("Transition blend window at dusk (seconds). Used by listeners for effect fading.")]
        [Min(0f)] public float duskTransition = 10f;

        [Tooltip("Normalised start time (0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset).")]
        [Range(0f, 1f)] public float startTime = 0.25f;

        [Tooltip("Whether the cycle starts in Day or Night.")]
        public StartPhase startPhase = StartPhase.Day;

        [Tooltip("Global time multiplier. 1 = real-time, 0 = paused.")]
        [Range(0f, 100f)] public float timeScale = 1f;

        [Tooltip("Pause the cycle (time freezes).")]
        public bool pause = false;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Calendar
        // ───────────────────────────────────────────────────────────────
        [Header("── Calendar & Seasons ───────────────────────")]
        [Tooltip("Day of year (1-365). Affects solar declination and moon phase.")]
        [Range(1, 365)] public int dayOfYear = 80;   // Near spring equinox for a picturesque 48° sun arc

        [Tooltip("Automatically advance the day-of-year counter after each full day.")]
        public bool autoAdvanceDays = true;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Astronomy
        // ───────────────────────────────────────────────────────────────
        [Header("── Astronomy ───────────────────────────────")]
        [Tooltip("Observer latitude in degrees. 42° gives a dramatic, visible 48° sun arc across the player's view.")]
        [Range(-89.9f, 89.9f)] public float latitude = 42f;

        [Tooltip("World-space yaw of the East compass direction (degrees). " +
                 "Default 0 means +X axis is East, +Z axis is North.")]
        [Range(0f, 360f)] public float eastDirectionYaw = 0f;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Moon
        // ───────────────────────────────────────────────────────────────
        [Header("── Moon ────────────────────────────────────")]
        [Tooltip("NightOrbit: Guarantees Moon rises at sunset and stays up all night (Standard AAA Game). " +
                 "AstronomicalRealtime: Pure astronomical simulation where moon rises at different hours.")]
        public MoonTrackingMode moonTrackingMode = MoonTrackingMode.NightOrbit;

        [Tooltip("Auto: moon phase advances over synodic cycle. Manual: set phase below.")]
        public MoonPhaseMode moonPhaseMode = MoonPhaseMode.Auto;

        [Tooltip("Length of the synodic (lunar) month in in-game days. Default 29.53.")]
        [Range(1f, 60f)] public float synodicPeriodDays = 29.53f;

        [Tooltip("Current day within the synodic month (0 = new moon, ~14.76 = full moon). " +
                 "Defaults to 14.76 (Full Moon) for breathtaking night illumination.")]
        [Range(0f, 29.53f)] public float moonCycleDay = 14.76f;

        [Tooltip("Orbital inclination of moon relative to ecliptic (degrees). ~5° is realistic.")]
        [Range(0f, 10f)] public float moonInclination = 5f;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Events
        // ───────────────────────────────────────────────────────────────
        [Header("── Events ──────────────────────────────────")]
        [Tooltip("Fires once when the sun crosses the horizon at dawn.")]
        public UnityEvent OnSunrise = new UnityEvent();

        [Tooltip("Fires once when the sun crosses the horizon at dusk.")]
        public UnityEvent OnSunset = new UnityEvent();

        [Tooltip("Fires once at the start of the Day phase.")]
        public UnityEvent OnDayStart = new UnityEvent();

        [Tooltip("Fires once at the start of the Night phase.")]
        public UnityEvent OnNightStart = new UnityEvent();

        // ───────────────────────────────────────────────────────────────
        //  Read-only public state (no allocation — value types)
        // ───────────────────────────────────────────────────────────────

        /// <summary>0 (midnight) → 1 (next midnight), piecewise-remapped.</summary>
        public float CurrentTimeNormalized  { get; private set; }

        /// <summary>True when the sun is above the horizon.</summary>
        public bool  IsDay                  { get; private set; }

        /// <summary>True when the sun is below the horizon.</summary>
        public bool  IsNight                => !IsDay;

        public float SunAzimuth             { get; private set; }
        public float SunAltitude            { get; private set; }
        public float MoonAzimuth            { get; private set; }
        public float MoonAltitude           { get; private set; }

        /// <summary>Phase angle in degrees: 0=new, 90=first quarter, 180=full, 270=last quarter.</summary>
        public float MoonPhaseAngle         { get; private set; }

        /// <summary>Current world-space direction toward the sun (unit vector).</summary>
        public Vector3 SunWorldDirection    { get; private set; }

        /// <summary>Current world-space direction toward the moon (unit vector).</summary>
        public Vector3 MoonWorldDirection   { get; private set; }

        /// <summary>Rotation quaternion for the star dome.</summary>
        public Quaternion StarDomeRotation  { get; private set; }

        /// <summary>0-1 progress within current phase (0=phase start, 1=phase end).</summary>
        public float PhaseProgress          { get; private set; }

        // ───────────────────────────────────────────────────────────────
        //  Private state
        // ───────────────────────────────────────────────────────────────
        // Phase timer: seconds elapsed within the current phase
        private float _phaseTimer;
        private bool  _isDay;
        private bool  _eventSunriseFired;
        private bool  _eventSunsetFired;
        private bool  _eventDayStartFired;
        private bool  _eventNightStartFired;

        // Rolling day counter for auto moon phase
        private float _totalDaysElapsed;

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────
        private void Awake()
        {
            InitFromStartTime();
        }

        private void Start()
        {
            RecalcCelestials();
        }

        private void Update()
        {
            if (pause || timeScale <= 0f) return;

            float dt = Time.deltaTime * timeScale;
            AdvanceTime(dt);
            RecalcCelestials();
        }

        // ───────────────────────────────────────────────────────────────
        //  Initialisation from Inspector startTime
        // ───────────────────────────────────────────────────────────────
        public void InitFromStartTime()
        {
            // Treat normalised 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset
            // Day phase: 0.25 → 0.75 (duration = dayDuration)
            // Night phase: 0.75 → 1.25 (wraps, duration = nightDuration)

            ResetEventFlags();

            if (startPhase == StartPhase.Day)
            {
                _isDay = true;
                // Map startTime within day range 0.25..0.75
                float dayFraction = Mathf.Clamp01((startTime - 0.25f) / 0.5f);
                _phaseTimer = dayFraction * dayDuration;
            }
            else
            {
                _isDay = false;
                // Map startTime within night range 0.75..1.25 (wrapping)
                float nightNorm = startTime >= 0.75f ? (startTime - 0.75f) : (startTime + 0.25f);
                float nightFraction = Mathf.Clamp01(nightNorm / 0.5f);
                _phaseTimer = nightFraction * nightDuration;
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  Time advancement (piecewise remapping)
        // ───────────────────────────────────────────────────────────────
        private void AdvanceTime(float dt)
        {
            float phaseDuration = _isDay ? dayDuration : nightDuration;
            _phaseTimer += dt;

            if (_phaseTimer >= phaseDuration)
            {
                float overflow = _phaseTimer - phaseDuration;
                // Switch phase
                _isDay = !_isDay;
                _phaseTimer = Mathf.Clamp(overflow, 0f, _isDay ? dayDuration : nightDuration);
                ResetEventFlags();

                if (_isDay && autoAdvanceDays)
                {
                    dayOfYear = (dayOfYear % 365) + 1;
                    _totalDaysElapsed += 1f;
                }
                if (!_isDay)
                {
                    _totalDaysElapsed += 0f; // Night elapsed tracked by daytime
                }
            }

            PhaseProgress = _phaseTimer / (_isDay ? dayDuration : nightDuration);
            PhaseProgress = Mathf.Clamp01(PhaseProgress);

            // Build the global normalised time (0=midnight, 0.25=sunrise, 0.5=noon, 0.75=sunset)
            if (_isDay)
            {
                // 0.25 .. 0.75
                CurrentTimeNormalized = 0.25f + PhaseProgress * 0.5f;
            }
            else
            {
                // 0.75 .. 1.25 (mod 1)
                float rawNight = 0.75f + PhaseProgress * 0.5f;
                CurrentTimeNormalized = rawNight >= 1f ? rawNight - 1f : rawNight;
            }

            IsDay = _isDay;
        }

        // ───────────────────────────────────────────────────────────────
        //  Celestial recalculation (called after time advance)
        // ───────────────────────────────────────────────────────────────
        private void RecalcCelestials()
        {
            // Solar hour angle: 0 at noon, -180 at midnight(day before), +180 at midnight(day after)
            float hourAngle = CelestialMath.TimeToHourAngle(CurrentTimeNormalized);

            // Sun
            SunWorldDirection = CelestialMath.SunDirection(latitude, dayOfYear, hourAngle, eastDirectionYaw);
            CelestialMath.DirectionToAzimuthAltitude(SunWorldDirection, eastDirectionYaw,
                out float sa, out float salt);
            SunAzimuth   = sa;
            SunAltitude  = salt;

            // Moon phase
            if (moonPhaseMode == MoonPhaseMode.Auto)
            {
                // Phase angle increases over synodic period
                float moonDayFraction = (_totalDaysElapsed % synodicPeriodDays + moonCycleDay) % synodicPeriodDays;
                MoonPhaseAngle = (moonDayFraction / synodicPeriodDays) * 360f;
            }
            else
            {
                // Manual: moonCycleDay drives phase 0..360
                MoonPhaseAngle = (moonCycleDay / synodicPeriodDays) * 360f;
            }

            // Moon direction
            if (moonTrackingMode == MoonTrackingMode.NightOrbit)
            {
                // In NightOrbit mode, moon is opposed to the sun (rises in East at sunset, climbs to zenith at midnight, sets in West at sunrise)
                MoonWorldDirection = CelestialMath.MoonDirection(
                    latitude, dayOfYear, hourAngle, 180f, moonInclination, eastDirectionYaw);
            }
            else
            {
                MoonWorldDirection = CelestialMath.MoonDirection(
                    latitude, dayOfYear, hourAngle, MoonPhaseAngle, moonInclination, eastDirectionYaw);
            }
            CelestialMath.DirectionToAzimuthAltitude(MoonWorldDirection, eastDirectionYaw,
                out float ma, out float malt);
            MoonAzimuth  = ma;
            MoonAltitude = malt;

            // Star dome rotation (full rotation per total day+night cycle)
            StarDomeRotation = CelestialMath.StarDomeRotation(CurrentTimeNormalized, latitude, eastDirectionYaw);

            // Fire events
            FireEvents();
        }

        // ───────────────────────────────────────────────────────────────
        //  Event firing (each fires exactly once per cycle)
        // ───────────────────────────────────────────────────────────────
        private void FireEvents()
        {
            bool sunAboveHorizon = SunAltitude >= 0f;

            if (_isDay)
            {
                if (!_eventDayStartFired)
                {
                    _eventDayStartFired = true;
                    OnDayStart.Invoke();
                }
                if (!_eventSunriseFired && SunAltitude >= 0f)
                {
                    _eventSunriseFired = true;
                    OnSunrise.Invoke();
                }
            }
            else
            {
                if (!_eventNightStartFired)
                {
                    _eventNightStartFired = true;
                    OnNightStart.Invoke();
                }
                if (!_eventSunsetFired && SunAltitude < 0f)
                {
                    _eventSunsetFired = true;
                    OnSunset.Invoke();
                }
            }
        }

        private void ResetEventFlags()
        {
            _eventSunriseFired    = false;
            _eventSunsetFired     = false;
            _eventDayStartFired   = false;
            _eventNightStartFired = false;
        }

        // ───────────────────────────────────────────────────────────────
        //  Public API (used by Editor and other systems)
        // ───────────────────────────────────────────────────────────────

        /// <summary>Directly set the normalised time (editor scrubbing).</summary>
        public void SetNormalizedTime(float t)
        {
            t = t % 1f;
            if (t < 0f) t += 1f;

            // Determine phase from normalised time
            // Day: 0.25 .. 0.75  |  Night: 0.75..1 and 0..0.25
            bool willBeDay = (t >= 0.25f && t < 0.75f);

            if (willBeDay != _isDay)
            {
                _isDay = willBeDay;
                ResetEventFlags();
            }

            if (_isDay)
            {
                PhaseProgress = Mathf.Clamp01((t - 0.25f) / 0.5f);
                _phaseTimer   = PhaseProgress * dayDuration;
            }
            else
            {
                float nightNorm = t >= 0.75f ? (t - 0.75f) : (t + 0.25f);
                PhaseProgress = Mathf.Clamp01(nightNorm / 0.5f);
                _phaseTimer   = PhaseProgress * nightDuration;
            }

            CurrentTimeNormalized = t;
            IsDay = _isDay;
            RecalcCelestials();
        }

        /// <summary>Jump to sunrise (normalised time 0.25).</summary>
        public void JumpToSunrise() => SetNormalizedTime(0.25f);

        /// <summary>Jump to solar noon (normalised time 0.5).</summary>
        public void JumpToNoon() => SetNormalizedTime(0.5f);

        /// <summary>Jump to sunset (normalised time 0.75).</summary>
        public void JumpToSunset() => SetNormalizedTime(0.75f);

        /// <summary>Jump to midnight (normalised time 0.0).</summary>
        public void JumpToMidnight() => SetNormalizedTime(0.0f);

        // ───────────────────────────────────────────────────────────────
        //  Scene gizmo
        // ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Sun direction line (yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + SunWorldDirection * 5f);
            UnityEditor.Handles.Label(transform.position + SunWorldDirection * 5.2f,
                $"SUN\nAz:{SunAzimuth:F1}° Alt:{SunAltitude:F1}°");

            // Moon direction line (cyan)
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + MoonWorldDirection * 5f);
            UnityEditor.Handles.Label(transform.position + MoonWorldDirection * 5.2f,
                $"MOON\nAz:{MoonAzimuth:F1}° Alt:{MoonAltitude:F1}°\nPhase:{MoonPhaseAngle:F1}°");

            // Compass rose
            DrawCompassGizmo();
        }

        private void DrawCompassGizmo()
        {
            float yaw = eastDirectionYaw * Mathf.Deg2Rad;
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            Vector3 east  = new Vector3( cy, 0f,  sy);
            Vector3 west  = new Vector3(-cy, 0f, -sy);
            Vector3 north = new Vector3(-sy, 0f,  cy);
            Vector3 south = new Vector3( sy, 0f, -cy);
            Vector3 o = transform.position + Vector3.up * 0.1f;

            Gizmos.color = Color.red;   Gizmos.DrawLine(o, o + east  * 3f);
            Gizmos.color = Color.blue;  Gizmos.DrawLine(o, o + north * 3f);
            Gizmos.color = Color.gray;  Gizmos.DrawLine(o, o + west  * 2f);
            Gizmos.color = Color.gray;  Gizmos.DrawLine(o, o + south * 2f);

            UnityEditor.Handles.Label(o + east  * 3.2f, "E");
            UnityEditor.Handles.Label(o + west  * 2.2f, "W");
            UnityEditor.Handles.Label(o + north * 3.2f, "N");
            UnityEditor.Handles.Label(o + south * 2.2f, "S");
        }
#endif
    }
}

