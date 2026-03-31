using Game.Core.Models.Inventory;
using Game.Defs;
using Game.Features.Characters.Hero;
using Game.Features.Characters.Hero.ItemUse;
using UnityEngine;

namespace Game.Features.Characters.Parrot {
    /// <summary>
    /// Item use strategy that deploys a parrot companion from inventory.
    /// When the parrot finishes its follow cycle (attack or timeout), it recalls itself
    /// and this strategy returns it to the player's inventory.
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
                   && controller.State.Inventory.GetCount(ItemId) > 0;
        }

        public void Use() {
            controller.State.Inventory.Remove(ItemId, 1);

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

            // Return parrot to inventory
            controller.State.Inventory.Add(ItemIds.Parrot, 1);

            Object.Destroy(activeParrot.gameObject);
            activeParrot = null;
        }
    }
}
