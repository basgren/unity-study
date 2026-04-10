using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using Game.Defs;
using UnityEngine;

namespace Game.Features.Characters.Hero.ItemUse {
    /// <summary>
    /// Stub perk strategy for the protection mask.
    /// TODO: implement actual gameplay effect (damage reduction, hazard immunity, etc.).
    /// </summary>
    public class ProtectionMaskStrategy : IItemUseStrategy {
        public ItemId ItemId => ItemIds.ProtectionMask;

        public bool CanUse() {
            return G.Game.playerState.InventoryModel.GetCount(ItemId) > 0;
        }

        public void Use() {
            Debug.Log("ProtectionMask activated (stub).");
        }

        public void Update(float deltaTime) {
        }
    }
}
