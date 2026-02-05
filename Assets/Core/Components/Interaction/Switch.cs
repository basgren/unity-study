using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Core.Components.Interaction {
    public enum SwitchType {
        MultipleUse,
        SingleUse
    }
    
    [RequireComponent(typeof(SpriteRenderer))]
    public class Switch : InteractableBase {
        [SerializeField]
        private List<Switchable> switchables = new List<Switchable>();
        
        [SerializeField]
        private SwitchType switchType = SwitchType.MultipleUse;
        
        [SerializeField]
        private UnityEvent onSwitch;

        // TODO: [BG] Take care multiple activations, or activation when linked action isn't yet complete when there's a delay.
        [SerializeField]
        private float activationDelay;
        
        /// <summary>
        /// Disables interaction with this switch.
        /// </summary>
        [SerializeField]
        public bool isDisabled;

        private SpriteRenderer spriteRenderer;
        
        private Coroutine activationCoroutine;

        protected override void Awake() {
            base.Awake();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        protected override void DoInteract() {
            if (isDisabled) {
                return;
            }

            // TODO: [BG] Enhancements: don't allow activation while in progress.
            if (switchables != null) {
                onSwitch?.Invoke();

                if (activationDelay > 0) {
                    if (activationCoroutine != null) {
                        StopCoroutine(activationCoroutine);
                    }

                    activationCoroutine = StartCoroutine(DoToggleDelayed());
                } else {
                    DoToggle();
                }
                

                if (switchType == SwitchType.SingleUse) {
                    isDisabled = true;
                    IsHovered = false;
                }
            }
        }

        private void DoToggle() {
            foreach (var switchable in switchables) {
                switchable.Toggle();
            }
        }

        private IEnumerator DoToggleDelayed() {
            yield return new WaitForSeconds(activationDelay);
            DoToggle();
        } 

        protected override void OnHoveredChange(bool isHovered) {
            Debug.Log("Hovered change");
            // TODO: [BG] implement better highlighting. Add some notification above. like button to press.
            // Very simple highlight - just for now.
            spriteRenderer.color = isHovered && !isDisabled
                ? Color.yellow
                : Color.white;
        }

        private void OnDrawGizmos() {
            if (switchables != null) {
                Gizmos.color = Color.yellow;

                foreach (var switchable in switchables) {
                    Gizmos.DrawLine(transform.position, switchable.transform.position);                    
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            var isPrefab = !gameObject.scene.IsValid();
            
            if (Application.isPlaying || isPrefab) {
                return;
            }

            if (switchables.Count == 0) {
                Debug.LogWarning($"Switch component: no switchables are connected with '{gameObject.name}'", this);
            }
        }
#endif
    }
}
