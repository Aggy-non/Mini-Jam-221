
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

            float healthPercent = currentHealth / maxHealth;

            if (healthPercent > 0.8f)
                frontHolder.color = Color.Lerp(new Color(0.8f, 0f, 0f), new Color(1f, 0.2f, 0.5f), (1f - healthPercent) / 0.2f);
            else if (healthPercent > 0.6f)
                frontHolder.color = Color.Lerp(new Color(1f, 0.2f, 0.5f), new Color(0.6f, 0.1f, 0.9f), (0.8f - healthPercent) / 0.2f);
            else if (healthPercent > 0.4f)
                frontHolder.color = Color.Lerp(new Color(0.6f, 0.1f, 0.9f), Color.blue, (0.6f - healthPercent) / 0.2f);
            else
                frontHolder.color = Color.Lerp(Color.blue, Color.cyan, (0.4f - healthPercent) / 0.4f);
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