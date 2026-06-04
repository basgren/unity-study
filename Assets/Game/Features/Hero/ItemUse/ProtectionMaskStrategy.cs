using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Defs;
using Game.Features.Hero;
using UnityEngine;

namespace Game.Features.Characters.Hero.ItemUse {
    /// <summary>
    /// Activates the protection mask perk: spawns a shield aura on the hero,
    /// grants invulnerability for a configurable duration, and pulsates the
    /// aura near the end to warn the player before it expires.
    /// </summary>
    public class ProtectionMaskStrategy : IItemUseStrategy {
        private readonly PlayerController controller;
        private readonly GameObject auraPrefab;
        private readonly Transform spawnPoint;
        private readonly float duration;
        private readonly float pulsateTime;

        private GameObject activeAura;
        private SpriteRenderer auraRenderer;
        private float remainingTime;
        private bool isActive;

        public ItemId ItemId => ItemIds.ProtectionMask;

        public ProtectionMaskStrategy(
            PlayerController controller,
            GameObject auraPrefab,
            Transform spawnPoint,
            float duration,
            float pulsateTime
        ) {
            this.controller = controller;
            this.auraPrefab = auraPrefab;
            this.spawnPoint = spawnPoint;
            this.duration = duration;
            this.pulsateTime = pulsateTime;
        }

        public bool CanUse() {
            return !isActive
                   && auraPrefab != null
                   && controller.State.InventoryModel.GetCount(ItemId) > 0;
        }

        public void Use() {
            isActive = true;
            remainingTime = duration;

            controller.SetCanTakeDamage(false);

            activeAura = Object.Instantiate(auraPrefab, spawnPoint.position, Quaternion.identity, spawnPoint);
            activeAura.transform.localPosition = Vector3.zero;
            auraRenderer = activeAura.GetComponent<SpriteRenderer>();
        }

        public void Update(float deltaTime) {
            if (!isActive) {
                return;
            }

            if (activeAura == null) {
                Deactivate();
                return;
            }

            remainingTime -= deltaTime;

            if (remainingTime <= 0f) {
                Deactivate();
                return;
            }

            if (remainingTime <= pulsateTime && auraRenderer != null) {
                float pulse = Mathf.Abs(Mathf.Sin(remainingTime * 8f));
                var color = auraRenderer.color;
                color.a = Mathf.Lerp(0.3f, 1f, pulse);
                auraRenderer.color = color;
            }
        }

        private void Deactivate() {
            isActive = false;
            controller.SetCanTakeDamage(true);

            if (activeAura != null) {
                Object.Destroy(activeAura);
                activeAura = null;
            }

            auraRenderer = null;
        }
    }
}
