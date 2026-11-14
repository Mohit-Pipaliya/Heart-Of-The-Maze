using System;
using UnityEngine;

namespace MazeSystem
{
    /// <summary>
    /// AAA 3D spatial audio controller for the Shifting Maze.
    /// Provides stone scraping, deep subterranean rumbles, and seismic lock-in thuds.
    /// Includes real-time procedural audio synthesis fallback if audio clips are not assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MazeAudioController : MonoBehaviour
    {
        [Header("── Audio Clips (Optional, Procedural Fallback included) ──")]
        public AudioClip stoneGrindClip;
        public AudioClip subterraneanRumbleClip;
        public AudioClip blockLockThudClip;
        public AudioClip doorSlideClip;

        [Header("── Volume & Mixing ─────────────────────────────")]
        [Range(0f, 1f)] public float masterVolume = 0.85f;
        [Range(0f, 1f)] public float rumbleVolume = 0.75f;
        [Range(0f, 1f)] public float thudVolume = 0.9f;

        [Header("── Spatial Settings ────────────────────────────")]
        public float minDistance = 20f;
        public float maxDistance = 600f;

        private AudioSource _scrapeSource;
        private AudioSource _rumbleSource;
        private AudioSource _oneshotSource;

        private void Awake()
        {
            SetupSources();
        }

        private void SetupSources()
        {
            if (_scrapeSource == null)
            {
                _scrapeSource = gameObject.AddComponent<AudioSource>();
                Configure3DSource(_scrapeSource, true);
            }
            if (_rumbleSource == null)
            {
                _rumbleSource = gameObject.AddComponent<AudioSource>();
                Configure3DSource(_rumbleSource, true);
            }
            if (_oneshotSource == null)
            {
                _oneshotSource = gameObject.AddComponent<AudioSource>();
                Configure3DSource(_oneshotSource, false);
            }
        }

        private void Configure3DSource(AudioSource src, bool loop)
        {
            src.spatialBlend = 1f; // full 3D
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.playOnAwake = false;
            src.loop = loop;
        }

        /// <summary>
        /// Begins playing stone scrape & rumble during block travel.
        /// </summary>
        public void StartTravelAudio(Vector3 position)
        {
            SetupSources();
            transform.position = position;

            if (subterraneanRumbleClip != null)
            {
                _rumbleSource.clip = subterraneanRumbleClip;
                _rumbleSource.volume = masterVolume * rumbleVolume;
                _rumbleSource.Play();
            }
            else
            {
                // Procedural low-frequency brownian rumble fallback
                PlayProceduralRumble(true);
            }

            if (stoneGrindClip != null)
            {
                _scrapeSource.clip = stoneGrindClip;
                _scrapeSource.volume = masterVolume * 0.7f;
                _scrapeSource.Play();
            }
        }

        /// <summary>
        /// Updates 3D audio emitter position as blocks move.
        /// </summary>
        public void UpdateAudioPosition(Vector3 position)
        {
            transform.position = position;
        }

        /// <summary>
        /// Stops continuous scrape & rumble audio and triggers heavy lock-in thud.
        /// </summary>
        public void StopTravelAudioAndPlayLock(Vector3 lockPosition)
        {
            SetupSources();
            transform.position = lockPosition;

            if (_scrapeSource != null && _scrapeSource.isPlaying) _scrapeSource.Stop();
            if (_rumbleSource != null && _rumbleSource.isPlaying) _rumbleSource.Stop();

            if (blockLockThudClip != null)
            {
                _oneshotSource.PlayOneShot(blockLockThudClip, masterVolume * thudVolume);
            }
            else
            {
                // Generate procedural impact thud
                AudioClip thud = CreateProceduralThud();
                _oneshotSource.PlayOneShot(thud, masterVolume * thudVolume);
            }
        }

        private void PlayProceduralRumble(bool play)
        {
            if (play)
            {
                if (_rumbleSource.clip == null)
                    _rumbleSource.clip = CreateProceduralRumbleClip();
                _rumbleSource.volume = masterVolume * rumbleVolume;
                _rumbleSource.Play();
            }
            else
            {
                if (_rumbleSource != null) _rumbleSource.Stop();
            }
        }

        private static AudioClip CreateProceduralRumbleClip()
        {
            int sampleRate = 44100;
            int length = sampleRate * 3; // 3 second loop
            float[] data = new float[length];
            float lastVal = 0f;

            for (int i = 0; i < length; i++)
            {
                // Low-pass filtered noise (brownian rumble)
                float white = (UnityEngine.Random.value * 2f - 1f);
                lastVal = Mathf.Lerp(lastVal, white, 0.04f);
                float sub = Mathf.Sin(2f * Mathf.PI * 38f * (i / (float)sampleRate)) * 0.35f;
                data[i] = (lastVal + sub) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Procedural_Rumble", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateProceduralThud()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.8f);
            float[] data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 6f);
                float freq = Mathf.Lerp(65f, 25f, t);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.4f;
                data[i] = (wave + noise) * env;
            }

            AudioClip clip = AudioClip.Create("Procedural_Thud", length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
