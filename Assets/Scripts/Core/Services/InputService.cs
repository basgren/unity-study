using System;
using UnityEditor;
using UnityEngine;

namespace Core.Services {
    public class InputService : MonoBehaviour {
        public static InputActions Actions { get; private set; }

        public InputActions.PlayerActions Player { get; private set; }
        public InputActions.UIActions UI { get; private set; }

        private void Awake() {
            Actions = new InputActions();
            Player = Actions.Player;
            UI = Actions.UI;
        }
        
        private void OnEnable() {
            Actions.Enable();
        }

        private void OnDisable() {
            Actions.Disable();
        }

        private void Update() {
            // TODO: [BG] Move quit handler to proper place when menu system is ready.
            if (Player.Quit.WasPressedThisFrame()) {
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
