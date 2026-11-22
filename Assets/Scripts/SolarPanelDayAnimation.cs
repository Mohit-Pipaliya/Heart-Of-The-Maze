using UnityEngine;

[DisallowMultipleComponent]
public class SolarPanelDayAnimation : MonoBehaviour
{
    [SerializeField] private DayNightController dayNightController;
    [SerializeField] private Animator panelAnimator;

    private float _dayAnimationSpeed;
    private bool _isDay;

    private void Awake()
    {
        if (dayNightController == null)
            dayNightController = FindFirstObjectByType<DayNightController>();

        if (panelAnimator == null)
            panelAnimator = GetComponentInChildren<Animator>();

        if (dayNightController == null)
        {
            Debug.LogError($"[{nameof(SolarPanelDayAnimation)}] Assign a DayNightController or add one to the scene.", this);
            enabled = false;
            return;
        }

        if (panelAnimator == null)
        {
            Debug.LogError($"[{nameof(SolarPanelDayAnimation)}] Assign the solar panel Animator.", this);
            enabled = false;
            return;
        }

        _dayAnimationSpeed = panelAnimator.speed;
        _isDay = dayNightController.IsDay;
        ApplyDayState();
    }

    private void Update()
    {
        if (_isDay == dayNightController.IsDay)
            return;

        _isDay = dayNightController.IsDay;
        ApplyDayState();
    }

    private void ApplyDayState()
    {
        panelAnimator.speed = _isDay ? _dayAnimationSpeed : 0f;
    }
}
