using System;
using Game.Core.Bootstrap;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.Services.Input {
    public class InputService : MonoBehaviour {
        // Binding group names must match the control schemes defined in
        // Assets/Game/System/InputActions.inputactions.
        private const string KeyboardMouseGroup = "Keyboard&Mouse";
        private const string GamepadGroup = "Gamepad";

        public static InputActions Actions { get; private set; }

        public InputActions InputActions => Actions;
        public InputActions.PlayerActions Player { get; private set; }
        public InputActions.UIActions UI { get; private set; }

        /// <summary>
        /// Binding group string for the most recently used control scheme.
        /// Pass to <see cref="InputAction.GetBindingDisplayString(int, string)"/>
        /// (via <see cref="InputBinding.MaskByGroup"/>) to render the right key label.
        /// Defaults to <c>Keyboard&amp;Mouse</c> until a real device is detected.
        /// </summary>
        public string CurrentSchemeBindingGroup { get; private set; } = KeyboardMouseGroup;

        /// <summary>
        /// Fires whenever <see cref="CurrentSchemeBindingGroup"/> changes (e.g. the
        /// player switched from keyboard to gamepad). HUD widgets that show binding
        /// labels subscribe to this to refresh their captions.
        /// </summary>
        public event Action OnSchemeChanged;

        private bool immediateQuit = false;

        private void Awake() {
            Actions = new InputActions();
            Player = Actions.Player;
            UI = Actions.UI;
        }

        private void OnEnable() {
            immediateQuit = G.Config.EscQuitsImmediately;
            Actions.Enable();

            InputSystem.onActionChange += OnActionChange;
        }

        private void OnDisable() {
            Actions.Disable();
            InputSystem.onActionChange -= OnActionChange;
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

        // Detect the last-used scheme by inspecting which device produced an action update.
        // We use InputSystem.onActionChange rather than InputUser.onChange because the
        // project does not formally pair devices with users.
        private void OnActionChange(object obj, InputActionChange change) {
            // ActionPerformed alone is enough — ActionStarted fires for the same press,
            // doubling work without adding any device-detection signal we don't already get.
            if (change != InputActionChange.ActionPerformed) {
                return;
            }

            if (obj is not InputAction action) {
                return;
            }

            var control = action.activeControl;
            if (control == null) {
                return;
            }

            string newGroup = ResolveBindingGroupForDevice(control.device);
            if (newGroup == null || newGroup == CurrentSchemeBindingGroup) {
                return;
            }

            CurrentSchemeBindingGroup = newGroup;
            OnSchemeChanged?.Invoke();
        }

        private static string ResolveBindingGroupForDevice(InputDevice device) {
            if (device is Gamepad) {
                return GamepadGroup;
            }

            if (device is Keyboard || device is Mouse) {
                return KeyboardMouseGroup;
            }

            return null;
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
