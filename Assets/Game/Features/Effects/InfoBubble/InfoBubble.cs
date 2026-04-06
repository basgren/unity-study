using System.Collections;
using Core.Components;
using Core.Components.Animation;
using Game.Core.Components.Animation;
using UnityEngine;

namespace Prefabs.Effects.InfoBubble {
    public enum InfoBubbleType {
        Exclamation,
        Question,
        Dead,
    }

    public class InfoBubble : MonoBehaviour {
        private MultiStateSpriteAnimator spriteAnim;
        private InfoBubbleType currentType;
        private bool isDisplayed;

        private void Awake() {
            spriteAnim = GetComponent<MultiStateSpriteAnimator>();
        }

        public void ShowBubble(InfoBubbleType type, float hideDelay) {
            StartCoroutine(ShowBubbleCoroutine(type, hideDelay));
        }

        private IEnumerator ShowBubbleCoroutine(InfoBubbleType type, float hideDelay) {
            PlayBubbleInClip(type);
            yield return new WaitUntil(() => isDisplayed);
            yield return new WaitForSeconds(hideDelay);
            PlayBubbleOutClip();
        }

        public void OnClipComplete() {
            if (!isDisplayed) {
                isDisplayed = true;                
            } else {
                Destroy(gameObject);                
            }
        }

        private void PlayBubbleInClip(InfoBubbleType type) {
            currentType = type;
            var clipName = GetClipName(type, false);

            spriteAnim.SetClip(clipName);
        }

        private void PlayBubbleOutClip() {
            var clipName = GetClipName(currentType, true);
            spriteAnim.SetClip(clipName);
        }

        private string GetClipName(InfoBubbleType type, bool isHiding) {
            switch (type) {
                case InfoBubbleType.Question:
                    return isHiding ? "QuestionOut" : "QuestionIn";
                
                case InfoBubbleType.Dead:
                    return isHiding ? "DeadOut" : "DeadIn";
                
                default:
                    return isHiding ? "ExclamationOut" : "ExclamationIn";
            }
        }
    }
}
