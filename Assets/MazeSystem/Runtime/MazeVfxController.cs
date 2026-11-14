using System;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// AAA particle VFX controller for the Shifting Maze.
    /// Spawns dust plumes, ground tremors, and impact shockwaves at block positions.
    /// Includes procedural particle system generator if no prefabs are assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MazeVfxController : MonoBehaviour
    {
        [Header("── Particle Systems (Optional, Auto-Generated fallback) ──")]
        public ParticleSystem travelDustPS;
        public ParticleSystem lockImpactPS;

        [Header("── VFX Tuning ────────────────────────────────")]
        [Range(0.2f, 3f)] public float vfxScale = 1.0f;
        public Color dustColor = new Color(0.72f, 0.68f, 0.60f, 0.45f);

        private void Awake()
        {
            EnsureSystems();
        }

        private void EnsureSystems()
        {
            if (travelDustPS == null)
            {
                var go = new GameObject("TravelDust_AutoVFX");
                go.transform.SetParent(transform, false);
                travelDustPS = go.AddComponent<ParticleSystem>();
                ConfigureDustSystem(travelDustPS, false);
            }

            if (lockImpactPS == null)
            {
                var go = new GameObject("LockImpact_AutoVFX");
                go.transform.SetParent(transform, false);
                lockImpactPS = go.AddComponent<ParticleSystem>();
                ConfigureLockImpactSystem(lockImpactPS);
            }
        }

        private void ConfigureDustSystem(ParticleSystem ps, bool loop)
        {
            var main = ps.main;
            main.startLifetime = 3.2f;
            main.startSpeed = 8f;
            main.startSize = 14f * vfxScale;
            main.startColor = dustColor;
            main.maxParticles = 500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = loop;

            var emission = ps.emission;
            emission.rateOverTime = 45f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80f, 2f, 80f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.4f);
            curve.AddKey(1f, 1.8f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(dustColor, 0f), new GradientColorKey(dustColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            ApplyParticleMaterial(ps, "M_Smoke");
        }

        private void ConfigureLockImpactSystem(ParticleSystem ps)
        {
            var main = ps.main;
            main.startLifetime = 2.4f;
            main.startSpeed = 22f;
            main.startSize = 18f * vfxScale;
            main.startColor = new Color(0.78f, 0.74f, 0.65f, 0.65f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 45f;
            shape.radiusThickness = 0.2f;

            ApplyParticleMaterial(ps, "M_ParticleAlpha");
        }

        private void ApplyParticleMaterial(ParticleSystem ps, string preferredMatName)
        {
            if (ps == null) return;
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr == null) return;

            Material mat = Resources.Load<Material>($"VFX/{preferredMatName}") ?? Resources.Load<Material>("VFX/M_ParticleAlpha");
#if UNITY_EDITOR
            if (mat == null)
                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/VFX/{preferredMatName}.mat");
            if (mat == null)
                mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_ParticleAlpha.mat");
#endif
            if (mat != null)
            {
                psr.sharedMaterial = mat;
            }
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (s != null) psr.sharedMaterial = new Material(s) { name = "M_MazeVfx_URP" };
            }
        }

        /// <summary>
        /// Starts continuous dust emissions at block position during relocation.
        /// </summary>
        public void StartTravelVfx(Vector3 worldPos)
        {
            EnsureSystems();
            transform.position = worldPos;
            if (travelDustPS != null && !travelDustPS.isPlaying)
                travelDustPS.Play();
        }

        /// <summary>
        /// Updates emitter location along travel path.
        /// </summary>
        public void UpdateVfxPosition(Vector3 worldPos)
        {
            transform.position = worldPos;
        }

        /// <summary>
        /// Stops continuous dust and triggers violent lock-in shockwave ring.
        /// </summary>
        public void StopTravelVfxAndEmitLock(Vector3 worldPos)
        {
            EnsureSystems();
            transform.position = worldPos;
            if (travelDustPS != null && travelDustPS.isPlaying)
                travelDustPS.Stop();

            if (lockImpactPS != null)
            {
                lockImpactPS.transform.position = worldPos;
                lockImpactPS.Play();
            }
        }
    }
}
