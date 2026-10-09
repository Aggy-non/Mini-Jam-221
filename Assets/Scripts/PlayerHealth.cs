
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float drainSpeed = 5f;

    private float currentHealth;
    private bool isDead;

    [Header("Health Bar")]
    [SerializeField] private Image backHolder;
    [SerializeField] private Image frontHolder;

    private void Start()
    {
        currentHealth = maxHealth;
        frontHolder.fillAmount = 1;
    }

    private void Update()
    {
        if (isDead)
            return;

       
        currentHealth -= drainSpeed * Time.deltaTime;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        UpdateHealthBar();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void UpdateHealthBar()
    {
        if (frontHolder != null)
        {
            frontHolder.fillAmount = currentHealth / maxHealth;
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Player Dead!");
    }

    public void EatBug(float healthAmount)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Clamp(currentHealth + healthAmount, 0f, maxHealth);
        UpdateHealthBar();
    }
}