using System;
using UnityEngine;
using UnityEngine.Events;

namespace Components {
  
    [Serializable]
    public class OnHitEvent: UnityEvent<Damager> {}
    
    [RequireComponent(typeof(Collider2D))]
    public class Damageable : MonoBehaviour {
        [Header("Health")]
        [SerializeField, Tooltip("Maximum health points this entity can have.")]
        private float maxHealth = 5;

        [SerializeField, Tooltip("Current health points. If <= 0 at start, it will be initialized from maxHealth.")]
        private float currentHealth;

        [Header("Damage Handling")]
        [SerializeField, Tooltip("Minimum time in seconds between two consecutive hits.")]
        private float invulnerabilityTime = 0.5f;

        // TODO: probably knockback may be a separate component, but now for simplicity it's here.
        [SerializeField]
        private bool hasKnockback = true;

        [SerializeField]
        private float knockbackForce = 13f;

        [SerializeField]
        private OnHitEvent onHit;
        
        // TODO: implement simple FSM for easier state management and automatic transitions.
        
        /// <summary>
        /// True if this entity was hit this frame. Note: this is reset to false in LateUpdate,
        /// so check it earlier, i.e. in `Update()` method.
        /// </summary>
        public bool IsHitThisFrame { get; private set; }
        
        /// <summary>
        /// Set to true if any damage should be ignored. For example, during cutscenes or death animation. 
        /// </summary>
        public bool IgnoreDamage { get; set; }
        
        private SpriteRenderer spriteRenderer;
        private float nextAllowedDamageTime;
        private Collider2D myCollider;
        private bool isDead;
        private float invulnerabilityTimer;
        
        public event Action<Damageable> Died;

        private void Awake() {
            myCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (currentHealth <= 0) {
                currentHealth = maxHealth;
            }
        }
        
        private void LateUpdate() {
            IsHitThisFrame = false;

            if (IgnoreDamage) {
                invulnerabilityTimer = 0;
            }
            
            if (invulnerabilityTimer > 0) {
                invulnerabilityTimer -= Time.deltaTime;
                DisplayInvulnerability();
            }
        }

        private void DisplayInvulnerability() {
            if (IsInvulnerable()) {
                float flashesPerSecond = 4;
                float coeff = invulnerabilityTime * flashesPerSecond;
                float phase = Mathf.Sin(2 * Mathf.PI * (1 - invulnerabilityTimer / invulnerabilityTime) * coeff) * 0.3f + 0.7f;
                spriteRenderer.color = new Color(1, 1, 1, phase);
            } else {
                spriteRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// Tries to apply damage, respecting the internal cooldown.
        /// </summary>
        public void TryTakeDamage(Damager damager) {
            if (IgnoreDamage || damager.Damage <= 0 || isDead || IsInvulnerable()) {
                return;
            }

            ApplyDamage(damager);
        }

        public void AddHealth(float amount) {
            currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        }

        private bool IsInvulnerable() {
            return invulnerabilityTimer > 0;
        }

        /// <summary>
        /// Applies damage immediately and checks for death.
        /// </summary>
        private void ApplyDamage(Damager damager) {
            if (isDead) {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth - damager.Damage, 0, maxHealth);
            IsHitThisFrame = true;
            invulnerabilityTimer = invulnerabilityTime;
            
            onHit?.Invoke(damager);

            if (currentHealth <= 0) {
                Die();
            } else {
                if (hasKnockback) {
                    ApplyKnockback(damager.DamageCollider);
                }
            }
        }

        private void ApplyKnockback(Collider2D damagerCollider) {
            Vector2 hitPoint = myCollider.ClosestPoint(damagerCollider.transform.position);
            Vector2 selfCenter = myCollider.bounds.center;
            Vector2 direction = (selfCenter - hitPoint).normalized;

            myCollider.attachedRigidbody.velocity = direction * knockbackForce;
        }

        private void Die() {
            Debug.Log($"[Damageable] '{name}' died.");
            isDead = true;
            Died?.Invoke(this);
        }
    }
}
