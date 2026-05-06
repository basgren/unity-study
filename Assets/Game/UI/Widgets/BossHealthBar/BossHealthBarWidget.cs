using Game.Core.Bootstrap;
using Game.Core.Components.Damage;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Widgets.BossHealthBar {
    /// <summary>
    /// Boss health bar driven by <see cref="Game.Core.Services.BossFightService"/>.
    /// HealthStripe tracks the boss's <see cref="Damageable"/>; ShieldStripe tracks the
    /// shield's <see cref="Damageable"/> when one is engaged. Each stripe uses the same
    /// snap + delayed highlight-trail pattern as the player health bar.
    /// The visual root is hidden until a boss is engaged so the widget can live permanently in the HUD scene.
    /// </summary>
    public class BossHealthBarWidget : MonoBehaviour {
        [Header("Visual root")]
        [SerializeField, Tooltip("Toggled on/off with boss engagement. Must be a child so the widget itself stays enabled to receive service events.")]
        private GameObject barRoot;

        [Header("Health stripe")]
        [SerializeField]
        private Image healthStripe;

        [SerializeField]
        private Image highlightStripe;

        [Header("Shield stripe")]
        [SerializeField, Tooltip("Toggled on/off with shield engagement.")]
        private GameObject shieldStripeRoot;

        [SerializeField]
        private Image shieldStripe;

        [SerializeField]
        private Image shieldHighlightStripe;

        [Header("Animation")]
        [SerializeField]
        private float highlightDelay = 0.5f;

        [SerializeField]
        private float highlightSpeed = 1f;

        private StripeView bossView;
        private StripeView shieldView;
        private Damageable bossDamageable;
        private Damageable shieldDamageable;

        private void Awake() {
            bossView = new StripeView(healthStripe, highlightStripe);
            shieldView = new StripeView(shieldStripe, shieldHighlightStripe);
        }

        private void OnEnable() {
            if (barRoot != null) {
                barRoot.SetActive(false);
            }

            if (shieldStripeRoot != null) {
                shieldStripeRoot.SetActive(false);
            }

            if (G.BossFight == null) {
                return;
            }

            G.BossFight.OnBossEngaged += OnBossEngaged;
            G.BossFight.OnBossDisengaged += OnBossDisengaged;
            G.BossFight.OnShieldEngaged += OnShieldEngaged;
            G.BossFight.OnShieldDisengaged += OnShieldDisengaged;

            // Late-subscriber path: if engagements happened before the widget enabled
            // (e.g. service initialised after boss spawn order), bind immediately.
            if (G.BossFight.Boss != null) {
                BindBoss(G.BossFight.Boss);
            }

            if (G.BossFight.Shield != null) {
                BindShield(G.BossFight.Shield);
            }
        }

        private void OnDisable() {
            if (G.BossFight != null) {
                G.BossFight.OnBossEngaged -= OnBossEngaged;
                G.BossFight.OnBossDisengaged -= OnBossDisengaged;
                G.BossFight.OnShieldEngaged -= OnShieldEngaged;
                G.BossFight.OnShieldDisengaged -= OnShieldDisengaged;
            }

            UnbindBoss();
            UnbindShield();
        }

        private void Update() {
            bossView?.TickHighlight(highlightDelay, highlightSpeed, Time.deltaTime);
            shieldView?.TickHighlight(highlightDelay, highlightSpeed, Time.deltaTime);
        }

        private void OnBossEngaged(Damageable boss) {
            BindBoss(boss);
        }

        private void OnBossDisengaged() {
            UnbindBoss();
        }

        private void OnShieldEngaged(Damageable shield) {
            BindShield(shield);
        }

        private void OnShieldDisengaged() {
            UnbindShield();
        }

        private void BindBoss(Damageable boss) {
            UnbindBoss();

            bossDamageable = boss;
            bossDamageable.OnHealthChanged += OnBossHealthChanged;
            bossDamageable.OnMaxHealthChanged += OnBossMaxHealthChanged;

            bossView.SetImmediate(bossDamageable.Health, bossDamageable.maxHealth);

            if (barRoot != null) {
                barRoot.SetActive(true);
            }
        }

        private void UnbindBoss() {
            if (bossDamageable != null) {
                bossDamageable.OnHealthChanged -= OnBossHealthChanged;
                bossDamageable.OnMaxHealthChanged -= OnBossMaxHealthChanged;
                bossDamageable = null;
            }

            if (barRoot != null) {
                barRoot.SetActive(false);
            }
        }

        private void BindShield(Damageable shield) {
            UnbindShield();

            shieldDamageable = shield;
            shieldDamageable.OnHealthChanged += OnShieldHealthChanged;

            shieldView.SetImmediate(shieldDamageable.Health, shieldDamageable.maxHealth);

            if (shieldStripeRoot != null) {
                shieldStripeRoot.SetActive(true);
            }
        }

        private void UnbindShield() {
            if (shieldDamageable != null) {
                shieldDamageable.OnHealthChanged -= OnShieldHealthChanged;
                shieldDamageable = null;
            }

            if (shieldStripeRoot != null) {
                shieldStripeRoot.SetActive(false);
            }
        }

        private void OnBossHealthChanged(float newHealth) {
            bossView.OnHealthChanged(newHealth, bossDamageable.maxHealth);
        }

        private void OnBossMaxHealthChanged(float newMax) {
            // Bar width is fixed by design; only refresh the fill ratio.
            bossView.SetImmediate(bossDamageable.Health, newMax);
        }

        private void OnShieldHealthChanged(float newHealth) {
            // The boss subscribes to the shield's OnHealthChanged before us, and on the
            // fatal-hit invocation it disengages the shield (which unbinds us) before our
            // handler runs. Guard against the now-null reference rather than depending on
            // subscriber order.
            if (shieldDamageable == null) {
                return;
            }

            shieldView.OnHealthChanged(newHealth, shieldDamageable.maxHealth);
        }

        /// <summary>
        /// Drives a single fill-stripe pair: a snap stripe that matches the current value
        /// instantly and a highlight stripe that drains toward the snap value after a delay.
        /// </summary>
        private class StripeView {
            private readonly Image fill;
            private readonly Image highlight;
            private float targetFill;
            private float highlightDelayTimer;
            private bool isAnimating;
            private bool needsDelayInit;

            public StripeView(Image fill, Image highlight) {
                this.fill = fill;
                this.highlight = highlight;
            }

            public void SetImmediate(float health, float max) {
                if (fill == null) {
                    return;
                }

                float value = max > 0f ? Mathf.Clamp01(health / max) : 0f;
                fill.fillAmount = value;
                if (highlight != null) {
                    highlight.fillAmount = value;
                }
                targetFill = value;
                isAnimating = false;
                needsDelayInit = false;
                highlightDelayTimer = 0f;
            }

            public void OnHealthChanged(float newHealth, float max) {
                if (fill == null) {
                    return;
                }

                float newFill = max > 0f ? Mathf.Clamp01(newHealth / max) : 0f;

                if (newFill < targetFill) {
                    // Damage: snap fill, animate highlight after delay.
                    fill.fillAmount = newFill;
                    targetFill = newFill;

                    if (!isAnimating && highlight != null) {
                        isAnimating = true;
                        // Defer reading the parent's delay value to the next tick.
                        needsDelayInit = true;
                    }
                } else if (newFill > targetFill) {
                    // Heal: snap both stripes immediately.
                    SetImmediate(newHealth, max);
                }
            }

            public void TickHighlight(float delay, float speed, float deltaTime) {
                if (!isAnimating || highlight == null) {
                    return;
                }

                if (needsDelayInit) {
                    needsDelayInit = false;
                    highlightDelayTimer = delay;
                }

                if (highlightDelayTimer > 0f) {
                    highlightDelayTimer -= deltaTime;
                    return;
                }

                highlight.fillAmount = Mathf.MoveTowards(highlight.fillAmount, targetFill, speed * deltaTime);

                if (Mathf.Approximately(highlight.fillAmount, targetFill)) {
                    isAnimating = false;
                }
            }
        }
    }
}
