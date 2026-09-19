using UnityEngine;

public class GunShooter : MonoBehaviour
{
    [Header("── Gun Barrel ──")]
    public Transform barrelPoint;

    [Header("── Bullet Settings ──")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 50f;
    public float bulletLifetime = 4f;
    public float fireRate = 0.1f;

    [Header("── AAA Circle Crosshair Settings ──")]
    public float crosshairBaseSize = 64f; 
    public float expandOnFire = 20f; 
    public float recoilRecoverSpeed = 10f;
    public Color crosshairColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);

    [Header("── Crosshair Shapes ──")]
    public float circleRadius = 28f;       
    public float lineGap = 6f;           
    public float lineLength = 40f;   
    public float lineThickness = 1.5f;   

    [Header("── Crosshair Position (Gun Distance) ──")]
    [Tooltip("Isko OFF rakhiye agar crosshair gun ke barrel ke samne chahiye!")]
    public bool centerOnScreen = false;
    public float crosshairDistance = 50f;

    private GunEquipSystem _gunSystem;
    private Camera _mainCam;
    private float _nextFireTime = 0f;
    
    private float _currentSize;
    public bool isAiming = false;
    
    private Texture2D _crosshairTex;
    private Texture2D _particleTex; // Custom soft circle texture to fix white squares

    private float _lastRadius, _lastGap, _lastLength, _lastThickness;

    private void Awake()
    {
        _gunSystem = GetComponentInParent<GunEquipSystem>();
        if (_gunSystem == null) _gunSystem = GetComponent<GunEquipSystem>();

        _mainCam = Camera.main;
        _currentSize = crosshairBaseSize;
        
        GenerateCrosshairTexture();
        GenerateParticleTexture();
    }

    private void GenerateParticleTexture()
    {
        // 32x32 soft radial gradient (Fixes the ugly white square issue)
        _particleTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for(int y=0; y<32; y++)
        {
            for(int x=0; x<32; x++)
            {
                float dx = (x - 16f)/16f;
                float dy = (y - 16f)/16f;
                float dist = Mathf.Sqrt(dx*dx + dy*dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = Mathf.Pow(alpha, 1.5f); // Softer edges
                _particleTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        _particleTex.Apply();
    }

    private void GenerateCrosshairTexture()
    {
        int texSize = 128;
        _crosshairTex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        
        Color transparent = new Color(0, 0, 0, 0);
        for (int y = 0; y < texSize; y++)
            for (int x = 0; x < texSize; x++)
                _crosshairTex.SetPixel(x, y, transparent);

        float center = texSize / 2f;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                bool draw = false;

                if (Mathf.Abs(dist - circleRadius) < lineThickness) draw = true;
                if (Mathf.Abs(dy) < lineThickness && Mathf.Abs(dx) >= lineGap && Mathf.Abs(dx) <= lineLength) draw = true;
                if (Mathf.Abs(dx) < lineThickness && Mathf.Abs(dy) >= lineGap && Mathf.Abs(dy) <= lineLength) draw = true;

                if (draw)
                    _crosshairTex.SetPixel(x, y, crosshairColor);
            }
        }
        
        _crosshairTex.Apply();

        _lastRadius = circleRadius;
        _lastGap = lineGap;
        _lastLength = lineLength;
        _lastThickness = lineThickness;
    }

    private void Update()
    {
        if (_lastRadius != circleRadius || _lastGap != lineGap || _lastLength != lineLength || _lastThickness != lineThickness)
        {
            GenerateCrosshairTexture();
        }

        if (_gunSystem == null || !_gunSystem.IsGunEquipped) 
        {
            isAiming = false;
            return;
        }

        isAiming = Input.GetMouseButton(1);
        _currentSize = Mathf.Lerp(_currentSize, crosshairBaseSize, Time.deltaTime * recoilRecoverSpeed);

        bool holdingFire = Input.GetMouseButton(0);
        bool clickedFire = Input.GetMouseButtonDown(0);

        if ((holdingFire || clickedFire) && Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + fireRate;
            Fire();
            _currentSize += expandOnFire; 
        }
    }

    private void Fire()
    {
        if (bulletPrefab == null || barrelPoint == null) return;

        Vector3 fireDir = CalculateAimDirection();

        GameObject bulletObj = Instantiate(bulletPrefab, barrelPoint.position, Quaternion.LookRotation(fireDir));
        Bullet bulletScript = bulletObj.GetComponent<Bullet>();
        if (bulletScript != null)
            bulletScript.Launch(fireDir, bulletSpeed, bulletLifetime);

        SpawnRealisticMuzzleFlash();
    }

    private Vector3 CalculateAimDirection()
    {
        if (_mainCam == null) return barrelPoint.forward;
        
        Ray ray = _mainCam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            return (hit.point - barrelPoint.position).normalized;
        }
        return ray.direction;
    }

    private void SpawnRealisticMuzzleFlash()
    {
        GameObject flashObj = new GameObject("AAA_MuzzleFlash");
        flashObj.transform.SetParent(barrelPoint);
        flashObj.transform.localPosition = Vector3.zero;
        flashObj.transform.localRotation = Quaternion.identity;

        Light light = flashObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.7f, 0.2f);
        light.range = 8f;
        light.intensity = 3f;

        Shader spriteShader = Shader.Find("Sprites/Default");
        Material mat = new Material(spriteShader);
        if (_particleTex != null) mat.mainTexture = _particleTex; // Applies soft circle!

        // 2. Core Flash (Bright Center)
        GameObject coreObj = new GameObject("Core");
        coreObj.transform.SetParent(flashObj.transform, false);
        ParticleSystem psCore = coreObj.AddComponent<ParticleSystem>();
        var main1 = psCore.main;
        main1.duration = 0.05f; main1.loop = false;
        main1.startLifetime = 0.05f; main1.startSpeed = 0f;
        main1.startSize = 0.5f; main1.startColor = Color.white;
        main1.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em1 = psCore.emission; em1.rateOverTime = 0; em1.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, 1) });
        var rend1 = coreObj.GetComponent<ParticleSystemRenderer>();
        rend1.material = mat;
        rend1.renderMode = ParticleSystemRenderMode.Billboard;

        // 3. Sparks (Stretched Fast Particles)
        GameObject sparksObj = new GameObject("Sparks");
        sparksObj.transform.SetParent(flashObj.transform, false);
        ParticleSystem psSparks = sparksObj.AddComponent<ParticleSystem>();
        var main2 = psSparks.main;
        main2.duration = 0.1f; main2.loop = false;
        main2.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main2.startSpeed = new ParticleSystem.MinMaxCurve(20f, 40f);
        main2.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main2.startColor = new Color(1f, 0.8f, 0.2f, 1f);
        main2.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em2 = psSparks.emission; em2.rateOverTime = 0; em2.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, 15, 25) });
        var shape2 = psSparks.shape; shape2.shapeType = ParticleSystemShapeType.Cone; shape2.angle = 20f; shape2.radius = 0.01f;
        var rend2 = sparksObj.GetComponent<ParticleSystemRenderer>();
        rend2.material = mat;
        rend2.renderMode = ParticleSystemRenderMode.Stretch; 
        rend2.lengthScale = 2f;

        // 4. Smoke Puff
        GameObject smokeObj = new GameObject("Smoke");
        smokeObj.transform.SetParent(flashObj.transform, false);
        ParticleSystem psSmoke = smokeObj.AddComponent<ParticleSystem>();
        var main3 = psSmoke.main;
        main3.duration = 0.5f; main3.loop = false;
        main3.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main3.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
        main3.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main3.startColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        main3.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em3 = psSmoke.emission; em3.rateOverTime = 0; em3.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, 5, 10) });
        var shape3 = psSmoke.shape; shape3.shapeType = ParticleSystemShapeType.Cone; shape3.angle = 15f;
        var colOverLife = psSmoke.colorOverLifetime; colOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f) }, new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colOverLife.color = grad;
        var rend3 = smokeObj.GetComponent<ParticleSystemRenderer>();
        rend3.material = mat;

        psCore.Play();
        psSparks.Play();
        psSmoke.Play();
        
        Destroy(flashObj, 0.6f); 
    }

    private void OnGUI()
    {
        if (!isAiming || _crosshairTex == null) return;

        float cx = 0f;
        float cy = 0f;

        if (centerOnScreen || _mainCam == null)
        {
            cx = Screen.width * 0.5f;
            cy = Screen.height * 0.5f;
        }
        else
        {
            Vector3 worldPoint = barrelPoint.position + barrelPoint.forward * crosshairDistance;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPoint);
            
            if (screenPos.z < 0) return;
            
            cx = screenPos.x;
            cy = Screen.height - screenPos.y; 
        }
        
        float s = _currentSize;
        float half = s * 0.5f;

        GUI.DrawTexture(new Rect(cx - half, cy - half, s, s), _crosshairTex);
    }
}
