using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CelestialSystem.Editor
{
    /// <summary>
    /// Editor utility to automatically assemble and save the complete, production-ready
    /// 'CelestialSystem.prefab' in Assets/CelestialSystem/Prefabs/.
    ///
    /// Runs via Window > Celestial System > Build Celestial System Prefab
    /// or automatically if the prefab is missing.
    /// </summary>
    public static class CelestialPrefabBuilder
    {
        private const string PrefabDir  = "Assets/CelestialSystem/Prefabs";
        private const string PrefabPath = "Assets/CelestialSystem/Prefabs/CelestialSystem.prefab";

        [InitializeOnLoadMethod]
        private static void AutoEnsurePrefabOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(PrefabPath))
                {
                    Debug.Log("[CelestialSystem] Prefab not found. Automatically building CelestialSystem.prefab...");
                    BuildPrefab();
                }
            };
        }

        [MenuItem("Window/Celestial System/Build Celestial System Prefab", priority = 102)]
        public static void BuildPrefab()
        {
            // 1. Ensure directory and materials
            if (!AssetDatabase.IsValidFolder(PrefabDir))
            {
                Directory.CreateDirectory(PrefabDir);
                AssetDatabase.Refresh();
            }

            CelestialMaterialGenerator.GenerateDefaultMaterials();

            Material sunDiskMat   = AssetDatabase.LoadAssetAtPath<Material>("Assets/CelestialSystem/Materials/M_SunDisk.mat");
            Material skyMat       = AssetDatabase.LoadAssetAtPath<Material>("Assets/CelestialSystem/Materials/M_SkyGradient.mat");
            Material moonMat      = AssetDatabase.LoadAssetAtPath<Material>("Assets/CelestialSystem/Materials/M_Moon.mat");
            Material starDomeMat  = AssetDatabase.LoadAssetAtPath<Material>("Assets/CelestialSystem/Materials/M_StarDome.mat");
            Material cloudsMat    = AssetDatabase.LoadAssetAtPath<Material>("Assets/CelestialSystem/Materials/M_Clouds.mat");

            // 2. Build root object
            GameObject root = new GameObject("CelestialSystem");
            root.transform.position = Vector3.zero;

            // 3. Core volume collider & master components
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(120f, 60f, 120f);
            box.center = new Vector3(0f, 25f, 0f);

            DayNightCycle cycle = root.AddComponent<DayNightCycle>();
            CelestialVolume volume = root.AddComponent<CelestialVolume>();
            volume.volumeCollider = box;

            CompassGizmo compass = root.AddComponent<CompassGizmo>();
            Volume uVolume = root.AddComponent<Volume>();
            uVolume.isGlobal = true;
            uVolume.weight = 1f;

            TimeOfDayGrading grading = root.AddComponent<TimeOfDayGrading>();
            grading.dayNightCycle = cycle;
            grading.celestialVolume = volume;
            grading.postProcessVolume = uVolume;

            QualityManager quality = root.AddComponent<QualityManager>();

            // 4. Sun Hierarchy
            GameObject sunRoot = new GameObject("Sun_Rig");
            sunRoot.transform.SetParent(root.transform, false);

            GameObject sunLightObj = new GameObject("Sun_DirectionalLight");
            sunLightObj.transform.SetParent(sunRoot.transform, false);
            Light sunLight = sunLightObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.shadows = LightShadows.Soft;
            sunLight.intensity = 100000f;
            sunLight.color = new Color(1.0f, 0.96f, 0.90f);

            GameObject sunDiskObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sunDiskObj.name = "Sun_Disk";
            sunDiskObj.transform.SetParent(sunRoot.transform, false);
            sunDiskObj.transform.localScale = Vector3.one * 80f;
            Object.DestroyImmediate(sunDiskObj.GetComponent<Collider>());
            Renderer sunDiskRend = sunDiskObj.GetComponent<Renderer>();
            if (sunDiskMat != null) sunDiskRend.sharedMaterial = sunDiskMat;

            SunController sunCtrl = sunRoot.AddComponent<SunController>();
            sunCtrl.dayNightCycle = cycle;
            sunCtrl.celestialVolume = volume;
            sunCtrl.sunLight = sunLight;
            sunCtrl.sunDiskRenderer = sunDiskRend;

            // 5. Moon Hierarchy
            GameObject moonRoot = new GameObject("Moon_Rig");
            moonRoot.transform.SetParent(root.transform, false);

            GameObject moonLightObj = new GameObject("Moon_DirectionalLight");
            moonLightObj.transform.SetParent(moonRoot.transform, false);
            Light moonLight = moonLightObj.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.shadows = LightShadows.Soft;
            moonLight.intensity = 1.2f;
            moonLight.color = new Color(0.72f, 0.84f, 1.0f);

            GameObject moonMeshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moonMeshObj.name = "Moon_Mesh";
            moonMeshObj.transform.SetParent(moonRoot.transform, false);
            moonMeshObj.transform.localScale = Vector3.one * 50f;
            Object.DestroyImmediate(moonMeshObj.GetComponent<Collider>());
            Renderer moonRend = moonMeshObj.GetComponent<Renderer>();
            if (moonMat != null) moonRend.sharedMaterial = moonMat;

            MoonController moonCtrl = moonRoot.AddComponent<MoonController>();
            moonCtrl.dayNightCycle = cycle;
            moonCtrl.celestialVolume = volume;
            moonCtrl.sunController = sunCtrl;
            moonCtrl.moonLight = moonLight;
            moonCtrl.moonRenderer = moonRend;

            // 6. Sky Dome
            GameObject skyDomeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            skyDomeObj.name = "Atmospheric_SkyDome";
            skyDomeObj.transform.SetParent(root.transform, false);
            skyDomeObj.transform.localScale = Vector3.one * 1500f;
            Object.DestroyImmediate(skyDomeObj.GetComponent<Collider>());
            Renderer skyRend = skyDomeObj.GetComponent<Renderer>();
            if (skyMat != null) skyRend.sharedMaterial = skyMat;
            SkyController skyCtrl = skyDomeObj.AddComponent<SkyController>();
            skyCtrl.dayNightCycle = cycle;
            skyCtrl.celestialVolume = volume;
            skyCtrl.skyRenderer = skyRend;

            // 7. Star Dome
            GameObject starDomeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            starDomeObj.name = "Celestial_StarDome";
            starDomeObj.transform.SetParent(root.transform, false);
            starDomeObj.transform.localScale = Vector3.one * 1400f;
            Object.DestroyImmediate(starDomeObj.GetComponent<Collider>());
            Renderer starRend = starDomeObj.GetComponent<Renderer>();
            if (starDomeMat != null) starRend.sharedMaterial = starDomeMat;
            StarField starField = starDomeObj.AddComponent<StarField>();
            starField.dayNightCycle = cycle;
            starField.celestialVolume = volume;
            starField.starDomeRenderer = starRend;

            // 8. Cloud Layer
            GameObject cloudsObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloudsObj.name = "Dynamic_CloudLayer";
            cloudsObj.transform.SetParent(root.transform, false);
            cloudsObj.transform.localScale = Vector3.one * 1200f;
            Object.DestroyImmediate(cloudsObj.GetComponent<Collider>());
            Renderer cloudRend = cloudsObj.GetComponent<Renderer>();
            if (cloudsMat != null) cloudRend.sharedMaterial = cloudsMat;
            CloudController cloudCtrl = cloudsObj.AddComponent<CloudController>();
            cloudCtrl.dayNightCycle = cycle;
            cloudCtrl.celestialVolume = volume;
            cloudCtrl.sunController = sunCtrl;
            cloudCtrl.moonController = moonCtrl;
            cloudCtrl.cloudRenderer = cloudRend;

            // 9. Ambient VFX (Dust Motes & Fireflies)
            GameObject vfxObj = new GameObject("Ambient_VFX");
            vfxObj.transform.SetParent(root.transform, false);

            GameObject dayDustObj = new GameObject("DayDust_Particles");
            dayDustObj.transform.SetParent(vfxObj.transform, false);
            ParticleSystem dayDustPS = dayDustObj.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(dayDustPS, new Color(1.0f, 0.95f, 0.8f, 0.6f), 0.05f, 40f, 25f);

            GameObject firefliesObj = new GameObject("NightFireflies_Particles");
            firefliesObj.transform.SetParent(vfxObj.transform, false);
            ParticleSystem firefliesPS = firefliesObj.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(firefliesPS, new Color(0.85f, 1.0f, 0.35f, 0.85f), 0.12f, 15f, 20f);

            AmbientVFXController ambientVFX = vfxObj.AddComponent<AmbientVFXController>();
            ambientVFX.dayNightCycle = cycle;
            ambientVFX.celestialVolume = volume;
            ambientVFX.dayDustParticles = dayDustPS;
            ambientVFX.nightFireflyParticles = firefliesPS;

            // 10. Wire QualityManager
            quality.starField = starField;
            quality.cloudController = cloudCtrl;
            quality.ambientVFX = ambientVFX;

            // 11. Populate CelestialVolume managed references
            volume.celestialRenderers = new Renderer[] {
                sunDiskRend,
                moonRend,
                skyRend,
                starRend,
                cloudRend
            };

            volume.celestialLights = new Light[] {
                sunLight,
                moonLight
            };

            volume.celestialParticles = new ParticleSystem[] {
                dayDustPS,
                firefliesPS
            };

            volume.celestialVolume = uVolume;

            // 12. Save Prefab
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CelestialSystem] Production-ready prefab saved successfully: " + PrefabPath);
        }

        private static void ConfigureParticleSystem(ParticleSystem ps, Color color, float size, float maxParticles, float radius)
        {
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.3f;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = (int)maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.2f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(color.a, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;
        }
    }
}
