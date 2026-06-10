using System.Collections;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Models.Inventory;
using UnityEngine;

namespace Game.Core.Components.Interaction {
    /// <summary>
    /// Gates the <see cref="InteractableBase"/> on this object (e.g. a Door) behind an inventory item.
    /// <list type="bullet">
    /// <item>No key: the interaction is suppressed and <see cref="lockedSound"/> plays.</item>
    /// <item>Has key: the key is consumed, <see cref="unlockSound"/> plays, and after
    /// <see cref="openDelayAfterUnlock"/> the interaction runs.</item>
    /// <item>Once unlocked, it stays unlocked and opens normally with no key/sound. The unlocked flag is
    /// persisted by a sibling <c>KeyLockStateSaver</c> so a key-gated door stays open for the rest of the
    /// game (and is not re-locked after the single-use key has been consumed).</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(InteractableBase))]
    public class KeyLock : MonoBehaviour, IInteractionGate {
        [Header("Key Lock")]
        [SerializeField]
        private ItemId requiredKey;

        [Tooltip("Played when the player tries to open the lock without the required key.")]
        [SerializeField]
        private AudioCue lockedSound;

        [Tooltip("Played when the key is used (key turning), before the interaction runs.")]
        [SerializeField]
        private AudioCue unlockSound;

        [Tooltip("Seconds between the unlock sound starting and the interaction running.")]
        [SerializeField]
        private float openDelayAfterUnlock = 0.6f;

        private InteractableBase interactable;
        private bool unlockInProgress;

        /// <summary>
        /// Whether this lock has been opened. Persisted by <c>KeyLockStateSaver</c>; once true the lock
        /// never requires the key again. Settable so the saver can restore it on scene load.
        /// </summary>
        public bool IsUnlocked { get; set; }

        private void Awake() {
            interactable = GetComponent<InteractableBase>();
        }

        public InteractionGateResult OnInteractRequested() {
            // Ignore re-presses while the unlock sequence is already running.
            if (unlockInProgress) {
                return InteractionGateResult.Deferred;
            }

            // Already opened, or no key configured: behave like a normal interactable.
            if (IsUnlocked || requiredKey.IsEmpty) {
                return InteractionGateResult.Allow;
            }

            var state = G.Game != null ? G.Game.playerState : null;
            if (state == null) {
                return InteractionGateResult.Allow;
            }

            if (state.InventoryModel.GetCount(requiredKey) <= 0) {
                PlaySound(lockedSound);
                return InteractionGateResult.Reject;
            }

            // Player has the key: consume it and mark unlocked now (so the persisted state and inventory are
            // consistent before the scene can unload), then open after the unlock sound has had time to play.
            state.InventoryModel.Remove(requiredKey, 1);
            IsUnlocked = true;
            StartCoroutine(UnlockRoutine());
            return InteractionGateResult.Deferred;
        }

        private IEnumerator UnlockRoutine() {
            unlockInProgress = true;
            PlaySound(unlockSound);

            if (openDelayAfterUnlock > 0f) {
                yield return new WaitForSeconds(openDelayAfterUnlock);
            }

            unlockInProgress = false;
            interactable.Activate();
        }

        private void PlaySound(AudioCue cue) {
            if (cue != null) {
                G.Audio.PlayAt(cue, transform.position);
            }
        }
    }
}
