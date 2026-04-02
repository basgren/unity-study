using System;
using Game.Core.Services.Tween.Components;
using UnityEngine;

namespace Game.Core.UI {
    /// <summary>
    /// A <see cref="MenuWindow"/> that uses <see cref="TweenGroup"/> presets for open/close transitions.
    /// By default fades a <see cref="CanvasGroup"/> alpha. Override <see cref="OnShowUpdate"/>
    /// and <see cref="OnHideUpdate"/> for custom animations.
    /// </summary>
    [RequireComponent(typeof(TweenGroup))]
    [RequireComponent(typeof(CanvasGroup))]
    public class TweenWindow : MenuWindow {
        private TweenGroup tweenGroup;
        private CanvasGroup canvasGroup;

        protected TweenGroup TweenGroup => tweenGroup;
        protected CanvasGroup CanvasGroup => canvasGroup;

        protected virtual void Awake() {
            tweenGroup = GetComponent<TweenGroup>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public override void Open(GameObject selected = null) {
            gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            tweenGroup.StopAll();
            tweenGroup.Play("show", OnShowUpdate);
            StartCoroutine(SelectFirstNextFrame(selected));
        }

        public override void Close(Action afterClosed = null) {
            if (!gameObject.activeSelf) {
                afterClosed?.Invoke();
                return;
            }

            tweenGroup.StopAll();
            tweenGroup.Play("hide", OnHideUpdate, () => {
                gameObject.SetActive(false);
                afterClosed?.Invoke();
            });
        }

        protected virtual void OnShowUpdate(float eased, float progress) {
            canvasGroup.alpha = eased;
        }

        protected virtual void OnHideUpdate(float eased, float progress) {
            canvasGroup.alpha = 1f - eased;
        }
    }
}
