using UnityEngine;
using UnityEngine.Events;

namespace Components.Interaction {
    [RequireComponent(typeof(Collider2D))]
    public abstract class InteractableBase : MonoBehaviour {
        /// <summary>
        /// Collider used to detect interaction. This may be separate colliders from the
        /// one used for collision detection. This collider is also forced to be a trigger.
        /// </summary>
        [Header("Interactable")]
        [SerializeField]
        private Collider2D interactionCollider;

        [SerializeField]
        private UnityEvent onInteract;

        private bool isHovered;
        
        /// <summary>
        /// When player can interact with this object, this property is set to true by player's controller,
        /// as ability to interact may depend on different factors. 
        /// </summary>
        public bool IsHovered {
            get => isHovered;
            set {
                if (isHovered != value) {
                    isHovered = value;
                    OnHoveredChange(isHovered);                    
                }
            }
        }

        public void Interact() {
            onInteract?.Invoke();
            DoInteract();
        }

        /// <summary>
        /// Here interaction behavior should be implemented in descendant classes if needed.
        /// </summary>
        protected virtual void DoInteract() {
            // Do nothing. Override in descendant classes to perform some action.
        }

        protected virtual void Awake() {
            interactionCollider.isTrigger = true;
        }
        
        protected virtual void OnHoveredChange(bool isHovered) {
            // Here we can perform addtional action like display interaction hint or something, if
            // object is hovered.
        }
    }
}
