using System;
using Core.Services;
using UnityEngine;

namespace Prefabs.Characters.PinkStar {
    public struct PinkyCommand {
        public readonly int XDirection;
        public readonly bool Attack;
        
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
