using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [Header("── Bullet Behaviour ──")]
    public bool disableTrailOnImpact = true;
    public string ignoreLayerName = "Player";

    private Rigidbody _rb;
    private bool _hasHit = false;
    private Texture2D _particleTex; // Soft circle texture

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (!string.IsNullOrEmpty(ignoreLayerName))
        {
            int playerLayer = LayerMask.NameToLayer(ignoreLayerName);
            if (playerLayer >= 0)
                Physics.IgnoreLayerCollision(gameObject.layer, playerLayer, true);
        }

        GenerateParticleTexture();

        TrailRenderer tr = GetComponent<TrailRenderer>();
        if (tr == null)
        {
            tr = gameObject.AddComponent<TrailRenderer>();
            tr.time = 0.1f;
            tr.startWidth = 0.05f;
            tr.endWidth = 0f;
            Shader spriteShader = Shader.Find("Sprites/Default");
            Material mat = new Material(spriteShader);
            if (_particleTex != null) mat.mainTexture = _particleTex;
            tr.material = mat;
            tr.startColor = new Color(1f, 0.8f, 0.2f, 1f);
            tr.endColor = new Color(1f, 0.4f, 0f, 0f);
        }
    }

    private void GenerateParticleTexture()
    {
        _particleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for(int y=0; y<32; y++)
        {
            for(int x=0; x<32; x++)
            {
                float dx = (x - 16f)/16f;
                float dy = (y - 16f)/16f;
                float dist = Mathf.Sqrt(dx*dx + dy*dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 1.5f);
                _particleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        _particleTex.Apply();
    }

    public void Launch(Vector3 direction, float speed, float lifetime)
    {
        _rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        _hasHit = true;

        ContactPoint contact = collision.GetContact(0);
        SpawnRealisticImpactEffect(contact.point, contact.normal);

        if (disableTrailOnImpact)
        {
            TrailRenderer trail = GetComponent<TrailRenderer>();
            if (trail != null) trail.enabled = false;
        }

        Destroy(gameObject);
    }

    private void SpawnRealisticImpactEffect(Vector3 pos, Vector3 normal)
    {
        GameObject impactObj = new GameObject("Realistic_ImpactEffect");
        impactObj.transform.position = pos;
        impactObj.transform.rotation = Quaternion.LookRotation(normal);

        GameObject lightObj = new GameObject("FlashLight");
        lightObj.transform.SetParent(impactObj.transform);
        lightObj.transform.localPosition = Vector3.zero;

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.8f, 0.3f);
        light.range = 3f;
        light.intensity = 1.5f;

        ParticleSystem ps = impactObj.AddComponent<ParticleSystem>();
        
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor = new Color(1f, 0.6f, 0.1f, 1f); 
        main.gravityModifier = 1.5f; 
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15, 30) });
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 45f;
        shape.radius = 0.05f;

        ParticleSystemRenderer psRenderer = impactObj.GetComponent<ParticleSystemRenderer>();
        Shader spriteShader = Shader.Find("Sprites/Default");
        Material mat = new Material(spriteShader);
        if (_particleTex != null) mat.mainTexture = _particleTex;
        psRenderer.material = mat;
        
        ps.Play();
        
        Destroy(lightObj, 0.1f);
        Destroy(impactObj, 2f);
    }
}
