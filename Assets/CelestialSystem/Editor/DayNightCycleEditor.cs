using UnityEngine;
using UnityEditor;

namespace CelestialSystem.Editor
{
    /// <summary>
    /// Custom inspector for DayNightCycle.
    /// Provides:
    ///   • "Scrub Time" slider (works in Edit mode and Play mode)
    ///   • Jump buttons: Sunrise / Noon / Sunset / Midnight
    ///   • Live readout of sun + moon azimuth / altitude
    ///   • Compass gizmo refresh trigger
    /// </summary>
    [CustomEditor(typeof(DayNightCycle))]
    public sealed class DayNightCycleEditor : UnityEditor.Editor
    {
        // ───────────────────────────────────────────────────────────────
        //  Scrub state (edit-mode only — not serialized)
        // ───────────────────────────────────────────────────────────────
        private float _scrubTime = 0.25f;   // defaults to sunrise
        private bool  _scrubbing = false;

        // ───────────────────────────────────────────────────────────────
        //  OnInspectorGUI
        // ───────────────────────────────────────────────────────────────
        public override void OnInspectorGUI()
        {
            DayNightCycle cycle = (DayNightCycle)target;

            // Draw all the default fields
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("── Editor Controls ──────────────────────────", EditorStyles.boldLabel);

            // ── Scrub Time ──────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Scrub Time (works in Edit & Play mode)", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            float newScrub = EditorGUILayout.Slider("Normalised Time", _scrubTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                _scrubTime = newScrub;
                _scrubbing = true;
                ApplyScrub(cycle, _scrubTime);
            }

            // Show current time if playing (keep slider in sync)
            if (Application.isPlaying)
            {
                _scrubTime = cycle.CurrentTimeNormalized;
                float displayHour = _scrubTime * 24f;
                EditorGUILayout.LabelField($"Live time: {displayHour:F1}h", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // ── Jump buttons ────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("☀ Sunrise"))
            {
                _scrubTime = 0.25f;
                ApplyScrub(cycle, _scrubTime);
            }
            if (GUILayout.Button("⬆ Noon"))
            {
                _scrubTime = 0.5f;
                ApplyScrub(cycle, _scrubTime);
            }
            if (GUILayout.Button("☽ Sunset"))
            {
                _scrubTime = 0.75f;
                ApplyScrub(cycle, _scrubTime);
            }
            if (GUILayout.Button("★ Midnight"))
            {
                _scrubTime = 0f;
                ApplyScrub(cycle, _scrubTime);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // ── Live Readout ────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("── Live Celestial Readout ──────────", EditorStyles.boldLabel);

            // Force recalc in edit mode so readout reflects scrub
            if (!Application.isPlaying)
                InvokePrivateRecalc(cycle);

            EditorGUILayout.LabelField($"🕐 Time Norm:  {cycle.CurrentTimeNormalized:F4}  " +
                                       $"({(cycle.IsDay ? "DAY" : "NIGHT")})", EditorStyles.miniLabel);
            EditorGUILayout.Space(2);

            EditorGUILayout.LabelField("☀  Sun", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"   Azimuth:  {cycle.SunAzimuth:F2}°   |   Altitude: {cycle.SunAltitude:F2}°",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"   Direction: {cycle.SunWorldDirection:F3}", EditorStyles.miniLabel);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("☽  Moon", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"   Azimuth:  {cycle.MoonAzimuth:F2}°   |   Altitude: {cycle.MoonAltitude:F2}°",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"   Phase:    {cycle.MoonPhaseAngle:F1}°  ({MoonPhaseName(cycle.MoonPhaseAngle)})",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"   Illum:    {CelestialMath.MoonIlluminationFraction(cycle.MoonPhaseAngle) * 100f:F0}%",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            // Repaint to keep readout live during scrub
            if (_scrubbing || Application.isPlaying)
            {
                Repaint();
                _scrubbing = false;
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  Scene GUI (compass overlay in scene view)
        // ───────────────────────────────────────────────────────────────
        private void OnSceneGUI()
        {
            DayNightCycle cycle = (DayNightCycle)target;
            if (cycle == null) return;

            // Force recalc when scrubbing in edit mode
            if (!Application.isPlaying)
                InvokePrivateRecalc(cycle);

            // The DayNightCycle.OnDrawGizmosSelected already draws the compass.
            // Add a screen-space overlay label for quick reference.
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 220, 120));
            GUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel) { wordWrap = false };
            GUILayout.Label($"☀ Sun  Az:{cycle.SunAzimuth:F0}°  Alt:{cycle.SunAltitude:F0}°", style);
            GUILayout.Label($"☽ Moon Az:{cycle.MoonAzimuth:F0}°  Alt:{cycle.MoonAltitude:F0}°", style);
            GUILayout.Label($"Phase: {cycle.MoonPhaseAngle:F0}° ({MoonPhaseName(cycle.MoonPhaseAngle)})", style);
            GUILayout.Label($"Time: {cycle.CurrentTimeNormalized * 24f:F1}h  {(cycle.IsDay ? "DAY" : "NIGHT")}", style);

            GUILayout.EndVertical();
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        // ───────────────────────────────────────────────────────────────
        //  Helpers
        // ───────────────────────────────────────────────────────────────
        private static void ApplyScrub(DayNightCycle cycle, float t)
        {
            Undo.RecordObject(cycle, "Scrub DayNight Time");
            cycle.SetNormalizedTime(t);
            EditorUtility.SetDirty(cycle);
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Calls SetNormalizedTime with the current value to force a recalculation in edit mode
        /// without changing the time — just refreshes derived properties.
        /// </summary>
        private static void InvokePrivateRecalc(DayNightCycle cycle)
        {
            // We call the public API which also calls the private RecalcCelestials
            cycle.SetNormalizedTime(cycle.CurrentTimeNormalized);
        }

        private static string MoonPhaseName(float phaseAngle)
        {
            float p = phaseAngle % 360f;
            if (p < 0) p += 360f;
            if (p < 11.25f || p >= 348.75f) return "New Moon";
            if (p < 56.25f)   return "Waxing Crescent";
            if (p < 101.25f)  return "First Quarter";
            if (p < 146.25f)  return "Waxing Gibbous";
            if (p < 191.25f)  return "Full Moon";
            if (p < 236.25f)  return "Waning Gibbous";
            if (p < 281.25f)  return "Last Quarter";
            if (p < 326.25f)  return "Waning Crescent";
            return "New Moon";
        }
    }
}

