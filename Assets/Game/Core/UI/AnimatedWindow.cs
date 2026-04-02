using System;
using System.Collections;
using UnityEngine;

namespace Game.Core.UI {
    [RequireComponent(typeof(Animator))]
    public abstract class AnimatedWindow : MenuWindow {
        private static readonly int OnShow = Animator.StringToHash("onShow");
        private static readonly int OnHide = Animator.StringToHash("onHide");

        [SerializeField]
        private bool autoOpenOnCreation = false;

        private Animator anim;
        private Action afterClosedCallback;
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

        public override void Open(GameObject selected = null) {
            isClosing = false;
            afterClosedCallback = null;
            gameObject.SetActive(true);
            // Start each open from a deterministic hidden baseline.
            anim.Rebind();
            anim.Update(0f);
            anim.ResetTrigger(OnHide);
            anim.SetTrigger(OnShow);
            StartCoroutine(SelectFirstNextFrame(selected));
        }

        public override void Close(Action afterClosed = null) {
            if (!gameObject.activeSelf) {
                afterClosed?.Invoke();
                return;
            }

            isClosing = true;
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
    }
}
