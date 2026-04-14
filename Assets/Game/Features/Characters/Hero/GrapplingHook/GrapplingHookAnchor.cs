using UnityEngine;

namespace Game.Features.Characters.Hero.GrapplingHook {
    /// <summary>
    /// Marker component placed on objects that can serve as grappling hook targets
    /// (rings, hooks, pegs, etc.). The object must be on the HookAnchors physics layer
    /// with a trigger collider for detection.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GrapplingHookAnchor : MonoBehaviour {
        [SerializeField]
        private Sprite anchorSprite;

        [SerializeField]
        private Sprite anchorActiveSprite;
    }
}
