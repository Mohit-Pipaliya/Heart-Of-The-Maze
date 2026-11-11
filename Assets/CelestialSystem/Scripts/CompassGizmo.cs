using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Scene-view compass gizmo component.
    /// Shows E/W/N/S labels, current sun/moon direction lines, and celestial pole axis.
    /// Attach to the same GameObject as DayNightCycle for auto-wiring.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DayNightCycle))]
    public sealed class CompassGizmo : MonoBehaviour
    {
        [Header("── Compass Gizmo Settings ────────────────────")]
        [Tooltip("Length of compass arms in scene-view units.")]
        [Range(1f, 20f)] public float armLength = 5f;

        [Tooltip("Length of sun/moon direction lines.")]
        [Range(1f, 20f)] public float directionLineLength = 8f;

        [Tooltip("Show the celestial pole axis (altitude = latitude).")]
        public bool showCelestialPole = true;

        // Cached reference — no allocation in gizmo draw
        private DayNightCycle _cycle;

        private void Awake()
        {
            _cycle = GetComponent<DayNightCycle>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_cycle == null) _cycle = GetComponent<DayNightCycle>();
            if (_cycle == null) return;

            Vector3 origin = transform.position + Vector3.up * 0.05f;
            float yaw = _cycle.eastDirectionYaw * Mathf.Deg2Rad;
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);

            Vector3 east  = new Vector3( cy, 0f,  sy);
            Vector3 west  = new Vector3(-cy, 0f, -sy);
            Vector3 north = new Vector3(-sy, 0f,  cy);
            Vector3 south = new Vector3( sy, 0f, -cy);

            // Compass arms
            Gizmos.color = new Color(1f, 0.3f, 0.3f);
            Gizmos.DrawLine(origin, origin + east  * armLength);
            Gizmos.color = new Color(0.3f, 0.3f, 1f);
            Gizmos.DrawLine(origin, origin + north * armLength);
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(origin, origin + west  * (armLength * 0.7f));
            Gizmos.DrawLine(origin, origin + south * (armLength * 0.7f));

            // Labels
            UnityEditor.Handles.Label(origin + east  * (armLength + 0.3f), "E");
            UnityEditor.Handles.Label(origin + west  * (armLength * 0.7f + 0.3f), "W");
            UnityEditor.Handles.Label(origin + north * (armLength + 0.3f), "N");
            UnityEditor.Handles.Label(origin + south * (armLength * 0.7f + 0.3f), "S");

            // Sun direction
            if (_cycle.SunWorldDirection.sqrMagnitude > 0.001f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(origin, origin + _cycle.SunWorldDirection * directionLineLength);
                // Sun dot
                Gizmos.DrawSphere(origin + _cycle.SunWorldDirection * directionLineLength, 0.15f);
                UnityEditor.Handles.Label(
                    origin + _cycle.SunWorldDirection * (directionLineLength + 0.3f),
                    $"☀ Az:{_cycle.SunAzimuth:F0}° Alt:{_cycle.SunAltitude:F0}°");
            }

            // Moon direction
            if (_cycle.MoonWorldDirection.sqrMagnitude > 0.001f)
            {
                Gizmos.color = new Color(0.7f, 0.9f, 1f);
                Gizmos.DrawLine(origin, origin + _cycle.MoonWorldDirection * directionLineLength * 0.8f);
                Gizmos.DrawSphere(origin + _cycle.MoonWorldDirection * directionLineLength * 0.8f, 0.1f);
                UnityEditor.Handles.Label(
                    origin + _cycle.MoonWorldDirection * (directionLineLength * 0.8f + 0.3f),
                    $"☽ Az:{_cycle.MoonAzimuth:F0}° Alt:{_cycle.MoonAltitude:F0}°\nPhase:{_cycle.MoonPhaseAngle:F0}°");
            }

            // Celestial pole
            if (showCelestialPole)
            {
                Quaternion poleRot = CelestialMath.StarDomeRotation(0f, _cycle.latitude, _cycle.eastDirectionYaw);
                // Pole points in the direction of the rotation axis
                Vector3 poleDir = poleRot * Vector3.up;
                // Reconstruct pole direction: tilted north by latitude
                float lat = _cycle.latitude * Mathf.Deg2Rad;
                float northLen = Mathf.Cos(lat);
                float upLen    = Mathf.Sin(lat);
                Vector3 pole   = (north * northLen + Vector3.up * upLen).normalized;

                Gizmos.color = new Color(0.5f, 1f, 0.5f);
                Gizmos.DrawLine(origin, origin + pole * armLength);
                UnityEditor.Handles.Label(origin + pole * (armLength + 0.3f),
                    $"★ Pole\nLat:{_cycle.latitude:F0}°");
            }

            // Time readout
            UnityEditor.Handles.Label(origin + Vector3.up * 0.5f,
                $"Time: {_cycle.CurrentTimeNormalized:F3}  Phase: {(_cycle.IsDay ? "DAY" : "NIGHT")}");
        }
#endif
    }
}
