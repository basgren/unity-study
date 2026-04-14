using Core.Audio;
using Core.Models;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Inventory {
    public class BackpackItemCtrl : MonoBehaviour {
        [SerializeField]
        private Image icon;

        [SerializeField]
        private TextMeshProUGUI text;

        [SerializeField]
        private GameObject selectionObject;

        [Header("Cooldown")]
        [SerializeField]
        private Image cooldownMask;

        [SerializeField]
        private bool showCooldownEndEffect;

        [SerializeField]
        private Image cooldownEndMask;
        
        [SerializeField]
        private AudioCue cooldownEndSound; 

        private const float CooldownEndEffectDuration = 0.5f;
        private const float CooldownEndEffectStartAlpha = 1f;
        private const float DisabledIconAlpha = 0.5f;

        private InventoryItem item;
        public InventoryItem Item => item;
        private float prevCooldownProgress;
        private float cooldownEndTimer;
        private const float RoundScale = 20f;


        public void SetItem(InventoryItem invItem) {
            if (invItem == null) {
                item = null;
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            item = invItem;
            var id = item.id;
            Sprite sprite = DefsFacade.I.Items.Get(id).Icon;

            icon.sprite = sprite;
            text.text = item.count.ToString();
            text.gameObject.SetActive(item.count > 1);
            
            SetCooldownEndMaskAlpha(0f);
            cooldownEndTimer = 0f;
            prevCooldownProgress = 0f;
        }

        public void SetSelected(bool selected) {
            selectionObject.SetActive(selected);
        }

        /// <summary>
        /// Dims the icon when the item cannot currently be used (e.g. grappling
        /// hook while grounded). Mirrors the shop "unaffordable" visual.
        /// </summary>
        public void SetEnabled(bool enabled) {
            var c = icon.color;
            c.a = enabled ? 1f : DisabledIconAlpha;
            icon.color = c;
        }

        /// <summary>
        /// Sets cooldown progress. 0 - cooldown in progress, 1 - cooldown complete.
        /// </summary>
        /// <param name="progress"></param>
        public void SetCooldownProgress(float progress) {
            // Take RoundScale into account to prevent visual glitches, when progress jumps up and down by 1 pixel
            // due to rounding.
            var remainingProgress = Mathf.Ceil(Mathf.Clamp01(1f - progress) * RoundScale) / RoundScale;

            if (
                showCooldownEndEffect
                && Mathf.Approximately(remainingProgress, 0f)
                && !Mathf.Approximately(prevCooldownProgress, 0f)
            ) {
                ShowCooldownEndEffect();
            }

            cooldownMask.fillAmount = remainingProgress;
            prevCooldownProgress = remainingProgress;
        }

        private void Update() {
            if (cooldownEndTimer <= 0f) {
                return;
            }

            cooldownEndTimer -= Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(cooldownEndTimer / CooldownEndEffectDuration);
            // Ease out (quadratic)
            var alpha = CooldownEndEffectStartAlpha * t * t;
            SetCooldownEndMaskAlpha(alpha);
        }

        private void ShowCooldownEndEffect() {
            cooldownEndTimer = CooldownEndEffectDuration;
            SetCooldownEndMaskAlpha(CooldownEndEffectStartAlpha);
            if (cooldownEndSound != null) {
                G.Audio.Play2D(cooldownEndSound);
            }
        }

        private void SetCooldownEndMaskAlpha(float alpha) {
            var c = cooldownEndMask.color;
            c.a = alpha;
            cooldownEndMask.color = c;
        }
    }
}
