/*
 * HEALTH BAR — THEMED UI SYSTEM
 * ==============================
 * Unity 6 uGUI + TextMeshPro responsive health bar system matching target SVG reference geometry.
 *
 * All 4 Themed Health Bars:
 * 1. Player  (Top-Left Screen Space, 600x80): Brushed steel, carbon-fiber pattern, cyan accents, left bolt, red fill.
 * 2. Zombie  (World Space / Screen Space, 400x44): Jagged wood/rust, skull icon, cracks, falling blood drops, dark red fill.
 * 3. Spider  (World Space / Screen Space, 480x80): Chitin hex, spider icon, fangs below, web lines, neon green fill.
 * 4. Boss    (Bottom-Center Screen Space, 700x96): Dual serpent heads, gold scales, red diamond jewel, emerald fill,
 *             "NAGA — GUARDIAN OF THE HEART" title plate, phase notches, embers & smoke.
 *
 * API:
 * - healthBar.SetMaxHealth(float maxHP)
 * - healthBar.SetHealth(float currentHP)
 */
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BarType { Player, Zombie, Spider, Boss }

public class HealthBar : MonoBehaviour
{
    public float CurrentHealth => _targetHealth;
    public float MaxHealth => _maxHealth;

    [Header("Bar Settings")]
    [UnityEngine.Serialization.FormerlySerializedAs("type")]
    public BarType barType = BarType.Player;

    [Header("Sprites (Optional overrides — empty uses procedural SVG art)")]
    public Sprite frame;
    public Sprite fill;
    public Sprite glow;
    public Sprite endCapLeft;
    public Sprite endCapRight;
    public Sprite icon;

    [Header("World Space Sizing")]
    [Tooltip("World space health bar scale multiplier (1.0 = normal, 2.0 = double size, 3.0 = bada)")]
    [Range(0.2f, 10f)]
    public float worldScale = 2.0f;

    private const int ParticlePoolSize = 24;
    private const float DamageFillDuration = 0.3f;
    private const float DamageGhostDelay = 0.5f;
    private const float DamageGhostDuration = 0.9f;

    // Dimensions
    private float _barWidth = 600f;
    private float _barHeight = 80f;
    private float _windowX = 44f;
    private float _windowY = 28f;
    private float _windowWidth = 520f;
    private float _windowHeight = 26f;

    // Components & Layer Hierarchy
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
    private Transform _trackedSubject;
    private Transform _anchor;
    private Transform _customAnchor;
    private Renderer[] _anchorRenderers;
    private float _headGap = 0.2f;
    private float _worldWidth = 1.5f;

    private Image[] _segmentImages;
    private Image[] _particles;
    private Particle[] _particleStates;

    // Timers & States
    private float[] _dropTimers;
    private Vector2 _baseAnchoredPosition;
    private Vector3 _baseScale;

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
    private float _shakeIntensity = 3f;
    private float _healTimer;
    private float _vignetteTimer;
    private float _lastDamageTime = -9999f;
    private float _glitchTimer;
    private float _visibilityAlpha = 1f;
    private float _nextPlayerSearchTime;
    private float _nextCameraSearchTime;
    private float _bubbleTimer;
    private float _emberTimer;

    private bool _isWorldSpace;
    public bool IsWorldSpace => _isWorldSpace;
    private bool _enemyDead;
    private bool _ghostIsAnimating;
    private bool _built;
    private float _prevHealthRatio = 1f;

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
        if (!_built)
            BuildCurrentBar();
    }

    public void SetBarType(BarType type)
    {
        if (barType == type && _built) return;
        barType = type;
        Rebuild();
    }

    public void Rebuild()
    {
        if (_built)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            _built = false;
        }
        BuildCurrentBar();
    }

    private void BuildCurrentBar()
    {
        _root = transform as RectTransform;
        if (_root == null)
            _root = gameObject.AddComponent<RectTransform>();

        _isWorldSpace = false;
        switch (barType)
        {
            case BarType.Player:
                _barWidth = 600f; _barHeight = 80f;
                _windowX = 44f; _windowY = 28f; _windowWidth = 520f; _windowHeight = 26f;
                BuildPlayerBar();
                break;

            case BarType.Zombie:
                _barWidth = 400f; _barHeight = 44f;
                _windowX = 46f; _windowY = 14f; _windowWidth = 332f; _windowHeight = 16f;
                BuildZombieBar();
                break;

            case BarType.Spider:
                _barWidth = 480f; _barHeight = 80f;
                _windowX = 52f; _windowY = 18f; _windowWidth = 376f; _windowHeight = 24f;
                BuildSpiderBar();
                break;

            case BarType.Boss:
                _barWidth = 700f; _barHeight = 96f;
                _windowX = 100f; _windowY = 34f; _windowWidth = 500f; _windowHeight = 28f;
                BuildBossBar();
                break;
        }

        _baseAnchoredPosition = _root.anchoredPosition;
        _baseScale = _root.localScale;
        _built = true;
        SetMaxHealth(_maxHealth);
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

    private void OnValidate()
    {
        if (_root != null && _isWorldSpace)
        {
            ApplyWorldScale();
        }
    }

    private void Update()
    {
        if (!_built)
            return;

        UpdateHealthMotion();

        switch (barType)
        {
            case BarType.Player:
                UpdateEffects();
                break;

            case BarType.Zombie:
                UpdateWorldVisibility();
                UpdateZombieBloodDrops();
                break;

            case BarType.Spider:
                UpdateWorldVisibility();
                UpdateSpiderBubbles();
                break;

            case BarType.Boss:
                UpdateWorldVisibility();
                UpdateBossEmbersAndEffects();
                break;
        }

        UpdateParticles();
    }

    private void LateUpdate()
    {
        if (!_built)
            return;

        if (_customAnchor != null)
        {
            transform.position = _customAnchor.position;
        }
        else if (_anchor != null)
        {
            transform.position = _anchor.position + Vector3.up * _headGap;
        }

        Camera cam = _worldCamera != null ? _worldCamera : Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    public void SetMaxHealth(float maxHealth)
    {
        if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth <= 0f)
            maxHealth = 1f;

        _maxHealth = maxHealth;
        _targetHealth = Mathf.Clamp(_targetHealth, 0f, _maxHealth);
        _displayHealth = _targetHealth;
        _displayStartHealth = _targetHealth;
        _ghostHealth = _targetHealth;
        _ghostStartHealth = _targetHealth;
        _fillElapsed = 0f;
        _ghostElapsed = 0f;
        _ghostIsAnimating = false;
        _prevHealthRatio = Mathf.Clamp01(_targetHealth / _maxHealth);
        RefreshBarWidths();
    }

    public void SetHealth(float health)
    {
        if (float.IsNaN(health) || float.IsInfinity(health))
            return;
        if (_maxHealth <= 0f)
            _maxHealth = 1f;
        float nextHealth = Mathf.Clamp(health, 0f, _maxHealth);
        if (Mathf.Approximately(nextHealth, _targetHealth))
            return;

        bool damaged = nextHealth < _targetHealth;
        float oldRatio = _targetHealth / _maxHealth;
        float newRatio = nextHealth / _maxHealth;

        _targetHealth = nextHealth;
        _displayStartHealth = _displayHealth;
        _fillElapsed = 0f;

        if (damaged)
        {
            _ghostStartHealth = _ghostHealth;
            _ghostElapsed = 0f;
            _ghostIsAnimating = true;
            _flashTimer = 0.1f;
            _lastDamageTime = Time.time;

            if (barType == BarType.Zombie)
            {
                _glitchTimer = 0.15f;
            }
            else if (barType == BarType.Spider)
            {
                _glitchTimer = 0.15f;
                SpawnAcidBurst(newRatio);
            }
            else if (barType == BarType.Boss)
            {
                _shakeTimer = 0.15f;
                _shakeIntensity = 3.5f;
                SpawnDamageSparks(newRatio);

                // Check phase notch crossings: 75%, 50%, 25%
                CheckBossPhaseCrossing(oldRatio, newRatio);
            }
            else // Player
            {
                _shakeTimer = 0.15f;
                _shakeIntensity = 3f;
                SpawnDamageSparks(newRatio);
            }
        }
        else
        {
            _ghostHealth = nextHealth;
            _ghostStartHealth = nextHealth;
            _ghostIsAnimating = false;

            if (barType == BarType.Player || barType == BarType.Boss)
            {
                _healTimer = 0.4f;
                SpawnHealParticles(newRatio);
            }
        }

        _prevHealthRatio = newRatio;
    }

    /// <summary>
    /// Screen-space player bar: top-left of the HUD, matching the reference layout.
    /// </summary>
    public void PinScreenTopLeft()
    {
        RectTransform rt = transform as RectTransform;
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -24f);
        rt.sizeDelta = new Vector2(600f, 80f);
        rt.localScale = Vector3.one;
        _baseAnchoredPosition = rt.anchoredPosition;
        _baseScale = rt.localScale;
    }

    /// <summary>
    /// Screen-space Naga bar: bottom-center, 700x96, title plate sitting just above the frame.
    /// </summary>
    public void PinScreenBottomCenter()
    {
        _isWorldSpace = false;
        _anchor = null;

        Canvas ownCanvas = GetComponent<Canvas>();
        if (ownCanvas != null && ownCanvas.renderMode == RenderMode.WorldSpace)
            ownCanvas.enabled = false;

        RectTransform rt = transform as RectTransform;
        if (rt == null)
            return;

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 28f);
        rt.sizeDelta = new Vector2(700f, 96f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        _baseAnchoredPosition = rt.anchoredPosition;
        _baseScale = rt.localScale;

        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.alpha = 1f;
        _visibilityAlpha = 1f;
    }

    public void SetTrackedSubject(Transform subject)
    {
        _trackedSubject = subject;
    }

    public void SetProximityTarget(Transform target)
    {
        _proximityTarget = target;
    }

    /// <summary>
    /// Live scale multiplier updater (called when changing Inspector slider).
    /// </summary>
    public void SetWorldScale(float scale)
    {
        worldScale = Mathf.Max(0.1f, scale);
        ApplyWorldScale();
    }

    public void ApplyWorldScale()
    {
        if (_root == null) return;
        float baseFactor = barType == BarType.Boss ? 0.008f : 0.01f;
        float s = baseFactor * worldScale;
        _root.localScale = new Vector3(s, s, s);
        _baseScale = _root.localScale;
    }

    /// <summary>
    /// Locks this health bar directly to a custom empty GameObject anchor (e.g. "healthbar position").
    /// Stays at that anchor's position, billboard-faces the camera, and scales cleanly in world space.
    /// </summary>
    public void SetCustomAnchor(Transform anchorTransform, float scaleMultiplier = 2.0f)
    {
        _customAnchor = anchorTransform;
        _anchor = anchorTransform;
        _trackedSubject = anchorTransform;
        _isWorldSpace = true;
        worldScale = scaleMultiplier;

        // Keep parented to anchor to move naturally with it
        transform.SetParent(anchorTransform, false);
        transform.localPosition = Vector3.zero;

        ConfigureWorldSpaceCanvas();
        ApplyWorldScale();

        _visibilityAlpha = 1f;
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        Camera cam = _worldCamera != null ? _worldCamera : Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    /// <summary>
    /// Locks this bar to an enemy root with a height offset and scale multiplier.
    /// Follows enemy automatically and stays above ground.
    /// </summary>
    public void SetWorldPositionAnchor(Transform enemyRoot, float heightOffset, float scaleMultiplier = 2.0f)
    {
        _anchor = enemyRoot;
        _trackedSubject = enemyRoot;
        _customAnchor = null;
        _isWorldSpace = true;
        _headGap = heightOffset;
        worldScale = scaleMultiplier;

        transform.SetParent(enemyRoot, false);
        transform.localPosition = new Vector3(0f, heightOffset, 0f);

        ConfigureWorldSpaceCanvas();
        ApplyWorldScale();

        _visibilityAlpha = 1f;
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        Camera cam = _worldCamera != null ? _worldCamera : Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    /// <summary>
    /// Locks this bar in world space above the character mesh with an explicit scale multiplier.
    /// </summary>
    public void AttachAbove(Transform anchor, float gapAboveHead, float scaleMultiplier = 2.0f)
    {
        _anchor = anchor;
        _trackedSubject = anchor;
        _customAnchor = null;
        _headGap = Mathf.Max(0.5f, gapAboveHead);
        worldScale = scaleMultiplier;
        _anchorRenderers = null;
        _isWorldSpace = true;

        transform.SetParent(anchor, false);
        transform.localPosition = new Vector3(0f, _headGap, 0f);

        ConfigureWorldSpaceCanvas();
        ApplyWorldScale();

        _visibilityAlpha = 1f;
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;
    }

    public void SetEnemyDead()
    {
        _enemyDead = true;
        _visibilityAlpha = 0f;
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BUILDER: PLAYER (600x80)
    // ═══════════════════════════════════════════════════════════════════════════

    private void BuildPlayerBar()
    {
        bool hasCustomFrame = this.frame != null;
        frame = hasCustomFrame ? this.frame : HealthBarSprites.GetPlayerFrame();
        fill = fill != null ? fill : HealthBarSprites.GetPlayerFill();
        glow = glow != null ? glow : HealthBarSprites.GetPlayerGlow();
        endCapLeft = endCapLeft != null ? endCapLeft : HealthBarSprites.GetTransparent();
        endCapRight = endCapRight != null ? endCapRight : HealthBarSprites.GetTransparent();
        icon = icon != null ? icon : HealthBarSprites.GetPlayerIcon();

        // 1. Glow behind
        _glowImage = CreateImage("Outer Glow", transform, glow, 36f, 20f, 536f, 42f);
        _glowImage.color = new Color(0.184f, 0.847f, 1f, 0.35f);

        // 2. Fill Window Mask with RectMask2D
        RectTransform mask = CreateRect("Fill Window Mask", transform, _windowX, _windowY, _windowWidth, _windowHeight);
        mask.gameObject.AddComponent<RectMask2D>();

        // 3. Track
        _trackImage = CreateImage("Dark Track", mask, HealthBarSprites.GetTrack(), 0f, 0f, _windowWidth, _windowHeight);
        _trackImage.color = Color.white;

        // 4. White Ghost Bar
        _ghostImage = CreateImage("Damage Ghost", mask, HealthBarSprites.GetPlayerFill(), 0f, 0f, _windowWidth, _windowHeight);
        _ghostImage.color = new Color(1f, 1f, 1f, 0.38f);
        _ghostRect = (RectTransform)_ghostImage.transform;

        // 5. Fill
        _fillImage = CreateImage("Health Fill", mask, fill, 0f, 0f, _windowWidth, _windowHeight);
        _fillRect = (RectTransform)_fillImage.transform;

        // 6. Healing Overlay
        Image greenOverlay = CreateImage("Healing Overlay", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        greenOverlay.color = new Color(0.302f, 1f, 0.533f, 0f);
        _healOverlayImage = greenOverlay;
        _healOverlayRect = (RectTransform)greenOverlay.transform;

        // 7. Gloss Highlight
        Image highlight = CreateImage("Gloss Highlight", mask, HealthBarSprites.GetPlayerHighlight(), 0f, 0f, _windowWidth, 11f);
        highlight.color = Color.white;
        _highlightRect = (RectTransform)highlight.transform;

        // 8. Light Sweep
        _sweepClipRect = CreateRect("Light Sweep Clip", mask, 0f, 0f, _windowWidth, _windowHeight);
        _sweepClipRect.gameObject.AddComponent<RectMask2D>();
        _sweepRect = CreateRect("Light Sweep", _sweepClipRect, -70f, 0f, 70f, _windowHeight);
        Image sweep = _sweepRect.gameObject.AddComponent<Image>();
        sweep.sprite = HealthBarSprites.GetPlayerSweep();
        sweep.raycastTarget = false;

        // 9. Segments every 10%
        _segmentImages = new Image[9];
        for (int i = 0; i < 9; i++)
        {
            float x = _windowWidth * (i + 1) / 10f - 0.5f;
            Image seg = CreateImage("Segment " + (i + 1), mask, HealthBarSprites.GetWhite(), x, 0f, 1f, _windowHeight);
            seg.color = new Color(0f, 0f, 0f, 0.45f);
            _segmentImages[i] = seg;
        }

        // 10. Damage Flash
        _flashImage = CreateImage("Damage Flash", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        _flashImage.color = new Color(1f, 1f, 1f, 0f);
        _flashRect = (RectTransform)_flashImage.transform;

        // 11. Frame on top
        Image frameImage = CreateImage("Frame", transform, frame, 0f, 0f, _barWidth, _barHeight);
        frameImage.type = hasCustomFrame ? Image.Type.Sliced : Image.Type.Simple;

        // 12. Particles Pool
        SetupParticlePool(HealthBarSprites.GetParticle(), 4f);

        // 13. Left Bolt Icon (Center 22, 40)
        Image iconImage = CreateImage("Player Icon", transform, icon, 12f, 30f, 20f, 20f);
        iconImage.raycastTarget = false;

        // 14. Low Health Vignette
        CreateLowHealthVignette();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BUILDER: ZOMBIE (400x44)
    // ═══════════════════════════════════════════════════════════════════════════

    private void BuildZombieBar()
    {
        frame = frame != null ? frame : HealthBarSprites.GetZombieFrame();
        fill = fill != null ? fill : HealthBarSprites.GetZombieFill();
        glow = glow != null ? glow : HealthBarSprites.GetZombieGlow();
        icon = icon != null ? icon : HealthBarSprites.GetZombieIcon();

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
        for (int i = 0; i < 9; i++)
        {
            float x = _windowWidth * (i + 1) / 10f - 0.5f;
            Image seg = CreateImage("Segment " + (i + 1), mask, HealthBarSprites.GetWhite(), x, 0f, 1f, _windowHeight);
            seg.color = new Color(0f, 0f, 0f, 0.45f);
            _segmentImages[i] = seg;
        }

        _flashImage = CreateImage("Damage Flash", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        _flashImage.color = new Color(1f, 1f, 1f, 0f);
        _flashRect = (RectTransform)_flashImage.transform;

        Image frameImage = CreateImage("Frame", transform, frame, 0f, 0f, _barWidth, _barHeight);
        frameImage.type = Image.Type.Simple;

        SetupParticlePool(HealthBarSprites.GetZombieBloodDrop(), 4.4f);

        Image iconImage = CreateImage("Zombie Icon", transform, icon, 10f, 9f, 24f, 26f);
        iconImage.raycastTarget = false;

        _dropTimers = new[] { 0f, 0.45f, 0.9f, 1.35f };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BUILDER: SPIDER (480x80)
    // ═══════════════════════════════════════════════════════════════════════════

    private void BuildSpiderBar()
    {
        frame = frame != null ? frame : HealthBarSprites.GetSpiderFrame();
        fill = fill != null ? fill : HealthBarSprites.GetSpiderFill();
        glow = glow != null ? glow : HealthBarSprites.GetSpiderGlow();
        icon = icon != null ? icon : HealthBarSprites.GetSpiderIcon();

        _glowImage = CreateImage("Outer Glow", transform, glow, 44f, 10f, 392f, 40f);
        _glowImage.color = new Color(0.22f, 1f, 0.35f, 0.40f);

        RectTransform mask = CreateRect("Fill Window Mask", transform, _windowX, _windowY, _windowWidth, _windowHeight);
        mask.gameObject.AddComponent<RectMask2D>();

        _trackImage = CreateImage("Dark Track", mask, HealthBarSprites.GetTrack(), 0f, 0f, _windowWidth, _windowHeight);
        _trackImage.color = Color.white;

        _ghostImage = CreateImage("Damage Ghost", mask, fill, 0f, 0f, _windowWidth, _windowHeight);
        _ghostImage.color = new Color(1f, 1f, 1f, 0.38f);
        _ghostRect = (RectTransform)_ghostImage.transform;

        _fillImage = CreateImage("Health Fill", mask, fill, 0f, 0f, _windowWidth, _windowHeight);
        _fillRect = (RectTransform)_fillImage.transform;

        Image highlight = CreateImage("Gloss Highlight", mask, HealthBarSprites.GetPlayerHighlight(), 0f, 0f, _windowWidth, 10f);
        highlight.color = Color.white;
        _highlightRect = (RectTransform)highlight.transform;

        _sweepClipRect = CreateRect("Light Sweep Clip", mask, 0f, 0f, _windowWidth, _windowHeight);
        _sweepClipRect.gameObject.AddComponent<RectMask2D>();
        _sweepRect = CreateRect("Light Sweep", _sweepClipRect, -70f, 0f, 70f, _windowHeight);
        Image sweep = _sweepRect.gameObject.AddComponent<Image>();
        sweep.sprite = HealthBarSprites.GetPlayerSweep();
        sweep.raycastTarget = false;

        _segmentImages = new Image[9];
        for (int i = 0; i < 9; i++)
        {
            float x = _windowWidth * (i + 1) / 10f - 0.5f;
            Image seg = CreateImage("Segment " + (i + 1), mask, HealthBarSprites.GetWhite(), x, 0f, 1f, _windowHeight);
            seg.color = new Color(0f, 0f, 0f, 0.45f);
            _segmentImages[i] = seg;
        }

        _flashImage = CreateImage("Damage Flash", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        _flashImage.color = new Color(1f, 1f, 1f, 0f);
        _flashRect = (RectTransform)_flashImage.transform;

        Image frameImage = CreateImage("Frame", transform, frame, 0f, 0f, _barWidth, _barHeight);
        frameImage.type = Image.Type.Simple;

        SetupParticlePool(HealthBarSprites.GetSpiderAcidDrop(), 4.0f);

        Image iconImage = CreateImage("Spider Icon", transform, icon, 18f, 18f, 24f, 24f);
        iconImage.raycastTarget = false;

        _bubbleTimer = 0f;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BUILDER: BOSS (NAGA — GUARDIAN OF THE HEART, 700x96)
    // ═══════════════════════════════════════════════════════════════════════════

    private void BuildBossBar()
    {
        frame = frame != null ? frame : HealthBarSprites.GetBossFrame();
        fill = fill != null ? fill : HealthBarSprites.GetBossFill();
        glow = glow != null ? glow : HealthBarSprites.GetBossGlow();
        endCapLeft = endCapLeft != null ? endCapLeft : HealthBarSprites.GetBossEndCapLeft();
        endCapRight = endCapRight != null ? endCapRight : HealthBarSprites.GetBossEndCapRight();

        // 1. Glow Behind
        _glowImage = CreateImage("Outer Glow", transform, glow, 92f, 26f, 516f, 44f);
        _glowImage.color = new Color(1f, 0.72f, 0.18f, 0.35f);

        // 2. Fill Window Mask with RectMask2D
        RectTransform mask = CreateRect("Fill Window Mask", transform, _windowX, _windowY, _windowWidth, _windowHeight);
        mask.gameObject.AddComponent<RectMask2D>();

        // 3. Dark Track
        _trackImage = CreateImage("Dark Track", mask, HealthBarSprites.GetTrack(), 0f, 0f, _windowWidth, _windowHeight);
        _trackImage.color = Color.white;

        // 4. White Ghost Bar
        _ghostImage = CreateImage("Damage Ghost", mask, fill, 0f, 0f, _windowWidth, _windowHeight);
        _ghostImage.color = new Color(1f, 1f, 1f, 0.38f);
        _ghostRect = (RectTransform)_ghostImage.transform;

        // 5. Emerald Fill
        _fillImage = CreateImage("Health Fill", mask, fill, 0f, 0f, _windowWidth, _windowHeight);
        _fillRect = (RectTransform)_fillImage.transform;

        // 6. Healing Overlay
        Image greenOverlay = CreateImage("Healing Overlay", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        greenOverlay.color = new Color(0.302f, 1f, 0.533f, 0f);
        _healOverlayImage = greenOverlay;
        _healOverlayRect = (RectTransform)greenOverlay.transform;

        // 7. Gloss Highlight
        Image highlight = CreateImage("Gloss Highlight", mask, HealthBarSprites.GetPlayerHighlight(), 0f, 0f, _windowWidth, 11f);
        highlight.color = Color.white;
        _highlightRect = (RectTransform)highlight.transform;

        // 8. Light Sweep
        _sweepClipRect = CreateRect("Light Sweep Clip", mask, 0f, 0f, _windowWidth, _windowHeight);
        _sweepClipRect.gameObject.AddComponent<RectMask2D>();
        _sweepRect = CreateRect("Light Sweep", _sweepClipRect, -70f, 0f, 70f, _windowHeight);
        Image sweep = _sweepRect.gameObject.AddComponent<Image>();
        sweep.sprite = HealthBarSprites.GetPlayerSweep();
        sweep.raycastTarget = false;

        // 9. Segment lines
        _segmentImages = new Image[9];
        for (int i = 0; i < 9; i++)
        {
            float x = _windowWidth * (i + 1) / 10f - 0.5f;
            Image seg = CreateImage("Segment " + (i + 1), mask, HealthBarSprites.GetWhite(), x, 0f, 1f, _windowHeight);
            seg.color = new Color(0f, 0f, 0f, 0.45f);
            _segmentImages[i] = seg;
        }

        // 10. Damage Flash
        _flashImage = CreateImage("Damage Flash", mask, HealthBarSprites.GetWhite(), 0f, 0f, _windowWidth, _windowHeight);
        _flashImage.color = new Color(1f, 1f, 1f, 0f);
        _flashRect = (RectTransform)_flashImage.transform;

        // 11. Gold Scale Frame on Top
        Image frameImage = CreateImage("Frame", transform, frame, 0f, 0f, _barWidth, _barHeight);
        frameImage.type = Image.Type.Simple;

        // 12. Snake Head Endcaps
        Image leftHead = CreateImage("Left Snake Head", transform, endCapLeft, 0f, 0f, 76f, 96f);
        leftHead.raycastTarget = false;
        Image rightHead = CreateImage("Right Snake Head", transform, endCapRight, 624f, 0f, 76f, 96f);
        rightHead.raycastTarget = false;

        // 13. Title Plate Above Bar
        RectTransform plateRect = CreateRect("Title Plate", transform, 100f, -44f, 500f, 38f);
        Image plateImg = plateRect.gameObject.AddComponent<Image>();
        plateImg.sprite = HealthBarSprites.GetBossNamePlate();
        plateImg.raycastTarget = false;

        // Title Text with TextMeshPro
        GameObject textObj = new GameObject("Boss Name", typeof(RectTransform));
        textObj.layer = transform.gameObject.layer;
        textObj.transform.SetParent(plateRect, false);
        RectTransform textRect = (RectTransform)textObj.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = "NAGA — GUARDIAN OF THE HEART";
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 6f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.961f, 0.827f, 0.416f, 1f);
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;

        // 14. VFX Particle Pool (Embers & Smoke)
        SetupParticlePool(HealthBarSprites.GetParticle(), 3.5f);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  VFX & MOTION UPDATES
    // ═══════════════════════════════════════════════════════════════════════════

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

        float ratio = Mathf.Clamp01(_displayHealth / _maxHealth);
        float ghostRatio = Mathf.Clamp01(_ghostHealth / _maxHealth);

        SetWidth(_fillRect, _windowWidth * ratio);
        SetWidth(_ghostRect, _windowWidth * ghostRatio);

        float displayedWidth = _windowWidth * ratio;
        if (_healOverlayRect != null) SetWidth(_healOverlayRect, displayedWidth);
        if (_highlightRect != null) SetWidth(_highlightRect, displayedWidth);
        if (_flashRect != null) SetWidth(_flashRect, displayedWidth);
        if (_sweepClipRect != null) SetWidth(_sweepClipRect, displayedWidth);
    }

    private static void SetWidth(RectTransform rect, float width)
    {
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
    }

    private void UpdateEffects()
    {
        float dt = Time.deltaTime;
        _flashTimer = Mathf.Max(0f, _flashTimer - dt);
        _shakeTimer = Mathf.Max(0f, _shakeTimer - dt);
        _healTimer = Mathf.Max(0f, _healTimer - dt);

        if (_flashImage != null)
            _flashImage.color = new Color(1f, 1f, 1f, _flashTimer > 0f ? 0.9f * (_flashTimer / 0.1f) : 0f);

        if (_healOverlayImage != null)
            _healOverlayImage.color = new Color(0.302f, 1f, 0.533f, 0.5f * (_healTimer / 0.4f));

        if (_anchor == null)
        {
            if (_shakeTimer > 0f)
                _root.anchoredPosition = _baseAnchoredPosition + new Vector2(UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity), UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity));
            else
                _root.anchoredPosition = _baseAnchoredPosition;
        }

        float healthRatio = Mathf.Clamp01(_targetHealth / _maxHealth);
        if (healthRatio < 0.25f)
        {
            float pulse = 1f + 0.015f * (1f - Mathf.Cos(2f * Mathf.PI * Time.time / 0.6f));
            if (_anchor == null)
                _root.localScale = _baseScale * pulse;
            float glowAlpha = Mathf.Lerp(0.6f, 0.9f, (1f + Mathf.Sin(2f * Mathf.PI * Time.time / 0.6f)) * 0.5f);
            if (_glowImage != null)
                _glowImage.color = Color.Lerp(new Color(0.184f, 0.847f, 1f, glowAlpha), new Color(0.302f, 1f, 0.533f, glowAlpha), _healTimer / 0.4f);

            if (_vignetteImage != null)
            {
                _vignetteTimer = Mathf.Repeat(Time.time, 1f);
                _vignetteImage.enabled = true;
                _vignetteImage.color = new Color(1f, 0.078f, 0.157f, _vignetteTimer <= 0.5f ? _vignetteTimer * 2f : (1f - _vignetteTimer) * 2f);
            }
        }
        else
        {
            if (_anchor == null)
                _root.localScale = _baseScale;
            if (_glowImage != null)
                _glowImage.color = Color.Lerp(new Color(0.184f, 0.847f, 1f, 0.35f), new Color(0.302f, 1f, 0.533f, 0.8f), _healTimer / 0.4f);

            if (_vignetteImage != null && _vignetteImage.enabled)
            {
                _vignetteImage.enabled = false;
                _vignetteImage.color = new Color(1f, 0.078f, 0.157f, 0f);
            }
        }

        if (_sweepRect != null)
        {
            float sweepTime = Mathf.Repeat(Time.time, 3f) / 3f;
            _sweepRect.anchoredPosition = new Vector2(Mathf.Lerp(-70f, _windowWidth, sweepTime), 0f);
        }
    }

    private void UpdateWorldVisibility()
    {
        float dt = Time.deltaTime;
        _glitchTimer = Mathf.Max(0f, _glitchTimer - dt);
        _flashTimer = Mathf.Max(0f, _flashTimer - dt);

        if (_flashImage != null)
            _flashImage.color = new Color(1f, 1f, 1f, _flashTimer > 0f ? 0.9f * (_flashTimer / 0.1f) : 0f);

        bool tracksSomeone = _isWorldSpace || _trackedSubject != null || _customAnchor != null;
        if (tracksSomeone)
        {
            if (_proximityTarget == null && Time.time >= _nextPlayerSearchTime)
            {
                PlayerController player = PlayerController.Instance;
                if (player != null) _proximityTarget = player.transform;
                _nextPlayerSearchTime = Time.time + 1f;
            }

            Vector3 origin = _customAnchor != null
                ? _customAnchor.position
                : (_trackedSubject != null
                    ? _trackedSubject.position
                    : (_anchor != null ? _anchor.position : transform.position));

            float maxDist = barType == BarType.Boss ? 150f : 55f;
            float showDistanceSqr = maxDist * maxDist;
            bool nearby = !_enemyDead && (_proximityTarget == null
                ? true
                : (_proximityTarget.position - origin).sqrMagnitude <= showDistanceSqr);
            bool recentlyDamaged = Time.time - _lastDamageTime <= 6f;

            if (_enemyDead) _visibilityAlpha = 0f;
            else if (nearby || recentlyDamaged) _visibilityAlpha = 1f;
            else _visibilityAlpha = Mathf.Lerp(_visibilityAlpha, 0f, 1f - Mathf.Exp(-dt / 0.25f));

            if (_isWorldSpace && _worldCamera == null && Time.unscaledTime >= _nextCameraSearchTime)
            {
                _worldCamera = Camera.main;
                if (_worldCanvas != null) _worldCanvas.worldCamera = _worldCamera;
                _nextCameraSearchTime = Time.unscaledTime + 1f;
            }
            if (_isWorldSpace && _anchor == null && _customAnchor == null && _worldCamera != null)
                transform.rotation = Quaternion.LookRotation(_worldCamera.transform.forward, Vector3.up);
        }
        else
        {
            _visibilityAlpha = _enemyDead ? 0f : 1f;
        }

        float flicker = _glitchTimer > 0f && UnityEngine.Random.value < 0.5f ? 0.35f : 1f;
        if (_canvasGroup != null)
            _canvasGroup.alpha = _visibilityAlpha * flicker;

        if (_sweepRect != null)
        {
            float sweepTime = Mathf.Repeat(Time.time, 3f) / 3f;
            _sweepRect.anchoredPosition = new Vector2(Mathf.Lerp(-70f, _windowWidth, sweepTime), 0f);
        }
    }

    private void UpdateZombieBloodDrops()
    {
        if (_dropTimers == null) return;
        for (int i = 0; i < _dropTimers.Length; i++)
        {
            _dropTimers[i] -= Time.deltaTime;
            if (_dropTimers[i] > 0f) continue;
            SpawnParticle(new Vector2(96f + i * 80f, 40f), 2.2f, Color.white, 0f, 0f, 176f, 0.5f);
            _dropTimers[i] = 1.8f;
        }
    }

    private void UpdateSpiderBubbles()
    {
        _bubbleTimer += Time.deltaTime;
        if (_bubbleTimer >= 0.35f)
        {
            _bubbleTimer = 0f;
            float rx = UnityEngine.Random.Range(56f, 420f);
            SpawnParticle(new Vector2(rx, 42f), 2.0f, new Color(0.54f, 1f, 0.48f, 0.85f),
                UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(18f, 32f), 0f, 1.2f);
        }

        if (_glowImage != null)
        {
            float pulse = Mathf.Lerp(0.35f, 0.65f, (1f + Mathf.Sin(Time.time * 3f)) * 0.5f);
            _glowImage.color = new Color(0.22f, 1f, 0.35f, pulse);
        }
    }

    private void UpdateBossEmbersAndEffects()
    {
        UpdateEffects();

        _emberTimer += Time.deltaTime;
        if (_emberTimer >= 0.22f)
        {
            _emberTimer = 0f;
            float ex = UnityEngine.Random.Range(100f, 600f);
            Color emberColor = UnityEngine.Random.value > 0.4f ? new Color(1f, 0.48f, 0.12f, 1f) : new Color(1f, 0.72f, 0.18f, 1f);
            SpawnParticle(new Vector2(ex, 62f), 1.6f, emberColor,
                UnityEngine.Random.Range(-15f, 15f), UnityEngine.Random.Range(35f, 75f), 10f, UnityEngine.Random.Range(0.8f, 1.5f));

            if (UnityEngine.Random.value < 0.25f)
            {
                SpawnParticle(new Vector2(ex, 62f), 5.0f, new Color(0.7f, 0.7f, 0.7f, 0.22f),
                    UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(20f, 40f), 0f, 1.8f);
            }
        }
    }

    private void CheckBossPhaseCrossing(float oldRatio, float newRatio)
    {
        float[] phases = { 0.75f, 0.50f, 0.25f };
        for (int i = 0; i < phases.Length; i++)
        {
            if (oldRatio > phases[i] && newRatio <= phases[i])
            {
                _shakeTimer = 0.25f;
                _shakeIntensity = 5.0f;
                break;
            }
        }
    }

    private void SpawnAcidBurst(float healthRatio)
    {
        Vector2 origin = new Vector2(_windowX + _windowWidth * healthRatio, _windowY + _windowHeight * 0.5f);
        for (int i = 0; i < 12; i++)
        {
            SpawnParticle(origin, UnityEngine.Random.Range(1.5f, 2.8f), new Color(0.54f, 1f, 0.48f, 1f),
                UnityEngine.Random.Range(-50f, 80f), UnityEngine.Random.Range(40f, 120f), 280f, UnityEngine.Random.Range(0.4f, 0.7f));
        }
    }

    private void SpawnDamageSparks(float healthRatio)
    {
        Vector2 origin = new Vector2(_windowX + _windowWidth * healthRatio, _windowY + _windowHeight * 0.5f);
        for (int i = 0; i < 14; i++)
        {
            Color color;
            float choice = UnityEngine.Random.value;
            if (choice < 0.55f) color = new Color(1f, 0.824f, 0.227f, 1f); // #ffd23a
            else if (choice < 0.88f) color = new Color(1f, 0.604f, 0.122f, 1f); // #ff9a1f
            else color = Color.white;

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

    private void SetupParticlePool(Sprite particleSprite, float initialSize)
    {
        _sparkRoot = CreateRect("VFX Particles", transform, 0f, 0f, _barWidth, _barHeight);
        _particles = new Image[ParticlePoolSize];
        _particleStates = new Particle[ParticlePoolSize];
        for (int i = 0; i < ParticlePoolSize; i++)
        {
            Image p = CreateImage("Particle " + i, _sparkRoot, particleSprite, 0f, 0f, initialSize, initialSize);
            p.enabled = false;
            _particles[i] = p;
        }
    }

    private void SpawnParticle(Vector2 origin, float radius, Color color, float velocityX, float velocityY, float gravity, float lifetime)
    {
        if (_particleStates == null) return;
        for (int i = 0; i < _particleStates.Length; i++)
        {
            if (_particleStates[i].Active) continue;

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
            _particles[i].rectTransform.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            break;
        }
    }

    private void UpdateParticles()
    {
        if (_particleStates == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < _particleStates.Length; i++)
        {
            Particle p = _particleStates[i];
            if (!p.Active) continue;

            p.Age += dt;
            if (p.Age >= p.Lifetime)
            {
                p.Active = false;
                _particles[i].enabled = false;
                _particleStates[i] = p;
                continue;
            }

            float life = p.Age / p.Lifetime;
            float rise = p.VelocityY * p.Age - 0.5f * p.Gravity * p.Age * p.Age;
            float x = p.Position.x + p.VelocityX * p.Age;
            float y = p.Position.y - rise;
            RectTransform rect = _particles[i].rectTransform;
            rect.anchoredPosition = new Vector2(x, -y);
            Color c = p.Color;
            c.a = 1f - life;
            _particles[i].color = c;
            _particleStates[i] = p;
        }
    }

    private void ApplyFollowScale()
    {
        if (_root == null)
            return;

        float scale = _worldWidth / Mathf.Max(1f, _barWidth);
        _root.localScale = new Vector3(scale, scale, scale);
        _baseScale = _root.localScale;
    }

    private void SnapAboveAnchor()
    {
        if (_anchor == null || _root == null || !_isWorldSpace)
            return;

        float scale = _root.localScale.x;
        if (scale < 0.0001f)
            scale = _worldWidth / Mathf.Max(1f, _barWidth);

        float halfHeight = _barHeight * scale * 0.5f;
        Vector3 pos;
        Transform head = FindHeadBone(_anchor);
        if (head != null)
        {
            pos = head.position;
            pos.y += _headGap + halfHeight;
        }
        else
        {
            if (!TryGetAnchorBounds(out Bounds bounds))
                bounds = new Bounds(_anchor.position + Vector3.up * 1.7f, new Vector3(0.6f, 1.7f, 0.6f));
            pos = bounds.center;
            pos.y = bounds.max.y + _headGap + halfHeight;
        }
        transform.position = pos;

        Camera cam = _worldCamera != null ? _worldCamera : Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    private static Transform FindHeadBone(Transform root)
    {
        if (root == null)
            return null;

        Transform[] bones = root.GetComponentsInChildren<Transform>(true);
        Transform fallback = null;
        for (int i = 0; i < bones.Length; i++)
        {
            string name = bones[i].name;
            if (name.IndexOf("health", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (string.Equals(name, "Head", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(" Head", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(":Head", System.StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_Head", System.StringComparison.OrdinalIgnoreCase))
                return bones[i];
            if (fallback == null && name.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) >= 0)
                fallback = bones[i];
        }
        return fallback;
    }

    private bool TryGetAnchorBounds(out Bounds bounds)
    {
        bounds = default;
        if (_anchor == null)
            return false;

        if (_anchorRenderers == null || _anchorRenderers.Length == 0)
            _anchorRenderers = _anchor.GetComponentsInChildren<Renderer>(true);

        bool found = false;
        for (int i = 0; i < _anchorRenderers.Length; i++)
        {
            Renderer renderer = _anchorRenderers[i];
            if (renderer == null || !renderer.enabled)
                continue;
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                continue;
            if (renderer.GetComponentInParent<HealthBar>() != null)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private void ConfigureWorldSpaceCanvas()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (_isWorldSpace)
        {
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(_barWidth, _barHeight);
            ApplyWorldScale();

            _worldCanvas = GetComponent<Canvas>();
            if (_worldCanvas == null) _worldCanvas = gameObject.AddComponent<Canvas>();
            _worldCanvas.renderMode = RenderMode.WorldSpace;
            _worldCanvas.worldCamera = Camera.main;
            _worldCanvas.overrideSorting = true;
            _worldCanvas.sortingOrder = 20;
            _worldCamera = _worldCanvas.worldCamera;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            _canvasGroup.alpha = 1f;
            _visibilityAlpha = 1f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
        else
        {
            _canvasGroup.alpha = 1f;
        }
    }

    private void CreateLowHealthVignette()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

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
