using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CelestialSystem
{
    /// <summary>
    /// Controls visibility of the entire CelestialSystem based on camera position
    /// relative to an assigned BoxCollider. Supports smooth fade and stencil-window modes.
    ///
    /// All renderers, lights, particle systems and URP volumes managed by this component
    /// are fully DISABLED (zero GPU cost) when visibility reaches 0.
    ///
    /// Rotated/scaled boxes are handled correctly via InverseTransformPoint.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CelestialVolume : MonoBehaviour
    {
        // ───────────────────────────────────────────────────────────────
        //  Enums
        // ───────────────────────────────────────────────────────────────
        public enum VisibilityMode { FadeMode, StencilWindowMode }

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Volume
        // ───────────────────────────────────────────────────────────────
        [Header("── Box Volume ───────────────────────────────")]
        [Tooltip("BoxCollider that defines the visibility zone. If null, uses the one on this GameObject.")]
        public BoxCollider volumeCollider;

        [Tooltip("The player camera. Falls back to Camera.main if null. " +
                 "Only this camera controls visibility (see multi-cam notes in script docs).")]
        public Camera playerCamera;

        [Header("── Fade ────────────────────────────────────")]
        [Tooltip("Seconds to fade in when camera enters the box.")]
        [Min(0f)] public float fadeInTime  = 2f;

        [Tooltip("Seconds to fade out when camera leaves the box.")]
        [Min(0f)] public float fadeOutTime = 1.5f;

        [Header("── Mode ────────────────────────────────────")]
        [Tooltip("FadeMode: fade by camera-inside-box. StencilWindowMode: render sky only inside stencil mask.")]
        public VisibilityMode visibilityMode = VisibilityMode.FadeMode;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Managed Objects
        // ───────────────────────────────────────────────────────────────
        [Header("── Managed Objects ──────────────────────────")]
        [Tooltip("All Renderers belonging to the celestial system (sun disk, moon, sky dome, stars…).")]
        public Renderer[] celestialRenderers = new Renderer[0];

        [Tooltip("All Lights belonging to the celestial system (sun directional light, moon directional light…).")]
        public Light[] celestialLights = new Light[0];

        [Tooltip("All ParticleSystems belonging to the celestial system.")]
        public ParticleSystem[] celestialParticles = new ParticleSystem[0];

        [Tooltip("URP Volume used for celestial post-processing (bloom, color grading…).")]
        public Volume celestialVolume;

        // ───────────────────────────────────────────────────────────────
        //  Inspector — Outside Sky Settings
        // ───────────────────────────────────────────────────────────────
        [Header("── Outside Sky Settings ──────────────────────")]
        [Tooltip("Solid color shown as the camera background when outside the volume.")]
        public Color outsideSkyColor = new Color(0.1f, 0.1f, 0.12f, 1f);

        [Tooltip("Optional custom skybox shown outside the volume. Leave null for solid outsideSkyColor.")]
        public Material outsideSkyboxMaterial;

        [Tooltip("Ambient color when outside the volume.")]
        public Color outsideAmbientColor = new Color(0.05f, 0.05f, 0.08f);

        // ───────────────────────────────────────────────────────────────
        //  Read-only public state
        // ───────────────────────────────────────────────────────────────

        /// <summary>Current visibility 0 (fully outside) → 1 (fully inside).</summary>
        public float Visibility { get; private set; }

        /// <summary>True if camera is inside the box this frame.</summary>
        public bool IsInside { get; private set; }

        // ───────────────────────────────────────────────────────────────
        //  Private state — cached original RenderSettings
        // ───────────────────────────────────────────────────────────────
        private Material  _originalSkybox;
        private Color     _originalAmbientColor;
        private AmbientMode _originalAmbientMode;
        private Light     _originalSunSource;
        private bool      _renderSettingsSaved;

        // Track last-known inside state to detect teleports (one-frame jump)
        private bool _wasInsideLastFrame;

        // Track whether managed objects are currently enabled
        private bool _objectsEnabled = true;

        // ───────────────────────────────────────────────────────────────
        //  Private state — emission intensities cache
        // ───────────────────────────────────────────────────────────────
        private float[] _lightIntensityCache = new float[0];
        private float   _volumeWeightCache   = 1f;

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────
        private void Awake()
        {
            // Auto-resolve collider
            if (volumeCollider == null)
                volumeCollider = GetComponent<BoxCollider>();

            // Cache original RenderSettings (do this once at startup)
            SaveRenderSettings();

            // Cache light intensities
            RebuildCaches();
        }

        private void Start()
        {
            // Resolve camera fallback (Camera.main may not exist in Awake)
            if (playerCamera == null)
                playerCamera = Camera.main;

            // Handle case where camera starts inside or outside (no fade on first frame)
            bool startInside = IsPointInBox(GetCameraPosition());
            Visibility = startInside ? 1f : 0f;
            IsInside   = startInside;
            _wasInsideLastFrame = startInside;

            ApplyVisibility(Visibility);

            if (!startInside)
                ApplyOutsideEnvironment();
            else
                RestoreInsideEnvironment();

            UpdateStencilState();
        }

        private void Update()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerCamera == null) return;

            Vector3 camPos = GetCameraPosition();
            bool inside = IsPointInBox(camPos);

            // Detect teleport (changed in one frame with no fade)
            bool teleported = (inside != _wasInsideLastFrame) &&
                              (Mathf.Approximately(Visibility, inside ? 0f : 1f));

            IsInside = inside;
            _wasInsideLastFrame = inside;

            // Advance fade
            if (fadeInTime <= 0f && fadeOutTime <= 0f)
            {
                Visibility = inside ? 1f : 0f;
            }
            else
            {
                float target = inside ? 1f : 0f;
                if (inside)
                {
                    float speed = fadeInTime  > 0f ? 1f / fadeInTime  : float.MaxValue;
                    Visibility = Mathf.MoveTowards(Visibility, 1f, speed * Time.deltaTime);
                }
                else
                {
                    float speed = fadeOutTime > 0f ? 1f / fadeOutTime : float.MaxValue;
                    Visibility = Mathf.MoveTowards(Visibility, 0f, speed * Time.deltaTime);
                }
            }

            ApplyVisibility(Visibility);

            // Environment switching at boundary
            if (inside && Visibility >= 1f)
                RestoreInsideEnvironment();
            else if (!inside && Visibility <= 0f)
                ApplyOutsideEnvironment();

            UpdateStencilState();
        }

        private void UpdateStencilState()
        {
            bool isStencilMode = (visibilityMode == VisibilityMode.StencilWindowMode);
            StencilWindowRendererFeature.StencilModeEnabled = isStencilMode && (volumeCollider != null);
            StencilWindowRendererFeature.ActiveBoxCollider  = isStencilMode ? volumeCollider : null;
        }

        /// <summary>
        /// Automatically calculates the combined bounding box of all active Terrains in the scene
        /// and sizes the BoxCollider to encompass them with horizontal and vertical clearance.
        /// Falls back to scene geometry if no Unity Terrain is found.
        /// </summary>
        /// <param name="horizontalPadding">Extra distance around the borders (meters).</param>
        /// <param name="heightPadding">Extra clearance above highest peak (meters).</param>
        /// <returns>True if bounds were calculated and applied.</returns>
        public bool FitToAllTerrains(float horizontalPadding = 30f, float heightPadding = 250f)
        {
            if (volumeCollider == null)
                volumeCollider = GetComponent<BoxCollider>();

            if (volumeCollider == null)
                volumeCollider = gameObject.AddComponent<BoxCollider>();

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool foundBounds = false;

            // 1. Try finding Unity Terrains
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains != null && terrains.Length > 0)
            {
                foreach (Terrain t in terrains)
                {
                    if (t == null || t.terrainData == null) continue;
                    Vector3 tPos = t.transform.position;
                    Vector3 tSize = t.terrainData.size;

                    min = Vector3.Min(min, tPos);
                    max = Vector3.Max(max, tPos + tSize);
                    foundBounds = true;
                }
            }

            // 2. Fallback to scene geometry if no active terrains found
            if (!foundBounds)
            {
                Renderer[] allRends = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (Renderer r in allRends)
                {
                    if (r == null || !r.enabled) continue;
                    if (r.transform.IsChildOf(transform)) continue; // ignore celestial dome meshes

                    Bounds b = r.bounds;
                    if (b.size.sqrMagnitude < 0.1f) continue;

                    min = Vector3.Min(min, b.min);
                    max = Vector3.Max(max, b.max);
                    foundBounds = true;
                }
            }

            if (!foundBounds)
            {
                Debug.LogWarning("[CelestialVolume] No terrains or scene renderers found to fit bounds to.");
                return false;
            }

            // Apply padding
            min.x -= horizontalPadding;
            min.z -= horizontalPadding;
            min.y -= 25f; // underground margin for dips and caves

            max.x += horizontalPadding;
            max.z += horizontalPadding;
            max.y += heightPadding; // sky clearance

            Vector3 worldCenter = (min + max) * 0.5f;
            Vector3 worldSize   = max - min;

            Transform tCol = volumeCollider.transform;
            Vector3 localCenter = tCol.InverseTransformPoint(worldCenter);
            Vector3 lossy = tCol.lossyScale;

            volumeCollider.center = localCenter;
            volumeCollider.size = new Vector3(
                lossy.x != 0 ? worldSize.x / Mathf.Abs(lossy.x) : worldSize.x,
                lossy.y != 0 ? worldSize.y / Mathf.Abs(lossy.y) : worldSize.y,
                lossy.z != 0 ? worldSize.z / Mathf.Abs(lossy.z) : worldSize.z
            );

            if (playerCamera == null)
                playerCamera = Camera.main;

            Debug.Log($"[CelestialVolume] Successfully fitted BoxCollider to scene terrains/geometry. " +
                      $"Center: {worldCenter}, Size: {worldSize}");
            return true;
        }

        // ───────────────────────────────────────────────────────────────
        //  Box test (rotated/scaled safe)
        // ───────────────────────────────────────────────────────────────
        private bool IsPointInBox(Vector3 worldPoint)
        {
            if (volumeCollider == null) return false;

            // Transform point into collider's local space
            Vector3 localPoint = volumeCollider.transform.InverseTransformPoint(worldPoint);
            Vector3 half = volumeCollider.size * 0.5f;
            Vector3 center = volumeCollider.center;

            // Check against local-space AABB
            return Mathf.Abs(localPoint.x - center.x) <= half.x &&
                   Mathf.Abs(localPoint.y - center.y) <= half.y &&
                   Mathf.Abs(localPoint.z - center.z) <= half.z;
        }

        private Vector3 GetCameraPosition()
        {
            return playerCamera != null ? playerCamera.transform.position : Vector3.zero;
        }

        // ───────────────────────────────────────────────────────────────
        //  Apply visibility multiplier to all managed objects
        // ───────────────────────────────────────────────────────────────
        private void ApplyVisibility(float v)
        {
            bool shouldEnable = v > 0f;

            // Enable/disable managed objects at the boundary (zero hidden cost when off)
            if (shouldEnable && !_objectsEnabled)
            {
                SetManagedObjectsEnabled(true);
                _objectsEnabled = true;
            }
            else if (!shouldEnable && _objectsEnabled)
            {
                SetManagedObjectsEnabled(false);
                _objectsEnabled = false;
                return; // Nothing to tint if disabled
            }

            if (!_objectsEnabled) return;

            // Scale light intensities
            for (int i = 0; i < celestialLights.Length; i++)
            {
                if (celestialLights[i] == null) continue;
                float baseIntensity = i < _lightIntensityCache.Length ? _lightIntensityCache[i] : 1f;
                celestialLights[i].intensity = baseIntensity * v;
            }

            // Scale URP Volume weight
            if (celestialVolume != null)
                celestialVolume.weight = _volumeWeightCache * v;
        }

        private void SetManagedObjectsEnabled(bool enabled)
        {
            for (int i = 0; i < celestialRenderers.Length; i++)
                if (celestialRenderers[i] != null)
                    celestialRenderers[i].enabled = enabled;

            for (int i = 0; i < celestialLights.Length; i++)
                if (celestialLights[i] != null)
                    celestialLights[i].enabled = enabled;

            for (int i = 0; i < celestialParticles.Length; i++)
            {
                if (celestialParticles[i] == null) continue;
                if (enabled) celestialParticles[i].Play();
                else         celestialParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (celestialVolume != null)
                celestialVolume.enabled = enabled;
        }

        // ───────────────────────────────────────────────────────────────
        //  RenderSettings save / restore
        // ───────────────────────────────────────────────────────────────
        private void SaveRenderSettings()
        {
            if (_renderSettingsSaved) return;
            _originalSkybox       = RenderSettings.skybox;
            _originalAmbientColor = RenderSettings.ambientLight;
            _originalAmbientMode  = RenderSettings.ambientMode;
            _originalSunSource    = RenderSettings.sun;
            _renderSettingsSaved  = true;
        }

        private void RestoreInsideEnvironment()
        {
            if (!_renderSettingsSaved) return;
            RenderSettings.skybox       = _originalSkybox;
            RenderSettings.ambientLight = _originalAmbientColor;
            RenderSettings.ambientMode  = _originalAmbientMode;
            RenderSettings.sun          = _originalSunSource;
        }

        private void ApplyOutsideEnvironment()
        {
            // Make sure we won't leave RenderSettings.sun pointing at a disabled light
            RenderSettings.sun = null;

            if (outsideSkyboxMaterial != null)
            {
                RenderSettings.skybox = outsideSkyboxMaterial;
            }
            else
            {
                RenderSettings.skybox = null;
                if (playerCamera != null)
                {
                    playerCamera.clearFlags  = CameraClearFlags.SolidColor;
                    playerCamera.backgroundColor = outsideSkyColor;
                }
            }

            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = outsideAmbientColor;
        }

        // ───────────────────────────────────────────────────────────────
        //  Cache rebuild (call after Inspector changes)
        // ───────────────────────────────────────────────────────────────
        private void RebuildCaches()
        {
            if (_lightIntensityCache.Length != celestialLights.Length)
                _lightIntensityCache = new float[celestialLights.Length];

            for (int i = 0; i < celestialLights.Length; i++)
                _lightIntensityCache[i] = celestialLights[i] != null ? celestialLights[i].intensity : 1f;

            _volumeWeightCache = celestialVolume != null ? celestialVolume.weight : 1f;
        }

        private void OnValidate()
        {
            RebuildCaches();
        }

        private void OnDisable()
        {
            StencilWindowRendererFeature.StencilModeEnabled = false;
            StencilWindowRendererFeature.ActiveBoxCollider  = null;
        }

        private void OnDestroy()
        {
            StencilWindowRendererFeature.StencilModeEnabled = false;
            StencilWindowRendererFeature.ActiveBoxCollider  = null;

            // Restore RenderSettings to avoid leaving the scene dirty
            if (_renderSettingsSaved)
                RestoreInsideEnvironment();
        }

        // ───────────────────────────────────────────────────────────────
        //  Scene gizmos
        // ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (volumeCollider == null) return;

            bool inside = Application.isPlaying
                ? IsInside
                : IsPointInBox(UnityEditor.SceneView.lastActiveSceneView?.camera.transform.position ?? Vector3.zero);

            Gizmos.matrix = volumeCollider.transform.localToWorldMatrix;
            Gizmos.color  = inside ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawCube(volumeCollider.center, volumeCollider.size);
            Gizmos.color  = inside ? Color.green : Color.red;
            Gizmos.DrawWireCube(volumeCollider.center, volumeCollider.size);
            Gizmos.matrix = Matrix4x4.identity;

            // Label
            Vector3 worldCenter = volumeCollider.transform.TransformPoint(volumeCollider.center);
            UnityEditor.Handles.Label(worldCenter,
                inside
                    ? $"INSIDE\nVisibility: {Visibility:F2}"
                    : $"OUTSIDE\nVisibility: {Visibility:F2}");
        }
#endif
    }
}


