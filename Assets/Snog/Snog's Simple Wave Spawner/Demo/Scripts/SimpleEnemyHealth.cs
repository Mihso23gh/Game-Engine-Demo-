using UnityEngine;

namespace Snog.SimpleWaveSystem.Demo
{
    public class SimpleEnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            currentHealth -= amount;

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            // Demo-simple: just destroy
            Destroy(gameObject);
        }
    }
}
