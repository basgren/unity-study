using System;
using UnityEngine;
using UnityEngine.Android;
using Utils;

namespace PixelCrew.Player {
    public class PlayerController : MonoBehaviour {
        [SerializeField]
        private float speed = 2f; // Run speed

        [SerializeField]
        private float jumpSpeed = 15f;
        
        [SerializeField]
        private LayerMask groundLayer;

        public InputActions.PlayerActions Actions { get; private set; }
        public bool IsGrounded { get; private set; }

        private InputActions input;
        private Rigidbody2D rigidBody;
        private BoxCollider2D boxCollider;
        private GroundChecker groundChecker;
        
        // TODO: move it to some global game state object.
        // TODO: rework interaction with coins. Currently we have to set which method to call in editor, which
        //   doesn't allow to provide extra parameters. While implementing something like CollectibleComponent
        //   with required collider, which provides collectible ID will allow to pass it to player and player
        //   could decide what to do with it and update game state accordingly.
        private int coins = 0;

        public void AddCoin() {
            coins++;
            Debug.Log($"Added coin. Current coins: {coins}");
        }
        
        private void Awake() {
            input = new InputActions();
            Actions = input.Player;
            
            rigidBody = GetComponent<Rigidbody2D>();
            boxCollider = GetComponent<BoxCollider2D>();
            groundChecker = new GroundChecker(boxCollider, groundLayer);
        }

        private void OnEnable() {
            Actions.Enable();
        }

        private void OnDisable() {
            Actions.Disable();
        }

        private void OnDestroy() {
            input.Dispose();
        }

        void Update() {
            // TODO: investigate proper solution for reading input and reacting on them. Main points:
            // * inputs are checked before `Update` event (while it may be configured to be checked
            //   in `FixedUpdate`, but usually `Update` is called more frequently)
            // * `FixedUpdate` is usually called with lower frequency that `Update`, so there may be
            //    input loss, if we check input on `FixedUpdate`: https://docs.unity3d.com/6000.2/Documentation/Manual/fixed-updates.html
            // * physics, including velocity and forces, should be applied in `FixedUpdate`
            // So now for simplicity we'll do everything in `Update`, as in `FixedUpdate` input loss
            // occurs for jump, for example, as it uses `WasPerformedThisFrame` action method.
            // But better solution should be considered for precise platforming. For example,
            // Corgi engine doesn't use physics for player and updates player coords manually (applying
            // gravity, etc) to be more responsive and have more control over movements (while I'm not
            // sure about physics for other draggable objects).
            
            // We won't use InputSystem events, as order of their invocation is not guaranteed, but
            // in case we want to check button combinations, it's easier to check them manually.
            // Using events are better for UI controls.
            CheckGround();

            CheckJump();
            CheckHorizontalMovement();
        }

        private void CheckGround() {
            groundChecker.Update();
            IsGrounded = groundChecker.IsGrounded;
        }

        private void CheckHorizontalMovement() {
            Vector2 dir = Actions.Move.ReadValue<Vector2>().normalized;

            var horzSpeed = Math.Sign(dir.x) * speed;
            rigidBody.velocity = new Vector2(horzSpeed, rigidBody.velocity.y);
        }
        
        private void CheckJump() {
            // TODO: implement input buffering for jump
            var isJumpPressed = Actions.Jump.WasPerformedThisFrame();

            if (IsGrounded && isJumpPressed) {
                // rigidBody.AddForce(Vector2.up * jumpForce,  ForceMode2D.Impulse);
                rigidBody.velocity = new Vector2(rigidBody.velocity.x, jumpSpeed);
            }
        }
        
        // ------------------- GIZMOS -------------------

        private void OnDrawGizmosSelected() {
            if (groundChecker == null) {
                return;
            }

            for (var i = 0; i < groundChecker.rayCount; i++) {
                Gizmos.color = groundChecker.HasRayCollision(i)
                    ? Color.green
                    : Color.red;

                Vector2 rayOrigin = groundChecker.RayOrigins[i];
                Gizmos.DrawLine(rayOrigin, rayOrigin + groundChecker.RayLength * groundChecker.RayDirection);
            }
        }
    }
}
