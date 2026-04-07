using System;
using System.Collections;
using Game.Core.Services.Scene;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.UI {
    /// <summary>
    /// Abstract base for any window managed by <see cref="MenuManager"/>.
    /// Subclasses decide how the open/close transition is performed (Animator, tween, etc.).
    /// </summary>
    public abstract class MenuWindow : MonoBehaviour {
        [SerializeField]
        private GameObject firstSelected;

        [SerializeField]
        private bool closeOnCancel = true;

        [SerializeField]
        private bool pausesGame = true;

        public bool CloseOnCancel => closeOnCancel;
        public bool PausesGame => pausesGame;
        public GameObject FirstSelected => firstSelected;

        /// <summary>
        /// Opens the window with a visual transition.
        /// </summary>
        /// <param name="selected">Optional UI element to focus instead of <see cref="FirstSelected"/>.</param>
        public abstract void Open(GameObject selected = null);

        /// <summary>
        /// Closes the window with a visual transition.
        /// The callback must be invoked after the transition finishes.
        /// </summary>
        public abstract void Close(Action afterClosed = null);

        protected IEnumerator SelectFirstNextFrame(GameObject selected) {
            yield return null;

            if (EventSystem.current == null) {
                yield break;
            }

            var target = selected != null ? selected : firstSelected;

            if (target == null) {
                yield break;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
