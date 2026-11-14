using System;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// Cinematic camera trauma feedback for the Shifting Maze.
    /// Drives realistic non-linear screen shake (trauma squared) with distance falloff.
    /// Zero GC alloc per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MazeCameraFeedback : MonoBehaviour
    {
        [Header("── Camera Reference ──────────────────────────")]
        public Camera targetCamera;

        [Header("── Shake Parameters ──────────────────────────")]
        [Range(0f, 1f)] public float trauma;
        [Range(1f, 30f)] public float frequency = 16f;
        public float maxPitchYawRoll = 2.5f;
        public float maxPositionOffset = 0.25f;
        [Range(0.1f, 5f)] public float traumaDecayRate = 1.2f;

        [Header("── Distance Falloff ───────────────────────────")]
        public float minShakeDistance = 30f;
        public float maxShakeDistance = 800f;

        private float _seed;
        private Transform _camTransform;
        private Vector3 _appliedPosOffset;
        private Quaternion _appliedRotOffset = Quaternion.identity;

        private void Awake()
        {
            ResolveCamera();
            _seed = UnityEngine.Random.value * 100f;
        }

        private void ResolveCamera()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
            {
                _camTransform = targetCamera.transform;
            }
        }

        /// <summary>
        /// Adds camera trauma, weighted by distance from the tremor epicenter.
        /// </summary>
        public void AddTraumaFromEpicenter(Vector3 epicenter, float amount)
        {
            ResolveCamera();
            if (_camTransform == null) return;

            float dist = Vector3.Distance(_camTransform.position, epicenter);
            float distFactor = 1f - Mathf.Clamp01((dist - minShakeDistance) / (maxShakeDistance - minShakeDistance));
            trauma = Mathf.Clamp01(trauma + amount * distFactor);
        }

        public void AddDirectTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + amount);
        }

        private void LateUpdate()
        {
            if (_camTransform == null)
            {
                ResolveCamera();
                if (_camTransform == null) return;
            }

            // Always cleanly undo previously applied shake delta first so base player/Cinemachine transform is never corrupted
            if (_appliedPosOffset != Vector3.zero)
            {
                _camTransform.localPosition -= _appliedPosOffset;
                _appliedPosOffset = Vector3.zero;
            }
            if (_appliedRotOffset != Quaternion.identity)
            {
                _camTransform.localRotation = _camTransform.localRotation * Quaternion.Inverse(_appliedRotOffset);
                _appliedRotOffset = Quaternion.identity;
            }

            if (trauma > 0.001f)
            {
                float shake = trauma * trauma; // Non-linear response (feels weightier)

                float time = Time.time * frequency;
                float yaw = (Mathf.PerlinNoise(_seed, time) * 2f - 1f) * maxPitchYawRoll * shake;
                float pitch = (Mathf.PerlinNoise(_seed + 1f, time) * 2f - 1f) * maxPitchYawRoll * shake;
                float roll = (Mathf.PerlinNoise(_seed + 2f, time) * 2f - 1f) * (maxPitchYawRoll * 0.5f) * shake;

                float offsetX = (Mathf.PerlinNoise(_seed + 3f, time) * 2f - 1f) * maxPositionOffset * shake;
                float offsetY = (Mathf.PerlinNoise(_seed + 4f, time) * 2f - 1f) * maxPositionOffset * shake;

                _appliedRotOffset = Quaternion.Euler(pitch, yaw, roll);
                _appliedPosOffset = new Vector3(offsetX, offsetY, 0f);

                _camTransform.localRotation = _camTransform.localRotation * _appliedRotOffset;
                _camTransform.localPosition += _appliedPosOffset;

                trauma = Mathf.Clamp01(trauma - Time.deltaTime * traumaDecayRate);
            }
        }
    }
}
