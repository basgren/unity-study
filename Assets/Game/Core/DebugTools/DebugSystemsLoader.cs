using System.Collections.Generic;
using Game.Configs;
using Game.Core.Bootstrap;
using Game.Defs;
using UnityEngine;

namespace Game.Core.DebugTools {
    /// <summary>
    /// Applies optional debug-only systems at startup based on <see cref="DebugSystemsConfig"/>
    /// toggles in <see cref="MainConfig"/>. When any system is active a single warning is logged
    /// listing them, so debug tooling is never silently left enabled in a build.
    /// </summary>
    public static class DebugSystemsLoader {
        public static void Apply(MainConfig config) {
            if (config == null || config.DebugSystems == null) {
                return;
            }

            var active = new List<string>();

            if (config.DebugSystems.EnableDebugInventory && ApplyDebugInventory()) {
                active.Add("Debug Inventory");
            }

            if (config.DebugSystems.EnableDebugStats && ApplyDebugStats()) {
                active.Add("Debug Stats");
            }

            if (config.DebugSystems.EnableDebugFlags && ApplyDebugFlags()) {
                active.Add("Debug Flags");
            }

            if (active.Count > 0) {
                Debug.LogWarning(
                    "[DebugSystems] ACTIVE debug systems (disable before shipping): " +
                    string.Join(", ", active));
            }
        }

        private static bool ApplyDebugInventory() {
            var debugInventory = Resources.Load<DebugInventoryConfig>(DebugInventoryConfig.ResourcePath);
            if (debugInventory == null) {
                Debug.LogError(
                    "[DebugSystems] Debug Inventory is enabled but no asset was found at " +
                    "Resources/" + DebugInventoryConfig.ResourcePath + ".");
                return false;
            }

            var playerState = G.Game != null ? G.Game.playerState : null;
            var inventory = playerState != null ? playerState.InventoryModel : null;
            if (inventory == null) {
                Debug.LogError("[DebugSystems] Cannot seed debug inventory: player state is not initialized.");
                return false;
            }

            foreach (var entry in debugInventory.InitialItems) {
                if (entry.itemId.IsEmpty || entry.count <= 0) {
                    continue;
                }

                inventory.Add(entry.itemId, entry.count);
            }

            // Debug seeding runs during bootstrap, before the hero subscribes to inventory changes,
            // so the normal "first sword becomes the equipped weapon" rule
            // (PlayerController.OnInventoryChanged) never fires for seeded swords. Reproduce it here:
            // if swords were seeded and the hero is not armed yet, arm the hero and consume one sword
            // as the equipped weapon. Any remaining swords stay in the inventory as throwables.
            if (!playerState.IsArmed && inventory.GetCount(ItemIds.Sword) > 0) {
                playerState.IsArmed = true;
                inventory.Remove(ItemIds.Sword, 1);
            }

            return true;
        }

        private static bool ApplyDebugStats() {
            var debugStats = Resources.Load<DebugStatsConfig>(DebugStatsConfig.ResourcePath);
            if (debugStats == null) {
                Debug.LogError(
                    "[DebugSystems] Debug Stats is enabled but no asset was found at " +
                    "Resources/" + DebugStatsConfig.ResourcePath + ".");
                return false;
            }

            var playerState = G.Game != null ? G.Game.playerState : null;
            if (playerState == null) {
                Debug.LogError("[DebugSystems] Cannot seed debug stats: player state is not initialized.");
                return false;
            }

            foreach (var entry in debugStats.InitialStats) {
                if (entry.level <= 0) {
                    continue;
                }

                playerState.SetStatLevel(entry.statId, entry.level);
            }

            // Seeded levels can raise the max health; this runs at bootstrap (before the hero reads
            // its starting health), so top current health up to the new max for a clean debug start.
            playerState.currentHealth = playerState.GetMaxHealth();

            return true;
        }

        private static bool ApplyDebugFlags() {
            var debugFlags = Resources.Load<DebugFlagConfig>(DebugFlagConfig.ResourcePath);
            if (debugFlags == null) {
                Debug.LogError(
                    "[DebugSystems] Debug Flags is enabled but no asset was found at " +
                    "Resources/" + DebugFlagConfig.ResourcePath + ".");
                return false;
            }

            var playerState = G.Game != null ? G.Game.playerState : null;
            if (playerState == null) {
                Debug.LogError("[DebugSystems] Cannot set debug flags: player state is not initialized.");
                return false;
            }

            foreach (var flag in debugFlags.InitialFlags) {
                if (string.IsNullOrWhiteSpace(flag)) {
                    continue;
                }

                playerState.SetFlag(flag);
            }

            return true;
        }
    }
}
