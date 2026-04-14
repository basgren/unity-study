using UnityEngine;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Marker component placed on objects that can serve as grappling hook targets
    /// (rings, hooks, pegs, etc.). The object must be on the HookAnchors physics layer
    /// with a trigger collider for detection.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
    public class GrapplingHookAnchor : MonoBehaviour {
        [SerializeField]
        private Sprite anchorActiveSprite;

        private SpriteRenderer spriteRenderer;
        private Sprite defaultSprite;

        private void Awake() {
            spriteRenderer = GetComponent<SpriteRenderer>();
            defaultSprite = spriteRenderer.sprite;
        }

        /// <summary>
        /// Swaps the renderer between the default sprite and the "active" sprite
        /// set when this anchor is the current grappling hook target. Safe to call
        /// with no active sprite configured — it will stay on the default.
        /// </summary>
        public void SetHighlighted(bool highlighted) {
            if (spriteRenderer == null) {
                return;
            }

            spriteRenderer.sprite = highlighted && anchorActiveSprite != null
                ? anchorActiveSprite
                : defaultSprite;
        }
    }
}
