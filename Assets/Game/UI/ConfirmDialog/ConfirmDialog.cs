using System;
using Game.Core.Bootstrap;
using Game.Core.UI;
using TMPro;
using UnityEngine;

namespace Game.UI.ConfirmDialog {
    /// <summary>
    /// Reusable yes/no confirmation window. Configure it with a message and an
    /// onConfirm callback before/just after opening. Cancel dismisses the dialog and
    /// returns to the previous window. Confirm hands control to the callback, which owns
    /// any follow-up navigation (e.g. closing menus and loading a scene) — the dialog does
    /// NOT close itself on confirm, because that would re-open the previous window and race
    /// with the callback's own menu transitions.
    /// </summary>
    public class ConfirmDialog : AnimatedWindow {
        [SerializeField]
        private TMP_Text messageLabel;

        private Action onConfirm;

        /// <summary>
        /// Sets the prompt text and the action to run if the player confirms.
        /// </summary>
        public void Configure(string message, Action onConfirm) {
            this.onConfirm = onConfirm;
            if (messageLabel != null) {
                messageLabel.text = message;
            }
        }

        public void OnConfirmClick() {
            var callback = onConfirm;
            onConfirm = null;
            // The callback owns navigation. For New Game it runs CloseAll(load scene), which
            // tears down this dialog and the menu underneath; closing here first would re-open
            // that menu and block CloseAll (it bails while a close transition is running).
            callback?.Invoke();
        }

        public void OnCancelClick() {
            onConfirm = null;
            G.Menu.CloseTopWindow();
        }
    }
}
