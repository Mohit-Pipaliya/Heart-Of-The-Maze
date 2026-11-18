using UnityEngine;

public class HealthBarDebugInput : MonoBehaviour
{
    [SerializeField] private HealthBar healthBar;

    private void Awake()
    {
        if (healthBar == null)
            healthBar = GetComponent<HealthBar>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            if (PlayerController.Instance != null)
                PlayerController.Instance.TakeDamage(Mathf.Max(1, (int)(PlayerController.Instance.MaxHealth * 0.15f)));
            ApplyToBars(bar => bar.SetHealth(bar.CurrentHealth - bar.MaxHealth * 0.15f));
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            if (PlayerController.Instance != null)
                PlayerController.Instance.Heal(Mathf.Max(1, (int)(PlayerController.Instance.MaxHealth * 0.25f)));
            ApplyToBars(bar => bar.SetHealth(bar.CurrentHealth + bar.MaxHealth * 0.25f));
        }
        else if (Input.GetKeyDown(KeyCode.R))
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
