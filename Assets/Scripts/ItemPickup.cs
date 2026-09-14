using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    private bool playerInRange = false;
    private PlayerController player;

    void Update()
    {
        // Jab player pas hoga aur 'E' dabayega
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            if (player != null)
            {
                // Player ko sword de do
                player.PickupSword();
                
                // Zameen wali sword ko gayab (destroy) kar do
                Destroy(gameObject); 
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            player = other.GetComponent<PlayerController>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            player = null;
        }
    }

    // Yeh function automatically Screen pe bina UI Canvas ke text draw kar dega
    private void OnGUI()
    {
        if (playerInRange)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 45;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            // Halki si Shadow ke liye taaki text clear dikhe
            GUIStyle shadow = new GUIStyle(style);
            shadow.normal.textColor = Color.black;
            
            Rect rect = new Rect(0, 0, Screen.width, Screen.height);
            Rect shadowRect = new Rect(2, 2, Screen.width, Screen.height);

            // Screen ke center me draw hoga
            GUI.Label(shadowRect, "Press [E] to Pickup Sword", shadow);
            GUI.Label(rect, "Press [E] to Pickup Sword", style);
        }
    }
}
