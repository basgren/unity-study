using System;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Core.Components {
    [RequireComponent(typeof(SpriteRenderer))]
    public class SimpleSpriteAnimator : MonoBehaviour {
        [SerializeField]
        private int frameRate = 10;

        [SerializeField]
        private bool loop = true;

        [SerializeField]
        private bool randomStartFrame;

        [SerializeField]
        private bool destroyOnComplete;

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

        private int startFrameIndex = 0;

        void Awake() {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable() {
            frameDuration = 1f / frameRate;

            if (randomStartFrame) {
                startFrameIndex = Random.Range(0, sprites.Length);
            }

            SetSprite(startFrameIndex);
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
