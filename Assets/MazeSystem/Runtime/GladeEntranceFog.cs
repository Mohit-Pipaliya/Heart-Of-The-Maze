using System.Collections;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// Self-contained fog wall used to hide nightly block swaps.
    /// Drives a cover shader property (and optional dust emission) from 0..1.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GladeEntranceFog : MonoBehaviour
    {
        [Header("── Fog Wall ─────────────────────────────────")]
        [SerializeField] private Renderer fogRenderer;
        [SerializeField] private Material fogMaterial;
        [SerializeField] private string coverageProperty = "_Coverage";
        [Range(0f, 1f)] [SerializeField] private float initialCoverage = 0f;

        [Header("── Optional Particles ───────────────────────")]
        [SerializeField] private ParticleSystem dustParticles;
        [SerializeField] private float maxDustRate = 36f;

        public float Coverage { get; private set; }

        private MaterialPropertyBlock _propertyBlock;
        private int _coverageId;
        private ParticleSystem.EmissionModule _emission;

        private void Awake()
        {
            if (fogRenderer == null)
                fogRenderer = GetComponent<Renderer>();
            if (fogMaterial == null && fogRenderer != null)
                fogMaterial = fogRenderer.sharedMaterial;

            _coverageId = Shader.PropertyToID(string.IsNullOrEmpty(coverageProperty)
                ? "_Coverage"
                : coverageProperty);
            _propertyBlock = new MaterialPropertyBlock();

            if (dustParticles != null)
                _emission = dustParticles.emission;

            SetCoverage(initialCoverage);
        }

        public void SetCoverage(float value)
        {
            Coverage = Mathf.Clamp01(value);

            if (fogRenderer != null)
            {
                fogRenderer.enabled = Coverage > 0.002f;
                if (_propertyBlock == null)
                    _propertyBlock = new MaterialPropertyBlock();
                fogRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(_coverageId, Coverage);
                fogRenderer.SetPropertyBlock(_propertyBlock);
            }

            if (dustParticles != null)
            {
                if (Coverage > 0.002f && !dustParticles.isPlaying)
                    dustParticles.Play();
                _emission.rateOverTime = maxDustRate * Coverage;
                if (Coverage <= 0.002f && dustParticles.isPlaying)
                    dustParticles.Stop();
            }
        }

        /// <summary>Editor setup hook.</summary>
        public void EditorConfigure(Renderer renderer, Material material)
        {
            fogRenderer = renderer;
            fogMaterial = material;
            if (fogRenderer != null && fogMaterial != null)
                fogRenderer.sharedMaterial = fogMaterial;
        }

        public IEnumerator FadeTo(float target, float duration)
        {
            target = Mathf.Clamp01(target);
            if (duration <= 0.0001f)
            {
                SetCoverage(target);
                yield break;
            }

            float start = Coverage;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetCoverage(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }

            SetCoverage(target);
        }
    }
}
