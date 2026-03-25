using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.UI {
    [RequireComponent(typeof(Animator))]
    public abstract class AnimatedWindow : MonoBehaviour {
        private static readonly int OnShow = Animator.StringToHash("onShow");
        private static readonly int OnHide = Animator.StringToHash("onHide");
        
        // If specified, the first button will be automatically focused after the window is shown.
        [SerializeField]
        private GameObject firstSelected;

        [SerializeField]
        private bool closeOnCancel = true;

        [SerializeField]
        private bool pausesGame = true;

        [SerializeField]
        private bool autoOpenOnCreation = false;

        public bool CloseOnCancel => closeOnCancel;
        public bool PausesGame => pausesGame;
        public GameObject FirstSelected => firstSelected;
        
        private Animator anim;
        private Action afterClosedCallback;
        private GameObject selectedOverride;
        private bool isClosing;

        private void Awake() {
            anim = GetComponent<Animator>();
            // Ensure Animator starts from controller defaults and Hidden state is sampled.
            anim.Rebind();
            anim.Update(0f);
        }

        private void Start() {
            if (autoOpenOnCreation) {
                StartCoroutine(AutoOpenNextFrame());
            }
        }

        private IEnumerator AutoOpenNextFrame() {
            yield return null;
            Open();
        }

        public virtual void Open(GameObject selected = null) {
            isClosing = false;
            afterClosedCallback = null;
            selectedOverride = selected;
            gameObject.SetActive(true);
            // Start each open from a deterministic hidden baseline.
            anim.Rebind();
            anim.Update(0f);
            anim.ResetTrigger(OnHide);
            anim.SetTrigger(OnShow);
            StartCoroutine(SelectFirstNextFrame());
        }

        public virtual void Close(Action afterClosed = null) {
            if (!gameObject.activeSelf) {
                afterClosed?.Invoke();
                return;
            }

            isClosing = true;
            selectedOverride = null;
            afterClosedCallback = afterClosed;
            anim.ResetTrigger(OnShow);
            anim.SetTrigger(OnHide);
        }

        // Invoked when the window was completely hidden from the screen (animation ends).
        protected virtual void OnAfterClosed() {
            if (!isClosing) {
                return;
            }

            isClosing = false;
            var callback = afterClosedCallback;
            afterClosedCallback = null;
            callback?.Invoke();

            if (this != null) {
                gameObject.SetActive(false);
            }
        }

        protected virtual IEnumerator SelectFirstNextFrame() {
            yield return null;

            if (EventSystem.current == null) {
                yield break;
            }

            var target = selectedOverride != null ? selectedOverride : firstSelected;
            selectedOverride = null;

            if (target == null) {
                yield break;
            }

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
