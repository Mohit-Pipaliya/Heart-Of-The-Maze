/*
 * HEALTH BAR — QUICK SETUP
 * Add a Player bar to a 600x80 RectTransform under a Screen Space - Overlay Canvas.
 * ZombieAI creates its own world-space Zombie bar when showFloatingHealthBar is enabled.
 * The bar builds its own layers and particle pool; empty sprite slots use generated art.
 * Use SetMaxHealth(value) and SetHealth(value) from your health logic.
 */
using System;
using UnityEngine;
using UnityEngine.UI;

public enum BarType { Player, Zombie, Spider, Boss }

public class HealthBar : MonoBehaviour
{
    public float CurrentHealth => _targetHealth;
    public float MaxHealth => _maxHealth;

    [Header("Bar")]
    public BarType barType = BarType.Player;

    [Header("Sprites (optional overrides)")]
    public Sprite frame;
    public Sprite fill;
    public Sprite glow;
    public Sprite endCapLeft;
    public Sprite endCapRight;
    public Sprite icon;

    private const float BarWidth = 600f;
    private const float BarHeight = 80f;
    private const float WindowX = 44f;
    private const float WindowY = 28f;
    private const float WindowWidth = 520f;
    private const float WindowHeight = 26f;
    private const int ParticleCount = 24;
    private const float DamageFillDuration = 0.3f;
    private const float DamageGhostDelay = 0.5f;
    private const float DamageGhostDuration = 0.9f;

    private RectTransform _root;
    private RectTransform _fillRect;
    private RectTransform _ghostRect;
    private RectTransform _healOverlayRect;
    private RectTransform _highlightRect;
    private RectTransform _flashRect;
    private RectTransform _sweepClipRect;
    private RectTransform _sweepRect;
    private RectTransform _sparkRoot;
    private Image _trackImage;
    private Image _fillImage;
    private Image _ghostImage;
    private Image _flashImage;
    private Image _healOverlayImage;
    private Image _glowImage;
    private Image _vignetteImage;
    private Canvas _worldCanvas;
    private CanvasGroup _canvasGroup;
    private Camera _worldCamera;
    private Transform _proximityTarget;
    private Image[] _segmentImages;
    private Image[] _particles;
    private Particle[] _particleStates;
    private float[] _zombieDropTimers;
    private Vector2 _baseAnchoredPosition;
    private Vector3 _baseScale;
    private float _windowX = WindowX;
    private float _windowY = WindowY;
    private float _windowWidth = WindowWidth;
    private float _windowHeight = WindowHeight;
    private float _maxHealth = 100f;
    private float _targetHealth = 100f;
    private float _displayHealth = 100f;
    private float _displayStartHealth = 100f;
    private float _ghostHealth = 100f;
    private float _ghostStartHealth = 100f;
    private float _fillElapsed;
    private float _ghostElapsed;
    private float _flashTimer;
    private float _shakeTimer;
    private float _healTimer;
    private float _vignetteTimer;
    private float _lastDamageTime = -9999f;
    private float _glitchTimer;
    private float _visibilityAlpha;
    private float _nextPlayerSearchTime;
    private float _nextCameraSearchTime;
    private bool _isZombie;
    private bool _enemyDead;
    private bool _ghostIsAnimating;
    private bool _built;

    private struct Particle
    {
        public Vector2 Position;
        public float VelocityX;
        public float VelocityY;
        public float Gravity;
        public float Radius;
        public float Age;
        public float Lifetime;
        public Color Color;
        public bool Active;
    }

    private void Awake()
    {
        _root = transform as RectTransform;
        if (_root == null)
            throw new InvalidOperationException("HealthBar requires a RectTransform.");

        _baseAnchoredPosition = _root.anchoredPosition;
        if (barType == BarType.Player)
        {
            _baseScale = _root.localScale;
            BuildPlayerBar();
            _built = true;
            SetMaxHealth(_maxHealth);
            return;
        }

        if (barType == BarType.Zombie)
        {
            _isZombie = true;
            _windowX = 46f;
            _windowY = 14f;
            _windowWidth = 332f;
            _windowHeight = 16f;
            BuildZombieBar();
            _built = true;
            SetMaxHealth(_maxHealth);
            return;
        }

        throw new NotSupportedException(barType + " health bar is not implemented yet.");
    }

    private void OnDestroy()
    {
        if (_vignetteImage != null)
            Destroy(_vignetteImage.gameObject);
    }

    private void OnDisable()
    {
        if (_vignetteImage != null)
            _vignetteImage.enabled = false;
        if (_root != null)
        {
            _root.anchoredPosition = _baseAnchoredPosition;
            _root.localScale = _baseScale;
        }
    }

    private void Update()
    {
        if (!_built)
            return;

        UpdateHealthMotion();
        if (_isZombie)
        {
            UpdateZombieVisibility();
            UpdateZombieBloodDrops();
        }
        else
        {
            UpdateEffects();
        }
        UpdateParticles();
    }

    public void SetMaxHealth(float maxHealth)
    {
        ValidateHealthValue(maxHealth, nameof(maxHealth));
        if (maxHealth <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "Maximum health must be greater than zero.");

        _maxHealth = maxHealth;
        _targetHealth = maxHealth;
        _displayHealth = maxHealth;
        _displayStartHealth = maxHealth;
        _ghostHealth = maxHealth;
        _ghostStartHealth = maxHealth;
        _fillElapsed = 0f;
        _ghostElapsed = 0f;
        _ghostIsAnimating = false;
        RefreshBarWidths();
    }

    public void SetHealth(float health)
    {
        ValidateHealthValue(health, nameof(health));
        float nextHealth = Mathf.Clamp(health, 0f, _maxHealth);
        if (Mathf.Approximately(nextHealth, _targetHealth))
            return;

        bool damaged = nextHealth < _targetHealth;
        _targetHealth = nextHealth;
        _displayStartHealth = _displayHealth;
        _fillElapsed = 0f;

        if (damaged)
        {
            _ghostStartHealth = _ghostHealth;
            _ghostElapsed = 0f;
            _ghostIsAnimating = true;
            _flashTimer = 0.1f;
            if (_isZombie)
            {
                _lastDamageTime = Time.time;
                _glitchTimer = 0.15f;
            }
            else
            {
                _shakeTimer = 0.15f;
                SpawnDamageSparks(nextHealth / _maxHealth);
            }
        }
        else
        {
            _ghostHealth = nextHealth;
            _ghostStartHealth = nextHealth;
            _ghostIsAnimating = false;
            if (!_isZombie)
            {
                _healTimer = 0.4f;
                SpawnHealParticles(nextHealth / _maxHealth);
            }
        }
    }

    public void SetProximityTarget(Transform target)
    {
        _proximityTarget = target;
    }

    public void SetEnemyDead()
    {
        if (!_isZombie)
            return;
        _enemyDead = true;
        _visibilityAlpha = 0f;
        _canvasGroup.alpha = 0f;
    }

    private static void ValidateHealthValue(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, "Health must be a finite number.");
    }

    private void BuildPlayerBar()
    {
        frame = frame != null ? frame : HealthBarSprites.GetPlayerFrame();
        fill = fill != null ? fill : HealthBarSprites.GetPlayerFill();
        glow = glow != null ? glow : HealthBarSprites.GetPlayerGlow();
        endCapLeft = endCapLeft != null ? endCapLeft : HealthBarSprites.GetTransparent();
        endCapRight = endCapRight != null ? endCapRight : HealthBarSprites.GetTransparent();
        icon = icon != null ? icon : HealthBarSprites.GetPlayerIcon();

        _glowImage = CreateImage("Outer Glow", transform, glow, 36f, 20f, 536f, 42f);
        _glowImage.color = new Color(0.184f, 0.847f, 1f, 0.35f);

        RectTransform mask = CreateRect("Fill Window Mask", transform, WindowX, WindowY, WindowWidth, WindowHeight);
        mask.gameObject.AddComponent<RectMask2D>();
        _trackImage = CreateImage("Dark Track", mask, HealthBarSprites.GetTrack(), 0f, 0f, WindowWidth, WindowHeight);
        _trackImage.color = Color.white;

        _ghostImage = CreateImage("Damage Ghost", mask, HealthBarSprites.GetPlayerFill(), 0f, 0f, WindowWidth, WindowHeight);
        _ghostImage.color = new Color(1f, 1f, 1f, 0.38f);
        _ghostRect = (RectTransform)_ghostImage.transform;

        _fillImage = CreateImage("Health Fill", mask, fill, 0f, 0f, WindowWidth, WindowHeight);
        _fillRect = (RectTransform)_fillImage.transform;
        _fillImage.color = Color.white;

        Image greenOverlay = CreateImage("Healing Overlay", mask, HealthBarSprites.GetWhite(), 0f, 0f, WindowWidth, WindowHeight);
        greenOverlay.color = new Color(0.302f, 1f, 0.533f, 0f);
        _healOverlayImage = greenOverlay;
        _healOverlayRect = (RectTransform)greenOverlay.transform;

        Image highlight = CreateImage("Gloss Highlight", mask, HealthBarSprites.GetPlayerHighlight(), 0f, 0f, WindowWidth, 11f);
        highlight.color = Color.white;
        _highlightRect = (RectTransform)highlight.transform;

        _sweepClipRect = CreateRect("Light Sweep Clip", mask, 0f, 0f, WindowWidth, WindowHeight);
        _sweepClipRect.gameObject.AddComponent<RectMask2D>();
        _sweepRect = CreateRect("Light Sweep", _sweepClipRect, -70f, 0f, 70f, WindowHeight);
        Image sweep = _sweepRect.gameObject.AddComponent<Image>();
        sweep.sprite = HealthBarSprites.GetPlayerSweep();
        sweep.raycastTarget = false;

        _segmentImages = new Image[9];
        for (int i = 0; i < _segmentImages.Length; i++)
        {
            float x = WindowWidth * (i + 1) / 10f - 0.5f;
            Image segment = CreateImage("Segment " + (i + 1), mask, HealthBarSprites.GetWhite(), x, 0f, 1f, WindowHeight);
            segment.color = new Color(0f, 0f, 0f, 0.45f);
            _segmentImages[i] = segment;
        }

        Image leftCap = CreateImage("Left End Cap", mask, endCapLeft, 0f, 0f, 8f, WindowHeight);
        leftCap.raycastTarget = false;
        Image rightCap = CreateImage("Right End Cap", mask, endCapRight, WindowWidth - 8f, 0f, 8f, WindowHeight);
        rightCap.raycastTarget = false;

        _flashImage = CreateImage("Damage Flash", mask, HealthBarSprites.GetWhite(), 0f, 0f, WindowWidth, WindowHeight);
        _flashImage.color = new Color(1f, 1f, 1f, 0f);
        _flashRect = (RectTransform)_flashImage.transform;

        Image frameImage = CreateImage("Frame", transform, frame, 0f, 0f, BarWidth, BarHeight);
        frameImage.type = Image.Type.Sliced;

        _sparkRoot = CreateRect("VFX Particles", transform, 0f, 0f, BarWidth, BarHeight);
        _particles = new Image[ParticleCount];
        _particleStates = new Particle[ParticleCount];
        Sprite particleSprite = HealthBarSprites.GetParticle();
        for (int i = 0; i < ParticleCount; i++)
        {
            Image particle = CreateImage("Particle " + i, _sparkRoot, particleSprite, 0f, 0f, 4f, 4f);
            particle.enabled = false;
            _particles[i] = particle;
        }

        Image iconImage = CreateImage("Player Icon", transform, icon, 12f, 30f, 20f, 20f);
        iconImage.raycastTarget = false;
        CreateLowHealthVignette();
        _baseScale = _root.localScale;
        _baseAnchoredPosition = _root.anchoredPosition;
    }

    private void BuildZombieBar()
    {
        frame = frame != null ? frame : HealthBarSprites.GetZombieFrame();
        fill = fill != null ? fill : HealthBarSprites.GetZombieFill();
        glow = glow != null ? glow : HealthBarSprites.GetZombieGlow();
        endCapLeft = endCapLeft != null ? endCapLeft : HealthBarSprites.GetTransparent();
        endCapRight = endCapRight != null ? endCapRight : HealthBarSprites.GetTransparent();
        icon = icon != null ? icon : HealthBarSprites.GetZombieIcon();

        _root.anchorMin = new Vector2(0.5f, 0.5f);
        _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = new Vector2(400f, 44f);
        _root.localScale = Vector3.one * 0.01f;

        _worldCanvas = GetComponent<Canvas>();
        if (_worldCanvas == null)
            _worldCanvas = gameObject.AddComponent<Canvas>();
        _worldCanvas.renderMode = RenderMode.WorldSpace;
        _worldCanvas.worldCamera = Camera.main;
        _worldCanvas.overrideSorting = true;
        _worldCanvas.sortingOrder = 20;
        _worldCamera = _worldCanvas.worldCamera;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _glowImage = CreateImage("Outer Glow", transform, glow, 38f, 6f, 348f, 32f);
        _glowImage.color = new Color(0.435f, 0.749f, 0.122f, 0.35f);

        RectTransform mask = CreateRect("Fill Window Mask", transform, _windowX, _windowY, _windowWidth, _windowHeight);
        mask.gameObject.AddComponent<RectMask2D>();
        _trackImage = CreateImage("Dark Track", mask, HealthBarSprites.GetTrack(), 0f, 0f, _windowWidth, _windowHeight);
        _trackImage.color = Color.white;

        _ghostImage = CreateImage("Damage Ghost", mask, fill, 0f, 0f, _windowWidth, _windowHeight);
        _ghostImage.color = new Color(1f, 1f, 1f, 0.38f);
        _ghostRect = (RectTransform)_ghostImage.transform;

        _fillImage = CreateImage("Health Fill", mask, fill, 0f, 0f, _windowWidth, _windowHeight);
        _fillRect = (RectTransform)_fillImage.transform;

        Image greenOverlay = CreateImage("Healing Overlay", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        greenOverlay.color = new Color(0.302f, 1f, 0.533f, 0f);
        _healOverlayImage = greenOverlay;
        _healOverlayRect = (RectTransform)greenOverlay.transform;

        Image highlight = CreateImage("Gloss Highlight", mask, HealthBarSprites.GetPlayerHighlight(), 0f, 0f, _windowWidth, 7f);
        highlight.color = Color.white;
        _highlightRect = (RectTransform)highlight.transform;

        _sweepClipRect = CreateRect("Light Sweep Clip", mask, 0f, 0f, _windowWidth, _windowHeight);
        _sweepClipRect.gameObject.AddComponent<RectMask2D>();
        _sweepRect = CreateRect("Light Sweep", _sweepClipRect, -70f, 0f, 70f, _windowHeight);
        Image sweep = _sweepRect.gameObject.AddComponent<Image>();
        sweep.sprite = HealthBarSprites.GetPlayerSweep();
        sweep.raycastTarget = false;

        _segmentImages = new Image[9];
        for (int i = 0; i < _segmentImages.Length; i++)
        {
            float x = _windowWidth * (i + 1) / 10f - 0.5f;
            Image segment = CreateImage("Segment " + (i + 1), mask, HealthBarSprites.GetWhite(), x, 0f, 1f, _windowHeight);
            segment.color = new Color(0f, 0f, 0f, 0.45f);
            _segmentImages[i] = segment;
        }

        Image leftCap = CreateImage("Left End Cap", mask, endCapLeft, 0f, 0f, 6f, _windowHeight);
        leftCap.raycastTarget = false;
        Image rightCap = CreateImage("Right End Cap", mask, endCapRight, _windowWidth - 6f, 0f, 6f, _windowHeight);
        rightCap.raycastTarget = false;

        _flashImage = CreateImage("Damage Flash", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        _flashImage.color = new Color(1f, 1f, 1f, 0f);
        _flashRect = (RectTransform)_flashImage.transform;

        Image frameImage = CreateImage("Frame", transform, frame, 0f, 0f, 400f, 44f);
        frameImage.type = Image.Type.Sliced;

        _sparkRoot = CreateRect("VFX Particles", transform, 0f, 0f, 400f, 44f);
        _particles = new Image[ParticleCount];
        _particleStates = new Particle[ParticleCount];
        for (int i = 0; i < ParticleCount; i++)
        {
            Image particle = CreateImage("Particle " + i, _sparkRoot, HealthBarSprites.GetZombieBloodDrop(), 0f, 0f, 4.4f, 4.4f);
            particle.enabled = false;
            _particles[i] = particle;
        }

        Image iconImage = CreateImage("Zombie Icon", transform, icon, 10f, 9f, 24f, 26f);
        iconImage.raycastTarget = false;
        _zombieDropTimers = new[] { 0f, 0.45f, 0.9f, 1.35f };
        _baseScale = _root.localScale;
        _baseAnchoredPosition = _root.anchoredPosition;
    }

    private void CreateLowHealthVignette()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            throw new InvalidOperationException("HealthBar must be placed under a Canvas.");
        Canvas rootCanvas = canvas.rootCanvas;
        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            throw new InvalidOperationException("The Player health bar requires a Screen Space - Overlay root Canvas.");

        RectTransform rect = CreateRect("Player Low Health Vignette", rootCanvas.transform, 0f, 0f, 0f, 0f);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        _vignetteImage = rect.gameObject.AddComponent<Image>();
        _vignetteImage.sprite = HealthBarSprites.GetPlayerVignette();
        _vignetteImage.color = new Color(1f, 0.078f, 0.157f, 0f);
        _vignetteImage.raycastTarget = false;
        _vignetteImage.enabled = false;
        rect.SetAsLastSibling();
    }

    private void UpdateHealthMotion()
    {
        if (!Mathf.Approximately(_displayHealth, _targetHealth))
        {
            _fillElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_fillElapsed / DamageFillDuration);
            float eased = 1f - Mathf.Exp(-_fillElapsed / 0.1f);
            float endpoint = 1f - Mathf.Exp(-DamageFillDuration / 0.1f);
            float progress = t >= 1f ? 1f : eased / endpoint;
            _displayHealth = Mathf.Lerp(_displayStartHealth, _targetHealth, Mathf.Clamp01(progress));
        }

        if (_ghostIsAnimating)
        {
            _ghostElapsed += Time.deltaTime;
            if (_ghostElapsed >= DamageGhostDelay)
            {
                float t = Mathf.Clamp01((_ghostElapsed - DamageGhostDelay) / DamageGhostDuration);
                float easeOut = 1f - (1f - t) * (1f - t);
                _ghostHealth = Mathf.Lerp(_ghostStartHealth, _targetHealth, easeOut);
                if (t >= 1f)
                {
                    _ghostHealth = _targetHealth;
                    _ghostIsAnimating = false;
                }
            }
        }

        RefreshBarWidths();
    }

    private void RefreshBarWidths()
    {
        if (_fillRect == null || _ghostRect == null)
            return;

        SetWidth(_fillRect, _windowWidth * Mathf.Clamp01(_displayHealth / _maxHealth));
        SetWidth(_ghostRect, _windowWidth * Mathf.Clamp01(_ghostHealth / _maxHealth));
        float displayedWidth = _windowWidth * Mathf.Clamp01(_displayHealth / _maxHealth);
        SetWidth(_healOverlayRect, displayedWidth);
        SetWidth(_highlightRect, displayedWidth);
        SetWidth(_flashRect, displayedWidth);
        SetWidth(_sweepClipRect, displayedWidth);
    }

    private static void SetWidth(RectTransform rect, float width)
    {
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
    }

    private void UpdateEffects()
    {
        float deltaTime = Time.deltaTime;
        _flashTimer = Mathf.Max(0f, _flashTimer - deltaTime);
        _shakeTimer = Mathf.Max(0f, _shakeTimer - deltaTime);
        _healTimer = Mathf.Max(0f, _healTimer - deltaTime);

        float flashAlpha = _flashTimer > 0f ? 0.9f * (_flashTimer / 0.1f) : 0f;
        _flashImage.color = new Color(1f, 1f, 1f, flashAlpha);

        float healProgress = _healTimer / 0.4f;
        _healOverlayImage.color = new Color(0.302f, 1f, 0.533f, 0.5f * healProgress);

        if (_shakeTimer > 0f)
        {
            _root.anchoredPosition = _baseAnchoredPosition + new Vector2(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f));
        }
        else
        {
            _root.anchoredPosition = _baseAnchoredPosition;
        }

        float healthRatio = Mathf.Clamp01(_targetHealth / _maxHealth);
        if (healthRatio < 0.25f)
        {
            float pulse = 1f + 0.015f * (1f - Mathf.Cos(2f * Mathf.PI * Time.time / 0.6f));
            _root.localScale = _baseScale * pulse;
            float glowAlpha = Mathf.Lerp(0.6f, 0.9f, (1f + Mathf.Sin(2f * Mathf.PI * Time.time / 0.6f)) * 0.5f);
            _glowImage.color = Color.Lerp(new Color(0.184f, 0.847f, 1f, glowAlpha), new Color(0.302f, 1f, 0.533f, glowAlpha), _healTimer / 0.4f);
            _vignetteTimer = Mathf.Repeat(Time.time, 1f);
            _vignetteImage.enabled = true;
            _vignetteImage.color = new Color(1f, 0.078f, 0.157f, _vignetteTimer <= 0.5f ? _vignetteTimer * 2f : (1f - _vignetteTimer) * 2f);
        }
        else
        {
            _root.localScale = _baseScale;
            float healGlow = _healTimer / 0.4f;
            _glowImage.color = Color.Lerp(new Color(0.184f, 0.847f, 1f, 0.35f), new Color(0.302f, 1f, 0.533f, 0.8f), healGlow);
            if (_vignetteImage.enabled)
            {
                _vignetteImage.enabled = false;
                _vignetteImage.color = new Color(1f, 0.078f, 0.157f, 0f);
            }
        }

        float sweepTime = Mathf.Repeat(Time.time, 3f) / 3f;
        _sweepRect.anchoredPosition = new Vector2(Mathf.Lerp(-70f, WindowWidth, sweepTime), 0f);
    }

    private void UpdateZombieVisibility()
    {
        float deltaTime = Time.deltaTime;
        _glitchTimer = Mathf.Max(0f, _glitchTimer - deltaTime);
        _flashTimer = Mathf.Max(0f, _flashTimer - deltaTime);
        _flashImage.color = new Color(1f, 1f, 1f, _flashTimer > 0f ? 0.9f * (_flashTimer / 0.1f) : 0f);

        if (_proximityTarget == null && Time.time >= _nextPlayerSearchTime)
        {
            PlayerController player = PlayerController.Instance;
            if (player != null)
                _proximityTarget = player.transform;
            _nextPlayerSearchTime = Time.time + 1f;
        }

        bool nearby = !_enemyDead && _proximityTarget != null &&
                      (_proximityTarget.position - transform.position).sqrMagnitude <= 225f;
        bool recentlyDamaged = Time.time - _lastDamageTime <= 4f;
        if (_enemyDead)
        {
            _visibilityAlpha = 0f;
        }
        else if (nearby || recentlyDamaged)
        {
            _visibilityAlpha = 1f;
        }
        else
        {
            _visibilityAlpha = Mathf.Lerp(_visibilityAlpha, 0f, 1f - Mathf.Exp(-deltaTime / 0.25f));
        }

        if (_worldCamera == null && Time.unscaledTime >= _nextCameraSearchTime)
        {
            _worldCamera = Camera.main;
            _worldCanvas.worldCamera = _worldCamera;
            _nextCameraSearchTime = Time.unscaledTime + 1f;
        }
        if (_worldCamera != null)
        {
            Vector3 toCamera = _worldCamera.transform.position - transform.position;
            if (toCamera.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }

        float flicker = _glitchTimer > 0f && UnityEngine.Random.value < 0.5f ? 0.35f : 1f;
        _canvasGroup.alpha = _visibilityAlpha * flicker;
    }

    private void UpdateZombieBloodDrops()
    {
        for (int i = 0; i < _zombieDropTimers.Length; i++)
        {
            _zombieDropTimers[i] -= Time.deltaTime;
            if (_zombieDropTimers[i] > 0f)
                continue;

            SpawnParticle(new Vector2(96f + i * 80f, 40f), 2.2f, Color.white, 0f, 0f, 176f, 0.5f);
            _zombieDropTimers[i] = 1.8f;
        }
    }

    private void SpawnDamageSparks(float healthRatio)
    {
        Vector2 origin = new Vector2(_windowX + _windowWidth * healthRatio, _windowY + _windowHeight * 0.5f);
        for (int i = 0; i < 14; i++)
        {
            Color color;
            float choice = UnityEngine.Random.value;
            if (choice < 0.55f)
                color = new Color(1f, 0.824f, 0.227f, 1f);
            else if (choice < 0.88f)
                color = new Color(1f, 0.604f, 0.122f, 1f);
            else
                color = Color.white;

            SpawnParticle(origin, UnityEngine.Random.Range(1.2f, 2.6f), color,
                UnityEngine.Random.Range(-30f, 100f), UnityEngine.Random.Range(40f, 170f),
                320f, UnityEngine.Random.Range(0.45f, 0.75f));
        }
    }

    private void SpawnHealParticles(float healthRatio)
    {
        float width = _windowWidth * Mathf.Clamp01(healthRatio);
        for (int i = 0; i < 8; i++)
        {
            Vector2 origin = new Vector2(_windowX + UnityEngine.Random.Range(0f, width), _windowY + _windowHeight);
            SpawnParticle(origin, 2f, new Color(0.49f, 1f, 0.604f, 1f), 0f,
                UnityEngine.Random.Range(30f, 70f), 0f, 0.9f);
        }
    }

    private void SpawnParticle(Vector2 origin, float radius, Color color, float velocityX, float velocityY, float gravity, float lifetime)
    {
        for (int i = 0; i < _particleStates.Length; i++)
        {
            if (_particleStates[i].Active)
                continue;

            _particleStates[i] = new Particle
            {
                Position = origin,
                VelocityX = velocityX,
                VelocityY = velocityY,
                Gravity = gravity,
                Radius = radius,
                Lifetime = lifetime,
                Color = color,
                Active = true
            };
            _particles[i].enabled = true;
            _particles[i].sprite = _isZombie ? HealthBarSprites.GetZombieBloodDrop() : HealthBarSprites.GetParticle();
            _particles[i].rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            break;
        }
    }

    private void UpdateParticles()
    {
        float deltaTime = Time.deltaTime;
        for (int i = 0; i < _particleStates.Length; i++)
        {
            Particle particle = _particleStates[i];
            if (!particle.Active)
                continue;

            particle.Age += deltaTime;
            if (particle.Age >= particle.Lifetime)
            {
                particle.Active = false;
                _particles[i].enabled = false;
                _particleStates[i] = particle;
                continue;
            }

            float life = particle.Age / particle.Lifetime;
            float rise = particle.VelocityY * particle.Age - 0.5f * particle.Gravity * particle.Age * particle.Age;
            float x = particle.Position.x + particle.VelocityX * particle.Age;
            float y = particle.Position.y - rise;
            RectTransform rect = _particles[i].rectTransform;
            rect.anchoredPosition = new Vector2(x, -y);
            Color color = particle.Color;
            color.a = 1f - life;
            _particles[i].color = color;
            _particleStates[i] = particle;
        }
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, float x, float y, float width, float height)
    {
        RectTransform rect = CreateRect(objectName, parent, x, y, width, height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent, float x, float y, float width, float height)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
}
