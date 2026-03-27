using System;
using Game.Core.Bootstrap;
using Prefabs.Characters.PinkStar;
using UnityEngine;

namespace Game.Features.Characters.PinkStar {
    public struct PinkyCommand {
        public readonly int XDirection; // -1 left, 0 idle, +1 right
        public readonly bool Attack; // one-tick request to start attack
        
        public PinkyCommand(int xDirection, bool doAttack) {
            XDirection = xDirection;
            Attack = doAttack;
        }
    }
    
    public abstract class PinkyControlSource : BaseControlSource<PinkyCommand> {
    }

    public class PinkyInputControlSource : PinkyControlSource {
        private InputActions.PlayerActions playerActions;

        private PinkyCommand? currentCommand;
        
        private void Awake() {
            playerActions = G.Input.Player;
        }

        private void Update() {
            bool isAttack = playerActions.Attack.WasPressedThisFrame();
            if (isAttack) {
                Debug.Log(">>>> ATTACK");
            }

            int xDirection = Math.Sign(playerActions.Move.ReadValue<Vector2>().x);

            if (isAttack || xDirection != 0) {
                currentCommand = new PinkyCommand(xDirection, isAttack);
            } else {
                currentCommand = null;
            }
        }

        public override PinkyCommand? GetCommand() {
            return currentCommand;
        }
    }
}
