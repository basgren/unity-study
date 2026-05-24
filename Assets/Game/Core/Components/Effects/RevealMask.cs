using Game.Core.Services.SceneState.Savers;
using UnityEngine;

namespace Game.Core.Components.Effects {
    /// <summary>
    /// Hides part of a scene behind an opaque mask until the player walks into it.
    /// On first trigger contact the SpriteRenderer fades to fully transparent over
    /// <c>fadeOutTime</c> seconds, then the GameObject is permanently removed via
    /// <see cref="DestructionStateSaver"/> so the reveal persists across reloads.
    ///
    /// Setup expectations:
    /// - The SpriteRenderer must be placed on a sorting layer/order that draws in
    ///   front of the tiles it should occlude (configured on the renderer itself).
    /// - The collider should be sized to match the visible mask area. Any Collider2D
    ///   works; it is forced into trigger mode at runtime so the player passes
    ///   through it as the reveal begins.
    /// - For solid-color masks of arbitrary size, use a sprite with Sliced/Tiled
    ///   draw mode and set the SpriteRenderer color.
    /// - Add a <see cref="DestructionStateSaver"/> (with its required StateRoot)
    ///   so revealed masks stay revealed when the scene is reloaded.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class RevealMask : MonoBehaviour {
        private const string PlayerTag = "Player";

        [SerializeField]
        [Tooltip("Seconds for the mask to fully fade out after the player touches it.")]
        private float fadeOutTime = 0.5f;

        private SpriteRenderer spriteRenderer;
        private Collider2D triggerCollider;
        private DestructionStateSaver destructionSaver;
        private bool isRevealing;
        private float fadeElapsed;

        private void Awake() {
            spriteRenderer = GetComponent<SpriteRenderer>();
            triggerCollider = GetComponent<Collider2D>();
            destructionSaver = GetComponent<DestructionStateSaver>();

            // Force trigger mode so the player can step into the masked area
            // instead of bouncing off the mask collider.
            triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (isRevealing) {
                return;
            }

            if (!other.CompareTag(PlayerTag)) {
                return;
            }

            isRevealing = true;
            fadeElapsed = 0f;
        }

        private void Update() {
            if (!isRevealing) {
                return;
            }

            fadeElapsed += Time.deltaTime;
            float t = fadeOutTime > 0f ? Mathf.Clamp01(fadeElapsed / fadeOutTime) : 1f;

            Color color = spriteRenderer.color;
            color.a = 1f - t;
            spriteRenderer.color = color;

            if (t >= 1f) {
                if (destructionSaver != null) {
                    destructionSaver.MarkDestroyedAndDestroy();
                } else {
                    Destroy(gameObject);
                }
            }
        }
    }
}
