using Game.Core.Bootstrap;
using Game.Core.Components.Damage;
using Game.Features.Characters.Hero;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Widgets.HealthBar {
    /// <summary>
    /// Displays the hero's health using two fill-mode Image stripes.
    /// HealthStripe snaps to the current value on damage; HighlightStripe trails behind
    /// with a configurable delay and constant drain speed.
    /// </summary>
    public class HealthBarWidget : MonoBehaviour {
        [SerializeField]
        private Image healthStripe;

        [SerializeField]
        private Image highlightStripe;

        [SerializeField]
        private float highlightDelay = 0.5f;

        [SerializeField]
        private float highlightSpeed = 1f;

        private Damageable damageable;
        private float targetFill;
        private float highlightDelayTimer;
        private bool isHighlightAnimating;

        private void OnEnable() {
            G.Hero.OnHeroRegistered += OnHeroRegistered;
            G.Hero.OnHeroUnregistered += OnHeroUnregistered;

            if (G.Hero.Controller != null) {
                Bind(G.Hero.Controller);
            }
        }

        private void OnDisable() {
            G.Hero.OnHeroRegistered -= OnHeroRegistered;
            G.Hero.OnHeroUnregistered -= OnHeroUnregistered;
            Unbind();
        }

        private void Update() {
            if (!isHighlightAnimating) {
                return;
            }

            if (highlightDelayTimer > 0f) {
                highlightDelayTimer -= Time.deltaTime;
                return;
            }

            highlightStripe.fillAmount = Mathf.MoveTowards(
                highlightStripe.fillAmount, targetFill, highlightSpeed * Time.deltaTime
            );

            if (Mathf.Approximately(highlightStripe.fillAmount, targetFill)) {
                isHighlightAnimating = false;
            }
        }

        /// <summary>
        /// Sets both stripes to the given health value immediately, cancelling any animation.
        /// </summary>
        public void SetHealthImmediate(float health, float maxHealth) {
            float fill = maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
            healthStripe.fillAmount = fill;
            highlightStripe.fillAmount = fill;
            targetFill = fill;
            isHighlightAnimating = false;
            highlightDelayTimer = 0f;
        }

        private void Bind(PlayerController controller) {
            Unbind();
            damageable = controller.Damageable;
            damageable.OnHealthChanged += OnHealthChanged;
            SetHealthImmediate(damageable.Health, damageable.maxHealth);
        }

        private void Unbind() {
            if (damageable != null) {
                damageable.OnHealthChanged -= OnHealthChanged;
                damageable = null;
            }
        }

        private void OnHeroRegistered(PlayerController controller) {
            Bind(controller);
        }

        private void OnHeroUnregistered() {
            Unbind();
        }

        private void OnHealthChanged(float newHealth) {
            float newFill = damageable.maxHealth > 0f
                ? Mathf.Clamp01(newHealth / damageable.maxHealth)
                : 0f;

            if (newFill < targetFill) {
                // Damage: snap health stripe, animate highlight
                healthStripe.fillAmount = newFill;
                targetFill = newFill;

                if (!isHighlightAnimating) {
                    highlightDelayTimer = highlightDelay;
                    isHighlightAnimating = true;
                }
            } else if (newFill > targetFill) {
                // Heal: snap both stripes immediately
                SetHealthImmediate(newHealth, damageable.maxHealth);
            }
        }
    }
}
