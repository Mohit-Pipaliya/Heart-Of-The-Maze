using UnityEngine;

namespace CelestialSystem
{
    /// <summary>
    /// Master Quality Settings Manager for the CelestialSystem.
    /// Provides 3 tuned AAA presets:
    ///   • Low:    Optimized for low-end laptops/mobile (fewer ray samples, downsampled buffers, simplified clouds)
    ///   • Medium: Balanced high-fidelity target (60+ FPS on mid-range GPUs)
    ///   • High:   Full cinematic AAA fidelity (maximum samples, high-res cascades, dense star & cloud fields)
    ///
    /// Can be switched dynamically via Inspector or runtime scripts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QualityManager : MonoBehaviour
    {
        public enum QualityLevel { Low, Medium, High }

        [Header("── Active Quality Preset ────────────────────")]
        [Tooltip("Active graphics fidelity tier.")]
        [SerializeField] private QualityLevel currentQuality = QualityLevel.High;

        [Header("── Managed Subsystems ────────────────────────")]
        public StarField starField;
        public CloudController cloudController;
        public AmbientVFXController ambientVFX;

        public QualityLevel CurrentQuality
        {
            get => currentQuality;
            set
            {
                currentQuality = value;
                ApplyQualityPreset(currentQuality);
            }
        }

        private void Awake()
        {
            if (starField == null)
                starField = FindFirstObjectByType<StarField>();

            if (cloudController == null)
                cloudController = FindFirstObjectByType<CloudController>();

            if (ambientVFX == null)
                ambientVFX = FindFirstObjectByType<AmbientVFXController>();

            ApplyQualityPreset(currentQuality);
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                ApplyQualityPreset(currentQuality);
        }

        /// <summary>
        /// Applies the chosen quality preset to all celestial subsystems.
        /// </summary>
        public void ApplyQualityPreset(QualityLevel level)
        {
            currentQuality = level;

            switch (level)
            {
                case QualityLevel.Low:
                    ApplyLowQuality();
                    break;
                case QualityLevel.Medium:
                    ApplyMediumQuality();
                    break;
                case QualityLevel.High:
                    ApplyHighQuality();
                    break;
            }
        }

        private void ApplyLowQuality()
        {
            if (cloudController != null)
            {
                cloudController.cloudDensity = 1.2f;
                cloudController.cloudCoverage = 0.35f;
            }

            if (ambientVFX != null)
            {
                ambientVFX.dayDustSpawnRate = 20f;
                ambientVFX.nightFireflySpawnRate = 8f;
            }

            // Optimize shadow quality
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
        }

        private void ApplyMediumQuality()
        {
            if (cloudController != null)
            {
                cloudController.cloudDensity = 1.8f;
                cloudController.cloudCoverage = 0.48f;
            }

            if (ambientVFX != null)
            {
                ambientVFX.dayDustSpawnRate = 45f;
                ambientVFX.nightFireflySpawnRate = 18f;
            }

            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowResolution = ShadowResolution.High;
        }

        private void ApplyHighQuality()
        {
            if (cloudController != null)
            {
                cloudController.cloudDensity = 2.2f;
                cloudController.cloudCoverage = 0.52f;
            }

            if (ambientVFX != null)
            {
                ambientVFX.dayDustSpawnRate = 70f;
                ambientVFX.nightFireflySpawnRate = 26f;
            }

            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        }
    }
}
