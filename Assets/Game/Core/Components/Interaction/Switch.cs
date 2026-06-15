using System.Collections;
using System.Collections.Generic;
using Game.Core.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Core.Components.Interaction {
    public enum SwitchType {
        MultipleUse,
        SingleUse
    }
    
    [RequireComponent(typeof(SpriteRenderer))]
    public class Switch : InteractableBase {
        [SerializeField]
        private List<SwitchableBase> switchables = new ();
        
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

        public override bool CanInteract() {
            return !isDisabled;
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
                    // Immediate visual reset; the resolver will also drop us as a candidate
                    // next frame (CanInteract becomes false), but we don't want a 1-frame
                    // yellow flash on the now-disabled sprite.
                    SetHighlighted(false);
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
            // TODO: [BG] implement better highlighting. Add some notification above. like button to press.
            // Very simple highlight - just for now.
            spriteRenderer.color = isHovered && !isDisabled
                ? Color.yellow
                : Color.white;
        }

        private static readonly Color ConnectorColor = new (1f, 0.92156863f, 0.015686275f, 0.25f);
        private void OnDrawGizmos() {
            if (switchables != null) {
                Gizmos.color = ConnectorColor;
                var pos = Geometry.GetColliderCenter(gameObject);

                foreach (var switchable in switchables) {
                    // A referenced switchable may have been destroyed at runtime
                    // (e.g. a one-shot trigger); Unity's overloaded == reports it
                    // as null, so skip it before dereferencing gameObject.
                    if (switchable == null) {
                        continue;
                    }

                    var targetPos = Geometry.GetColliderCenter(switchable.gameObject);
                    Gizmos.DrawLine(pos, targetPos);
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
