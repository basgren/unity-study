using System;
using Game.Core.Bootstrap;
using UnityEditor;
using UnityEngine;

namespace Game.Core.Services.Input {
    public class InputService : MonoBehaviour {
        public static InputActions Actions { get; private set; }

        public InputActions InputActions => Actions;
        public InputActions.PlayerActions Player { get; private set; }
        public InputActions.UIActions UI { get; private set; }

        private bool immediateQuit = false;
        
        private void Awake() {
            Actions = new InputActions();
            Player = Actions.Player;
            UI = Actions.UI;
        }
        
        private void OnEnable() {
            immediateQuit = G.Config.EscQuitsImmediately;
            Actions.Enable();
        }

        private void OnDisable() {
            Actions.Disable();
        }

        private void Update() {
            // TODO: [BG] Move quit handler to proper place when menu system is ready.
            if (immediateQuit && Player.Pause.WasPressedThisFrame()) {
                QuitGame();
            }
        }
        
        private void OnDestroy() {
            Actions.Dispose();
        }

        private void QuitGame() {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
