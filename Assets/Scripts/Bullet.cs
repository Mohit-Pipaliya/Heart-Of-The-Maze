using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [Header("── Bullet Settings ──")]
    [Tooltip("Bullet damage dealt per impact (Recommended: 75f - 100f).")]
    public float damage = 85f;
    public bool disableTrailOnImpact = true;
    public string ignoreLayerName = "Player";
    public LayerMask hitMask = ~0;

    [Header("── Audio SFX ──")]
    public AudioClip fleshHitSound;
    public AudioClip groundHitSound;

    private Rigidbody _rb;
    private bool _hasHit = false;
    private Vector3 _velocity;
    private Vector3 _prevPos;
    private TrailRenderer _trail;
    private LineRenderer _tracer;
    private Light _bulletLight;

    private void Awake()
    {
        if (damage <= 0f) damage = 85f;

        // Hide bulky 3D unfired bullet cartridge mesh so only the optical laser-sharp supersonic tracer streak is visible
        MeshRenderer[] mrs = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < mrs.Length; i++)
        {
            if (mrs[i] != null) mrs[i].enabled = false;
        }

        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            _rb.useGravity = false;
            _rb.isKinematic = true; // Use high-precision continuous raycast
        }

        if (!string.IsNullOrEmpty(ignoreLayerName))
        {
            int playerLayer = LayerMask.NameToLayer(ignoreLayerName);
            if (playerLayer >= 0)
            {
                Physics.IgnoreLayerCollision(gameObject.layer, playerLayer, true);
                hitMask &= ~(1 << playerLayer);
            }
        }

        // Strictly ignore physical collisions with player CharacterController & model
        Collider myCol = GetComponent<Collider>();
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null && myCol != null)
        {
            Collider[] playerCols = playerObj.GetComponentsInChildren<Collider>();
            for (int i = 0; i < playerCols.Length; i++)
            {
                if (playerCols[i] != null)
                    Physics.IgnoreCollision(myCol, playerCols[i], true);
            }
        }

        SetupTracerVisuals();
    }

    public static Material GetVFXMaterial(bool additive)
    {
        return VFXUtil.Get(additive);
    }

    private void SetupTracerVisuals()
    {
        MeshRenderer[] existing = GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null) existing[i].enabled = false;
        }

        _tracer = gameObject.GetComponent<LineRenderer>();
        if (_tracer == null) _tracer = gameObject.AddComponent<LineRenderer>();
        _tracer.positionCount = 2;
        _tracer.useWorldSpace = true;
        _tracer.startWidth = 0.055f;
        _tracer.endWidth = 0.012f;
        _tracer.numCapVertices = 4;
        _tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _tracer.receiveShadows = false;
        _tracer.textureMode = LineTextureMode.Stretch;

        Shader shader = VFXUtil.FindValidShader(false);
        Material tracerMat = new Material(shader != null ? shader : VFXUtil.Get(false).shader);
        Color head = new Color(1f, 0.72f, 0.15f, 1f);
        Color tail = new Color(1f, 0.28f, 0.02f, 0.15f);
        if (tracerMat.HasProperty("_BaseColor")) tracerMat.SetColor("_BaseColor", head);
        if (tracerMat.HasProperty("_Color")) tracerMat.SetColor("_Color", head);
        tracerMat.color = head;
        _tracer.material = tracerMat;
        _tracer.startColor = head;
        _tracer.endColor = tail;

        GameObject lightObj = new GameObject("TracerLight");
        lightObj.transform.SetParent(transform, false);
        _bulletLight = lightObj.AddComponent<Light>();
        _bulletLight.type = LightType.Point;
        _bulletLight.color = new Color(1f, 0.62f, 0.15f);
        _bulletLight.range = 3.2f;
        _bulletLight.intensity = 2.2f;
        _bulletLight.shadows = LightShadows.None;
    }

    private void DrawTracer()
    {
        if (_tracer == null || _velocity.sqrMagnitude < 0.01f) return;
        Vector3 dir = _velocity.normalized;
        _tracer.SetPosition(0, transform.position);
        _tracer.SetPosition(1, transform.position - dir * 3.2f);
    }

    private Vector3 _destination;
    private bool _flyToPoint;
    private SpiderEnemy _lockedSpider;
    private ZombieAI _lockedZombie;
    private Vector3 _spawnPos;

    public void LaunchTo(Vector3 destination, SpiderEnemy spider, ZombieAI zombie, float lifetime, AudioClip fleshSfx = null, AudioClip groundSfx = null)
    {
        _destination = destination;
        _flyToPoint = true;
        _lockedSpider = spider;
        _lockedZombie = zombie;
        _spawnPos = transform.position;
        Vector3 dir = destination - transform.position;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
        _velocity = dir.normalized * 78f;
        _prevPos = transform.position;
        if (fleshSfx != null) fleshHitSound = fleshSfx;
        if (groundSfx != null) groundHitSound = groundSfx;
        DrawTracer();
        Destroy(gameObject, lifetime);
    }

    public void Launch(Vector3 direction, float speed, float lifetime, AudioClip fleshSfx = null, AudioClip groundSfx = null)
    {
        LaunchTo(transform.position + direction.normalized * 40f, null, null, lifetime, fleshSfx, groundSfx);
    }

    private void Update()
    {
        if (_hasHit) return;

        DrawTracer();

        if (_flyToPoint)
        {
            Vector3 toPoint = _destination - transform.position;
            float step = 78f * Time.deltaTime;
            if (toPoint.magnitude <= step)
            {
                transform.position = _destination;
                DrawTracer();
                ArriveAtAim();
                return;
            }

            Vector3 next = transform.position + toPoint.normalized * step;
            if (BlockedBeforeAim(_prevPos, next, out RaycastHit block))
            {
                transform.position = block.point;
                OnImpact(block.point, block.normal, block.collider);
                return;
            }

            transform.position = next;
            transform.rotation = Quaternion.LookRotation(toPoint.normalized);
            _prevPos = transform.position;
            DrawTracer();
            return;
        }

        Vector3 moveStep = _velocity * Time.deltaTime;
        float distance = moveStep.magnitude;

        // High-precision raycast sweep (checks triggers too for enemies, skips player colliders & non-enemy triggers)
        RaycastHit[] hits = Physics.RaycastAll(_prevPos, moveStep.normalized, distance, hitMask, QueryTriggerInteraction.Collide);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCol = hits[i].collider;
                if (hitCol == null) continue;
                if (hitCol.GetComponentInParent<PlayerController>() != null) continue;
                if (hitCol.GetComponentInParent<GunShooter>() != null) continue;

                // For triggers, only hit if it belongs to an enemy (SpiderEnemy or ZombieAI)
                if (hitCol.isTrigger)
                {
                    bool isEnemy = hitCol.GetComponentInParent<SpiderEnemy>() != null ||
                                   hitCol.GetComponentInParent<ZombieAI>() != null ||
                                   hitCol.GetComponentInChildren<SpiderEnemy>() != null ||
                                   hitCol.GetComponentInChildren<ZombieAI>() != null;
                    if (!isEnemy) continue;
                }

                transform.position = hits[i].point;
                OnImpact(hits[i].point, hits[i].normal, hitCol);
                return;
            }
        }

        transform.position += moveStep;
        transform.rotation = Quaternion.LookRotation(_velocity);
        _prevPos = transform.position;
    }

    private bool BlockedBeforeAim(Vector3 from, Vector3 to, out RaycastHit block)
    {
        block = default;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.001f) return false;

        RaycastHit[] hits = Physics.RaycastAll(from, delta / dist, dist, hitMask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.GetComponentInParent<PlayerController>() != null) continue;
            if (col.GetComponentInParent<GunShooter>() != null) continue;
            if (Vector3.Distance(from, _spawnPos) + hits[i].distance < 0.45f) continue;

            SpiderEnemy spider = col.GetComponentInParent<SpiderEnemy>();
            ZombieAI zombie = col.GetComponentInParent<ZombieAI>();
            if ((_lockedSpider != null && spider == _lockedSpider) || (_lockedZombie != null && zombie == _lockedZombie))
                continue;
            if (col.isTrigger && spider == null && zombie == null)
                continue;

            float remain = Vector3.Distance(hits[i].point, _destination);
            if (remain < 0.75f)
                continue;

            block = hits[i];
            return true;
        }
        return false;
    }

    private void ArriveAtAim()
    {
        if (_hasHit) return;
        _hasHit = true;

        Vector3 point = _destination;
        Vector3 normal = (_spawnPos - _destination).normalized;
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;

        if (_lockedSpider != null && !_lockedSpider.IsDead)
        {
            _lockedSpider.TakeDamage(Mathf.Max(damage, 1f), point, normal);
            if (GunShooter.Instance != null) GunShooter.Instance.RegisterHitMarker(_lockedSpider.IsDead);
            GameAudio.PlayBulletHit(point, true);
            VFXUtil.GunFleshHit(point, normal);
        }
        else if (_lockedZombie != null && !_lockedZombie.IsDead)
        {
            _lockedZombie.TakeDamage(Mathf.Max(damage, 1f), point, normal);
            if (GunShooter.Instance != null) GunShooter.Instance.RegisterHitMarker(_lockedZombie.IsDead);
            GameAudio.PlayBulletHit(point, true);
            VFXUtil.GunFleshHit(point, normal);
        }
        else
        {
            GameAudio.PlayBulletHit(point, false);
            VFXUtil.GunSurfaceHit(point, normal);
        }

        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        ContactPoint contact = collision.GetContact(0);
        OnImpact(contact.point, contact.normal, collision.collider);
    }

    private void OnImpact(Vector3 point, Vector3 normal, Collider hitCollider)
    {
        if (_hasHit) return;
        if (hitCollider != null)
        {
            if (hitCollider.GetComponentInParent<PlayerController>() != null) return;
            if (hitCollider.isTrigger)
            {
                bool isEnemyTrigger = hitCollider.GetComponentInParent<SpiderEnemy>() != null ||
                                      hitCollider.GetComponentInParent<ZombieAI>() != null ||
                                      hitCollider.GetComponentInChildren<SpiderEnemy>() != null ||
                                      hitCollider.GetComponentInChildren<ZombieAI>() != null;
                if (!isEnemyTrigger) return;
            }
        }

        _hasHit = true;

        SpiderEnemy spider = hitCollider != null ? (hitCollider.GetComponentInParent<SpiderEnemy>() ?? hitCollider.GetComponentInChildren<SpiderEnemy>() ?? hitCollider.transform.root.GetComponentInChildren<SpiderEnemy>()) : null;
        if (spider == null && hitCollider != null && (hitCollider.gameObject.name.IndexOf("Spider", System.StringComparison.OrdinalIgnoreCase) >= 0 || hitCollider.transform.root.gameObject.name.IndexOf("Spider", System.StringComparison.OrdinalIgnoreCase) >= 0))
        {
            spider = Object.FindAnyObjectByType<SpiderEnemy>();
            if (spider != null && Vector3.Distance(point, spider.transform.position) > 18f)
                spider = null;
        }

        ZombieAI zombie = hitCollider != null ? (hitCollider.GetComponentInParent<ZombieAI>() ?? hitCollider.GetComponentInChildren<ZombieAI>() ?? hitCollider.transform.root.GetComponentInChildren<ZombieAI>()) : null;
        bool isEnemy = (spider != null) || (zombie != null);

        if (isEnemy)
        {
            float effectiveDamage = Mathf.Max(damage, 1f);
            bool isKill = false;
            if (spider != null)
            {
                spider.TakeDamage(effectiveDamage, point, normal);
                isKill = spider.IsDead;
            }
            else if (zombie != null)
            {
                zombie.TakeDamage(effectiveDamage, point, normal);
                isKill = zombie.IsDead;
            }

            if (GunShooter.Instance != null)
                GunShooter.Instance.RegisterHitMarker(isKill);

            GameAudio.PlayBulletHit(point, true);
            VFXUtil.GunFleshHit(point, normal);
            GameFeel.HitStop(0.035f);
        }
        else
        {
            GameAudio.PlayBulletHit(point, false);
            VFXUtil.GunSurfaceHit(point, normal);
        }

        if (disableTrailOnImpact && _trail != null)
        {
            _trail.transform.SetParent(null);
            _trail.autodestruct = true;
        }

        Destroy(gameObject);
    }
}
