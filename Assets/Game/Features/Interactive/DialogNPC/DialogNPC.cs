using System.Collections;
using Game.Core.Bootstrap;
using Game.Core.Components.Interaction;
using Game.Features.Hero;
using UnityEngine;

namespace Game.Features.Interactive.DialogNPC {
    /// <summary>
    /// An interactable that starts a dialog conversation when the player interacts with it.
    /// When a <see cref="talkPoint"/> is assigned, the player first walks to it (along X only)
    /// and turns to face the NPC before the dialog opens.
    /// </summary>
    public class DialogNPC : InteractableBase {
        // Hero reaches the talk point within roughly one frame of movement; this early-out
        // distance avoids a needless scripted walk when the hero is already standing on it.
        private const float ReachThreshold = 0.1f;

        [SerializeField]
        private string dialogId;

        [SerializeField]
        [Tooltip("Optional. When set, interacting first disables player controls and walks the hero " +
                 "(along X only) to this point, then turns the hero to face the NPC, and only then " +
                 "opens the dialog. Leave empty to open the dialog immediately at the current position.")]
        private Transform talkPoint;

        private Coroutine approachRoutine;

        protected override void DoInteract() {
            if (string.IsNullOrEmpty(dialogId)) {
                Debug.LogWarning($"DialogNPC '{name}': dialogId is not set.");
                return;
            }

            if (talkPoint == null) {
                G.Dialog.StartDialog(dialogId);
                return;
            }

            if (approachRoutine != null) {
                return;
            }

            approachRoutine = StartCoroutine(ApproachThenTalk());
        }

        private IEnumerator ApproachThenTalk() {
            var hero = G.Hero.Controller;

            // No hero registered (shouldn't happen in normal play) — fall back to opening immediately.
            if (hero == null) {
                G.Dialog.StartDialog(dialogId);
                approachRoutine = null;
                yield break;
            }

            hero.SetControlsEnabled(false);

            yield return WalkHeroToTalkPoint(hero);

            // Turn toward the NPC before talking.
            var towardNpc = transform.position.x - hero.transform.position.x;
            hero.SetFacing(towardNpc >= 0f ? 1 : -1);

            G.Dialog.StartDialog(dialogId);

            // If the dialog failed to open (e.g. missing definition), the menu never took over
            // input handling, so restore controls instead of leaving the hero frozen.
            if (!G.Menu.IsAnyWindowOpen) {
                hero.SetControlsEnabled(true);
            }

            approachRoutine = null;
        }

        private IEnumerator WalkHeroToTalkPoint(PlayerController hero) {
            var targetX = talkPoint.position.x;

            if (Mathf.Abs(targetX - hero.transform.position.x) <= ReachThreshold) {
                yield break;
            }

            var dirSign = targetX > hero.transform.position.x ? 1 : -1;
            hero.BeginScriptedWalk(dirSign);

            // Walk until the hero reaches the target X or overshoots it (sign of the remaining
            // distance flips), since per-frame steps may skip past the threshold band.
            while (true) {
                var remaining = targetX - hero.transform.position.x;
                var reached = Mathf.Abs(remaining) <= ReachThreshold;
                var overshot = (remaining > 0f ? 1 : -1) != dirSign;

                if (reached || overshot) {
                    break;
                }

                yield return null;
            }

            hero.EndScriptedWalk();
        }
    }
}
