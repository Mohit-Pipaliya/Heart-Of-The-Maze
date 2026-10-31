using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [Header("── Bullet Settings ──")]
    public float damage = 10f;
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

        _trail.time = 0.08f;
        _trail.startWidth = 0.09f;
        _trail.endWidth = 0.015f;
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
                new GradientColorKey(new Color(2.0f, 1.8f, 1.2f), 0f),   // White-hot core
                new GradientColorKey(new Color(1.0f, 0.75f, 0.2f), 0.3f), // Gold flame
                new GradientColorKey(new Color(0.9f, 0.25f, 0.05f), 1f)  // Fiery ember tail
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
        _bulletLight.color = new Color(1f, 0.75f, 0.3f);
        _bulletLight.range = 5.0f;
        _bulletLight.intensity = 1.4f;
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

        SpiderEnemy spider = hitCollider != null ? hitCollider.GetComponentInParent<SpiderEnemy>() : null;
        ZombieAI zombie = hitCollider != null ? hitCollider.GetComponentInParent<ZombieAI>() : null;
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
            GameFeel.HitStop(0.03f);
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
        VFXUtil.SpawnFadingLight(pos, new Color(0.9f, 0.15f, 0.05f), 5f, 2.2f, 0.08f);
        VFXUtil.Burst(pos, normal, new Color(0.85f, 0.1f, 0.05f, 1f), 28, 6f, 18f, 0.08f, 0.28f, 0.18f, 0.42f, true, true, 2.2f, 35f);
    }

    private void SpawnRicochetImpactEffect(Vector3 pos, Vector3 normal)
    {
        VFXUtil.SpawnFadingLight(pos, new Color(1f, 0.8f, 0.3f), 6f, 2.4f, 0.07f);
        VFXUtil.Burst(pos, normal, new Color(1f, 0.75f, 0.2f, 1f), 22, 10f, 24f, 0.05f, 0.16f, 0.1f, 0.3f, true, true, 1.1f, 48f);
        VFXUtil.Burst(pos, Vector3.up, new Color(0.6f, 0.58f, 0.55f, 0.4f), 10, 1.5f, 4.5f, 0.35f, 0.9f, 0.28f, 0.5f, false, false, -0.08f, 50f);
    }
}
