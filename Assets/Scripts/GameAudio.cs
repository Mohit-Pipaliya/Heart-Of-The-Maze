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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ResetCache()
    {
        _gunshotClip = null;
        _swordSwingClip = null;
        _swordClashClip = null;
        _swordFleshClip = null;
        _spiderAttackClip = null;
        _spiderHurtClip = null;
        _bulletFleshClip = null;
        _bulletGroundClip = null;
    }

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

    // ─── High-Grade Procedural Audio Synthesizers (Studio Saturated Fallback) ─

    private static AudioClip SynthesizeGunshot()
    {
        int sampleRate = 44100;
        float duration = 0.55f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            // 1. Initial sharp explosive transient crack (first 8ms)
            float crack = (t < 0.04f) ? (Random.value * 2f - 1f) * Mathf.Exp(-t * 300f) * 1.8f : 0f;
            // 2. Chamber & bolt mechanical snap
            float bolt = (t > 0.01f && t < 0.04f) ? Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-Mathf.Pow(t - 0.02f, 2f) * 50000f) * 0.4f : 0f;
            // 3. Heavy saturated sub-bass boom (65Hz -> 45Hz)
            float freq = 65f - t * 40f;
            float bass = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 18f) * 1.4f;
            // 4. Punchy mid rumble
            float punch = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 28f) * 0.7f;
            // 5. Environmental blast tail
            float tail = (Random.value * 2f - 1f) * Mathf.Exp(-t * 11f) * 0.55f;

            float raw = (crack + bolt + bass + punch + tail) * 1.3f;
            samples[i] = Mathf.Clamp((float)System.Math.Tanh(raw), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_Gunshot", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeSwordSwing()
    {
        int sampleRate = 44100;
        float duration = 0.28f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        float lp = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float p = t / duration;
            float env = Mathf.Pow(Mathf.Sin(p * Mathf.PI), 1.8f);
            float noise = Random.value * 2f - 1f;
            float cutoff = 400f + 2200f * Mathf.Sin(p * Mathf.PI);
            float alpha = (2f * Mathf.PI * cutoff / sampleRate) / (1f + 2f * Mathf.PI * cutoff / sampleRate);
            lp += alpha * (noise - lp);
            float flutter = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.25f * env;
            float raw = (lp * 2.5f + flutter) * env;
            samples[i] = Mathf.Clamp((float)System.Math.Tanh(raw), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SwordSwing", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeImpact(bool flesh)
    {
        int sampleRate = 44100;
        float duration = flesh ? 0.30f : 0.38f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            if (flesh)
            {
                // Deep low sub thump + wet bone crunch
                float thump = Mathf.Sin(2f * Mathf.PI * 75f * t) * Mathf.Exp(-t * 22f) * 1.2f;
                float crunch = (Random.value * 2f - 1f) * Mathf.Exp(-t * 32f) * 0.85f;
                float squish = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t * 18f) * 0.4f;
                samples[i] = Mathf.Clamp((float)System.Math.Tanh(thump + crunch + squish), -1f, 1f);
            }
            else
            {
                // Sharp steel-on-steel impact transient + multi-harmonic metallic ring
                float snap = (t < 0.02f) ? (Random.value * 2f - 1f) * Mathf.Exp(-t * 400f) * 1.5f : 0f;
                float h1 = Mathf.Sin(2f * Mathf.PI * 840f * t) * Mathf.Exp(-t * 16f) * 0.6f;
                float h2 = Mathf.Sin(2f * Mathf.PI * 1420f * t) * Mathf.Exp(-t * 20f) * 0.5f;
                float h3 = Mathf.Sin(2f * Mathf.PI * 2380f * t) * Mathf.Exp(-t * 26f) * 0.4f;
                float h4 = Mathf.Sin(2f * Mathf.PI * 4100f * t) * Mathf.Exp(-t * 35f) * 0.3f;
                float ring = (h1 + h2 + h3 + h4);
                samples[i] = Mathf.Clamp((float)System.Math.Tanh(snap + ring * 1.2f), -1f, 1f);
            }
        }

        AudioClip clip = AudioClip.Create(flesh ? "Proc_FleshHit" : "Proc_MetalClash", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeSpiderAttack()
    {
        int sampleRate = 44100;
        float duration = 0.45f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float mandible = ((t > 0.02f && t < 0.04f) || (t > 0.08f && t < 0.10f) || (t > 0.15f && t < 0.17f))
                ? (Random.value * 2f - 1f) * 1.2f
                : 0f;
            float hiss = (Random.value * 2f - 1f) * Mathf.Exp(-t * 6f) * 0.55f;
            float growl = Mathf.Sin(2f * Mathf.PI * (85f + Mathf.Sin(2f * Mathf.PI * 25f * t) * 30f) * t) * Mathf.Exp(-t * 7f) * 0.7f;
            samples[i] = Mathf.Clamp((float)System.Math.Tanh(mandible + hiss + growl), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SpiderAttack", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip SynthesizeSpiderHurt()
    {
        int sampleRate = 44100;
        float duration = 0.35f;
        int count = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            float crack = (t < 0.02f) ? (Random.value * 2f - 1f) * Mathf.Exp(-t * 220f) * 1.5f : 0f;
            float mod = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * 40f * t);
            float screech = Mathf.Sin(2f * Mathf.PI * (520f - t * 450f) * t) * Mathf.Exp(-t * 12f) * 0.6f * mod;
            float squelch = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.5f;
            samples[i] = Mathf.Clamp((float)System.Math.Tanh(crack + screech + squelch), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Proc_SpiderHurt", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
