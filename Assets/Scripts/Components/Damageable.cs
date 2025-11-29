using System;
using UnityEngine;
using UnityEngine.Events;

namespace Components {
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
        private UnityEvent onHit;
        
        // TODO: implement simple FSM for easier state management and automatic transitions.
        
        /// <summary>
        /// True if this entity was hit this frame. Note: this is reset to false in LateUpdate,
        /// so check it earlier, i.e. in `Update()` method.
        /// </summary>
        public bool IsHitThisFrame { get; private set; }
        
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
        public void TryTakeDamage(int amount, Collider2D damager) {
            if (amount <= 0 || isDead) {
                return;
            }

            if (IsInvulnerable()) {
                return;
            }

            ApplyDamage(amount, damager);
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
        private void ApplyDamage(int amount, Collider2D damager) {
            if (isDead) {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
            IsHitThisFrame = true;
            invulnerabilityTimer = invulnerabilityTime;
            
            onHit?.Invoke();

            if (currentHealth <= 0) {
                Die();
            } else {
                if (hasKnockback) {
                    ApplyKnockback(damager);
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
