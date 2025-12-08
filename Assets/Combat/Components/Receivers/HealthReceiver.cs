// HealthReceiver.cs
// Purpose: Only handles health changes for a combat target node.

using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Handles HP changes for a combat node.
    ///
    /// This does NOT implement IActionTarget and does NOT know about stun/knockback.
    /// It only cares about health and reacts to IHealthReceiver.ApplyHealthChange.
    /// </summary>
    public class HealthReceiver : MonoBehaviour, IHealthReceiver
    {
        [Header("Health Settings")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;

        private void Reset()
        {
            _currentHealth = _maxHealth;
        }

        public void ApplyHealthChange(float amount, ActionContext ctx)
        {
            _currentHealth += amount; // negative = damage
            Debug.Log(
                $"[HealthReceiver] {name} HP change {amount}, now {_currentHealth}/{_maxHealth}.",
                this);

            if (_currentHealth <= 0f)
            {
                _currentHealth = 0f;
                Debug.Log($"[HealthReceiver] {name} died.", this);
                // TODO: death/cleanup/respawn hook.
            }
        }
        
        public override string ToString()
        {
            return $"{_currentHealth}/{_maxHealth} HP";
        }
    }

    
}
