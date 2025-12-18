using System.Collections.Generic;
using Components.Collisions;
using UnityEngine;
using Utils;

namespace Components.Abilities {
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(GroundCheckComponent))]
    public class DraggableBarrel : MonoBehaviour {
        [SerializeField]
        private float jointUpBreakForce = 2500f;

        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Collider { get; private set; }

        public float ReactiveForceOnBreak { get; private set; }
        public float ReactiveTorqueOnBreak { get; private set; }

        public bool IsGrounded => groundCheckComponent.IsGrounded;

        RigidbodyConstraints2D freeConstraints;
        RigidbodyConstraints2D lockedConstraints;
        
        // Additional capsule collider on top, height about 1.5 pixels just to make rounded
        // corners of barrel, so it will reduce cheance of collidint when dragging barrel on
        // top or a line of barrels (or sometimes player is stuck too when we have only
        // rectangular colliders).
        private CapsuleCollider2D smoothTopColider;
        private GroundCheckComponent groundCheckComponent;
        private BarrelHighlighter highlighter;
        private MultiRayCaster topRayCaster;
        private bool isDragged;
        
        // Joint between lower and upper barrels
        private FixedJoint2D barrelJoint;

        private void Awake() {
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<BoxCollider2D>();
            groundCheckComponent = GetComponent<GroundCheckComponent>();
            highlighter = GetComponent<BarrelHighlighter>();
            smoothTopColider = GetComponent<CapsuleCollider2D>();

            freeConstraints = RigidbodyConstraints2D.FreezeRotation;
            lockedConstraints = freeConstraints | RigidbodyConstraints2D.FreezePositionX;
            
            topRayCaster = new MultiRayCaster(Collider, groundCheckComponent.groundLayerMask)
                .WithDirection(Direction2D.Up)
                .WithRayCount(3);

            if (smoothTopColider != null) {
                topRayCaster
                    .ExcludeColliders(smoothTopColider)
                    .WithRayLength(1.5f * AllConst.PixelSize);
            }

            Body.constraints = lockedConstraints;
        }

        void FixedUpdate() {
            topRayCaster.Update();
        }

        public void SetDragged(bool dragged) {
            if (dragged == isDragged) {
                return;
            }
            
            isDragged = dragged;
            
            if (dragged) {
                Body.constraints = freeConstraints;
            } else {
                Body.constraints = lockedConstraints;
                Body.velocity = new Vector2(0f, Body.velocity.y);
            }
        }

        public void SetHighlighted(BarrelHighlightMode mode) {
            highlighter.SetHighlighted(mode);
        }

        public void ConnectToDraggable(DraggableBarrel otherDraggable) {
            Debug.Log("Creating joint between barrels");
            barrelJoint = gameObject.AddComponent<FixedJoint2D>();
            barrelJoint.connectedBody = otherDraggable.Body;
            barrelJoint.enableCollision = true;

            // For now this value is ok to keep stacked barrels together and to break it
            // if upper barrel runs into an obstacle, while lower one is still dragged. 
            barrelJoint.breakForce = jointUpBreakForce;

            ReactiveForceOnBreak = -1f;
            ReactiveTorqueOnBreak = -1f;
        }

        public void DisconnectFromDraggable() {
            if (barrelJoint) {
                Destroy(barrelJoint);
            }

            barrelJoint = null;
        }

        public List<T> GetDraggablesAbove<T>() where T : DraggableBarrel {
            return topRayCaster.GetHitComponents<T>();
        }

        private void OnJointBreak2D(Joint2D brokenJoint) {
            barrelJoint = null;

            // Just for debug. In case we change movement parameters, we might need to tune break forces,
            // and to do that we should know reaction force when the link with the top barrel is broken.
            // These values are be displayed in inspector for DraggableBarrel component.
            ReactiveForceOnBreak = brokenJoint.reactionForce.magnitude;
            ReactiveTorqueOnBreak = brokenJoint.reactionTorque;
        }

        private void OnDrawGizmosSelected() {
            topRayCaster?.DrawGizmos();
        }
    }
}
