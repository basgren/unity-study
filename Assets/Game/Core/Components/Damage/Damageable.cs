using System;
using Core.Audio;
using Game.Core.Bootstrap;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Core.Components.Damage {
    [Serializable]
    public class OnHitEvent : UnityEvent<Damager> {
    }

    [RequireComponent(typeof(Collider2D))]
    public class Damageable : MonoBehaviour {
        [Header("Health")]
        [SerializeField, Tooltip("Maximum health points this entity can have.")]
        public float maxHealth = 5;

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

        [SerializeField]
        private OnHitEvent onDeath;
        
        [Header("Sounds")]
        [SerializeField]
        private AudioCue hitSound;
        
        [SerializeField]
        private AudioCue destroySound;

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

        public float Health => currentHealth;
        public bool IsDead { get; private set; }

        public event Action<float> OnHealthChanged;

        private SpriteRenderer spriteRenderer;
        private float nextAllowedDamageTime;
        private Collider2D myCollider;
        private float invulnerabilityTimer;

        private void Awake() {
            myCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (currentHealth <= 0) {
                currentHealth = maxHealth;
            }
        }

        private void LateUpdate() {
            // TODO: [BG] We also update animator in LateUpdate, so sometimes this update is called
            //   before update of a character and it doesn't recognize hit. 
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
                float phase =
                    Mathf.Sin(2 * Mathf.PI * (1 - invulnerabilityTimer / invulnerabilityTime) * coeff) * 0.3f + 0.7f;
                spriteRenderer.color = new Color(1, 1, 1, phase);
            } else {
                spriteRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// Tries to apply damage, respecting the internal cooldown.
        /// </summary>
        public bool TryTakeDamage(Damager damager) {
            if (IgnoreDamage || damager.Damage <= 0 || IsDead || IsInvulnerable()) {
                return false;
            }

            ApplyDamage(damager);
            return true;
        }

        public void AddHealth(float amount) {
            SetHealth(currentHealth + amount);
        }

        public void SetHealth(float amount) {
            float previous = currentHealth;
            currentHealth = Mathf.Clamp(amount, 0, maxHealth);

            if (!Mathf.Approximately(previous, currentHealth)) {
                OnHealthChanged?.Invoke(currentHealth);
            }
        }

        public void Revive() {
            IsDead = false;
            SetHealth(maxHealth);
        }

        private bool IsInvulnerable() {
            return invulnerabilityTimer > 0;
        }

        /// <summary>
        /// Applies damage immediately and checks for death.
        /// </summary>
        private void ApplyDamage(Damager damager) {
            if (IsDead) {
                return;
            }

            SetHealth(currentHealth - damager.Damage);
            IsHitThisFrame = true;
            invulnerabilityTimer = invulnerabilityTime;

            if (currentHealth <= 0) {
                IsDead = true;
            }

            if (hasKnockback) {
                // TODO: [BG] Implement easing for knockback
                ApplyKnockback(damager.DamageCollider);
            }

            // Call on hit at the end 
            onHit?.Invoke(damager);

            if (IsDead) {
                onDeath?.Invoke(damager);

                if (destroySound != null) {
                    G.Audio.PlayAt(destroySound, transform.position);
                }
            } else {
                if (hitSound != null) {
                    G.Audio.PlayAt(hitSound, transform.position);
                }
            }
        }

        private void ApplyKnockback(Collider2D damagerCollider) {
            Vector2 hitPoint = myCollider.ClosestPoint(damagerCollider.transform.position);
            Vector2 selfCenter = myCollider.bounds.center;
            Vector2 direction = (selfCenter - hitPoint).normalized;

            myCollider.attachedRigidbody.velocity = direction * knockbackForce;
        }
    }
}
