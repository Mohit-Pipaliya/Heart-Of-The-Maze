using UnityEngine;

public class HealthBarDebugInput : MonoBehaviour
{
    [SerializeField] private HealthBar healthBar;

    private void Awake()
    {
        if (healthBar == null)
            healthBar = GetComponent<HealthBar>();
    }

    [Header("Debug Controls (Require Left/Right Ctrl)")]
    [SerializeField] private bool enableDebugKeys = false; // Disabled by default so normal gameplay keys are never affected
    [SerializeField] private KeyCode damageKey = KeyCode.F9;
    [SerializeField] private KeyCode healKey = KeyCode.F10;
    [SerializeField] private KeyCode resetKey = KeyCode.F11;

    private void Update()
    {
        if (!enableDebugKeys) return;

        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrlHeld) return;

        if (Input.GetKeyDown(damageKey))
        {
            if (PlayerController.Instance != null)
                PlayerController.Instance.TakeDamage(Mathf.Max(1, (int)(PlayerController.Instance.MaxHealth * 0.15f)));
            ApplyToBars(bar => bar.SetHealth(bar.CurrentHealth - bar.MaxHealth * 0.15f));
        }
        else if (Input.GetKeyDown(healKey))
        {
            if (PlayerController.Instance != null)
                PlayerController.Instance.Heal(Mathf.Max(1, (int)(PlayerController.Instance.MaxHealth * 0.25f)));
            ApplyToBars(bar => bar.SetHealth(bar.CurrentHealth + bar.MaxHealth * 0.25f));
        }
        else if (Input.GetKeyDown(resetKey))
        {
            if (PlayerController.Instance != null)
                PlayerController.Instance.ResetHealth();
            ApplyToBars(bar => bar.SetHealth(bar.MaxHealth));
        }
    }

    private void ApplyToBars(System.Action<HealthBar> action)
    {
        if (healthBar != null)
        {
            action(healthBar);
        }
        else
        {
            HealthBar[] allBars = FindObjectsByType<HealthBar>(FindObjectsSortMode.None);
            for (int i = 0; i < allBars.Length; i++)
            {
                if (allBars[i] != null && allBars[i].gameObject.activeInHierarchy)
                    action(allBars[i]);
            }
        }
    }
}
