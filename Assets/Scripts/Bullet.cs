using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [Header("── Bullet Settings ──")]
    [Tooltip("Bullet damage dealt per impact (Default: 50f for punchy, balanced combat against 1000 HP enemies).")]
    public float damage = 50f;
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
    private Light _bulletLight;

    private void Awake()
    {
        if (damage < 50f) damage = 75f;

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
        // High-velocity realistic military tracer beam (needle-sharp, blazing luminous glow)
        _trail = GetComponent<TrailRenderer>();
        if (_trail == null)
            _trail = gameObject.AddComponent<TrailRenderer>();

        _trail.time = 0.12f;
        _trail.startWidth = 0.18f;
        _trail.endWidth = 0.03f;
        _trail.minVertexDistance = 0.02f;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.numCapVertices = 4;
        _trail.numCornerVertices = 4;

        // Guaranteed fail-safe URP Additive Material
        Material trailMat = new Material(GetVFXMaterial(true));
        VFXUtil.ApplyTexture(trailMat);
        _trail.material = trailMat;

        // Gradient: HDR white-hot tip → luminous gold → fiery orange-red tail
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(2.8f, 2.6f, 2.0f), 0f),   // White-hot core
                new GradientColorKey(new Color(1.8f, 1.2f, 0.35f), 0.3f), // Gold flame
                new GradientColorKey(new Color(1.2f, 0.4f, 0.08f), 1f)  // Fiery ember tail
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.85f, 0.5f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        _trail.colorGradient = gradient;

        // Dynamic bullet point light (casts moving light as bullet streaks across scene)
        GameObject lightObj = new GameObject("TracerLight");
        lightObj.transform.SetParent(transform, false);
        _bulletLight = lightObj.AddComponent<Light>();
        _bulletLight.type = LightType.Point;
        _bulletLight.color = new Color(1f, 0.85f, 0.45f);
        _bulletLight.range = 8.0f;
        _bulletLight.intensity = 3.5f;
        _bulletLight.shadows = LightShadows.None;
    }

    public void Launch(Vector3 direction, float speed, float lifetime, AudioClip fleshSfx = null, AudioClip groundSfx = null)
    {
        _velocity = direction.normalized * speed;
        _prevPos = transform.position;
        if (fleshSfx != null) fleshHitSound = fleshSfx;
        if (groundSfx != null) groundHitSound = groundSfx;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (_hasHit) return;

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
        ZombieAI zombie = hitCollider != null ? (hitCollider.GetComponentInParent<ZombieAI>() ?? hitCollider.GetComponentInChildren<ZombieAI>() ?? hitCollider.transform.root.GetComponentInChildren<ZombieAI>()) : null;
        bool isEnemy = (spider != null) || (zombie != null);

        if (isEnemy)
        {
            float effectiveDamage = Mathf.Max(damage, 75f);
            bool isKill = false;
            if (spider != null)
            {
                spider.TakeDamage(effectiveDamage);
                isKill = spider.IsDead;
            }
            else if (zombie != null)
            {
                zombie.TakeDamage(effectiveDamage);
                isKill = zombie.IsDead;
            }

            if (GunShooter.Instance != null)
                GunShooter.Instance.RegisterHitMarker(isKill);

            GameAudio.PlayBulletHit(point, true);

            SpawnFleshImpactEffect(point, normal);
            GameFeel.HitStop(0.035f);
        }
        else
        {
            GameAudio.PlayBulletHit(point, false);

            SpawnRicochetImpactEffect(point, normal);
        }

        if (disableTrailOnImpact && _trail != null)
        {
            _trail.transform.SetParent(null);
            _trail.autodestruct = true;
        }

        Destroy(gameObject);
    }

    private void SpawnFleshImpactEffect(Vector3 pos, Vector3 normal)
    {
        // AAA Flesh Impact: Dynamic red flash + high-velocity arterial blood spray + mist
        VFXUtil.SpawnFadingLight(pos, new Color(1f, 0.05f, 0.02f), 7.0f, 6.0f, 0.12f);
        VFXUtil.Burst(pos, normal, new Color(0.85f, 0.05f, 0.02f, 1f), 32, 9f, 22f, 0.12f, 0.35f, 0.18f, 0.45f, true, true, 2.0f, 40f);
        VFXUtil.Burst(pos, Vector3.up, new Color(0.65f, 0.04f, 0.03f, 0.6f), 12, 1.5f, 4.5f, 0.3f, 0.7f, 0.35f, 0.8f, false, false, -0.05f, 55f);
    }

    private void SpawnRicochetImpactEffect(Vector3 pos, Vector3 normal)
    {
        // AAA Ricochet Impact: Luminous golden flash + bouncing embers + dirt dust puff
        VFXUtil.SpawnFadingLight(pos, new Color(1f, 0.85f, 0.35f), 8.0f, 7.0f, 0.10f);
        VFXUtil.Burst(pos, normal, new Color(1f, 0.85f, 0.28f, 1f), 28, 14f, 28f, 0.08f, 0.22f, 0.12f, 0.35f, true, true, 1.5f, 50f);
        VFXUtil.Burst(pos, Vector3.up, new Color(0.7f, 0.68f, 0.6f, 0.5f), 14, 2.0f, 5.5f, 0.4f, 0.9f, 0.4f, 1.0f, false, false, -0.08f, 50f);
    }
}
