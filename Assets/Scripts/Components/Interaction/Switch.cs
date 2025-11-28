using UnityEngine;
using UnityEngine.Events;

namespace Components.Interaction {
    [RequireComponent(typeof(SpriteRenderer))]
    public class Switch : InteractableBase {
        [SerializeField]
        private Switchable switchable;
        
        [SerializeField]
        private UnityEvent onSwitch;

        private SpriteRenderer spriteRenderer;

        protected override void Awake() {
            base.Awake();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void Interact() {
            // TODO: [BG] Enhancements: don't allow activation while in progress.
            if (switchable != null) {
                switchable.Toggle();
                onSwitch?.Invoke();
            }
        }

        protected override void OnHoveredChange(bool isHovered) {
            // TODO: [BG] implement better highlighting. Add some notification above. like button to press.
            // Very simple highlight - just for now.
            spriteRenderer.color = isHovered ? Color.yellow : Color.white;
        }

        private void OnDrawGizmos() {
            if (switchable != null) {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, switchable.transform.position);                
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            var isPrefab = !gameObject.scene.IsValid();
            
            if (Application.isPlaying || isPrefab) {
                return;
            }

            if (switchable == null) {
                Debug.LogWarning($"Switch component: switchable is null on '{gameObject.name}'", this);
            }
        }
#endif
    }
}
