using UnityEngine;

public class HealthBarDebugInput : MonoBehaviour
{
    [SerializeField] private HealthBar healthBar;

    private void Awake()
    {
        if (healthBar == null)
            healthBar = GetComponent<HealthBar>();
        if (healthBar == null)
            throw new MissingComponentException("HealthBarDebugInput requires a HealthBar reference.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
            healthBar.SetHealth(healthBar.CurrentHealth - healthBar.MaxHealth * 0.15f);
        else if (Input.GetKeyDown(KeyCode.H))
            healthBar.SetHealth(healthBar.CurrentHealth + healthBar.MaxHealth * 0.25f);
        else if (Input.GetKeyDown(KeyCode.R))
            healthBar.SetHealth(healthBar.MaxHealth);
    }
}
