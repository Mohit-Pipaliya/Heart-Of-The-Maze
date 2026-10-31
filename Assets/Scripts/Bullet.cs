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

        _trail.time = 0.07f;
        _trail.startWidth = 0.075f;
        _trail.endWidth = 0.012f;
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
                new GradientColorKey(new Color(2.5f, 2.3f, 1.8f), 0f),   // White-hot core
                new GradientColorKey(new Color(1.6f, 1.1f, 0.3f), 0.3f), // Gold flame
                new GradientColorKey(new Color(1.0f, 0.35f, 0.05f), 1f)  // Fiery ember tail
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
        _bulletLight.range = 6.0f;
        _bulletLight.intensity = 2.2f;
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

        // High-precision raycast sweep (prevents high-speed tunneling)
        if (Physics.Raycast(_prevPos, moveStep.normalized, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
            OnImpact(hit.point, hit.normal, hit.collider);
            return;
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
            if (hitCollider.isTrigger) return;
        }

        _hasHit = true;

        SpiderEnemy spider = hitCollider != null ? (hitCollider.GetComponentInParent<SpiderEnemy>() ?? hitCollider.GetComponentInChildren<SpiderEnemy>() ?? hitCollider.transform.root.GetComponentInChildren<SpiderEnemy>()) : null;
        ZombieAI zombie = hitCollider != null ? (hitCollider.GetComponentInParent<ZombieAI>() ?? hitCollider.GetComponentInChildren<ZombieAI>() ?? hitCollider.transform.root.GetComponentInChildren<ZombieAI>()) : null;
        bool isEnemy = (spider != null) || (zombie != null);

        if (isEnemy)
        {
            bool isKill = false;
            if (spider != null)
            {
                spider.TakeDamage(damage);
                isKill = spider.IsDead;
            }
            else if (zombie != null)
            {
                zombie.TakeDamage(damage);
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
        VFXUtil.SpawnFadingLight(pos, new Color(0.95f, 0.08f, 0.05f), 5.5f, 2.8f, 0.09f);
        VFXUtil.Burst(pos, normal, new Color(0.85f, 0.08f, 0.05f, 1f), 26, 8f, 20f, 0.08f, 0.28f, 0.14f, 0.38f, true, true, 2.0f, 38f);
        VFXUtil.Burst(pos, Vector3.up, new Color(0.6f, 0.05f, 0.04f, 0.5f), 8, 1.2f, 3.8f, 0.2f, 0.5f, 0.25f, 0.6f, false, false, -0.05f, 55f);
    }

    private void SpawnRicochetImpactEffect(Vector3 pos, Vector3 normal)
    {
        // AAA Ricochet Impact: Luminous golden flash + bouncing embers + dirt dust puff
        VFXUtil.SpawnFadingLight(pos, new Color(1f, 0.82f, 0.35f), 6.5f, 3.0f, 0.07f);
        VFXUtil.Burst(pos, normal, new Color(1f, 0.82f, 0.28f, 1f), 24, 12f, 26f, 0.05f, 0.16f, 0.1f, 0.28f, true, true, 1.4f, 48f);
        VFXUtil.Burst(pos, Vector3.up, new Color(0.65f, 0.62f, 0.55f, 0.45f), 10, 1.5f, 4.5f, 0.3f, 0.8f, 0.3f, 0.8f, false, false, -0.08f, 50f);
    }
}
