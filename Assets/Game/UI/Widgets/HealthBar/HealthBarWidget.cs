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
    /// The bar resizes itself when max health changes (stat upgrades).
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

        [Header("Sizing")]
        [SerializeField, Tooltip("Current maximum HP the bar represents.")]
        private int maxHealth = 5;

        [SerializeField, Tooltip("Width in pixels per 1 HP.")]
        private int pixelsPerHp = 5;

        private Damageable damageable;
        private float targetFill;
        private float highlightDelayTimer;
        private bool isHighlightAnimating;

        private void OnValidate() {
            if (healthStripe == null || highlightStripe == null) {
                return;
            }

            UpdateBarSize();
        }

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
            damageable.OnMaxHealthChanged += OnMaxHealthChanged;
            maxHealth = Mathf.RoundToInt(damageable.maxHealth);
            UpdateBarSize();
            SetHealthImmediate(damageable.Health, damageable.maxHealth);
        }

        private void Unbind() {
            if (damageable != null) {
                damageable.OnHealthChanged -= OnHealthChanged;
                damageable.OnMaxHealthChanged -= OnMaxHealthChanged;
                damageable = null;
            }
        }

        private void OnHeroRegistered(PlayerController controller) {
            Bind(controller);
        }

        private void OnHeroUnregistered() {
            Unbind();
        }

        private void OnMaxHealthChanged(float newMax) {
            maxHealth = Mathf.RoundToInt(newMax);
            UpdateBarSize();
            SetHealthImmediate(damageable.Health, newMax);
        }

        /// <summary>
        /// Resizes both stripes and the root RectTransform so the bar width matches
        /// <c>maxHealth * pixelsPerHp</c>. The root grows or shrinks by the same delta
        /// as the stripes, preserving the frame border padding.
        /// </summary>
        private void UpdateBarSize() {
            float stripeWidth = maxHealth * pixelsPerHp;

            var healthRT = healthStripe.rectTransform;
            var highlightRT = highlightStripe.rectTransform;
            var rootRT = (RectTransform)transform;

            float widthDelta = stripeWidth - healthRT.sizeDelta.x;
            healthRT.sizeDelta = new Vector2(stripeWidth, healthRT.sizeDelta.y);
            highlightRT.sizeDelta = new Vector2(stripeWidth, highlightRT.sizeDelta.y);
            rootRT.sizeDelta = new Vector2(rootRT.sizeDelta.x + widthDelta, rootRT.sizeDelta.y);
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
