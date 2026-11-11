using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Pure static math library for celestial body positioning.
    /// No MonoBehaviour, no allocations, no side-effects.
    /// All angles in degrees unless noted. Vectors in Unity world space.
    /// </summary>
    public static class CelestialMath
    {
        // ───────────────────────────────────────────────────────────────
        //  Constants
        // ───────────────────────────────────────────────────────────────
        private const float Deg2Rad = Mathf.Deg2Rad;
        private const float Rad2Deg = Mathf.Rad2Deg;

        // ───────────────────────────────────────────────────────────────
        //  Declination
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Solar declination in degrees for the given day of year (1-365).
        /// Uses Spencer approximation: delta = 23.44 * sin(2*pi*(dayOfYear-81)/365)
        /// </summary>
        public static float SolarDeclination(int dayOfYear)
        {
            float angle = 2f * Mathf.PI * (dayOfYear - 81) / 365f;
            return 23.44f * Mathf.Sin(angle);
        }

        // ───────────────────────────────────────────────────────────────
        //  Hour-angle utilities
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a normalised time (0 = midnight, 0.5 = noon) to a solar hour angle in degrees.
        /// Hour angle H = 0 at solar noon, negative in the morning, positive in the afternoon.
        /// Range: -180 .. +180
        /// </summary>
        public static float TimeToHourAngle(float normalizedTime)
        {
            // normalizedTime 0.5 → noon → H=0
            // normalizedTime 0   → midnight → H=±180
            return (normalizedTime - 0.5f) * 360f;
        }

        // ───────────────────────────────────────────────────────────────
        //  Core direction solver (East-Up-North local frame → world)
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the WORLD-SPACE direction FROM the scene origin TOWARD the celestial body.
        /// Uses the classic equatorial-to-horizontal coordinate transform.
        ///
        ///   east  = -cos(delta) * sin(H)
        ///   up    =  sin(lat) * sin(delta) + cos(lat) * cos(delta) * cos(H)
        ///   north =  sin(delta) * cos(lat) - cos(delta) * sin(lat) * cos(H)
        ///
        /// Then rotated by eastDirectionYaw so the user can align East to the actual world.
        /// </summary>
        /// <param name="latitudeDeg">Observer latitude in degrees (-90..90).</param>
        /// <param name="declinationDeg">Celestial body declination in degrees.</param>
        /// <param name="hourAngleDeg">Hour angle in degrees (0=noon, neg=morning, pos=afternoon).</param>
        /// <param name="eastDirectionYawDeg">World-space yaw of the East compass direction in degrees.</param>
        /// <returns>Normalised world-space direction toward the body. May be (0,-1,0) below horizon.</returns>
        public static Vector3 CelestialDirection(
            float latitudeDeg,
            float declinationDeg,
            float hourAngleDeg,
            float eastDirectionYawDeg)
        {
            float lat   = latitudeDeg     * Deg2Rad;
            float delta = declinationDeg  * Deg2Rad;
            float H     = hourAngleDeg    * Deg2Rad;

            float sinLat   = Mathf.Sin(lat);
            float cosLat   = Mathf.Cos(lat);
            float sinDelta = Mathf.Sin(delta);
            float cosDelta = Mathf.Cos(delta);
            float sinH     = Mathf.Sin(H);
            float cosH     = Mathf.Cos(H);

            // Local horizon frame (east, up, north)
            float east  = -cosDelta * sinH;
            float up    =  sinLat * sinDelta + cosLat * cosDelta * cosH;
            float north =  sinDelta * cosLat - cosDelta * sinLat * cosH;

            // Rotate the east axis by eastDirectionYaw around the world-up axis.
            // Unity: Y = up, X = right (default east when yaw=0), Z = forward (default north when yaw=0)
            float yaw = eastDirectionYawDeg * Deg2Rad;
            float cosYaw = Mathf.Cos(yaw);
            float sinYaw = Mathf.Sin(yaw);

            // East unit vector in world space after yaw rotation:
            //   eastWorld  = ( cosYaw, 0,  sinYaw)   [rotated X axis]
            //   northWorld = (-sinYaw, 0,  cosYaw)   [rotated Z axis]
            //   upWorld    = (0, 1, 0)
            Vector3 worldDir = new Vector3(
                east  * cosYaw - north * sinYaw,   // X
                up,                                 // Y
                east  * sinYaw + north * cosYaw    // Z
            );

            // Safety: return zero-safe normalised direction
            if (worldDir.sqrMagnitude < 1e-6f)
                return Vector3.down;

            return worldDir.normalized;
        }

        // ───────────────────────────────────────────────────────────────
        //  Convenience wrappers
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Sun direction from latitude, day-of-year, solar hour angle and compass yaw.
        /// </summary>
        public static Vector3 SunDirection(
            float latitudeDeg, int dayOfYear, float hourAngleDeg, float eastDirectionYawDeg)
        {
            float decl = SolarDeclination(dayOfYear);
            return CelestialDirection(latitudeDeg, decl, hourAngleDeg, eastDirectionYawDeg);
        }

        /// <summary>
        /// Moon direction. The moon's hour angle is shifted from the sun's by the phase angle,
        /// and an optional inclination tilt is applied.
        /// moonH = sunH - phaseAngle   (phase=0 → new moon, phase=180 → full moon)
        /// </summary>
        public static Vector3 MoonDirection(
            float latitudeDeg, int dayOfYear,
            float sunHourAngleDeg, float phaseAngleDeg,
            float inclinationDeg, float eastDirectionYawDeg)
        {
            float moonH    = sunHourAngleDeg - phaseAngleDeg;
            float declSun  = SolarDeclination(dayOfYear);
            // Moon declination slightly tilted
            float moonDecl = declSun + inclinationDeg * Mathf.Cos((moonH - sunHourAngleDeg) * Deg2Rad);
            moonDecl = Mathf.Clamp(moonDecl, -89.9f, 89.9f);
            return CelestialDirection(latitudeDeg, moonDecl, moonH, eastDirectionYawDeg);
        }

        // ───────────────────────────────────────────────────────────────
        //  Azimuth / Altitude from direction
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns (azimuth, altitude) in degrees from a world-space direction vector.
        /// Azimuth: 0=North, 90=East, 180=South, 270=West (compass bearing).
        /// Altitude: -90..+90 (positive above horizon).
        /// </summary>
        public static void DirectionToAzimuthAltitude(Vector3 worldDir, float eastDirectionYawDeg,
            out float azimuthDeg, out float altitudeDeg)
        {
            altitudeDeg = Mathf.Asin(Mathf.Clamp(worldDir.y, -1f, 1f)) * Rad2Deg;

            // Project to horizontal plane
            float yaw = eastDirectionYawDeg * Deg2Rad;
            float cosYaw = Mathf.Cos(yaw);
            float sinYaw = Mathf.Sin(yaw);

            // Recover east/north components
            float east  =  worldDir.x * cosYaw + worldDir.z * sinYaw;
            float north = -worldDir.x * sinYaw + worldDir.z * cosYaw;

            // Azimuth from north, clockwise
            azimuthDeg = Mathf.Atan2(east, north) * Rad2Deg;
            if (azimuthDeg < 0f) azimuthDeg += 360f;
        }

        // ───────────────────────────────────────────────────────────────
        //  Star dome rotation
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a Quaternion that rotates the celestial sphere (east to west)
        /// for a given normalised time and latitude. The pole axis is at altitude=latitude.
        /// </summary>
        public static Quaternion StarDomeRotation(float normalizedTime, float latitudeDeg, float eastDirectionYawDeg)
        {
            // Celestial pole direction: tilted from vertical by colatitude, pointing "north" in world
            float yaw    = eastDirectionYawDeg * Deg2Rad;
            float coLat  = (90f - latitudeDeg) * Deg2Rad;

            // Pole in world space: north = (-sin(yaw), 0, cos(yaw))
            float cosYaw = Mathf.Cos(yaw);
            float sinYaw = Mathf.Sin(yaw);
            Vector3 northHoriz = new Vector3(-sinYaw, 0f, cosYaw);
            // Tilt the pole up from north by latitude
            Vector3 pole = Vector3.Lerp(northHoriz.normalized, Vector3.up, Mathf.Sin(latitudeDeg * Deg2Rad)).normalized;

            // One full rotation per day (east to west → negative rotation around pole)
            float angle = -normalizedTime * 360f;
            return Quaternion.AngleAxis(angle, pole);
        }

        // ───────────────────────────────────────────────────────────────
        //  Phase illumination fraction
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns 0 (new moon) to 1 (full moon) based on phase angle degrees.
        /// </summary>
        public static float MoonIlluminationFraction(float phaseAngleDeg)
        {
            return (1f - Mathf.Cos(phaseAngleDeg * Deg2Rad)) * 0.5f;
        }
    }
}
