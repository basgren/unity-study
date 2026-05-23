using System;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Game.Core.Components.Animation {
    [Serializable]
    public class NewFrameEvent : UnityEvent<MultiStateSpriteAnimator> {
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public class MultiStateSpriteAnimator : MonoBehaviour {
        [Tooltip("Playback speed in frames per second. Applies to every clip in this animator.")]
        [SerializeField]
        private int frameRate = 10;

        [Tooltip("If enabled, the first clip starts playing automatically when the component becomes enabled.")]
        [SerializeField]
        private bool playOnStart = true;

        [Tooltip("If enabled, the starting clip begins at a random frame instead of frame 0.")]
        [SerializeField]
        private bool randomStartFrame;

        [Tooltip("If enabled, the GameObject is destroyed when a non-looping clip finishes playing.")]
        [SerializeField]
        private bool destroyOnComplete;

        [Tooltip("Animation clips this animator can switch between. Reference a clip by its Name via SetClip(name).")]
        [SerializeField]
        private StateAnimationClip[] clips;

        [Tooltip("Invoked when a non-looping clip finishes playing (after its last frame). Not fired for looping clips.")]
        [SerializeField]
        private UnityEvent onComplete;

        [Tooltip("Invoked every time the animator advances to a new frame of the current clip.")]
        [SerializeField]
        private NewFrameEvent onNewFrame;

        public StateAnimationClip CurrentClip => clips.Length > 0 ? clips[currentClipIndex] : null;
        public int CurrentFrameIndex => currentFrameIndex;

        private SpriteRenderer spriteRenderer;

        private int currentClipIndex;
        private int currentFrameIndex;
        private float timer;
        private float frameDuration;

        private int startFrameIndex;
        private bool isPlaying;

        private Sprite[] CurrentSprites => CurrentClip?.Sprites ?? Array.Empty<Sprite>();

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
            var clip = CurrentClip;

            if (!clip.Loop && nextSpriteIndex == 0) {
                enabled = clip.AllowNextClip;
                clip.OnComplete?.Invoke();
                onComplete.Invoke();

                if (destroyOnComplete) {
                    Destroy(gameObject);
                }

                if (clip.AllowNextClip) {
                    SetClip((currentClipIndex + 1) % clips.Length);
                } else {
                    isPlaying = false;
                    enabled = false;
                }

                return;
            }

            SetSprite(nextSpriteIndex);
            onNewFrame?.Invoke(this);
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
            currentClipIndex = clipIndex;
            // Reset timer so the new clip's first frame holds for the full frame duration,
            // otherwise leftover accumulated time from the previous clip can skip past it.
            timer = 0f;
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
