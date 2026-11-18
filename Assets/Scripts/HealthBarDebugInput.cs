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
            ApplyToBars(bar => bar.SetHealth(bar.CurrentHealth - bar.MaxHealth * 0.15f));
        }
        else if (Input.GetKeyDown(KeyCode.H))
        {
            ApplyToBars(bar => bar.SetHealth(bar.CurrentHealth + bar.MaxHealth * 0.25f));
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
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
