using Game.Core.Bootstrap;
using Game.Core.Components.Interaction;
using UnityEngine;

namespace Game.Features.Interactive.DialogNPC {
    /// <summary>
    /// An interactable that starts a dialog conversation when the player interacts with it.
    /// </summary>
    public class DialogNPC : InteractableBase {
        [SerializeField]
        private string dialogId;

        protected override void DoInteract() {
            if (string.IsNullOrEmpty(dialogId)) {
                Debug.LogWarning($"DialogNPC '{name}': dialogId is not set.");
                return;
            }

            G.Dialog.StartDialog(dialogId);
        }
    }
}
