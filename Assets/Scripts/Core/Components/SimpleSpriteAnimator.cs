using UnityEngine;
using UnityEngine.Events;

namespace Core.Components {
    [RequireComponent(typeof(SpriteRenderer))]
    public class SimpleSpriteAnimator : MonoBehaviour {
        [SerializeField]
        private int frameRate = 10;

        [SerializeField]
        private bool loop = true;
        
        [SerializeField]
        private bool destroyOnComplete = false;

        [SerializeField]
        private Sprite[] sprites;

        /// <summary>
        /// OnComplete is invoked when all frames have been played. ONLY for non-looped animations.
        /// </summary>
        [SerializeField]
        public UnityEvent onComplete;

        private SpriteRenderer spriteRenderer;

        private int currentFrameIndex = 0;
        private float timer = 0;
        private float frameDuration;

        void Awake() {
            spriteRenderer = GetComponent<SpriteRenderer>();
            SetSprite(0);
        }

        void OnEnable() {
            frameDuration = 1f / frameRate;
        }

        void Update() {
            if (sprites.Length == 0) {
                return;
            }

            timer += Time.deltaTime;

            if (timer < frameDuration) {
                return;
            }

            timer -= frameDuration;

            var nextSpriteIndex = (currentFrameIndex + 1) % sprites.Length;

            if (!loop && nextSpriteIndex == 0) {
                Debug.Log("Animation finished");
                onComplete.Invoke();
                enabled = false;

                if (destroyOnComplete) {
                    Destroy(gameObject);
                }

                return;
            }

            SetSprite(nextSpriteIndex);
        }

        private void SetSprite(int i) {
            if (i >= sprites.Length) {
                return;
            }

            currentFrameIndex = i;
            spriteRenderer.sprite = sprites[i];
        }
    }
}
