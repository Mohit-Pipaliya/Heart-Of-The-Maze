using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [Header("── Bullet Settings ──")]
    public float damage = 15f;
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
    private Texture2D _particleTex;
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

        GenerateParticleTexture();
        SetupTracerVisuals();
    }

    private void GenerateParticleTexture()
    {
        _particleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 16f) / 16f;
                float dy = (y - 16f) / 16f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 1.5f);
                _particleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        _particleTex.Apply();
    }

    private static Material _sharedAdditiveMat;
    private static Material _sharedAlphaMat;

    public static Material GetVFXMaterial(bool additive)
    {
        if (additive && _sharedAdditiveMat != null) return _sharedAdditiveMat;
        if (!additive && _sharedAlphaMat != null) return _sharedAlphaMat;

        Material loaded = Resources.Load<Material>(additive ? "VFX/M_ParticleAdditive" : "VFX/M_ParticleAlpha");

#if UNITY_EDITOR
        if (loaded == null)
        {
            string path = additive 
                ? "Assets/Materials/VFX/M_ParticleAdditive.mat" 
                : "Assets/Materials/VFX/M_ParticleAlpha.mat";
            loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        }
#endif

        if (loaded == null)
        {
            loaded = Resources.Load<Material>("VFX/M_TorchFire");
#if UNITY_EDITOR
            if (loaded == null)
                loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/M_TorchFire.mat");
#endif
        }

        if (loaded != null)
        {
            Material cloned = new Material(loaded);
            if (additive) _sharedAdditiveMat = cloned;
            else _sharedAlphaMat = cloned;
            return cloned;
        }

        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");

        Material m = new Material(s != null ? s : Shader.Find("Sprites/Default"));
        m.color = Color.white;
        if (additive) _sharedAdditiveMat = m;
        else _sharedAlphaMat = m;
        return m;
    }

    private void SetupTracerVisuals()
    {
        // High-energy luminous tracer trail (scaled for game world)
        _trail = GetComponent<TrailRenderer>();
        if (_trail == null)
            _trail = gameObject.AddComponent<TrailRenderer>();

        _trail.time = 0.10f;
        _trail.startWidth = 0.35f;
        _trail.endWidth = 0.02f;
        _trail.minVertexDistance = 0.05f;
        _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _trail.receiveShadows = false;

        // URP Additive Material
        _trail.material = GetVFXMaterial(true);

        // Gradient: bright white-gold at head → deep orange → fade out
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1.0f, 0.95f, 0.7f), 0f),   // Bright white-gold
                new GradientColorKey(new Color(1.0f, 0.65f, 0.1f), 0.3f), // Orange
                new GradientColorKey(new Color(0.8f, 0.2f, 0.0f), 1f)     // Deep red-orange tail
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,   0f),
                new GradientAlphaKey(0.8f, 0.5f),
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
        _bulletLight.range = 10f;
        _bulletLight.intensity = 1.5f;
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
        _hasHit = true;

        SpiderEnemy enemy = hitCollider != null ? hitCollider.GetComponentInParent<SpiderEnemy>() : null;
        bool isEnemy = enemy != null;

        if (isEnemy)
        {
            enemy.TakeDamage(damage);

            if (GunShooter.Instance != null)
                GunShooter.Instance.RegisterHitMarker(enemy.IsDead);

            GameAudio.PlayBulletHit(point, true);

            SpawnFleshImpactEffect(point, normal);
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
        GameObject impactObj = new GameObject("AAA_FleshImpact");
        impactObj.SetActive(false); // Configure stopped to avoid duration warnings
        impactObj.transform.position = pos;
        impactObj.transform.rotation = Quaternion.LookRotation(normal);

        // Flash light
        GameObject lightObj = new GameObject("FleshLight");
        lightObj.transform.SetParent(impactObj.transform, false);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.9f, 0.15f, 0.05f);
        light.range = 8f;
        light.intensity = 3f;
        Destroy(lightObj, 0.07f);

        // Visceral blood / flesh splatter particles
        ParticleSystem ps = impactObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startColor = new Color(0.85f, 0.1f, 0.05f, 1f);
        main.gravityModifier = 2.5f;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25, 45) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.1f;

        ParticleSystemRenderer psRenderer = impactObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = GetVFXMaterial(true);

        impactObj.SetActive(true);
        ps.Play();
        Destroy(impactObj, 2f);
    }

    private void SpawnRicochetImpactEffect(Vector3 pos, Vector3 normal)
    {
        GameObject impactObj = new GameObject("AAA_RicochetImpact");
        impactObj.SetActive(false); // Configure stopped to avoid duration warnings
        impactObj.transform.position = pos;
        impactObj.transform.rotation = Quaternion.LookRotation(normal);

        // Flash Light
        GameObject lightObj = new GameObject("SparkLight");
        lightObj.transform.SetParent(impactObj.transform, false);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.8f, 0.3f);
        light.range = 10f;
        light.intensity = 3f;
        Destroy(lightObj, 0.07f);

        // 1. Hot Bouncing Ricochet Sparks
        ParticleSystem ps = impactObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(14f, 35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new Color(1f, 0.75f, 0.2f, 1f);
        main.gravityModifier = 1.2f;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20, 35) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 50f;
        shape.radius = 0.05f;

        ParticleSystemRenderer psRenderer = impactObj.GetComponent<ParticleSystemRenderer>();
        psRenderer.material = GetVFXMaterial(true);
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.lengthScale = 2.5f;

        // 2. Dust / Smoke Puff
        GameObject smokeObj = new GameObject("DustPuff");
        smokeObj.transform.SetParent(impactObj.transform, false);
        ParticleSystem psSmoke = smokeObj.AddComponent<ParticleSystem>();
        var mainSmoke = psSmoke.main;
        mainSmoke.loop = false;
        mainSmoke.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        mainSmoke.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        mainSmoke.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        mainSmoke.startColor = new Color(0.6f, 0.58f, 0.55f, 0.4f);
        mainSmoke.gravityModifier = -0.1f;
        mainSmoke.playOnAwake = false;

        var emSmoke = psSmoke.emission;
        emSmoke.rateOverTime = 0;
        emSmoke.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8, 14) });

        var rendSmoke = smokeObj.GetComponent<ParticleSystemRenderer>();
        rendSmoke.material = GetVFXMaterial(false);

        impactObj.SetActive(true);
        ps.Play();
        psSmoke.Play();
        Destroy(impactObj, 2f);
    }
}
