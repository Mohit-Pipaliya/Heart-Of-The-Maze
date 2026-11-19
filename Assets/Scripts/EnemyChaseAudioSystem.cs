using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class EnemyChaseAudioSystem : MonoBehaviour
{
    [Header("Zombie Audio")]
    [SerializeField] private AudioClip zombieChaseLoop;
    [SerializeField] private AudioClip zombieAttack;

    [Header("Spider Audio")]
    [SerializeField] private AudioClip spiderChaseLoop;
    [SerializeField] private AudioClip spiderAttack;

    [Header("Human Snake Chase and Attack")]
    [SerializeField] private AudioClip humanSnakeChaseLoop;
    [SerializeField] private AudioClip humanSnakeAttack;

    [Header("Human Snake Audio Library")]
    [SerializeField] private AudioClip[] humanSnakeAttack1Clips;
    [SerializeField] private AudioClip[] humanSnakeAttack2Clips;
    [SerializeField] private AudioClip[] humanSnakeAttack3Clips;
    [SerializeField] private AudioClip[] humanSnakeCast1Clips;
    [SerializeField] private AudioClip[] humanSnakeCast2Clips;
    [SerializeField] private AudioClip[] humanSnakeDeathClips;
    [SerializeField] private AudioClip[] humanSnakeDodgeClips;
    [SerializeField] private AudioClip[] humanSnakeHitClips;
    [SerializeField] private AudioClip[] humanSnakeChaseVariants;
    [SerializeField] private AudioClip[] humanSnakeIdleClips;
    [SerializeField] private AudioClip[] humanSnakeTailWhipClips;
    [SerializeField] private AudioClip[] humanSnakeTauntClips;

    [SerializeField, Min(0.1f)] private float fullVolumeDistance = 8f;
    [SerializeField, Min(1f)] private float maximumAudioDistance = 80f;

    private static readonly RaycastHit[] ObstacleHits = new RaycastHit[32];
    private static EnemyChaseAudioSystem _instance;

    private readonly Dictionary<int, EnemyAudioEntry> _audioEntries =
        new Dictionary<int, EnemyAudioEntry>();
    private readonly Dictionary<int, ChaseDestinationCache> _destinationCaches =
        new Dictionary<int, ChaseDestinationCache>();
    private readonly List<int> _staleEntries = new List<int>();
    private readonly HashSet<Type> _reportedMissingAudio = new HashSet<Type>();
    private readonly HashSet<int> _reportedLoadFailures = new HashSet<int>();
    private readonly HashSet<Type> _reportedPlayback = new HashSet<Type>();

    private float _nextEnemyScan;
    private bool _reportedSetup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToMazeManager()
    {
        MazeManager manager = FindFirstObjectByType<MazeManager>();
        if (manager != null && manager.GetComponent<EnemyChaseAudioSystem>() == null)
            manager.gameObject.AddComponent<EnemyChaseAudioSystem>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            enabled = false;
            return;
        }

        _instance = this;
        if (FindFirstObjectByType<AudioListener>() == null)
            Debug.LogError("[EnemyChaseAudioSystem] No active AudioListener was found in the scene.");
    }

    private void Update()
    {
        if (Time.time >= _nextEnemyScan)
        {
            ScanForEnemies();
            _nextEnemyScan = Time.time + 0.75f;
        }

        UpdateChaseAudio();
    }

    private void ScanForEnemies()
    {
        RegisterEnemies(
            FindObjectsByType<ZombieAI>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            zombieChaseLoop,
            zombieAttack);
        RegisterEnemies(
            FindObjectsByType<SpiderEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            spiderChaseLoop,
            spiderAttack);
        RegisterEnemies(
            FindObjectsByType<HumanSnakeEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            humanSnakeChaseLoop,
            humanSnakeAttack);
    }

    private void RegisterEnemies<T>(T[] enemies, AudioClip chaseClip, AudioClip attackClip)
        where T : MonoBehaviour
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            T enemy = enemies[i];
            if (enemy == null) continue;

            int id = enemy.GetInstanceID();
            if (_audioEntries.TryGetValue(id, out EnemyAudioEntry existing))
            {
                existing.attackClip = GetAttackClip(enemy, attackClip);
                if (existing.chaseClip == null)
                    existing.chaseClip = GetChaseClip(enemy, chaseClip);
                EnsureAudioDataLoaded(existing.chaseClip);
                EnsureAudioDataLoaded(existing.attackClip);
                continue;
            }

            AudioClip assignedChaseClip = GetChaseClip(enemy, chaseClip);
            AudioClip assignedAttackClip = GetAttackClip(enemy, attackClip);
            AudioSource loopSource = CreateSpatialSource(enemy.gameObject, true);
            AudioSource attackSource = CreateSpatialSource(enemy.gameObject, false);
            _audioEntries.Add(
                id,
                new EnemyAudioEntry(enemy, loopSource, attackSource, assignedChaseClip, assignedAttackClip));
            EnsureAudioDataLoaded(assignedChaseClip);
            EnsureAudioDataLoaded(assignedAttackClip);
            if (assignedChaseClip == null && _reportedMissingAudio.Add(typeof(T)))
                Debug.LogError($"[EnemyChaseAudioSystem] No chase audio clip is assigned for {typeof(T).Name}.");
        }

        if (!_reportedSetup && _audioEntries.Count > 0)
        {
            Debug.Log($"[EnemyChaseAudioSystem] Registered audio for {_audioEntries.Count} enemies.");
            _reportedSetup = true;
        }
    }

    private AudioSource CreateSpatialSource(GameObject owner, bool loop)
    {
        AudioSource source = owner.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = fullVolumeDistance;
        source.maxDistance = maximumAudioDistance;
        source.priority = 32;
        source.volume = 1f;
        return source;
    }

    private AudioClip GetChaseClip(MonoBehaviour enemy, AudioClip fallback)
    {
        if (enemy is SpiderEnemy spider)
            return spider.ChaseLoopClip != null ? spider.ChaseLoopClip : fallback;

        if (enemy is HumanSnakeEnemy humanSnake)
        {
            return SelectRandomClipFromPools(humanSnake.ChaseVariants, humanSnakeChaseVariants)
                   ?? humanSnake.ChaseLoopClip
                   ?? fallback;
        }

        return fallback;
    }

    private static AudioClip GetAttackClip(MonoBehaviour enemy, AudioClip fallback)
    {
        if (enemy is SpiderEnemy spider && spider.AttackClip != null)
            return spider.AttackClip;
        if (enemy is HumanSnakeEnemy humanSnake && humanSnake.AttackClip != null)
            return humanSnake.AttackClip;
        return fallback;
    }

    private static AudioClip SelectRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        int start = UnityEngine.Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip != null) return clip;
        }

        return null;
    }

    private bool EnsureAudioDataLoaded(AudioClip clip)
    {
        if (clip == null) return false;
        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        if (clip.loadState == AudioDataLoadState.Failed &&
            _reportedLoadFailures.Add(clip.GetInstanceID()))
        {
            Debug.LogError($"[EnemyChaseAudioSystem] Failed to load audio clip '{clip.name}'.");
        }

        return clip.loadState == AudioDataLoadState.Loaded;
    }

    private void UpdateChaseAudio()
    {
        Transform player = PlayerController.Instance != null
            ? PlayerController.Instance.transform
            : null;

        _staleEntries.Clear();
        foreach (KeyValuePair<int, EnemyAudioEntry> pair in _audioEntries)
        {
            EnemyAudioEntry entry = pair.Value;
            if (entry.enemy == null || entry.loopSource == null || entry.attackSource == null)
            {
                _staleEntries.Add(pair.Key);
                continue;
            }

            bool shouldPlay = player != null &&
                              entry.enemy.isActiveAndEnabled &&
                              IsChasing(entry.enemy) &&
                              Vector3.Distance(entry.enemy.transform.position, player.position) <= maximumAudioDistance &&
                              EnsureAudioDataLoaded(entry.chaseClip);

            if (!shouldPlay)
            {
                if (entry.loopSource.isPlaying)
                    entry.loopSource.Stop();
                continue;
            }

            if (entry.loopSource.clip != entry.chaseClip)
                entry.loopSource.clip = entry.chaseClip;
            if (!entry.loopSource.isPlaying)
            {
                entry.loopSource.Play();
                if (_reportedPlayback.Add(entry.enemy.GetType()))
                    Debug.Log($"[EnemyChaseAudioSystem] Chase audio started for {entry.enemy.GetType().Name}.");
            }
        }

        for (int i = 0; i < _staleEntries.Count; i++)
        {
            _audioEntries.Remove(_staleEntries[i]);
            _destinationCaches.Remove(_staleEntries[i]);
        }
    }

    private static bool IsChasing(MonoBehaviour enemy)
    {
        if (enemy is ZombieAI zombie)
        {
            return zombie.CurrentState == ZombieAI.State.Chase ||
                   zombie.CurrentState == ZombieAI.State.Raad ||
                   zombie.CurrentState == ZombieAI.State.Attack;
        }

        if (enemy is SpiderEnemy spider)
            return spider.IsChasing;

        if (enemy is HumanSnakeEnemy humanSnake)
            return humanSnake.IsChasing;

        return false;
    }

    public static void PlayAttack(MonoBehaviour enemy)
    {
        if (_instance == null || enemy == null) return;

        int id = enemy.GetInstanceID();
        if (!_instance._audioEntries.TryGetValue(id, out EnemyAudioEntry entry))
        {
            _instance.ScanForEnemies();
            if (!_instance._audioEntries.TryGetValue(id, out entry)) return;
        }

        AudioClip clip = enemy is HumanSnakeEnemy humanSnake
            ? _instance.SelectHumanSnakeAttackClip(humanSnake) ?? entry.attackClip
            : entry.attackClip;
        _instance.PlayOneShot(entry, clip);
    }

    public static void PlayHumanSnakeEvent(HumanSnakeEnemy enemy, string eventName)
    {
        if (_instance == null || enemy == null) return;

        int id = enemy.GetInstanceID();
        if (!_instance._audioEntries.TryGetValue(id, out EnemyAudioEntry entry))
        {
            _instance.ScanForEnemies();
            if (!_instance._audioEntries.TryGetValue(id, out entry)) return;
        }

        _instance.PlayOneShot(entry, _instance.SelectHumanSnakeEventClip(enemy, eventName));
    }

    private AudioClip SelectHumanSnakeAttackClip(HumanSnakeEnemy enemy)
    {
        AudioClip[][] pools =
        {
            enemy.Attack1Clips,
            enemy.Attack2Clips,
            enemy.Attack3Clips,
            enemy.TailWhipClips,
            humanSnakeAttack1Clips,
            humanSnakeAttack2Clips,
            humanSnakeAttack3Clips,
            humanSnakeTailWhipClips
        };
        return SelectRandomClipFromPools(pools);
    }

    private AudioClip SelectHumanSnakeCastClip(HumanSnakeEnemy enemy)
    {
        return SelectRandomClipFromPools(
            enemy.Cast1Clips,
            enemy.Cast2Clips,
            humanSnakeCast1Clips,
            humanSnakeCast2Clips);
    }

    private static AudioClip SelectRandomClipFromPools(params AudioClip[][] pools)
    {
        int start = UnityEngine.Random.Range(0, pools.Length);
        for (int i = 0; i < pools.Length; i++)
        {
            AudioClip clip = SelectRandomClip(pools[(start + i) % pools.Length]);
            if (clip != null) return clip;
        }

        return null;
    }

    private AudioClip SelectHumanSnakeEventClip(HumanSnakeEnemy enemy, string eventName)
    {
        string normalized = eventName ?? string.Empty;
        if (normalized.IndexOf("tailwhip", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("tail whip", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.TailWhipClips, humanSnakeTailWhipClips);
        if (normalized.IndexOf("attack1", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.Attack1Clips, humanSnakeAttack1Clips);
        if (normalized.IndexOf("attack2", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.Attack2Clips, humanSnakeAttack2Clips);
        if (normalized.IndexOf("attack3", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectHumanSnakeAttackClip(enemy);
        if (normalized.IndexOf("cast1", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.Cast1Clips, humanSnakeCast1Clips);
        if (normalized.IndexOf("cast2", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.Cast2Clips, humanSnakeCast2Clips);
        if (normalized.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectHumanSnakeCastClip(enemy);
        if (normalized.IndexOf("dodge", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.DodgeClips, humanSnakeDodgeClips);
        if (normalized.IndexOf("death", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.DeathClips, humanSnakeDeathClips);
        if (normalized.IndexOf("hit", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.HitClips, humanSnakeHitClips);
        if (normalized.IndexOf("taunt", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("chestbump", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("chest bump", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.TauntClips, humanSnakeTauntClips);
        if (normalized.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("inhale", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("exhale", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.IdleClips, humanSnakeIdleClips);
        if (normalized.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf("footstep", StringComparison.OrdinalIgnoreCase) >= 0)
            return SelectRandomClipFromPools(enemy.ChaseVariants, humanSnakeChaseVariants);

        return null;
    }

    private void PlayOneShot(EnemyAudioEntry entry, AudioClip clip)
    {
        if (entry.attackSource != null && EnsureAudioDataLoaded(clip))
            entry.attackSource.PlayOneShot(clip, 1f);
    }

    public static void SetChaseDestination(NavMeshAgent agent, Vector3 target)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

        if (_instance == null || !_instance.enabled)
        {
            agent.SetDestination(target);
            return;
        }

        _instance.SetChaseDestinationInternal(agent, target);
    }

    private void SetChaseDestinationInternal(NavMeshAgent agent, Vector3 target)
    {
        int id = agent.GetInstanceID();
        if (!_destinationCaches.TryGetValue(id, out ChaseDestinationCache cache))
        {
            cache = new ChaseDestinationCache();
            _destinationCaches.Add(id, cache);
        }

        bool targetMoved = (target - cache.lastTarget).sqrMagnitude > 1f;
        bool reachedWaypoint = agent.hasPath && !agent.pathPending &&
                               agent.remainingDistance <= Mathf.Max(agent.radius * 1.5f, 0.8f);
        if (Time.time >= cache.nextCheck || targetMoved || reachedWaypoint)
        {
            cache.lastTarget = target;
            cache.destination = ResolveChaseWaypoint(agent, target, cache.path);
            cache.nextCheck = Time.time + 0.3f;
        }

        agent.isStopped = false;
        if (!agent.hasPath || agent.isPathStale ||
            (!agent.pathPending && (agent.destination - cache.destination).sqrMagnitude > 0.36f))
        {
            agent.SetDestination(cache.destination);
        }
    }

    private Vector3 ResolveChaseWaypoint(NavMeshAgent agent, Vector3 target, NavMeshPath path)
    {
        if (NavMesh.SamplePosition(target, out NavMeshHit targetHit, 3f, agent.areaMask))
            target = targetHit.position;

        if (!TryFindBlockingObstacle(agent, target, out RaycastHit blocker))
            return target;

        if (agent.CalculatePath(target, path) &&
            path.status == NavMeshPathStatus.PathComplete &&
            path.corners.Length > 2)
        {
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                if ((corners[i] - agent.nextPosition).sqrMagnitude > 0.36f)
                    return corners[i];
            }
        }

        return FindSideStep(agent, target, blocker, path, out Vector3 sideStep)
            ? sideStep
            : target;
    }

    private static bool TryFindBlockingObstacle(
        NavMeshAgent agent,
        Vector3 target,
        out RaycastHit nearestHit)
    {
        nearestHit = default;
        float eyeHeight = Mathf.Clamp(agent.height * 0.55f, 0.7f, 1.35f);
        Vector3 origin = agent.nextPosition + Vector3.up * eyeHeight;
        Vector3 aimPoint = target + Vector3.up * 0.9f;
        Vector3 ray = aimPoint - origin;
        float distance = ray.magnitude - Mathf.Max(0.3f, agent.radius * 0.5f);
        if (distance <= 0.1f) return false;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            ray / ray.magnitude,
            ObstacleHits,
            distance,
            ~LayerMask.GetMask("Ignore Raycast"),
            QueryTriggerInteraction.Ignore);

        Transform player = PlayerController.Instance != null
            ? PlayerController.Instance.transform
            : null;
        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = ObstacleHits[i];
            if (hit.collider == null) continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == agent.transform || hitTransform.IsChildOf(agent.transform))
                continue;
            if (player != null &&
                (hitTransform == player || hitTransform.IsChildOf(player) || player.IsChildOf(hitTransform)))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private static bool FindSideStep(
        NavMeshAgent agent,
        Vector3 target,
        RaycastHit blocker,
        NavMeshPath path,
        out Vector3 bestSideStep)
    {
        bestSideStep = target;
        Vector3 forward = target - agent.nextPosition;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) return false;
        forward.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
        Bounds obstacleBounds = blocker.collider.bounds;
        float offset = Mathf.Max(agent.radius * 2.5f, 1.25f);
        Vector3 extents = obstacleBounds.extents;
        float forwardExtent = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.z) * extents.z;
        float sideExtent = Mathf.Abs(side.x) * extents.x + Mathf.Abs(side.z) * extents.z;
        Vector3 beforeObstacle = obstacleBounds.center - forward * (forwardExtent + offset);
        float bestPathLength = float.PositiveInfinity;
        bool found = false;

        for (int sideSign = -1; sideSign <= 1; sideSign += 2)
        {
            Vector3 candidate = beforeObstacle + side * ((sideExtent + offset) * sideSign);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit sample, offset * 1.75f, agent.areaMask))
                continue;
            if (!agent.CalculatePath(sample.position, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;

            float pathLength = GetPathLength(path);
            if (pathLength < bestPathLength)
            {
                bestPathLength = pathLength;
                bestSideStep = sample.position;
                found = true;
            }
        }

        return found;
    }

    private static float GetPathLength(NavMeshPath path)
    {
        Vector3[] corners = path.corners;
        float length = 0f;
        for (int i = 1; i < corners.Length; i++)
            length += Vector3.Distance(corners[i - 1], corners[i]);
        return length;
    }

    private void OnDestroy()
    {
        foreach (EnemyAudioEntry entry in _audioEntries.Values)
        {
            if (entry.loopSource != null)
                entry.loopSource.Stop();
            if (entry.attackSource != null)
                entry.attackSource.Stop();
        }

        _audioEntries.Clear();
        _destinationCaches.Clear();
        if (_instance == this)
            _instance = null;
    }

    private sealed class EnemyAudioEntry
    {
        public readonly MonoBehaviour enemy;
        public readonly AudioSource loopSource;
        public readonly AudioSource attackSource;
        public AudioClip chaseClip;
        public AudioClip attackClip;

        public EnemyAudioEntry(
            MonoBehaviour enemy,
            AudioSource loopSource,
            AudioSource attackSource,
            AudioClip chaseClip,
            AudioClip attackClip)
        {
            this.enemy = enemy;
            this.loopSource = loopSource;
            this.attackSource = attackSource;
            this.chaseClip = chaseClip;
            this.attackClip = attackClip;
        }
    }

    private sealed class ChaseDestinationCache
    {
        public readonly NavMeshPath path = new NavMeshPath();
        public Vector3 lastTarget;
        public Vector3 destination;
        public float nextCheck;
    }
}
