using UnityEngine;

/// <summary>
/// Interface for any game object that can take damage
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Applies damage to the object
    /// </summary>
    /// <param name="damage">Amount of damage to apply</param>
    void TakeDamage(float damage);
}

/// <summary>
/// Example implementation of IDamageable for the player
/// Attach this to your player or any other object that should take damage
/// </summary>
public class Damageable : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage. Current health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} has been defeated!");
        // Add death effects, game over logic, etc.
        // For now, just deactivate the object
        gameObject.SetActive(false);
    }
}
