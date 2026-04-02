using Game.Core.Models.Inventory;
using Game.Defs;
using Game.Features.Characters.Hero;
using Game.Features.Characters.Hero.ItemUse;
using UnityEngine;

namespace Game.Features.Characters.Parrot {
    /// <summary>
    /// Item use strategy that deploys a parrot companion.
    /// The parrot stays in inventory (Usable item) and is blocked from re-use while active.
    /// Cooldown after use is managed by <see cref="ItemUseService"/>.
    /// </summary>
    public class ParrotDeployStrategy : IItemUseStrategy {
        private readonly PlayerController controller;
        private readonly GameObject parrotPrefab;
        private ParrotController activeParrot;

        public ItemId ItemId => ItemIds.Parrot;

        public ParrotDeployStrategy(PlayerController controller, GameObject parrotPrefab) {
            this.controller = controller;
            this.parrotPrefab = parrotPrefab;
        }

        public bool CanUse() {
            return activeParrot == null
                   && parrotPrefab != null
                   && controller.State.InventoryModel.GetCount(ItemId) > 0;
        }

        public void Use() {
            var spawnPos = controller.transform.position;
            var go = Object.Instantiate(parrotPrefab, spawnPos, Quaternion.identity);
            activeParrot = go.GetComponent<ParrotController>();
            activeParrot.Deploy(ParrotMode.Follow);
            activeParrot.OnRecalled += OnParrotRecalled;
        }

        public void Update(float deltaTime) {
            // Clean up reference if parrot was destroyed externally
            if (activeParrot != null && activeParrot.gameObject == null) {
                activeParrot = null;
            }
        }

        private void OnParrotRecalled() {
            if (activeParrot == null) {
                return;
            }

            activeParrot.OnRecalled -= OnParrotRecalled;
            Object.Destroy(activeParrot.gameObject);
            activeParrot = null;
        }
    }
}
