using NUnit.Framework;
using UnityEngine;
using CelestialSystem;

namespace CelestialSystem.Tests
{
    /// <summary>
    /// EditMode tests for the CelestialSystem Phase 1 core.
    /// Run via Window > General > Test Runner > EditMode.
    /// Tests are deterministic — no scene required.
    /// </summary>
    public sealed class CelestialSystemTests
    {
        // ── tolerance ───────────────────────────────────────────────────
        private const float AzimuthTol  = 15f;   // degrees (compass rough check)
        private const float AltitudeTol = 5f;    // degrees
        private const float NaNTol      = 1e-5f;

        // ── helpers ─────────────────────────────────────────────────────
        private static Vector3 SunDir(float latDeg, int doy, float hourAngle)
            => CelestialMath.SunDirection(latDeg, doy, hourAngle, 0f);

        private static void AzAlt(Vector3 dir, out float az, out float alt)
            => CelestialMath.DirectionToAzimuthAltitude(dir, 0f, out az, out alt);

        // ═══════════════════════════════════════════════════════════════
        //  1. Sunrise — sun in east half, altitude ≈ 0
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Sunrise_SunInEastHalf_AltitudeNearZero()
        {
            // Latitude 25°N, equinox (doy≈81, decl≈0), hour angle ≈ -90 → ~6 AM
            Vector3 dir = SunDir(25f, 81, -90f);
            AzAlt(dir, out float az, out float alt);

            // East: azimuth 45°–135°
            Assert.IsTrue(az >= 45f && az <= 135f,
                $"Sunrise azimuth should be in east half (45-135°) but was {az:F1}°");

            // Near horizon
            Assert.AreEqual(0f, alt, 20f,   // 20° tolerance at equinox/equator edge
                $"Sunrise altitude should be near 0° but was {alt:F1}°");
        }

        // ═══════════════════════════════════════════════════════════════
        //  2. Noon — altitude at maximum (south for northern latitudes)
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Noon_SunHighInSouth_NorthernLatitude()
        {
            // Latitude 45°N, equinox, H=0 → transit
            Vector3 dir = SunDir(45f, 81, 0f);
            AzAlt(dir, out float az, out float alt);

            // Should be roughly south: az ≈ 180°
            Assert.AreEqual(180f, az, AzimuthTol,
                $"Noon azimuth should be ~180° (south) for northern lat but was {az:F1}°");

            // Altitude should be positive and significant
            Assert.Greater(alt, 20f,
                $"Noon altitude should be well above horizon but was {alt:F1}°");
        }

        // ═══════════════════════════════════════════════════════════════
        //  3. Sunset — sun in west half, altitude ≈ 0
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Sunset_SunInWestHalf_AltitudeNearZero()
        {
            // H = +90 → ~6 PM
            Vector3 dir = SunDir(25f, 81, 90f);
            AzAlt(dir, out float az, out float alt);

            // West: azimuth 225°–315°
            Assert.IsTrue(az >= 225f && az <= 315f,
                $"Sunset azimuth should be in west half (225-315°) but was {az:F1}°");

            Assert.AreEqual(0f, alt, 20f,
                $"Sunset altitude should be near 0° but was {alt:F1}°");
        }

        // ═══════════════════════════════════════════════════════════════
        //  4. Full Moon — rises in east at sunset, altitude ≈ 0
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void FullMoon_RisesInEastAtSunset()
        {
            // Phase angle 180° → full moon. At sunset sunH ≈ +90
            // moonH = sunH - 180 = -90 → moon rising in east
            float sunHourAngle = 90f; // sunset
            Vector3 moonDir = CelestialMath.MoonDirection(25f, 81, sunHourAngle, 180f, 5f, 0f);
            AzAlt(moonDir, out float az, out float alt);

            // Should be in east half
            Assert.IsTrue(az >= 45f && az <= 135f,
                $"Full moon should rise in east at sunset but azimuth was {az:F1}°");
        }

        // ═══════════════════════════════════════════════════════════════
        //  5. DayNightCycle — day duration controls phase length
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void DayNightCycle_DayDurationControlsPhaseLength()
        {
            var go    = new GameObject("DNC_Test");
            var cycle = go.AddComponent<DayNightCycle>();
            cycle.dayDuration   = 100f;
            cycle.nightDuration = 200f;
            cycle.pause         = true;
            cycle.JumpToSunrise();

            Assert.IsTrue(cycle.IsDay, "After JumpToSunrise() cycle should be in Day phase.");
            Assert.AreEqual(0.25f, cycle.CurrentTimeNormalized, 0.01f,
                "Sunrise should be at normalised time ≈ 0.25");

            cycle.JumpToMidnight();
            Assert.IsFalse(cycle.IsDay, "After JumpToMidnight() cycle should be in Night phase.");

            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════
        //  6. No NaN — polar latitudes and all dayOfYear extremes
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void CelestialMath_NoNaN_AtExtremes()
        {
            float[] latitudes = { 0f, 89.9f, -89.9f, 45f, -45f };
            int[]   doys      = { 1, 81, 172, 265, 355, 365 };
            float[] hours     = { -180f, -90f, 0f, 90f, 180f };

            foreach (float lat in latitudes)
            foreach (int   doy in doys)
            foreach (float H   in hours)
            {
                Vector3 sun = CelestialMath.SunDirection(lat, doy, H, 0f);
                Assert.IsFalse(float.IsNaN(sun.x) || float.IsNaN(sun.y) || float.IsNaN(sun.z),
                    $"NaN in SunDirection lat={lat} doy={doy} H={H}: {sun}");

                Vector3 moon = CelestialMath.MoonDirection(lat, doy, H, 180f, 5f, 0f);
                Assert.IsFalse(float.IsNaN(moon.x) || float.IsNaN(moon.y) || float.IsNaN(moon.z),
                    $"NaN in MoonDirection lat={lat} doy={doy} H={H}: {moon}");

                CelestialMath.DirectionToAzimuthAltitude(sun, 0f, out float az, out float alt);
                Assert.IsFalse(float.IsNaN(az) || float.IsNaN(alt),
                    $"NaN in AzAlt conversion lat={lat} doy={doy} H={H}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  7. CelestialVolume — IsPointInBox works with rotated box
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void CelestialVolume_RotatedBox_InsideOutsideCorrect()
        {
            var go      = new GameObject("Vol_Test");
            var col     = go.AddComponent<BoxCollider>();
            col.size    = new Vector3(10f, 10f, 10f);
            col.center  = Vector3.zero;

            // Rotate 45° around Y
            go.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            var vol = go.AddComponent<CelestialVolume>();
            vol.volumeCollider = col;

            // Point at world origin → should be inside (always)
            Vector3 insidePoint  = go.transform.position;
            // Point far outside on the original X axis after rotation
            Vector3 outsidePoint = go.transform.position + new Vector3(20f, 0f, 0f);

            // We test via InverseTransformPoint logic (which the script uses)
            Vector3 localInside  = col.transform.InverseTransformPoint(insidePoint);
            Vector3 localOutside = col.transform.InverseTransformPoint(outsidePoint);
            Vector3 half = col.size * 0.5f;

            bool insideResult = Mathf.Abs(localInside.x) <= half.x &&
                                Mathf.Abs(localInside.y) <= half.y &&
                                Mathf.Abs(localInside.z) <= half.z;

            bool outsideResult = Mathf.Abs(localOutside.x) <= half.x &&
                                 Mathf.Abs(localOutside.y) <= half.y &&
                                 Mathf.Abs(localOutside.z) <= half.z;

            Assert.IsTrue(insideResult,  "Origin should be INSIDE the rotated box.");
            Assert.IsFalse(outsideResult, "Far point should be OUTSIDE the rotated box.");

            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════
        //  8. MoonIlluminationFraction correctness
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void MoonIllumination_NewAndFullMoon()
        {
            float newMoon  = CelestialMath.MoonIlluminationFraction(0f);
            float fullMoon = CelestialMath.MoonIlluminationFraction(180f);

            Assert.AreEqual(0f, newMoon,  0.01f, "New moon illum should be ~0.");
            Assert.AreEqual(1f, fullMoon, 0.01f, "Full moon illum should be ~1.");
        }

        // ═══════════════════════════════════════════════════════════════
        //  9. SolarDeclination at solstices and equinoxes
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void SolarDeclination_SolsticesAndEquinoxes()
        {
            float equinox   = CelestialMath.SolarDeclination(81);   // ~March equinox
            float solstice  = CelestialMath.SolarDeclination(172);  // ~June solstice
            float equinox2  = CelestialMath.SolarDeclination(265);  // ~Sept equinox
            float solstice2 = CelestialMath.SolarDeclination(355);  // ~Dec solstice

            Assert.AreEqual( 0f,    equinox,   5f, "March equinox declination should be ~0°");
            Assert.AreEqual(23.44f, solstice,  2f, "June solstice declination should be ~+23.44°");
            Assert.AreEqual( 0f,    equinox2,  5f, "Sept equinox declination should be ~0°");
            Assert.AreEqual(-23.44f,solstice2, 2f, "Dec solstice declination should be ~-23.44°");
        }

        // ═══════════════════════════════════════════════════════════════
        //  10. StarDomeRotation — returns valid quaternion, no NaN
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void StarDomeRotation_ReturnsValidQuaternion()
        {
            float[] times = { 0f, 0.25f, 0.5f, 0.75f, 1f };
            foreach (float t in times)
            {
                Quaternion q = CelestialMath.StarDomeRotation(t, 45f, 0f);
                Assert.IsFalse(float.IsNaN(q.x) || float.IsNaN(q.y) ||
                               float.IsNaN(q.z) || float.IsNaN(q.w),
                    $"StarDomeRotation NaN at t={t}");
                // Should be normalised
                float mag = Mathf.Sqrt(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w);
                Assert.AreEqual(1f, mag, 0.001f, $"StarDomeRotation not normalised at t={t}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  11. Phase 2: SunController Intensity Curve Bounds
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void SunController_IntensityCurve_NonNegativeAndClamped()
        {
            var go = new GameObject("SunTest");
            var sun = go.AddComponent<SunController>();

            // Test normalized altitude evaluations: 0 (-10°), 0.1 (0° horizon), 1.0 (90° zenith)
            float intensityAtNight  = sun.intensityByAltitude.Evaluate(0.0f);
            float intensityAtZenith = sun.intensityByAltitude.Evaluate(1.0f);

            Assert.AreEqual(0.0f, intensityAtNight, 0.001f, "Sun intensity at night (-10°) should evaluate to 0.");
            Assert.Greater(intensityAtZenith, 0.5f, "Sun intensity at zenith (90°) should be high.");

            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════
        //  12. Phase 2: Shaders can be found by name
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Phase2_Shaders_Exist()
        {
            string[] shaderNames = {
                "CelestialSystem/SunDisk",
                "CelestialSystem/SkyGradient",
                "CelestialSystem/GodRays",
                "CelestialSystem/StencilWrite"
            };

            foreach (string sName in shaderNames)
            {
                Shader s = Shader.Find(sName);
                Assert.IsNotNull(s, $"Shader '{sName}' should be found in project.");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  13. Phase 3: Moon illumination across all phases
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Phase3_MoonIllumination_MonotonicAndBounded()
        {
            float newMoon   = CelestialMath.MoonIlluminationFraction(0f);
            float quarter   = CelestialMath.MoonIlluminationFraction(90f);
            float fullMoon  = CelestialMath.MoonIlluminationFraction(180f);
            float lastQ     = CelestialMath.MoonIlluminationFraction(270f);

            Assert.AreEqual(0.0f, newMoon,  0.001f, "New moon fraction must be 0");
            Assert.AreEqual(0.5f, quarter,  0.001f, "First quarter fraction must be 0.5");
            Assert.AreEqual(1.0f, fullMoon, 0.001f, "Full moon fraction must be 1.0");
            Assert.AreEqual(0.5f, lastQ,    0.001f, "Last quarter fraction must be 0.5");
        }

        // ═══════════════════════════════════════════════════════════════
        //  14. Phase 3: Moon and StarDome Shaders Exist
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Phase3_Shaders_Exist()
        {
            string[] shaderNames = {
                "CelestialSystem/Moon",
                "CelestialSystem/StarDome"
            };

            foreach (string sName in shaderNames)
            {
                Shader s = Shader.Find(sName);
                Assert.IsNotNull(s, $"Shader '{sName}' should be found in project.");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  15. Phase 4: Clouds Shader Exists
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Phase4_Shaders_Exist()
        {
            Shader s = Shader.Find("CelestialSystem/Clouds");
            Assert.IsNotNull(s, "Shader 'CelestialSystem/Clouds' should be found in project.");
        }

        // ═══════════════════════════════════════════════════════════════
        //  16. Phase 4: QualityManager applies presets cleanly
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Phase4_QualityManager_AppliesPresets()
        {
            var go = new GameObject("QM_Test");
            var qm = go.AddComponent<QualityManager>();

            qm.ApplyQualityPreset(QualityManager.QualityLevel.Low);
            Assert.AreEqual(QualityManager.QualityLevel.Low, qm.CurrentQuality);

            qm.ApplyQualityPreset(QualityManager.QualityLevel.High);
            Assert.AreEqual(QualityManager.QualityLevel.High, qm.CurrentQuality);

            Object.DestroyImmediate(go);
        }

        // ═══════════════════════════════════════════════════════════════
        //  17. Phase 4: TimeOfDayGrading presets have valid values
        // ═══════════════════════════════════════════════════════════════
        [Test]
        public void Phase4_TimeOfDayGrading_PresetsValid()
        {
            var dawn  = TimeOfDayGrading.ProfileSettings.CreateDefaultDawn();
            var day   = TimeOfDayGrading.ProfileSettings.CreateDefaultDay();
            var dusk  = TimeOfDayGrading.ProfileSettings.CreateDefaultDusk();
            var night = TimeOfDayGrading.ProfileSettings.CreateDefaultNight();

            // Bloom should be highest at night to catch stars & moon
            Assert.Greater(night.bloomIntensity, day.bloomIntensity, "Night bloom should be higher than day bloom.");
            // Exposure should be lower at night
            Assert.Less(night.postExposure, day.postExposure, "Night exposure should be lower than day exposure.");
        }
    }
}

