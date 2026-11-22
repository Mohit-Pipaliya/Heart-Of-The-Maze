using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class IntroPromptUI : MonoBehaviour
{
    [SerializeField] private string promptText = "Press U to Go Up";

    private CanvasGroup _canvasGroup;
    private RectTransform _textRect;
    private Component _textComponent;
    private Coroutine _fadeRoutine;
    private bool _visible;
    private Vector3 _baseTextScale = Vector3.one;

    public static IntroPromptUI GetOrCreate()
    {
        IntroPromptUI existing = FindFirstObjectByType<IntroPromptUI>();
        if (existing != null)
            return existing;

        GameObject canvasObject = new GameObject(
            "IntroPromptCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));
        return canvasObject.AddComponent<IntroPromptUI>();
    }

    private void Awake()
    {
        EnsureCanvas();
        EnsureEventSystem();
        BuildPromptText();
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private void Update()
    {
        if (!_visible || _textRect == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.025f;
        _textRect.localScale = _baseTextScale * pulse;
    }

    public void SetPrompt(string message)
    {
        promptText = message;
        SetTextValue(promptText);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        _visible = true;
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(1f, 0.25f, false));
    }

    public void Hide()
    {
        _visible = false;
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(0f, 0.2f, true));
    }

    private void EnsureCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void BuildPromptText()
    {
        Transform existing = transform.Find("PromptText");
        GameObject textObject = existing != null
            ? existing.gameObject
            : new GameObject("PromptText", typeof(RectTransform));

        if (existing == null)
            textObject.transform.SetParent(transform, false);

        _textRect = textObject.GetComponent<RectTransform>();
        _textRect.anchorMin = new Vector2(0.5f, 0f);
        _textRect.anchorMax = new Vector2(0.5f, 0f);
        _textRect.pivot = new Vector2(0.5f, 0f);
        _textRect.anchoredPosition = new Vector2(0f, 82f);
        _textRect.sizeDelta = new Vector2(900f, 90f);
        _baseTextScale = _textRect.localScale;

        Type tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        if (tmpType != null && typeof(Component).IsAssignableFrom(tmpType))
        {
            _textComponent = textObject.GetComponent(tmpType) ?? textObject.AddComponent(tmpType);
            SetProperty(_textComponent, "fontSize", 38f);
            SetEnumProperty(_textComponent, "alignment", "Center");
            SetEnumProperty(_textComponent, "fontStyle", "Bold");
            SetProperty(_textComponent, "enableWordWrapping", false);
            SetProperty(_textComponent, "color", Color.white);
        }
        else
        {
            Text legacyText = textObject.GetComponent<Text>();
            if (legacyText == null)
                legacyText = textObject.AddComponent<Text>();

            legacyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyText.font == null)
                legacyText.font = Font.CreateDynamicFontFromOSFont("Arial", 38);
            legacyText.fontSize = 38;
            legacyText.fontStyle = FontStyle.Bold;
            legacyText.alignment = TextAnchor.MiddleCenter;
            legacyText.color = Color.white;
            legacyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            legacyText.verticalOverflow = VerticalWrapMode.Overflow;
            _textComponent = legacyText;
        }

        Outline outline = textObject.GetComponent<Outline>();
        if (outline == null)
            outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(1f, -1f);

        SetTextValue(promptText);
        textObject.SetActive(false);
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        Type inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            eventSystemObject.AddComponent(inputModuleType);
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (eventSystemObject.GetComponent<BaseInputModule>() == null)
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, bool deactivateWhenDone)
    {
        if (_textRect != null)
            _textRect.gameObject.SetActive(true);

        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _fadeRoutine = null;
        if (deactivateWhenDone)
        {
            if (_textRect != null)
                _textRect.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
    }

    private void SetTextValue(string value)
    {
        if (_textComponent is Text legacyText)
        {
            legacyText.text = value;
            return;
        }

        SetProperty(_textComponent, "text", value);
    }

    private static void SetProperty(Component target, string propertyName, object value)
    {
        if (target == null)
            return;

        PropertyInfo property = target.GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
            property.SetValue(target, value);
    }

    private static void SetEnumProperty(Component target, string propertyName, string value)
    {
        if (target == null)
            return;

        PropertyInfo property = target.GetType().GetProperty(propertyName);
        if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
            return;

        object enumValue = Enum.Parse(property.PropertyType, value);
        property.SetValue(target, enumValue);
    }
}
