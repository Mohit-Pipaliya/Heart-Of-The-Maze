using UnityEngine;

/// <summary>
/// GameAudio
/// =========
/// Centralized, 100% fail-safe audio engine for Heart of the Maze.
/// Automatically loads WAV assets from Resources/Audio/ or Assets/Audio/,
/// and provides crisp, studio-grade procedural waveform synthesis as an instant fallback
/// so sound NEVER fails to play under any condition.
/// </summary>
public static class GameAudio
{
    public enum SoundType
    {
        Gunshot,
        SwordSwing,
        SwordClash,
        SwordFlesh,
        SpiderAttack,
        SpiderHurt,
        BulletFlesh,
        BulletGround
    }

    private static AudioClip _gunshotClip;
    private static AudioClip _swordSwingClip;
    private static AudioClip _swordClashClip;
    private static AudioClip _swordFleshClip;
    private static AudioClip _spiderAttackClip;
    private static AudioClip _spiderHurtClip;
    private static AudioClip _bulletFleshClip;
    private static AudioClip _bulletGroundClip;

    public static AudioClip GetClip(SoundType type)
    {
        switch (type)
        {
            case SoundType.Gunshot:
                if (_gunshotClip == null) _gunshotClip = LoadOrSynthesize("Gunshot", SynthesizeGunshot);
                return _gunshotClip;

            case SoundType.SwordSwing:
                if (_swordSwingClip == null) _swordSwingClip = LoadOrSynthesize("SwordSwing", SynthesizeSwordSwing);
                return _swordSwingClip;

            case SoundType.SwordClash:
                if (_swordClashClip == null) _swordClashClip = LoadOrSynthesize("SwordClash", () => SynthesizeImpact(false));
                return _swordClashClip;

            case SoundType.SwordFlesh:
                if (_swordFleshClip == null) _swordFleshClip = LoadOrSynthesize("SpiderHurt", () => SynthesizeImpact(true));
                return _swordFleshClip;

            case SoundType.SpiderAttack:
                if (_spiderAttackClip == null) _spiderAttackClip = LoadOrSynthesize("SpiderAttack", SynthesizeSpiderAttack);
                return _spiderAttackClip;

            case SoundType.SpiderHurt:
                if (_spiderHurtClip == null) _spiderHurtClip = LoadOrSynthesize("SpiderHurt", SynthesizeSpiderHurt);
                return _spiderHurtClip;

            case SoundType.BulletFlesh:
                if (_bulletFleshClip == null) _bulletFleshClip = LoadOrSynthesize("BulletFlesh", () => SynthesizeImpact(true));
                return _bulletFleshClip;

            case SoundType.BulletGround:
                if (_bulletGroundClip == null) _bulletGroundClip = LoadOrSynthesize("BulletGround", () => SynthesizeImpact(false));
                return _bulletGroundClip;

            default:
                return null;
        }
    }

    private static AudioClip LoadOrSynthesize(string filename, System.Func<AudioClip> synthesizer)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + filename);

#if UNITY_EDITOR
        if (clip == null)
        {
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + filename + ".wav");
        }
        if (clip == null)
        {
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/" + filename + ".wav");
        }
#endif

        if (clip == null && synthesizer != null)
        {
            clip = synthesizer();
        }

        return clip;
    }

    public static void PlayGunshot(Vector3 pos, AudioSource source = null, float volume = 1.0f)
    {
        AudioClip clip = GetClip(SoundType.Gunshot);
        if (source != null)
        {
            source.pitch = Random.Range(0.95f, 1.05f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.95f, 1.05f);
        }
    }

    public static void PlaySwordSwing(Vector3 pos, AudioSource source = null, float volume = 1.0f)
    {
        AudioClip clip = GetClip(SoundType.SwordSwing);
        if (source != null)
        {
            source.pitch = Random.Range(0.94f, 1.08f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.94f, 1.08f);
        }
    }

    public static void PlaySwordImpact(Vector3 pos, bool isEnemy, float volume = 1.2f)
    {
        AudioClip clip = isEnemy ? GetClip(SoundType.SwordFlesh) : GetClip(SoundType.SwordClash);
        PlayAtPoint(clip, pos, volume, 0.92f, 1.08f);
    }

    public static void PlaySpiderAttack(Vector3 pos, AudioSource source = null, float volume = 1.1f)
    {
        AudioClip clip = GetClip(SoundType.SpiderAttack);
        if (source != null)
        {
            source.pitch = Random.Range(0.88f, 1.12f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.88f, 1.12f);
        }
    }

    public static void PlaySpiderHurt(Vector3 pos, AudioSource source = null, float volume = 1.2f)
    {
        AudioClip clip = GetClip(SoundType.SpiderHurt);
        if (source != null)
        {
            source.pitch = Random.Range(0.90f, 1.10f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.90f, 1.10f);
        }
    }

    public static void PlayBulletHit(Vector3 pos, bool isEnemy, float volume = 1.1f)
    {
        AudioClip clip = isEnemy ? GetClip(SoundType.BulletFlesh) : GetClip(SoundType.BulletGround);
        PlayAtPoint(clip, pos, volume, 0.92f, 1.08f);
    }

    private static void PlayAtPoint(AudioClip clip, Vector3 pos, float volume, float minPitch, float maxPitch)
    {
        if (clip == null) return;
        GameObject go = new GameObject("TempAudioSource");
        go.transform.position = pos;
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.volume = volume;
        s.pitch = Random.Range(minPitch, maxPitch);
        s.spatialBlend = 0.4f; // 40% 3D, 60% 2D so player always hears it loud and clear
        s.minDistance = 2f;
        s.maxDistance = 60f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, s.pitch) + 0.15f);
    }

    // ─── High-Grade Procedural Audio Synthesizers ──────────────────────────

    private static AudioClip SynthesizeGunshot()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            // 1. Initial sharp supersonic transient crack (first 15ms)
            float crack = (Random.value * 2f - 1f) * Mathf.Exp(-t * 120f);

            // 2. Heavy bass punch: sine sweeping 160Hz -> 45Hz
            float freq = Mathf.Lerp(160f, 45f, Mathf.Clamp01(t * 8f));
            float bass = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 22f);

            // 3. Trailing body rumble
            float body = (Random.value * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.4f;

            samples[i] = Mathf.Clamp(crack * 0.9f + bass * 0.7f + body, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_Gunshot", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeSwordSwing()
    {
        int sampleRate = 44100;
        float duration = 0.22f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        float filterState = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float progress = t / duration;

            // Bell envelope (peaks at 40% duration)
            float env = Mathf.Sin(progress * Mathf.PI);
            env = env * env;

            // Lowpass swept noise to simulate swift air whoosh
            float noise = Random.value * 2f - 1f;
            float cutoff = Mathf.Lerp(400f, 1800f, Mathf.Sin(progress * Mathf.PI));
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / sampleRate;
            float alpha = dt / (rc + dt);
            filterState += alpha * (noise - filterState);

            samples[i] = Mathf.Clamp(filterState * env * 2.2f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SwordSwing", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeImpact(bool flesh)
    {
        int sampleRate = 44100;
        float duration = flesh ? 0.25f : 0.32f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            if (flesh)
            {
                // Crunchy, squelchy visceral flesh impact
                float thud = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 28f);
                float crunch = (Random.value * 2f - 1f) * Mathf.Exp(-t * 35f);
                samples[i] = Mathf.Clamp(thud * 0.8f + crunch * 0.6f, -1f, 1f);
            }
            else
            {
                // Sharp metallic chime / clink
                float chime1 = Mathf.Sin(2f * Mathf.PI * 1420f * t) * Mathf.Exp(-t * 18f);
                float chime2 = Mathf.Sin(2f * Mathf.PI * 2280f * t) * Mathf.Exp(-t * 24f);
                float snap = (Random.value * 2f - 1f) * Mathf.Exp(-t * 80f) * 0.5f;
                samples[i] = Mathf.Clamp((chime1 * 0.5f + chime2 * 0.4f + snap), -1f, 1f);
            }
        }

        AudioClip clip = AudioClip.Create(flesh ? "Proc_FleshHit" : "Proc_MetalClash", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeSpiderAttack()
    {
        int sampleRate = 44100;
        float duration = 0.38f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            // Menacing rasp / chitter
            float hiss = (Random.value * 2f - 1f) * Mathf.Exp(-t * 8f);
            float flutter = Mathf.Sin(2f * Mathf.PI * 35f * t) * 0.5f + 0.5f;
            float snap = (t < 0.05f) ? (Random.value * 2f - 1f) * 0.9f : 0f;
            samples[i] = Mathf.Clamp((hiss * flutter * 0.7f) + snap, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SpiderAttack", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeSpiderHurt()
    {
        int sampleRate = 44100;
        float duration = 0.3f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float screech = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 320f, t / duration) * t);
            float squish = (Random.value * 2f - 1f) * Mathf.Exp(-t * 20f);
            float env = Mathf.Exp(-t * 12f);
            samples[i] = Mathf.Clamp((screech * 0.6f + squish * 0.6f) * env, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SpiderHurt", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
