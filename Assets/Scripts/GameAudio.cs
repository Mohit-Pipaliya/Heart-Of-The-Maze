using UnityEngine;

/// <summary>
/// GameAudio
/// =========
/// Centralized AAA Production Audio Engine for Heart of the Maze.
/// 
/// Features:
/// 1. Guaranteed Direct Disk Loader (Loads 44.1kHz Stereo 16-bit uncompressed WAVs 
///    directly from disk, completely bypassing any Unity metadata / import pipeline lag).
/// 2. Anti-Phase Protection: Prevents duplicate triggers on the same frame to eliminate 
///    flanging / hollow comb filtering.
/// 3. Standard fallback to Resources.Load & AssetDatabase.
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
        BulletGround,
        Footstep,
        Jump,
        Land,
        Pickup,
        Equip,
        Paddle,
        Hit,
        Death,
        UI,
        ZombieScream,
        ZombieAttack,
        ZombieHurt,
        ZombieDeath,
        ZombieGroan
    }

    private static AudioClip _gunshotClip;
    private static AudioClip _swordSwingClip;
    private static AudioClip _swordClashClip;
    private static AudioClip _swordFleshClip;
    private static AudioClip _spiderAttackClip;
    private static AudioClip _spiderHurtClip;
    private static AudioClip _bulletFleshClip;
    private static AudioClip _bulletGroundClip;
    private static AudioClip _footstepClip;
    private static AudioClip _jumpClip;
    private static AudioClip _landClip;
    private static AudioClip _pickupClip;
    private static AudioClip _equipClip;
    private static AudioClip _paddleClip;
    private static AudioClip _hitClip;
    private static AudioClip _deathClip;
    private static AudioClip _uiClip;
    private static AudioClip _zombieScreamClip;
    private static AudioClip _zombieAttackClip;
    private static AudioClip _zombieHurtClip;
    private static AudioClip _zombieDeathClip;
    private static AudioClip _zombieGroanClip;

    private static AudioSource[] _pool;
    private static int _poolIndex;
    private const int PoolSize = 16;

    // Anti-phasing timestamps to prevent double-trigger comb filtering
    private static float _lastGunshotTime = -1f;
    private static float _lastSwordSwingTime = -1f;
    private static float _lastSwordImpactTime = -1f;
    private static float _lastSpiderAttackTime = -1f;
    private static float _lastSpiderHurtTime = -1f;
    private static float _lastBulletHitTime = -1f;
    private static float _lastFootstepTime = -1f;
    private static float _lastPaddleTime = -1f;

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
        _footstepClip = _jumpClip = _landClip = _pickupClip = _equipClip = null;
        _paddleClip = _hitClip = _deathClip = _uiClip = null;
        _pool = null;

        _lastGunshotTime = -1f;
        _lastSwordSwingTime = -1f;
        _lastSwordImpactTime = -1f;
        _lastSpiderAttackTime = -1f;
        _lastSpiderHurtTime = -1f;
        _lastBulletHitTime = -1f;
        _lastFootstepTime = -1f;
        _lastPaddleTime = -1f;

        // Force-preload core combat audio on startup
        PreloadCoreAudio();
    }

    public static void PreloadCoreAudio()
    {
        GetClip(SoundType.Gunshot);
        GetClip(SoundType.SwordSwing);
        GetClip(SoundType.SwordClash);
        GetClip(SoundType.SwordFlesh);
        GetClip(SoundType.SpiderAttack);
        GetClip(SoundType.SpiderHurt);
        GetClip(SoundType.BulletFlesh);
        GetClip(SoundType.BulletGround);
    }

    public static AudioClip GetClip(SoundType type)
    {
        switch (type)
        {
            case SoundType.Gunshot:
                if (_gunshotClip == null) _gunshotClip = LoadAudioClip("Gunshot");
                return _gunshotClip;

            case SoundType.SwordSwing:
                if (_swordSwingClip == null) _swordSwingClip = LoadAudioClip("SwordSwing");
                return _swordSwingClip;

            case SoundType.SwordClash:
                if (_swordClashClip == null) _swordClashClip = LoadAudioClip("SwordClash");
                return _swordClashClip;

            case SoundType.SwordFlesh:
                if (_swordFleshClip == null) _swordFleshClip = LoadAudioClip("BulletFlesh");
                return _swordFleshClip;

            case SoundType.SpiderAttack:
                if (_spiderAttackClip == null) _spiderAttackClip = LoadAudioClip("SpiderAttack");
                return _spiderAttackClip;

            case SoundType.SpiderHurt:
                if (_spiderHurtClip == null) _spiderHurtClip = LoadAudioClip("SpiderHurt");
                return _spiderHurtClip;

            case SoundType.BulletFlesh:
                if (_bulletFleshClip == null) _bulletFleshClip = LoadAudioClip("BulletFlesh");
                return _bulletFleshClip;

            case SoundType.BulletGround:
                if (_bulletGroundClip == null) _bulletGroundClip = LoadAudioClip("BulletGround");
                return _bulletGroundClip;

            case SoundType.Footstep:
                if (_footstepClip == null) _footstepClip = MakeProcedural("Footstep", 0.09f, 90f, 0.35f);
                return _footstepClip;
            case SoundType.Jump:
                if (_jumpClip == null) _jumpClip = MakeProcedural("Jump", 0.12f, 180f, 0.4f);
                return _jumpClip;
            case SoundType.Land:
                if (_landClip == null) _landClip = MakeProcedural("Land", 0.14f, 70f, 0.55f);
                return _landClip;
            case SoundType.Pickup:
                if (_pickupClip == null) _pickupClip = MakeProcedural("Pickup", 0.16f, 420f, 0.35f);
                return _pickupClip;
            case SoundType.Equip:
                if (_equipClip == null) _equipClip = MakeProcedural("Equip", 0.18f, 260f, 0.4f);
                return _equipClip;
            case SoundType.Paddle:
                if (_paddleClip == null) _paddleClip = MakeProcedural("Paddle", 0.22f, 110f, 0.45f);
                return _paddleClip;
            case SoundType.Hit:
                if (_hitClip == null) _hitClip = LoadAudioClip("BulletFlesh") ?? MakeProcedural("Hit", 0.2f, 140f, 0.6f);
                return _hitClip;
            case SoundType.Death:
                if (_deathClip == null) _deathClip = MakeProcedural("Death", 0.45f, 80f, 0.7f);
                return _deathClip;
            case SoundType.UI:
                if (_uiClip == null) _uiClip = MakeProcedural("UI", 0.06f, 880f, 0.25f);
                return _uiClip;
            case SoundType.ZombieScream:
                if (_zombieScreamClip == null) _zombieScreamClip = LoadAudioClip("ZombieScream") ?? MakeProcedural("ZombieScream", 1.2f, 130f, 0.75f);
                return _zombieScreamClip;
            case SoundType.ZombieAttack:
                if (_zombieAttackClip == null) _zombieAttackClip = LoadAudioClip("ZombieAttack") ?? MakeProcedural("ZombieAttack", 0.5f, 110f, 0.65f);
                return _zombieAttackClip;
            case SoundType.ZombieHurt:
                if (_zombieHurtClip == null) _zombieHurtClip = LoadAudioClip("ZombieHurt") ?? MakeProcedural("ZombieHurt", 0.35f, 150f, 0.6f);
                return _zombieHurtClip;
            case SoundType.ZombieDeath:
                if (_zombieDeathClip == null) _zombieDeathClip = LoadAudioClip("ZombieDeath") ?? MakeProcedural("ZombieDeath", 0.9f, 75f, 0.8f);
                return _zombieDeathClip;
            case SoundType.ZombieGroan:
                if (_zombieGroanClip == null) _zombieGroanClip = LoadAudioClip("ZombieGroan") ?? MakeProcedural("ZombieGroan", 0.8f, 95f, 0.45f);
                return _zombieGroanClip;

            default:
                return null;
        }
    }

    private static AudioClip LoadAudioClip(string filename)
    {
        AudioClip clip = null;

        // 1. Recursive High-Fidelity Disk Loader (Checks Assets/Audio and all subfolders Gun, Sword, Spider, zombie)
        try
        {
            string audioRoot = System.IO.Path.Combine(Application.dataPath, "Audio");
            if (System.IO.Directory.Exists(audioRoot))
            {
                string[] files = System.IO.Directory.GetFiles(audioRoot, filename + ".wav", System.IO.SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    clip = LoadWavDirectlyFromDisk(files[0], filename);
                }
            }
        }
        catch { }

        if (clip == null)
        {
            try
            {
                string resRoot = System.IO.Path.Combine(Application.dataPath, "Resources", "Audio");
                if (System.IO.Directory.Exists(resRoot))
                {
                    string[] files = System.IO.Directory.GetFiles(resRoot, filename + ".wav", System.IO.SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        clip = LoadWavDirectlyFromDisk(files[0], filename);
                    }
                }
            }
            catch { }
        }

        // 2. Standard Resources.Load fallback
        if (clip == null)
        {
            clip = Resources.Load<AudioClip>("Audio/" + filename);
        }

#if UNITY_EDITOR
        // 3. AssetDatabase fallback (in editor)
        if (clip == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(filename + " t:AudioClip");
            if (guids.Length > 0)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            }
        }
#endif

        return clip;
    }

    // ─── Playback Methods with Anti-Phase & Pitch Variation ────────────────

    public static void PlayGunshot(Vector3 pos, AudioSource source = null, float volume = 1.0f)
    {
        if (Time.time - _lastGunshotTime < 0.05f) return; // Prevent duplicate frame trigger
        _lastGunshotTime = Time.time;

        AudioClip clip = GetClip(SoundType.Gunshot);
        if (clip == null) return;

        if (source != null)
        {
            source.pitch = Random.Range(0.97f, 1.03f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.97f, 1.03f, 0.3f);
        }
    }

    public static void PlaySwordSwing(Vector3 pos, AudioSource source = null, float volume = 1.0f)
    {
        if (Time.time - _lastSwordSwingTime < 0.08f) return; // Prevent double swing trigger
        _lastSwordSwingTime = Time.time;

        AudioClip clip = GetClip(SoundType.SwordSwing);
        if (clip == null) return;

        if (source != null)
        {
            source.pitch = Random.Range(0.96f, 1.04f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.96f, 1.04f, 0.25f);
        }
    }

    public static void PlaySwordImpact(Vector3 pos, bool isEnemy, float volume = 1.2f)
    {
        if (Time.time - _lastSwordImpactTime < 0.06f) return; // Prevent double impact trigger
        _lastSwordImpactTime = Time.time;

        AudioClip clip = isEnemy ? GetClip(SoundType.SwordFlesh) : GetClip(SoundType.SwordClash);
        if (clip == null) return;

        PlayAtPoint(clip, pos, volume, 0.96f, 1.04f, 0.35f);
    }

    public static void PlaySpiderAttack(Vector3 pos, AudioSource source = null, float volume = 1.1f)
    {
        if (Time.time - _lastSpiderAttackTime < 0.15f) return;
        _lastSpiderAttackTime = Time.time;

        AudioClip clip = GetClip(SoundType.SpiderAttack);
        if (clip == null) return;

        if (source != null)
        {
            source.pitch = Random.Range(0.95f, 1.05f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.95f, 1.05f, 0.4f);
        }
    }

    public static void PlaySpiderHurt(Vector3 pos, AudioSource source = null, float volume = 1.2f)
    {
        if (Time.time - _lastSpiderHurtTime < 0.10f) return;
        _lastSpiderHurtTime = Time.time;

        AudioClip clip = GetClip(SoundType.SpiderHurt);
        if (clip == null) return;

        if (source != null)
        {
            source.pitch = Random.Range(0.96f, 1.04f);
            source.PlayOneShot(clip, volume);
        }
        else
        {
            PlayAtPoint(clip, pos, volume, 0.96f, 1.04f, 0.4f);
        }
    }

    public static void PlayBulletHit(Vector3 pos, bool isEnemy, float volume = 1.1f)
    {
        if (Time.time - _lastBulletHitTime < 0.03f) return;
        _lastBulletHitTime = Time.time;

        AudioClip clip = isEnemy ? GetClip(SoundType.BulletFlesh) : GetClip(SoundType.BulletGround);
        if (clip == null) return;

            PlayAtPoint(clip, pos, volume, 0.95f, 1.05f, 0.35f);
    }

    public static void PlayFootstep(Vector3 pos, float volume = 0.28f)
    {
        if (Time.time - _lastFootstepTime < 0.22f) return;
        _lastFootstepTime = Time.time;
        AudioClip clip = GetClip(SoundType.Footstep);
        PlayAtPoint(clip, pos, volume, 0.88f, 1.12f, 0.45f);
    }

    public static void PlayJump(Vector3 pos) => PlayAtPoint(GetClip(SoundType.Jump), pos, 0.45f, 0.95f, 1.08f, 0.3f);
    public static void PlayLand(Vector3 pos) => PlayAtPoint(GetClip(SoundType.Land), pos, 0.55f, 0.9f, 1.05f, 0.4f);
    public static void PlayPickup(Vector3 pos)
    {
        // Pickup sounds removed as requested by user
    }
    public static void PlayEquip(Vector3 pos) => PlayAtPoint(GetClip(SoundType.Equip), pos, 0.45f, 0.94f, 1.06f, 0.25f);
    public static void PlayHit(Vector3 pos) => PlayAtPoint(GetClip(SoundType.Hit), pos, 0.7f, 0.92f, 1.08f, 0.35f);
    public static void PlayDeath(Vector3 pos) => PlayAtPoint(GetClip(SoundType.Death), pos, 0.8f, 0.85f, 1.0f, 0.4f);
    public static void PlayUI() => PlayAtPoint(GetClip(SoundType.UI), Vector3.zero, 0.35f, 0.98f, 1.04f, 0f);

    public static void PlayPaddle(Vector3 pos, float volume = 0.4f)
    {
        if (Time.time - _lastPaddleTime < 0.35f) return;
        _lastPaddleTime = Time.time;
        PlayAtPoint(GetClip(SoundType.Paddle), pos, volume, 0.9f, 1.1f, 0.55f);
    }

    public static void PlayZombieScream(Vector3 pos, AudioSource source = null)
    {
        AudioClip clip = GetClip(SoundType.ZombieScream);
        if (source != null) { source.pitch = Random.Range(0.92f, 1.05f); source.PlayOneShot(clip, 1.0f); }
        else PlayAtPoint(clip, pos, 0.95f, 0.92f, 1.05f, 0.55f);
    }

    public static void PlayZombieAttack(Vector3 pos, AudioSource source = null)
    {
        AudioClip clip = GetClip(SoundType.ZombieAttack);
        if (source != null) { source.pitch = Random.Range(0.95f, 1.08f); source.PlayOneShot(clip, 0.9f); }
        else PlayAtPoint(clip, pos, 0.85f, 0.95f, 1.08f, 0.45f);
    }

    public static void PlayZombieHurt(Vector3 pos, AudioSource source = null)
    {
        AudioClip clip = GetClip(SoundType.ZombieHurt);
        if (source != null) { source.pitch = Random.Range(0.9f, 1.1f); source.PlayOneShot(clip, 0.85f); }
        else PlayAtPoint(clip, pos, 0.8f, 0.9f, 1.1f, 0.45f);
    }

    public static void PlayZombieDeath(Vector3 pos, AudioSource source = null)
    {
        AudioClip clip = GetClip(SoundType.ZombieDeath);
        if (source != null) { source.pitch = Random.Range(0.85f, 0.98f); source.PlayOneShot(clip, 1.0f); }
        else PlayAtPoint(clip, pos, 0.95f, 0.85f, 0.98f, 0.5f);
    }

    public static void PlayZombieGroan(Vector3 pos, AudioSource source = null)
    {
        AudioClip clip = GetClip(SoundType.ZombieGroan);
        if (source != null) { source.pitch = Random.Range(0.88f, 1.05f); source.PlayOneShot(clip, 0.6f); }
        else PlayAtPoint(clip, pos, 0.55f, 0.88f, 1.05f, 0.5f);
    }

    private static void EnsurePool()
    {
        if (_pool != null) return;
        _pool = new AudioSource[PoolSize];
        GameObject root = new GameObject("GameAudioPool");
        Object.DontDestroyOnLoad(root);
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject go = new GameObject("AudioSlot_" + i);
            go.transform.SetParent(root.transform);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0.35f;
            s.minDistance = 3f;
            s.maxDistance = 80f;
            s.rolloffMode = AudioRolloffMode.Linear;
            _pool[i] = s;
        }
    }

    private static void PlayAtPoint(AudioClip clip, Vector3 pos, float volume, float minPitch, float maxPitch, float spatialBlend = 0.35f)
    {
        if (clip == null) return;
        EnsurePool();
        AudioSource s = _pool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % PoolSize;
        if (s == null) return;
        s.transform.position = pos;
        s.spatialBlend = spatialBlend;
        s.pitch = Random.Range(minPitch, maxPitch);
        s.volume = volume;
        s.PlayOneShot(clip, volume);
    }

    private static AudioClip MakeProcedural(string clipName, float duration, float freq, float amplitude)
    {
        int sampleRate = 44100;
        int samples = Mathf.Max(64, Mathf.RoundToInt(duration * sampleRate));
        float[] data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float env = Mathf.Sin(t * Mathf.PI);
            env *= env;
            float noise = (Random.value * 2f - 1f);
            phase += (freq * (1f - t * 0.4f)) / sampleRate;
            float tone = Mathf.Sin(phase * 2f * Mathf.PI);
            data[i] = (tone * 0.55f + noise * 0.45f) * env * amplitude;
        }
        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ─── Direct Binary WAV Parser (Guaranteed disk loading) ────────────────

    private static AudioClip LoadWavDirectlyFromDisk(string filepath, string clipName)
    {
        if (string.IsNullOrEmpty(filepath) || !System.IO.File.Exists(filepath)) return null;

        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(filepath);
            if (bytes.Length < 44) return null;

            // Check RIFF header
            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F') return null;

            // Find "fmt " chunk
            int fmtIdx = -1;
            for (int i = 12; i < bytes.Length - 8; i++)
            {
                if (bytes[i] == 'f' && bytes[i + 1] == 'm' && bytes[i + 2] == 't' && bytes[i + 3] == ' ')
                {
                    fmtIdx = i;
                    break;
                }
            }
            if (fmtIdx == -1) return null;

            int channels = System.BitConverter.ToInt16(bytes, fmtIdx + 10);
            int sampleRate = System.BitConverter.ToInt32(bytes, fmtIdx + 12);
            int bitsPerSample = System.BitConverter.ToInt16(bytes, fmtIdx + 22);

            // Find "data" chunk
            int dataIdx = -1;
            for (int i = fmtIdx + 8; i < bytes.Length - 8; i++)
            {
                if (bytes[i] == 'd' && bytes[i + 1] == 'a' && bytes[i + 2] == 't' && bytes[i + 3] == 'a')
                {
                    dataIdx = i;
                    break;
                }
            }
            if (dataIdx == -1) return null;

            int dataSize = System.BitConverter.ToInt32(bytes, dataIdx + 4);
            int dataStart = dataIdx + 8;
            int dataEnd = Mathf.Min(dataStart + dataSize, bytes.Length);
            int bytesPerSample = bitsPerSample / 8;
            int sampleCount = (dataEnd - dataStart) / bytesPerSample;
            float[] samples = new float[sampleCount];

            if (bitsPerSample == 16)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short val = System.BitConverter.ToInt16(bytes, dataStart + i * 2);
                    samples[i] = val / 32768.0f;
                }
            }
            else if (bitsPerSample == 8)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] = (bytes[dataStart + i] - 128) / 128.0f;
                }
            }
            else
            {
                return null;
            }

            int totalFrames = sampleCount / channels;
            AudioClip clip = AudioClip.Create(clipName, totalFrames, channels, sampleRate, false);
            clip.SetData(samples, 0);
            Debug.Log($"<color=#00FF66><b>[GameAudio]</b></color> Loaded authentic AAA audio '{clipName}' directly from: {filepath} ({channels}ch, {sampleRate}Hz, {totalFrames} frames)");
            return clip;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameAudio] Direct WAV load failed for {filepath}: {ex.Message}");
            return null;
        }
    }
}
