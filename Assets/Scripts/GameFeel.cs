using UnityEngine;

/// <summary>
/// Tiny hit-stop / FOV punch director used by combat scripts.
/// </summary>
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    private float _hitStopUntil;
    private float _storedTimeScale = 1f;
    private Camera _cam;
    private float _baseFov = 60f;
    private float _fovPunch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("GameFeel");
        DontDestroyOnLoad(go);
        go.AddComponent<GameFeel>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _cam = Camera.main;
        if (_cam != null) _baseFov = _cam.fieldOfView;
    }

    public static void HitStop(float duration)
    {
        if (Instance == null) return;
        Instance.DoHitStop(duration);
    }

    public static void FovPunch(float amount)
    {
        if (Instance == null) return;
        Instance._fovPunch = Mathf.Max(Instance._fovPunch, amount);
    }

    private void DoHitStop(float duration)
    {
        if (Time.timeScale < 0.99f) return;
        _storedTimeScale = Time.timeScale;
        Time.timeScale = 0.08f;
        _hitStopUntil = Time.unscaledTime + duration;
    }

    private void Update()
    {
        if (Time.timeScale < 0.99f && Time.unscaledTime >= _hitStopUntil)
            Time.timeScale = _storedTimeScale <= 0.01f ? 1f : _storedTimeScale;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        _fovPunch = Mathf.Lerp(_fovPunch, 0f, Time.unscaledDeltaTime * 10f);
        _cam.fieldOfView = _baseFov + _fovPunch;
    }

    private void OnDisable()
    {
        if (Time.timeScale < 0.2f)
            Time.timeScale = 1f;
    }
}
