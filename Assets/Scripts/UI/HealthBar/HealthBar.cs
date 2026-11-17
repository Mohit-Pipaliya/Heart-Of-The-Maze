/*
 * ====================================================================================================
 * AAA THEMED HEALTH BAR SYSTEM - HEART OF THE MAZE
 * ====================================================================================================
 * 
 * Supports 4 distinct AAA themes:
 * 1. PLAYER:
 *    - Top-left 600x80 HUD bar. Brushed steel gradient, carbon-fiber weave, angled right end,
 *      cyan accent lines, bolt, chevron. Red gloss gradient fill, cyan bloom glow, red low-HP vignette.
 * 
 * 2. ZOMBIE:
 *    - Floating world-space bar (400x44) above head. Billboards to camera, visible when hit or <15m,
 *      fades out after 4s. Rusty metal frame, toxic green cracks (#9aff3a), blood stains, skull icon.
 *      Dark blood-red fill (#b01818 to #4d0707), sickly green glow (#6fbf1f), dripping blood VFX.
 * 
 * 3. SPIDER:
 *    - Floating world-space bar (480x60) above head. Billboards to camera, fades after 4s.
 *      Glossy black chitin frame, hexagonal pointed ends, neon green (#38ff5a) outline, fang spikes,
 *      web strands, neon spider icon. Neon poison green fill (#8bff7a to #0d6b20), rising venom bubbles.
 * 
 * 4. BOSS ("NAGA GUARDIAN OF THE HEART"):
 *    - Bottom-center 700x96 wide screen bar. Heavy gold bevel, serpent scales, red diamond jewel,
 *      snake-head end caps with glowing slit eyes and fangs. Dark emerald-to-black fill (#1fae7a to #02150f),
 *      molten gold inner glow, gold bloom (#ffb82e). Rising embers/smoke. Phase notches at 75/50/25%
 *      with 5px shake and screen-wide red radial flare. Bold gold TMP title plate above bar.
 * 
 * ====================================================================================================
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[AddComponentMenu("Maze/UI/Health Bar")]
public class HealthBar : MonoBehaviour
{
    public enum BarType
    {
        Player,
        Zombie,
        Spider,
        Boss
    }

    [Header("── Bar Configuration ─────────────────────────────────────────")]
    [Tooltip("The theme and behavior of this health bar.")]
    public BarType type = BarType.Player;

    [Tooltip("Maximum health value.")]
    [SerializeField] private float _maxHealth = 1000f;

    [Tooltip("Current health value.")]
    [SerializeField] private float _currentHealth = 1000f;

    [Header("── Sprite Slots (Procedural Fallback If Null) ───────────────")]
    [Tooltip("Outer border chassis frame.")]
    public Sprite frame;

    [Tooltip("Main health bar fill texture.")]
    public Sprite fill;

    [Tooltip("Outer bloom glow sprite.")]
    public Sprite glow;

    [Tooltip("Optional left end-cap decoration.")]
    public Sprite endCapLeft;

    [Tooltip("Optional right end-cap decoration.")]
    public Sprite endCapRight;

    [Tooltip("Optional theme emblem / icon.")]
    public Sprite icon;

    [Header("── World Space & Enemy Settings ──────────────────────────────")]
    [Tooltip("Target transform to follow (if world-space floating bar).")]
    public Transform followTarget;
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    public float maxVisibleDistance = 18f;
    public float autoFadeDelay = 4.0f;

    [Header("── Internal UI References ────────────────────────────────────")]
    [SerializeField] private RectTransform _rootRect;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _glowImage;
    [SerializeField] private Image _trackImage;
    [SerializeField] private RectTransform _maskWindow;
    [SerializeField] private Image _ghostImage;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _sweepImage;
    [SerializeField] private Image _segmentLinesImage;
    [SerializeField] private Image _frameImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _endCapLeftImage;
    [SerializeField] private Image _endCapRightImage;
    [SerializeField] private Image _flashOverlay;
    [SerializeField] private Transform _sparksPoolContainer;
    [SerializeField] private Image _vignetteOverlay;
    [SerializeField] private TextMeshProUGUI _bossTitleText;
    [SerializeField] private RectTransform _bossTitlePlate;

    // Public property accessors
    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public float HealthRatio => _maxHealth > 0.001f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;

    // Static texture cache (zero GC runtime allocations)
    private static readonly Dictionary<string, Sprite> _cachedSprites = new Dictionary<string, Sprite>();

    // Sparks object pool
    private const int SPARK_POOL_SIZE = 16;
    private readonly List<Image> _sparkPool = new List<Image>(SPARK_POOL_SIZE);

    // Coroutine handles
    private Coroutine _fillRoutine;
    private Coroutine _ghostRoutine;
    private Coroutine _shakeRoutine;
    private Coroutine _flashRoutine;
    private Coroutine _healGlowRoutine;
    private Coroutine _fadeRoutine;

    // State tracking
    private float _displayedFill = 1f;
    private float _displayedGhost = 1f;
    private Vector2 _originalRootPosition;
    private bool _isInitialized = false;
    private float _lastDamageTime = -100f;
    private int _crossedPhases = 0; // Boss 75%, 50%, 25%

    // Sweep & particle timers
    private float _sweepTimer = 0f;
    private const float SWEEP_INTERVAL = 3.0f;
    private const float SWEEP_DURATION = 0.8f;
    private float _ambientParticleTimer = 0f;

    // Colors
    private static readonly Color PlayerGlowCyan = new Color(0.18f, 0.85f, 1f, 0.75f);
    private static readonly Color ZombieGlowGreen = new Color(0.44f, 0.75f, 0.12f, 0.8f);
    private static readonly Color SpiderGlowNeon = new Color(0.22f, 1f, 0.35f, 0.85f);
    private static readonly Color BossGlowGold   = new Color(1f, 0.72f, 0.18f, 0.9f);
    private static readonly Color HealGlowGreen   = new Color(0.22f, 1f, 0.35f, 0.95f);
    private static readonly Color LowHealthVignetteColor = new Color(0.78f, 0.06f, 0.12f, 0.45f);

    private void Awake()
    {
        EnsureHierarchy();
        InitializeSparksPool();
        if (_rootRect != null)
        {
            _originalRootPosition = _rootRect.anchoredPosition;
        }
        _displayedFill = HealthRatio;
        _displayedGhost = HealthRatio;
        UpdateVisualsImmediate();
        _isInitialized = true;
    }

    private void Start()
    {
        EnsureCanvasScaler();
        ApplyThemeStyling();

        if (IsWorldSpaceEnemy())
        {
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }
    }

    private void OnEnable()
    {
        if (_rootRect != null)
        {
            _originalRootPosition = _rootRect.anchoredPosition;
        }
    }

    private void Update()
    {
        UpdateSweepAnimation();
        UpdateLowHealthThrob();
        UpdateWorldEnemyVisibility();
        UpdateAmbientParticles();
    }

    private void LateUpdate()
    {
        UpdateBillboarding();
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void SetMaxHealth(float max)
    {
        _maxHealth = Mathf.Max(1f, max);
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        UpdateFillValues(false);
    }

    public void SetHealth(float health)
    {
        float previous = _currentHealth;
        _currentHealth = Mathf.Clamp(health, 0f, _maxHealth);

        if (!_isInitialized)
        {
            _displayedFill = HealthRatio;
            _displayedGhost = HealthRatio;
            UpdateVisualsImmediate();
            return;
        }

        float delta = _currentHealth - previous;
        if (Mathf.Abs(delta) < 0.001f) return;

        if (delta < 0f)
        {
            _lastDamageTime = Time.time;
            TriggerDamageVFX(Mathf.Abs(delta));
            CheckBossPhaseThresholds(previous, _currentHealth);
        }
        else
        {
            TriggerHealVFX(delta);
        }
    }

    // ── Shared VFX Implementations ──────────────────────────────────────────

    private void TriggerDamageVFX(float damageAmount)
    {
        float targetRatio = HealthRatio;

        // 1. Fill drains smoothly over 0.3s
        if (_fillRoutine != null) StopCoroutine(_fillRoutine);
        _fillRoutine = StartCoroutine(DrainFillRoutine(targetRatio, 0.3f));

        // 2. White ghost bar trails after 0.5s delay (0.9s ease-out)
        if (_ghostRoutine != null) StopCoroutine(_ghostRoutine);
        _ghostRoutine = StartCoroutine(DrainGhostRoutine(targetRatio, 0.5f, 0.9f));

        // 3. Fill flashes bright white for 0.1s
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashWhiteRoutine(0.1f));

        // 4. Shake
        float shakeAmount = (type == BarType.Boss) ? 4f : 3f;
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(shakeAmount, 0.15f));

        // 5. Sparks / Blood / Acid burst from damage edge
        int sparkCount = Random.Range(12, 16);
        SpawnSparks(sparkCount);

        // Make floating bar visible immediately
        if (IsWorldSpaceEnemy() && _canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        if (type == BarType.Zombie)
        {
            StartCoroutine(ZombieGlitchFlickerRoutine());
        }
    }

    private IEnumerator ZombieGlitchFlickerRoutine()
    {
        if (_canvasGroup == null) yield break;

        for (int i = 0; i < 3; i++)
        {
            _canvasGroup.alpha = 0.25f;
            yield return new WaitForSeconds(0.035f);
            _canvasGroup.alpha = 1f;
            yield return new WaitForSeconds(0.035f);
        }
    }

    private void TriggerHealVFX(float healAmount)
    {
        float targetRatio = HealthRatio;

        if (_fillRoutine != null) StopCoroutine(_fillRoutine);
        _fillRoutine = StartCoroutine(DrainFillRoutine(targetRatio, 0.35f));

        if (_ghostRoutine != null) StopCoroutine(_ghostRoutine);
        _displayedGhost = targetRatio;
        if (_ghostImage != null) _ghostImage.fillAmount = _displayedGhost;

        if (_healGlowRoutine != null) StopCoroutine(_healGlowRoutine);
        _healGlowRoutine = StartCoroutine(HealGlowPulseRoutine(0.4f));

        SpawnHealingSparks(8);
    }

    private IEnumerator DrainFillRoutine(float targetRatio, float duration)
    {
        float start = _displayedFill;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _displayedFill = Mathf.Lerp(start, targetRatio, t);
            if (_fillImage != null) _fillImage.fillAmount = _displayedFill;
            yield return null;
        }

        _displayedFill = targetRatio;
        if (_fillImage != null) _fillImage.fillAmount = _displayedFill;
    }

    private IEnumerator DrainGhostRoutine(float targetRatio, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float start = _displayedGhost;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easeOut = 1f - Mathf.Pow(1f - t, 3f);
            _displayedGhost = Mathf.Lerp(start, targetRatio, easeOut);
            if (_ghostImage != null) _ghostImage.fillAmount = _displayedGhost;
            yield return null;
        }

        _displayedGhost = targetRatio;
        if (_ghostImage != null) _ghostImage.fillAmount = _displayedGhost;
    }

    private IEnumerator FlashWhiteRoutine(float duration)
    {
        if (_flashOverlay == null) yield break;

        _flashOverlay.color = new Color(1f, 1f, 1f, 0.75f);
        _flashOverlay.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.75f, 0f, elapsed / duration);
            _flashOverlay.color = new Color(1f, 1f, 1f, alpha);
            yield return null;
        }

        _flashOverlay.gameObject.SetActive(false);
    }

    private IEnumerator ShakeRoutine(float amplitude, float duration)
    {
        if (_rootRect == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float decay = 1f - (elapsed / duration);
            Vector2 offset = Random.insideUnitCircle * (amplitude * decay);
            _rootRect.anchoredPosition = _originalRootPosition + offset;
            yield return null;
        }

        _rootRect.anchoredPosition = _originalRootPosition;
    }

    private IEnumerator HealGlowPulseRoutine(float duration)
    {
        if (_glowImage == null) yield break;

        Color startColor = _glowImage.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            _glowImage.color = Color.Lerp(startColor, HealGlowGreen, pulse);
            yield return null;
        }

        _glowImage.color = GetThemeGlowColor();
    }

    // ── Boss Phase Notches & Radial Flare ───────────────────────────────────

    private void CheckBossPhaseThresholds(float previousHp, float currentHp)
    {
        if (type != BarType.Boss || _maxHealth <= 0.001f) return;

        float prevRatio = previousHp / _maxHealth;
        float curRatio = currentHp / _maxHealth;

        float[] notches = { 0.75f, 0.50f, 0.25f };
        for (int i = 0; i < notches.Length; i++)
        {
            int bit = 1 << i;
            if ((_crossedPhases & bit) == 0 && prevRatio >= notches[i] && curRatio < notches[i])
            {
                _crossedPhases |= bit;
                TriggerBossPhaseCrossing();
            }
        }
    }

    private void TriggerBossPhaseCrossing()
    {
        // 5px shake + full-screen red radial flare for 0.5s
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine(5f, 0.5f));

        if (_vignetteOverlay != null)
        {
            StartCoroutine(BossRadialFlareRoutine(0.5f));
        }
    }

    private IEnumerator BossRadialFlareRoutine(float duration)
    {
        if (_vignetteOverlay == null) yield break;
        _vignetteOverlay.gameObject.SetActive(true);

        Color flareColor = new Color(0.9f, 0.05f, 0.1f, 0.8f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float a = Mathf.Sin(t * Mathf.PI) * 0.75f;
            Color c = flareColor;
            c.a = a;
            _vignetteOverlay.color = c;
            yield return null;
        }

        _vignetteOverlay.gameObject.SetActive(false);
    }

    // ── Low Health & Sweep Animations ───────────────────────────────────────

    private void UpdateLowHealthThrob()
    {
        bool isLowHealth = HealthRatio < 0.25f && _currentHealth > 0.001f;

        if (isLowHealth)
        {
            float throbCycle = Mathf.Sin(Time.time * (Mathf.PI * 2f / 0.6f));
            float scaleMultiplier = Mathf.Lerp(1.0f, 1.03f, (throbCycle + 1f) * 0.5f);
            if (_rootRect != null)
            {
                _rootRect.localScale = new Vector3(scaleMultiplier, scaleMultiplier, 1f);
            }

            if (_glowImage != null)
            {
                float glowAlpha = Mathf.Lerp(0.6f, 1.0f, (throbCycle + 1f) * 0.5f);
                Color c = GetThemeGlowColor();
                c.a = glowAlpha;
                _glowImage.color = c;
            }

            if (type == BarType.Player && _vignetteOverlay != null)
            {
                _vignetteOverlay.gameObject.SetActive(true);
                float vignettePulse = (Mathf.Sin(Time.time * (Mathf.PI * 2f / 1.0f)) + 1f) * 0.5f;
                Color vc = LowHealthVignetteColor;
                vc.a = Mathf.Lerp(0.15f, 0.55f, vignettePulse);
                _vignetteOverlay.color = vc;
            }
        }
        else
        {
            if (_rootRect != null && _rootRect.localScale != Vector3.one)
            {
                _rootRect.localScale = Vector3.one;
            }

            Color defaultGlow = GetThemeGlowColor();
            if (_glowImage != null && _glowImage.color != defaultGlow)
            {
                _glowImage.color = defaultGlow;
            }

            if (type == BarType.Player && _vignetteOverlay != null && _vignetteOverlay.gameObject.activeSelf)
            {
                _vignetteOverlay.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateSweepAnimation()
    {
        if (_sweepImage == null || _maskWindow == null) return;

        _sweepTimer += Time.deltaTime;
        if (_sweepTimer >= SWEEP_INTERVAL)
        {
            float sweepProgress = (_sweepTimer - SWEEP_INTERVAL) / SWEEP_DURATION;
            if (sweepProgress <= 1.0f)
            {
                _sweepImage.gameObject.SetActive(true);
                float maskWidth = _maskWindow.rect.width;
                float sweepWidth = _sweepImage.rectTransform.rect.width;
                float startX = -sweepWidth;
                float endX = maskWidth * _displayedFill;
                float currentX = Mathf.Lerp(startX, endX, sweepProgress);
                _sweepImage.rectTransform.anchoredPosition = new Vector2(currentX, 0f);
            }
            else
            {
                _sweepImage.gameObject.SetActive(false);
                _sweepTimer = 0f;
            }
        }
        else
        {
            if (_sweepImage.gameObject.activeSelf) _sweepImage.gameObject.SetActive(false);
        }
    }

    // ── World Space Billboarding & Enemy Auto-Fade ──────────────────────────

    private bool IsWorldSpaceEnemy()
    {
        return type == BarType.Zombie || type == BarType.Spider;
    }

    private void UpdateBillboarding()
    {
        if (!IsWorldSpaceEnemy()) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Follow target position if assigned
        if (followTarget != null)
        {
            transform.position = followTarget.position + worldOffset;
        }

        // Billboard: Face the camera directly
        transform.forward = cam.transform.forward;
    }

    private void UpdateWorldEnemyVisibility()
    {
        if (!IsWorldSpaceEnemy() || _canvasGroup == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        float dist = Vector3.Distance(transform.position, cam.transform.position);
        bool recentlyHit = (Time.time - _lastDamageTime) < autoFadeDelay;
        bool inRange = dist <= maxVisibleDistance;

        float targetAlpha = (recentlyHit || inRange) && _currentHealth > 0.001f ? 1f : 0f;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.deltaTime * 2.5f);
    }

    private void UpdateAmbientParticles()
    {
        _ambientParticleTimer += Time.deltaTime;

        if (type == BarType.Zombie && _ambientParticleTimer >= 0.45f)
        {
            _ambientParticleTimer = 0f;
            SpawnDrippingBlood(1);
        }
        else if (type == BarType.Spider && _ambientParticleTimer >= 0.35f)
        {
            _ambientParticleTimer = 0f;
            SpawnVenomBubble(1);
        }
        else if (type == BarType.Boss && _ambientParticleTimer >= 0.25f)
        {
            _ambientParticleTimer = 0f;
            SpawnBossEmbers(1);
        }
    }

    private Color GetThemeGlowColor()
    {
        switch (type)
        {
            case BarType.Zombie: return ZombieGlowGreen;
            case BarType.Spider: return SpiderGlowNeon;
            case BarType.Boss:   return BossGlowGold;
            default:             return PlayerGlowCyan;
        }
    }

    private void UpdateFillValues(bool immediate)
    {
        float ratio = HealthRatio;
        if (immediate)
        {
            _displayedFill = ratio;
            _displayedGhost = ratio;
            if (_fillImage != null) _fillImage.fillAmount = ratio;
            if (_ghostImage != null) _ghostImage.fillAmount = ratio;
        }
        else
        {
            if (_fillRoutine != null) StopCoroutine(_fillRoutine);
            _fillRoutine = StartCoroutine(DrainFillRoutine(ratio, 0.25f));
            if (_ghostRoutine != null) StopCoroutine(_ghostRoutine);
            _ghostRoutine = StartCoroutine(DrainGhostRoutine(ratio, 0.4f, 0.6f));
        }
    }

    private void UpdateVisualsImmediate()
    {
        if (_fillImage != null) _fillImage.fillAmount = _displayedFill;
        if (_ghostImage != null) _ghostImage.fillAmount = _displayedGhost;
    }

    // ── Particle System & VFX Pools (Zero GC Allocations) ────────────────────

    private void InitializeSparksPool()
    {
        if (_sparksPoolContainer == null) return;

        Sprite sparkSprite = GetOrCreateSparkSprite();

        for (int i = 0; i < SPARK_POOL_SIZE; i++)
        {
            GameObject sparkObj = new GameObject($"Spark_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sparkObj.transform.SetParent(_sparksPoolContainer, false);
            sparkObj.SetActive(false);

            Image img = sparkObj.GetComponent<Image>();
            img.sprite = sparkSprite;
            img.raycastTarget = false;
            _sparkPool.Add(img);
        }
    }

    private void SpawnSparks(int count)
    {
        if (_maskWindow == null || _sparkPool.Count == 0) return;

        float maskWidth = _maskWindow.rect.width;
        float damageX = maskWidth * _displayedFill;
        Vector2 spawnLocalPos = new Vector2(damageX, 0f);

        Color[] sparkColors;
        if (type == BarType.Zombie)
        {
            sparkColors = new Color[] {
                new Color(0.7f, 0.08f, 0.08f, 1f), // Blood red
                new Color(0.5f, 0.95f, 0.2f, 1f), // Toxic green
                new Color(1f, 1f, 1f, 1f)
            };
        }
        else if (type == BarType.Spider)
        {
            sparkColors = new Color[] {
                new Color(0.2f, 1f, 0.4f, 1f),  // Acid neon green
                new Color(0.85f, 1f, 0.2f, 1f), // Yellow acid
                new Color(1f, 1f, 1f, 1f)
            };
        }
        else if (type == BarType.Boss)
        {
            sparkColors = new Color[] {
                new Color(1f, 0.8f, 0.2f, 1f),  // Molten Gold
                new Color(1f, 0.45f, 0.1f, 1f), // Fiery Orange
                new Color(1f, 1f, 1f, 1f)
            };
        }
        else
        {
            sparkColors = new Color[] {
                new Color(1f, 0.95f, 0.6f, 1f),
                new Color(1f, 0.65f, 0.15f, 1f),
                new Color(1f, 1f, 1f, 1f)
            };
        }

        int spawned = 0;
        for (int i = 0; i < _sparkPool.Count && spawned < count; i++)
        {
            Image spark = _sparkPool[i];
            if (!spark.gameObject.activeSelf)
            {
                Color chosenCol = sparkColors[Random.Range(0, sparkColors.Length)];
                float angle = Random.Range(-65f, 65f);
                float speed = Random.Range(180f, 380f);
                Vector2 velocity = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                float duration = Random.Range(0.22f, 0.38f);
                float startSize = Random.Range(8f, 14f);

                StartCoroutine(AnimateSparkRoutine(spark, spawnLocalPos, velocity, chosenCol, startSize, duration, -600f));
                spawned++;
            }
        }
    }

    private void SpawnDrippingBlood(int count)
    {
        if (_maskWindow == null || _sparkPool.Count == 0) return;

        float maskWidth = _maskWindow.rect.width;
        int spawned = 0;
        for (int i = 0; i < _sparkPool.Count && spawned < count; i++)
        {
            Image spark = _sparkPool[i];
            if (!spark.gameObject.activeSelf)
            {
                float randX = Random.Range(10f, maskWidth * Mathf.Max(0.15f, _displayedFill));
                Vector2 pos = new Vector2(randX, -18f);
                Vector2 vel = new Vector2(Random.Range(-5f, 5f), -Random.Range(40f, 90f));
                Color col = new Color(0.6f, 0.05f, 0.08f, 0.9f);

                StartCoroutine(AnimateSparkRoutine(spark, pos, vel, col, Random.Range(6f, 10f), 0.6f, -120f));
                spawned++;
            }
        }
    }

    private void SpawnVenomBubble(int count)
    {
        if (_maskWindow == null || _sparkPool.Count == 0) return;

        float maskWidth = _maskWindow.rect.width;
        int spawned = 0;
        for (int i = 0; i < _sparkPool.Count && spawned < count; i++)
        {
            Image spark = _sparkPool[i];
            if (!spark.gameObject.activeSelf)
            {
                float randX = Random.Range(10f, maskWidth * Mathf.Max(0.15f, _displayedFill));
                Vector2 pos = new Vector2(randX, Random.Range(-10f, 10f));
                Vector2 vel = new Vector2(Random.Range(-15f, 15f), Random.Range(30f, 65f));
                Color col = new Color(0.35f, 1f, 0.45f, 0.85f);

                StartCoroutine(AnimateSparkRoutine(spark, pos, vel, col, Random.Range(5f, 9f), 0.55f, 0f));
                spawned++;
            }
        }
    }

    private void SpawnBossEmbers(int count)
    {
        if (_maskWindow == null || _sparkPool.Count == 0) return;

        float maskWidth = _maskWindow.rect.width;
        int spawned = 0;
        for (int i = 0; i < _sparkPool.Count && spawned < count; i++)
        {
            Image spark = _sparkPool[i];
            if (!spark.gameObject.activeSelf)
            {
                float randX = Random.Range(10f, maskWidth * Mathf.Max(0.1f, _displayedFill));
                Vector2 pos = new Vector2(randX, -10f);
                Vector2 vel = new Vector2(Random.Range(-30f, 30f), Random.Range(60f, 130f));
                Color col = Random.value < 0.6f ? new Color(1f, 0.65f, 0.1f, 0.9f) : new Color(1f, 0.88f, 0.3f, 0.95f);

                StartCoroutine(AnimateSparkRoutine(spark, pos, vel, col, Random.Range(6f, 11f), 0.7f, 15f));
                spawned++;
            }
        }
    }

    private void SpawnHealingSparks(int count)
    {
        if (_maskWindow == null || _sparkPool.Count == 0) return;

        float maskWidth = _maskWindow.rect.width;
        int spawned = 0;

        for (int i = 0; i < _sparkPool.Count && spawned < count; i++)
        {
            Image spark = _sparkPool[i];
            if (!spark.gameObject.activeSelf)
            {
                float randX = Random.Range(0f, maskWidth * _displayedFill);
                Vector2 spawnLocalPos = new Vector2(randX, -15f);
                Color healColor = new Color(0.35f, 1f, 0.5f, 0.9f);
                Vector2 velocity = new Vector2(Random.Range(-25f, 25f), Random.Range(70f, 140f));
                float duration = Random.Range(0.35f, 0.5f);
                float startSize = Random.Range(6f, 10f);

                StartCoroutine(AnimateSparkRoutine(spark, spawnLocalPos, velocity, healColor, startSize, duration, 0f));
                spawned++;
            }
        }
    }

    private IEnumerator AnimateSparkRoutine(Image spark, Vector2 startPos, Vector2 velocity, Color color, float size, float duration, float gravity)
    {
        spark.gameObject.SetActive(true);
        RectTransform rt = spark.rectTransform;
        rt.anchoredPosition = startPos;
        rt.sizeDelta = new Vector2(size, size);
        spark.color = color;

        Vector2 curPos = startPos;
        Vector2 curVel = velocity;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            curVel.y += gravity * Time.deltaTime;
            curPos += curVel * Time.deltaTime;
            rt.anchoredPosition = curPos;

            float scale = Mathf.Lerp(1f, 0.2f, t);
            rt.localScale = new Vector3(scale, scale, 1f);

            Color c = color;
            c.a = Mathf.Lerp(color.a, 0f, t);
            spark.color = c;

            yield return null;
        }

        spark.gameObject.SetActive(false);
    }

    // ── Hierarchy & Layout Creation ─────────────────────────────────────────

    public void EnsureHierarchy()
    {
        _rootRect = GetComponent<RectTransform>();
        if (_rootRect == null) _rootRect = gameObject.AddComponent<RectTransform>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null && IsWorldSpaceEnemy()) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Set dimensions per theme
        Vector2 referenceSize = GetThemeReferenceSize();
        if (_rootRect.sizeDelta == Vector2.zero)
        {
            _rootRect.sizeDelta = referenceSize;
        }

        // 1. Glow (behind)
        if (_glowImage == null)
        {
            Transform existing = transform.Find("01_Glow");
            _glowImage = existing != null ? existing.GetComponent<Image>() : CreateChildImage("01_Glow", transform, referenceSize + new Vector2(50f, 50f), Vector2.zero);
        }

        // 2. Dark Track
        if (_trackImage == null)
        {
            Transform existing = transform.Find("02_DarkTrack");
            _trackImage = existing != null ? existing.GetComponent<Image>() : CreateChildImage("02_DarkTrack", transform, referenceSize - new Vector2(64f, 32f), Vector2.zero);
        }

        // 3. Mask Window (Contains Ghost, Fill, Sweep, Segments)
        if (_maskWindow == null)
        {
            Transform existing = transform.Find("03_MaskWindow");
            if (existing != null)
            {
                _maskWindow = existing.GetComponent<RectTransform>();
            }
            else
            {
                GameObject maskObj = new GameObject("03_MaskWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(RectMask2D));
                maskObj.transform.SetParent(transform, false);
                _maskWindow = maskObj.GetComponent<RectTransform>();
                _maskWindow.anchorMin = new Vector2(0f, 0.5f);
                _maskWindow.anchorMax = new Vector2(0f, 0.5f);
                _maskWindow.pivot = new Vector2(0f, 0.5f);
                _maskWindow.anchoredPosition = new Vector2(34f, 0f);
                _maskWindow.sizeDelta = referenceSize - new Vector2(80f, 34f);
            }
        }

        // 3a. Ghost Bar
        if (_ghostImage == null && _maskWindow != null)
        {
            Transform existing = _maskWindow.Find("GhostBar");
            _ghostImage = existing != null ? existing.GetComponent<Image>() : CreateFillImage("GhostBar", _maskWindow, new Color(1f, 1f, 1f, 0.9f));
        }

        // 3b. Fill Bar
        if (_fillImage == null && _maskWindow != null)
        {
            Transform existing = _maskWindow.Find("FillBar");
            _fillImage = existing != null ? existing.GetComponent<Image>() : CreateFillImage("FillBar", _maskWindow, Color.white);
        }

        // 3c. Flash Overlay
        if (_flashOverlay == null && _maskWindow != null)
        {
            Transform existing = _maskWindow.Find("FlashOverlay");
            _flashOverlay = existing != null ? existing.GetComponent<Image>() : CreateChildImage("FlashOverlay", _maskWindow, _maskWindow.sizeDelta, new Vector2(_maskWindow.sizeDelta.x * 0.5f, 0f));
            _flashOverlay.gameObject.SetActive(false);
            _flashOverlay.raycastTarget = false;
        }

        // 3d. Light Sweep
        if (_sweepImage == null && _maskWindow != null)
        {
            Transform existing = _maskWindow.Find("LightSweep");
            _sweepImage = existing != null ? existing.GetComponent<Image>() : CreateChildImage("LightSweep", _maskWindow, new Vector2(90f, _maskWindow.sizeDelta.y), Vector2.zero);
            _sweepImage.gameObject.SetActive(false);
            _sweepImage.raycastTarget = false;
        }

        // 3e. Segment Lines
        if (_segmentLinesImage == null && _maskWindow != null)
        {
            Transform existing = _maskWindow.Find("SegmentLines");
            _segmentLinesImage = existing != null ? existing.GetComponent<Image>() : CreateChildImage("SegmentLines", _maskWindow, _maskWindow.sizeDelta, new Vector2(_maskWindow.sizeDelta.x * 0.5f, 0f));
            _segmentLinesImage.raycastTarget = false;
        }

        // 4. Frame (On Top)
        if (_frameImage == null)
        {
            Transform existing = transform.Find("04_Frame");
            _frameImage = existing != null ? existing.GetComponent<Image>() : CreateChildImage("04_Frame", transform, referenceSize, Vector2.zero);
            _frameImage.raycastTarget = false;
        }

        // 5. Sparks Container
        if (_sparksPoolContainer == null)
        {
            Transform existing = transform.Find("05_SparksContainer");
            if (existing != null)
            {
                _sparksPoolContainer = existing;
            }
            else
            {
                GameObject poolObj = new GameObject("05_SparksContainer", typeof(RectTransform));
                poolObj.transform.SetParent(transform, false);
                _sparksPoolContainer = poolObj.transform;
            }
        }

        // 6. Boss Title Plate (Boss Only)
        if (type == BarType.Boss && _bossTitlePlate == null)
        {
            Transform existingPlate = transform.Find("BossTitlePlate");
            if (existingPlate != null)
            {
                _bossTitlePlate = existingPlate.GetComponent<RectTransform>();
                _bossTitleText = existingPlate.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                GameObject plateObj = new GameObject("BossTitlePlate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                plateObj.transform.SetParent(transform, false);
                _bossTitlePlate = plateObj.GetComponent<RectTransform>();
                _bossTitlePlate.anchorMin = new Vector2(0.5f, 1f);
                _bossTitlePlate.anchorMax = new Vector2(0.5f, 1f);
                _bossTitlePlate.pivot = new Vector2(0.5f, 0f);
                _bossTitlePlate.anchoredPosition = new Vector2(0f, 8f);
                _bossTitlePlate.sizeDelta = new Vector2(520f, 40f);

                Image pImg = plateObj.GetComponent<Image>();
                pImg.sprite = GetOrCreateTrackSprite();
                pImg.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);
                pImg.raycastTarget = false;

                GameObject textObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(plateObj.transform, false);
                RectTransform trt = textObj.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;

                _bossTitleText = textObj.GetComponent<TextMeshProUGUI>();
                _bossTitleText.alignment = TextAlignmentOptions.Center;
                _bossTitleText.fontSize = 20;
                _bossTitleText.fontStyle = FontStyles.Bold;
                _bossTitleText.characterSpacing = 8f;
                _bossTitleText.color = new Color(1f, 0.88f, 0.35f, 1f);
                _bossTitleText.text = "NAGA GUARDIAN OF THE HEART";
                _bossTitleText.raycastTarget = false;
            }
        }

        // 7. Fullscreen Vignette Overlay
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null && _vignetteOverlay == null && (type == BarType.Player || type == BarType.Boss))
        {
            Transform existingVig = parentCanvas.transform.Find("HealthBar_VignetteOverlay");
            if (existingVig != null)
            {
                _vignetteOverlay = existingVig.GetComponent<Image>();
            }
            else
            {
                GameObject vigObj = new GameObject("HealthBar_VignetteOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                vigObj.transform.SetParent(parentCanvas.transform, false);
                vigObj.transform.SetAsLastSibling();

                RectTransform vrt = vigObj.GetComponent<RectTransform>();
                vrt.anchorMin = Vector2.zero;
                vrt.anchorMax = Vector2.one;
                vrt.sizeDelta = Vector2.zero;
                vrt.anchoredPosition = Vector2.zero;

                _vignetteOverlay = vigObj.GetComponent<Image>();
                _vignetteOverlay.sprite = GetOrCreateVignetteSprite();
                _vignetteOverlay.color = LowHealthVignetteColor;
                _vignetteOverlay.raycastTarget = false;
                vigObj.SetActive(false);
            }
        }
    }

    private Vector2 GetThemeReferenceSize()
    {
        switch (type)
        {
            case BarType.Zombie: return new Vector2(400f, 44f);
            case BarType.Spider: return new Vector2(480f, 60f);
            case BarType.Boss:   return new Vector2(700f, 96f);
            default:             return new Vector2(600f, 80f); // Player
        }
    }

    public void ApplyThemeStyling()
    {
        // 1. Frame Sprite
        if (_frameImage != null)
        {
            if (frame != null)
            {
                _frameImage.sprite = frame;
                _frameImage.type = frame.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
            else
            {
                _frameImage.sprite = GetThemeFrameSprite();
                _frameImage.type = Image.Type.Simple;
            }
            _frameImage.color = Color.white;
        }

        // 2. Glow
        if (_glowImage != null)
        {
            _glowImage.sprite = glow != null ? glow : GetThemeGlowSprite();
            _glowImage.color = GetThemeGlowColor();
            _glowImage.raycastTarget = false;
        }

        // 3. Track
        if (_trackImage != null)
        {
            _trackImage.sprite = GetOrCreateTrackSprite();
            _trackImage.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
            _trackImage.raycastTarget = false;
        }

        // 4. Fill Sprite
        if (_fillImage != null)
        {
            _fillImage.sprite = fill != null ? fill : GetThemeFillSprite();
            _fillImage.type = Image.Type.Filled;
            _fillImage.fillMethod = Image.FillMethod.Horizontal;
            _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fillImage.color = Color.white;
            _fillImage.raycastTarget = false;
        }

        // 5. Ghost
        if (_ghostImage != null)
        {
            _ghostImage.sprite = GetOrCreateGhostSprite();
            _ghostImage.type = Image.Type.Filled;
            _ghostImage.fillMethod = Image.FillMethod.Horizontal;
            _ghostImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            _ghostImage.color = new Color(1f, 1f, 1f, 0.88f);
            _ghostImage.raycastTarget = false;
        }

        // 6. Segment Lines
        if (_segmentLinesImage != null)
        {
            _segmentLinesImage.sprite = GetOrCreateSegmentLinesSprite();
            _segmentLinesImage.color = Color.white;
        }

        // 7. Sweep
        if (_sweepImage != null)
        {
            _sweepImage.sprite = GetOrCreateLightSweepSprite();
            _sweepImage.color = new Color(1f, 1f, 1f, 0.45f);
        }
    }

    private void EnsureCanvasScaler()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }

    private Image CreateChildImage(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        return obj.GetComponent<Image>();
    }

    private Image CreateFillImage(string name, RectTransform parent, Color tint)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = obj.GetComponent<Image>();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.color = tint;
        img.raycastTarget = false;
        return img;
    }

    // ── Procedural Theme Sprites (Cached Statically) ─────────────────────────

    private Sprite GetThemeFrameSprite()
    {
        switch (type)
        {
            case BarType.Zombie: return GetOrCreateZombieFrameSprite();
            case BarType.Spider: return GetOrCreateSpiderFrameSprite();
            case BarType.Boss:   return GetOrCreateBossFrameSprite();
            default:             return GetOrCreatePlayerFrameSprite();
        }
    }

    private Sprite GetThemeFillSprite()
    {
        switch (type)
        {
            case BarType.Zombie: return GetOrCreateZombieFillSprite();
            case BarType.Spider: return GetOrCreateSpiderFillSprite();
            case BarType.Boss:   return GetOrCreateBossFillSprite();
            default:             return GetOrCreatePlayerFillSprite();
        }
    }

    private Sprite GetThemeGlowSprite()
    {
        switch (type)
        {
            case BarType.Zombie: return GetOrCreateZombieGlowSprite();
            case BarType.Spider: return GetOrCreateSpiderGlowSprite();
            case BarType.Boss:   return GetOrCreateBossGlowSprite();
            default:             return GetOrCreatePlayerGlowSprite();
        }
    }

    // ── PLAYER SPRITES ──

    private static Sprite GetOrCreatePlayerFrameSprite()
    {
        const string key = "Player_Frame_600x80";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 600, height = 80;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.name = key;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        Color transparent = new Color(0, 0, 0, 0);
        Color cyanAccent = new Color(0.18f, 0.85f, 1f, 1f);

        int fillLeft = 32, fillRight = 538, fillBottom = 18, fillTop = 62;

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                float rightCutX = width - 40f + (normY * 30f);
                if (x > rightCutX) { pixels[y * width + x] = transparent; continue; }

                if (x >= fillLeft && x <= fillRight && y >= fillBottom && y <= fillTop)
                {
                    if (y == fillTop || y == fillBottom) pixels[y * width + x] = cyanAccent;
                    else pixels[y * width + x] = transparent;
                    continue;
                }

                Color steelTop = new Color(0.62f, 0.65f, 0.70f, 1f);
                Color steelBottom = new Color(0.08f, 0.09f, 0.11f, 1f);
                Color baseSteel = Color.Lerp(steelBottom, steelTop, normY);

                int cx = (x / 4) % 2, cy = (y / 4) % 2;
                float weaveMod = (cx ^ cy) == 0 ? 0.05f : -0.05f;
                float grain = Mathf.Sin(y * 1.5f) * 0.02f;

                Color finalColor = new Color(
                    Mathf.Clamp01(baseSteel.r + weaveMod + grain),
                    Mathf.Clamp01(baseSteel.g + weaveMod + grain),
                    Mathf.Clamp01(baseSteel.b + weaveMod + grain),
                    1f
                );

                if (y < 8 && y > 2 && (x % 14 == 0) && x > fillLeft && x < fillRight) finalColor = Color.Lerp(finalColor, cyanAccent, 0.75f);

                float distToBolt = Vector2.Distance(new Vector2(x, y), new Vector2(16f, 40f));
                if (distToBolt <= 7f)
                {
                    if (distToBolt <= 2.5f) finalColor = cyanAccent;
                    else if (distToBolt <= 5.5f) finalColor = new Color(0.75f, 0.78f, 0.82f, 1f);
                    else finalColor = new Color(0.04f, 0.05f, 0.06f, 1f);
                }

                if (y >= 48 && y <= 62 && x >= 556 && x <= 576)
                {
                    int dy = Mathf.Abs(y - 55);
                    int chevX = 562 + (7 - dy);
                    if (x == chevX || x == chevX - 1 || x == chevX + 6 || x == chevX + 5) finalColor = cyanAccent;
                }

                pixels[y * width + x] = finalColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreatePlayerFillSprite()
    {
        const string key = "Player_Fill_256x64";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 256, height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color darkRed = new Color(0.48f, 0.04f, 0.11f, 1f);
        Color midRed  = new Color(0.77f, 0.08f, 0.18f, 1f);
        Color topRed  = new Color(1.00f, 0.35f, 0.43f, 1f);

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            Color baseRed = normY < 0.5f ? Color.Lerp(darkRed, midRed, normY * 2f) : Color.Lerp(midRed, topRed, (normY - 0.5f) * 2f);
            if (normY >= 0.60f)
            {
                float glossT = (normY - 0.60f) / 0.40f;
                baseRed = Color.Lerp(baseRed, Color.white, Mathf.Pow(glossT, 1.8f) * 0.75f);
            }
            for (int x = 0; x < width; x++) pixels[y * width + x] = baseRed;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreatePlayerGlowSprite()
    {
        const string key = "Player_Glow_256x64";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 256, height = 64;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float ny = Mathf.Abs((y - (height * 0.5f)) / (height * 0.5f));
            float falloffY = Mathf.Clamp01(1f - ny);
            for (int x = 0; x < width; x++)
            {
                float nx = Mathf.Abs((x - (width * 0.5f)) / (width * 0.5f));
                float falloffX = Mathf.Clamp01(1f - nx);
                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Pow(falloffX * falloffY, 1.5f));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    // ── ZOMBIE SPRITES ──

    private static Sprite GetOrCreateZombieFrameSprite()
    {
        const string key = "Zombie_Frame_400x44";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 400, height = 44;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color transparent = new Color(0, 0, 0, 0);
        Color toxicGreen = new Color(0.6f, 1f, 0.23f, 1f); // #9aff3a
        Color boneSkull = new Color(0.92f, 0.90f, 0.82f, 1f);
        Color rustDark = new Color(0.18f, 0.10f, 0.05f, 1f);
        Color rustLight = new Color(0.42f, 0.24f, 0.12f, 1f);

        int fillLeft = 32, fillRight = 376, fillBottom = 9, fillTop = 35;

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                // Jagged edges at ends
                if (x < 6 || x > width - 6)
                {
                    if ((x + y) % 3 == 0) { pixels[y * width + x] = transparent; continue; }
                }

                // Fill window cutout
                if (x >= fillLeft && x <= fillRight && y >= fillBottom && y <= fillTop)
                {
                    pixels[y * width + x] = transparent;
                    continue;
                }

                // Rusty brown metal gradient with noise
                Color baseRust = Color.Lerp(rustDark, rustLight, normY);
                float rustSpot = Mathf.Sin(x * 0.4f) * Mathf.Cos(y * 0.8f) * 0.08f;
                Color col = new Color(
                    Mathf.Clamp01(baseRust.r + rustSpot),
                    Mathf.Clamp01(baseRust.g + rustSpot * 0.5f),
                    Mathf.Clamp01(baseRust.b + rustSpot * 0.3f),
                    1f
                );

                // Toxic green crack lines
                if ((x > 180 && x < 230 && y == 38 + (int)(Mathf.Sin(x * 0.25f) * 2f)) ||
                    (x > 290 && x < 330 && y == 6 + (int)(Mathf.Cos(x * 0.3f) * 2f)))
                {
                    col = toxicGreen;
                }

                // Small bone-colored skull icon on left (center x=16, y=22)
                float dx = x - 16f, dy = y - 22f;
                float skullDist = (dx * dx) / 36f + (dy * dy) / 49f;
                if (skullDist <= 1.0f)
                {
                    col = boneSkull;
                    // Hollow dark eye sockets
                    if ((dx > 1.2f && dx < 3.8f && Mathf.Abs(dy) < 2.0f) ||
                        (dx < -1.2f && dx > -3.8f && Mathf.Abs(dy) < 2.0f))
                    {
                        col = new Color(0.08f, 0.05f, 0.04f, 1f);
                    }
                    // Teeth slit
                    if (dy < -4f && (int)dx % 2 == 0)
                    {
                        col = new Color(0.08f, 0.05f, 0.04f, 1f);
                    }
                }

                pixels[y * width + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateZombieFillSprite()
    {
        const string key = "Zombie_Fill_256x44";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 256, height = 44;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color darkBlood = new Color(0.30f, 0.03f, 0.03f, 1f); // #4d0707
        Color brightBlood = new Color(0.69f, 0.09f, 0.09f, 1f); // #b01818

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            Color col = Color.Lerp(darkBlood, brightBlood, normY);
            if (normY >= 0.60f)
            {
                float t = (normY - 0.60f) / 0.40f;
                col = Color.Lerp(col, Color.white, Mathf.Pow(t, 2f) * 0.65f);
            }
            for (int x = 0; x < width; x++) pixels[y * width + x] = col;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateZombieGlowSprite()
    {
        return GetOrCreatePlayerGlowSprite();
    }

    // ── SPIDER SPRITES ──

    private static Sprite GetOrCreateSpiderFrameSprite()
    {
        const string key = "Spider_Frame_480x60";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 480, height = 60;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color transparent = new Color(0, 0, 0, 0);
        Color neonGreen = new Color(0.22f, 1f, 0.35f, 1f); // #38ff5a
        Color chitinDark = new Color(0.04f, 0.05f, 0.04f, 1f);
        Color chitinMid = new Color(0.12f, 0.16f, 0.13f, 1f);

        int fillLeft = 38, fillRight = 442, fillBottom = 13, fillTop = 47;

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                // Hexagonal pointed ends (cut corners on left and right)
                float leftCut = Mathf.Abs(normY - 0.5f) * 40f;
                float rightCut = width - (Mathf.Abs(normY - 0.5f) * 40f);
                if (x < leftCut || x > rightCut) { pixels[y * width + x] = transparent; continue; }

                // Fill window cutout
                if (x >= fillLeft && x <= fillRight && y >= fillBottom && y <= fillTop)
                {
                    pixels[y * width + x] = transparent;
                    continue;
                }

                // Chitin gradient
                Color col = Color.Lerp(chitinDark, chitinMid, normY);

                // Neon green outline
                if (Mathf.Abs(x - leftCut) < 2f || Mathf.Abs(x - rightCut) < 2f || y <= 2 || y >= height - 3)
                {
                    col = neonGreen;
                }

                // Two white-green fang spikes hanging down at bottom center (x=230, x=250)
                if (y < fillBottom)
                {
                    if ((Mathf.Abs(x - 230) <= (fillBottom - y) * 0.6f) ||
                        (Mathf.Abs(x - 250) <= (fillBottom - y) * 0.6f))
                    {
                        col = Color.Lerp(neonGreen, Color.white, 0.5f);
                    }
                }

                // Spider icon on left (center x=20, y=30)
                float sx = x - 20f, sy = y - 30f;
                float bodyDist = (sx * sx) / 9f + (sy * sy) / 25f;
                if (bodyDist <= 1.0f) col = neonGreen;
                // Legs
                if (Mathf.Abs(sx) > 3f && Mathf.Abs(sx) < 11f && Mathf.Abs(sy) < 9f && ((int)sy % 3 == 0))
                {
                    col = neonGreen;
                }

                pixels[y * width + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateSpiderFillSprite()
    {
        const string key = "Spider_Fill_256x60";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 256, height = 60;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color darkPoison = new Color(0.05f, 0.42f, 0.12f, 1f); // #0d6b20
        Color midPoison  = new Color(0.14f, 0.77f, 0.23f, 1f); // #25c43b
        Color topPoison  = new Color(0.55f, 1.00f, 0.48f, 1f); // #8bff7a

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            Color col = normY < 0.5f ? Color.Lerp(darkPoison, midPoison, normY * 2f) : Color.Lerp(midPoison, topPoison, (normY - 0.5f) * 2f);
            if (normY >= 0.60f)
            {
                float t = (normY - 0.60f) / 0.40f;
                col = Color.Lerp(col, Color.white, Mathf.Pow(t, 2f) * 0.7f);
            }
            for (int x = 0; x < width; x++) pixels[y * width + x] = col;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateSpiderGlowSprite()
    {
        return GetOrCreatePlayerGlowSprite();
    }

    // ── BOSS SPRITES ──

    private static Sprite GetOrCreateBossFrameSprite()
    {
        const string key = "Boss_Frame_700x96";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 700, height = 96;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color goldLight = new Color(1.00f, 0.94f, 0.66f, 1f); // #fff1a8
        Color goldMid   = new Color(0.89f, 0.73f, 0.24f, 1f); // #e2b93c
        Color goldDark  = new Color(0.29f, 0.21f, 0.03f, 1f); // #4a3508
        Color redJewel  = new Color(0.88f, 0.08f, 0.15f, 1f);
        Color transparent = new Color(0, 0, 0, 0);

        int fillLeft = 44, fillRight = 656, fillBottom = 20, fillTop = 76;

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                // Fill window cutout
                if (x >= fillLeft && x <= fillRight && y >= fillBottom && y <= fillTop)
                {
                    // Bright gold top and bottom border lines
                    if (y == fillBottom || y == fillTop) pixels[y * width + x] = goldLight;
                    else pixels[y * width + x] = transparent;
                    continue;
                }

                // Heavy gold bevel gradient
                Color baseGold = normY < 0.5f ? Color.Lerp(goldDark, goldMid, normY * 2f) : Color.Lerp(goldMid, goldLight, (normY - 0.5f) * 2f);

                // Serpent scale diamond overlay pattern
                int sx = (x / 6) % 2, sy = (y / 6) % 2;
                float scaleMod = (sx ^ sy) == 0 ? 0.06f : -0.06f;

                Color col = new Color(
                    Mathf.Clamp01(baseGold.r + scaleMod),
                    Mathf.Clamp01(baseGold.g + scaleMod),
                    Mathf.Clamp01(baseGold.b + scaleMod),
                    1f
                );

                // Centered red jewel diamond on top edge (center x=350, y=86)
                float jx = Mathf.Abs(x - 350f), jy = Mathf.Abs(y - 86f);
                if (jx + jy <= 8f)
                {
                    col = jx + jy <= 4f ? Color.Lerp(redJewel, Color.white, 0.4f) : redJewel;
                }

                // Snake head end caps (Left center x=22, Right center x=678)
                if (x < fillLeft || x > fillRight)
                {
                    float ex = x < fillLeft ? Mathf.Abs(x - 22f) : Mathf.Abs(x - 678f);
                    float ey = Mathf.Abs(y - 48f);
                    // Glowing red slit eye
                    if (ex < 2f && ey < 5f) col = Color.red;
                }

                pixels[y * width + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateBossFillSprite()
    {
        const string key = "Boss_Fill_256x96";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 256, height = 96;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color darkEmerald = new Color(0.01f, 0.08f, 0.06f, 1f); // #02150f
        Color midEmerald  = new Color(0.04f, 0.37f, 0.27f, 1f); // #0a5f45
        Color topEmerald  = new Color(0.12f, 0.68f, 0.48f, 1f); // #1fae7a

        for (int y = 0; y < height; y++)
        {
            float normY = (float)y / height;
            Color col = normY < 0.5f ? Color.Lerp(darkEmerald, midEmerald, normY * 2f) : Color.Lerp(midEmerald, topEmerald, (normY - 0.5f) * 2f);
            if (normY >= 0.60f)
            {
                float t = (normY - 0.60f) / 0.40f;
                col = Color.Lerp(col, Color.white, Mathf.Pow(t, 2f) * 0.7f);
            }
            for (int x = 0; x < width; x++) pixels[y * width + x] = col;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateBossGlowSprite()
    {
        return GetOrCreatePlayerGlowSprite();
    }

    // ── COMMON SHARED SPRITES ──

    private static Sprite GetOrCreateTrackSprite()
    {
        const string key = "DarkTrack_64x64";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int s = 64;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            float ny = (float)y / s;
            Color c = Color.Lerp(new Color(0.04f, 0.04f, 0.06f, 1f), new Color(0.12f, 0.12f, 0.15f, 1f), ny);
            for (int x = 0; x < s; x++) pixels[y * s + x] = c;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateGhostSprite()
    {
        const string key = "GhostBar_32x32";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        Sprite spr = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateSegmentLinesSprite()
    {
        const string key = "SegmentLines_500x32";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 500, height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        Color clear = new Color(0, 0, 0, 0);
        Color line = new Color(0f, 0f, 0f, 0.85f);

        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        for (int step = 1; step <= 9; step++)
        {
            int xPos = (int)(width * (step / 10f));
            for (int y = 0; y < height; y++)
            {
                pixels[y * width + xPos] = line;
                if (xPos + 1 < width) pixels[y * width + xPos + 1] = line;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateLightSweepSprite()
    {
        const string key = "LightSweep_90x32";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int width = 90, height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float offset = (float)y * 0.45f;
                float center = (width * 0.5f) + offset - (height * 0.225f);
                float dist = Mathf.Abs(x - center) / (width * 0.35f);
                float alpha = Mathf.Clamp01(1f - dist);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha * 0.45f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateSparkSprite()
    {
        const string key = "Spark_16x16";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int s = 16;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[s * s];
        Vector2 c = new Vector2(7.5f, 7.5f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / 7.5f;
                float a = Mathf.Clamp01(1f - d);
                float diamond = 1f - (Mathf.Abs(x - 7.5f) + Mathf.Abs(y - 7.5f)) / 7.5f;
                float finalA = Mathf.Clamp01(Mathf.Max(a, diamond));
                pixels[y * s + x] = new Color(1f, 1f, 1f, finalA);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    private static Sprite GetOrCreateVignetteSprite()
    {
        const string key = "Player_Vignette_256x256";
        if (_cachedSprites.TryGetValue(key, out Sprite existing) && existing != null) return existing;

        int s = 256;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[s * s];
        Vector2 center = new Vector2(128f, 128f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / 128f;
                float alpha = dist < 0.5f ? 0f : Mathf.SmoothStep(0f, 1f, (dist - 0.5f) / 0.5f);
                pixels[y * s + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        Sprite spr = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        _cachedSprites[key] = spr;
        return spr;
    }

    // ── Static Enemy Attachment Helper ──────────────────────────────────────

    /// <summary>
    /// Attaches or returns a floating World Space HealthBar for an enemy.
    /// </summary>
    public static HealthBar AttachToEnemy(GameObject enemy, BarType barType, float maxHp = 1000f)
    {
        if (enemy == null) return null;

        HealthBar existing = enemy.GetComponentInChildren<HealthBar>();
        if (existing != null)
        {
            existing.SetMaxHealth(maxHp);
            return existing;
        }

        GameObject barRoot = new GameObject($"HealthBar_{barType}", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(HealthBar));
        barRoot.transform.SetParent(enemy.transform, false);

        Canvas c = barRoot.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;

        float yOffset = (barType == BarType.Spider) ? 1.8f : 2.2f;

        // Compensate for parent scale (e.g. Spider model has root scale 40, 40, 40)
        Vector3 parentScale = enemy.transform.lossyScale;
        if (parentScale.sqrMagnitude < 0.0001f) parentScale = enemy.transform.localScale;
        float invX = Mathf.Abs(parentScale.x) > 0.0001f ? (1f / parentScale.x) : 1f;
        float invY = Mathf.Abs(parentScale.y) > 0.0001f ? (1f / parentScale.y) : 1f;
        float invZ = Mathf.Abs(parentScale.z) > 0.0001f ? (1f / parentScale.z) : 1f;

        barRoot.transform.localPosition = new Vector3(0f, yOffset * invY, 0f);
        barRoot.transform.localScale = new Vector3(0.0075f * invX, 0.0075f * invY, 0.0075f * invZ);

        HealthBar hb = barRoot.GetComponent<HealthBar>();
        hb.type = barType;
        hb.followTarget = enemy.transform;
        hb.worldOffset = new Vector3(0f, yOffset, 0f);
        hb.EnsureHierarchy();
        hb.ApplyThemeStyling();
        hb.SetMaxHealth(maxHp);
        hb.SetHealth(maxHp);

        return hb;
    }

    // ── Editor Helpers ──────────────────────────────────────────────────────

    [ContextMenu("Setup Player Bar Hierarchy")]
    public void SetupPlayerBarInEditor()
    {
        type = BarType.Player;
        EnsureHierarchy();
        ApplyThemeStyling();
        EnsureCanvasScaler();

        if (_rootRect != null)
        {
            _rootRect.anchorMin = new Vector2(0f, 1f);
            _rootRect.anchorMax = new Vector2(0f, 1f);
            _rootRect.pivot = new Vector2(0f, 1f);
            _rootRect.anchoredPosition = new Vector2(40f, -40f);
            _rootRect.sizeDelta = new Vector2(600f, 80f);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    [ContextMenu("Setup Zombie Bar Hierarchy")]
    public void SetupZombieBarInEditor()
    {
        type = BarType.Zombie;
        EnsureHierarchy();
        ApplyThemeStyling();

        Canvas c = GetComponent<Canvas>();
        if (c == null) c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;

        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;

        transform.localScale = Vector3.one * 0.0075f;
        if (_rootRect != null)
        {
            _rootRect.sizeDelta = new Vector2(400f, 44f);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

    [ContextMenu("Setup Spider Bar Hierarchy")]
    public void SetupSpiderBarInEditor()
    {
        type = BarType.Spider;
        EnsureHierarchy();
        ApplyThemeStyling();

        Canvas c = GetComponent<Canvas>();
        if (c == null) c = gameObject.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;

        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 1f;

        transform.localScale = Vector3.one * 0.0075f;
        if (_rootRect != null)
        {
            _rootRect.sizeDelta = new Vector2(480f, 60f);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(gameObject);
#endif
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("GameObject/UI/Theme Health Bar/Player Bar", false, 10)]
    public static void CreatePlayerBarMenuItem()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject barObj = new GameObject("PlayerHealthBar", typeof(RectTransform), typeof(HealthBar));
        barObj.transform.SetParent(canvas.transform, false);

        HealthBar hb = barObj.GetComponent<HealthBar>();
        hb.SetupPlayerBarInEditor();
        UnityEditor.Selection.activeGameObject = barObj;
    }

    [UnityEditor.MenuItem("GameObject/UI/Theme Health Bar/Boss Bar (Naga Guardian)", false, 11)]
    public static void CreateBossBarMenuItem()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject barObj = new GameObject("BossHealthBar_NagaGuardian", typeof(RectTransform), typeof(HealthBar));
        barObj.transform.SetParent(canvas.transform, false);

        HealthBar hb = barObj.GetComponent<HealthBar>();
        hb.type = BarType.Boss;
        hb.EnsureHierarchy();
        hb.ApplyThemeStyling();
        hb.EnsureCanvasScaler();

        RectTransform rt = barObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 50f);
        rt.sizeDelta = new Vector2(700f, 96f);

        UnityEditor.Selection.activeGameObject = barObj;
    }

    [UnityEditor.MenuItem("GameObject/UI/Theme Health Bar/Zombie Floating Health Bar", false, 12)]
    public static void CreateZombieBarMenuItem()
    {
        GameObject selected = UnityEditor.Selection.activeGameObject;
        Transform parent = selected != null ? selected.transform : null;

        GameObject barObj = new GameObject("HealthBar_Zombie", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(HealthBar));
        if (parent != null) barObj.transform.SetParent(parent, false);

        Canvas c = barObj.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;

        barObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        barObj.transform.localScale = Vector3.one * 0.0075f;

        HealthBar hb = barObj.GetComponent<HealthBar>();
        hb.type = BarType.Zombie;
        hb.followTarget = parent;
        hb.worldOffset = new Vector3(0f, 2.2f, 0f);
        hb.EnsureHierarchy();
        hb.ApplyThemeStyling();
        hb.SetMaxHealth(1000f);
        hb.SetHealth(1000f);

        UnityEditor.Selection.activeGameObject = barObj;
        UnityEditor.Undo.RegisterCreatedObjectUndo(barObj, "Create Zombie Health Bar");
    }

    [UnityEditor.MenuItem("GameObject/UI/Theme Health Bar/Spider Floating Health Bar", false, 13)]
    public static void CreateSpiderBarMenuItem()
    {
        GameObject selected = UnityEditor.Selection.activeGameObject;
        Transform parent = selected != null ? selected.transform : null;

        GameObject barObj = new GameObject("HealthBar_Spider", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(HealthBar));
        if (parent != null) barObj.transform.SetParent(parent, false);

        Canvas c = barObj.GetComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;

        barObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        barObj.transform.localScale = Vector3.one * 0.0075f;

        HealthBar hb = barObj.GetComponent<HealthBar>();
        hb.type = BarType.Spider;
        hb.followTarget = parent;
        hb.worldOffset = new Vector3(0f, 1.8f, 0f);
        hb.EnsureHierarchy();
        hb.ApplyThemeStyling();
        hb.SetMaxHealth(1000f);
        hb.SetHealth(1000f);

        UnityEditor.Selection.activeGameObject = barObj;
        UnityEditor.Undo.RegisterCreatedObjectUndo(barObj, "Create Spider Health Bar");
    }

    [UnityEditor.MenuItem("GameObject/UI/Theme Health Bar/Attach to Selected Enemy (Auto-Detect)", false, 14)]
    public static void AttachToSelectedEnemyMenuItem()
    {
        GameObject selected = UnityEditor.Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[HealthBar] Please select an enemy (Zombie or Spider) GameObject in the Hierarchy first!");
            return;
        }

        bool isSpider = selected.GetComponent<SpiderEnemy>() != null || selected.name.ToLower().Contains("spider");
        BarType bType = isSpider ? BarType.Spider : BarType.Zombie;
        float hp = 1000f;

        HealthBar hb = AttachToEnemy(selected, bType, hp);
        if (hb != null)
        {
            UnityEditor.Selection.activeGameObject = hb.gameObject;
            UnityEditor.Undo.RegisterCreatedObjectUndo(hb.gameObject, "Attach Enemy Health Bar");
            Debug.Log($"<color=#00FF88>[HealthBar] Successfully attached {bType} health bar to '{selected.name}'!</color>");
        }
    }
#endif
}
