
using UnityEngine;

public class Bug : MonoBehaviour
{
    [Header("Bug Settings")]
    [SerializeField] private float healthAmount = 20f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();


            if (playerHealth != null)
            {
                playerHealth.EatBug(healthAmount);
                Destroy(gameObject);
            }
        }
    }
}