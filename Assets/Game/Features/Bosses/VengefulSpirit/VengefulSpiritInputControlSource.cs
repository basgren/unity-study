using System;
using Game.Core.Bootstrap;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit {
    /// <summary>
    /// Debug control source that drives the Vengeful Spirit from the shared player InputActions.
    ///
    /// Action mapping (reuses existing bindings on <c>G.Input.Player</c>):
    /// - Move       : fly (X/Y) — WASD + arrows
    /// - Interact   : melee attack (C key) — temporary stand-in for a dedicated boss "Attack" action
    /// - UseItem    : spawn shield (A key) — temporary stand-in for a dedicated boss cast action
    /// - UsePerk    : cast spectral swords (S key / RT) — temporary stand-in for a dedicated boss cast action
    /// - SwitchItem : teleport (Tab) — temporary stand-in for a dedicated boss teleport action
    ///
    /// The Interact/UseItem/UsePerk/SwitchItem choice is only to avoid modifying the shared
    /// InputActions asset during stage 1 prototyping. When the boss moves past debug input, add
    /// dedicated BossAttack / BossSpawnShield / BossCastSwords / BossTeleport actions and swap
    /// the references here.
    /// </summary>
    public class VengefulSpiritInputControlSource : VengefulSpiritControlSource {
        private InputActions.PlayerActions playerActions;
        private VengefulSpiritCommand? currentCommand;

        private void Awake() {
            playerActions = G.Input.Player;
        }

        private void Update() {
            Vector2 move = playerActions.Move.ReadValue<Vector2>();
            int x = Math.Sign(move.x);
            int y = Math.Sign(move.y);

            bool attack = playerActions.Interact.WasPressedThisFrame();
            bool spawnShield = playerActions.UseItem.WasPressedThisFrame();
            bool castSwords = playerActions.UsePerk.WasPressedThisFrame();
            bool teleport = playerActions.SwitchItem.WasPressedThisFrame();

            currentCommand = new VengefulSpiritCommand(x, y, attack, spawnShield, castSwords, teleport);
        }

        public override VengefulSpiritCommand? GetCommand() {
            return currentCommand;
        }
    }
}
