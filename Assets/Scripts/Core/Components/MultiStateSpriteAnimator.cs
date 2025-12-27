using System;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Core.Components {
    [RequireComponent(typeof(SpriteRenderer))]
    public class MultiStateSpriteAnimator : MonoBehaviour {
        [SerializeField]
        private int frameRate = 10;

        [SerializeField]
        private bool playOnStart = true;

        [SerializeField]
        private bool randomStartFrame;

        [SerializeField]
        private bool destroyOnComplete;

        [SerializeField]
        private StateAnimationClip[] clips;

        /// <summary>
        /// OnComplete is invoked when all frames have been played. ONLY for non-looped animations.
        /// </summary>
        [SerializeField]
        public UnityEvent onComplete;

        private SpriteRenderer spriteRenderer;

        private StateAnimationClip currentClip;
        private int currentFrameIndex;
        private float timer;
        private float frameDuration;

        private int startFrameIndex;
        private bool isPlaying;
        
        private Sprite[] CurrentSprites => currentClip?.Sprites ?? Array.Empty<Sprite>();

        void Awake() {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable() {
            frameDuration = 1f / frameRate;

            if (clips.Length > 0) {
                SetClip(0);
            }

            if (randomStartFrame) {
                startFrameIndex = Random.Range(0, CurrentSprites.Length);
            }

            SetSprite(startFrameIndex);
            isPlaying = playOnStart;
        }

        void Update() {
            if (CurrentSprites.Length == 0 || !isPlaying) {
                return;
            }

            timer += Time.deltaTime;

            if (timer < frameDuration) {
                return;
            }

            timer -= frameDuration;

            var nextSpriteIndex = (currentFrameIndex + 1) % CurrentSprites.Length;

            if (!currentClip.Loop && nextSpriteIndex == 0) {
                enabled = currentClip.AllowNextClip;
                currentClip.OnComplete?.Invoke();
                onComplete.Invoke();

                if (destroyOnComplete) {
                    Destroy(gameObject);
                }

                if (currentClip.AllowNextClip) {
                    var currentClipIndex = Array.IndexOf(clips, currentClip);
                    SetClip((currentClipIndex + 1) % clips.Length);
                }
                
                isPlaying = false;
                enabled = false;

                return;
            }

            SetSprite(nextSpriteIndex);
        }
        
        public void SetClip(string clipName) {
            for (var index = 0; index < clips.Length; index++) {
                if (clips[index].Name == clipName) {
                    SetClip(index);
                    return;
                }
            }

            Debug.LogError($"Clip {clipName} not found in {gameObject.name}");
            enabled = false;
        }
        
        private void SetClip(int clipIndex) {
            if (clipIndex >= clips.Length || clipIndex < 0) {
                throw new IndexOutOfRangeException($"Clip index {clipIndex} is out of range for {gameObject.name}");
            }

            enabled = true;
            isPlaying = true;
            currentClip = clips[clipIndex];
            SetSprite(0);
        }
        
        private void SetSprite(int i) {
            if (i >= CurrentSprites.Length) {
                return;
            }

            currentFrameIndex = i;
            spriteRenderer.sprite = CurrentSprites[i];
        }
    }

    [Serializable]
    public class StateAnimationClip {
        [SerializeField]
        private string name;

        [SerializeField]
        private Sprite[] sprites;

        [SerializeField]
        private bool loop;

        [SerializeField]
        private bool allowNextClip;

        [SerializeField]
        private UnityEvent onComplete;
        
        public string Name => name;
        public Sprite[] Sprites => sprites;
        public bool Loop => loop;
        public bool AllowNextClip => allowNextClip;
        public UnityEvent OnComplete => onComplete;
    }
}
