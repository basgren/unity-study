using System;
using System.Collections.Generic;
using Components;
using Components.Interaction;
using Core.Collectables;
using Core.Services;
using PixelCrew.Collectibles;
using UnityEngine;
using Utils;

namespace PixelCrew.Player {
    public static class HeroAnimationKeys {
        public static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        public static readonly int IsRunning = Animator.StringToHash("isRunning");
        public static readonly int VelocityY = Animator.StringToHash("velocityY");
        public static readonly int OnJump = Animator.StringToHash("onJump");
        public static readonly int OnHit = Animator.StringToHash("onHit");
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : MonoBehaviour, ICollectableReceiver<CollectableId> {
        private const string DustPositionObjectName = "DustSpawnPoint";
        private const float MinFallHeightForDustEffect = 2.8f;

        [SerializeField]
        private float moveSpeed = 5f; // Run speed

        /// <summary>
        /// Layers, which collisions should be checked to detect if player stands on ground.
        /// </summary>
        [SerializeField]
        private LayerMask groundLayer;

        [Header("Jump")]
        [SerializeField]
        private float jumpSpeed = 15f;

        [SerializeField]
        private float jumpSustainTime = 0.2f;

        /// <summary>
        /// Time in seconds during which the player can jump after falling down.
        /// </summary>
        [SerializeField]
        private float coyoteJumpTime = 0.1f;

        [Header("Effects")]
        [SerializeField]
        private GameObject runDustPrefab;

        [SerializeField]
        private GameObject jumpDustPrefab;

        [SerializeField]
        private GameObject groundDustPrefab;

        public InputActions.PlayerActions Actions { get; private set; }
        public bool IsGrounded { get; private set; }

        private InputActions input;
        private Rigidbody2D rb;
        private BoxCollider2D myCollider;
        private Damageable damageable;
        private Animator animator;
        private LootDropper lootDropper;

        private GroundChecker groundChecker;
        private CeilingChecker ceilingChecker;
        private float coyoteTimer;
        private bool isJumped;
        private float jumpSustainTimer;

        // TODO: move it to some global game state object.
        private int coinsValue;

        // List of all interactable components which are currently available for interaction.
        private readonly List<InteractableBase> availableInteractables = new List<InteractableBase>();
        private InteractableBase closestInteractable;

        private Transform dustSpawnPoint;

        private readonly float jumpInputBufferTime = 0.1f;
        private float jumpInputBufferTimer;
        private bool isJumpPressedBuffer;

        private bool dragStarted;

        private void Awake() {
            input = new InputActions();
            Actions = input.Player;

            rb = GetComponent<Rigidbody2D>();
            myCollider = GetComponent<BoxCollider2D>();
            animator = GetComponent<Animator>();
            groundChecker = new GroundChecker(myCollider, groundLayer);
            ceilingChecker = new CeilingChecker(myCollider, groundLayer);
            damageable = GetComponent<Damageable>();
            lootDropper = GetComponent<LootDropper>();

            dustSpawnPoint = transform.Find(DustPositionObjectName);
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
            //   * inputs are checked before `Update` event (while it may be configured to be checked
            //     in `FixedUpdate`, but usually `Update` is called more frequently)
            //   * `FixedUpdate` is usually called with lower frequency that `Update`, so there may be
            //      input loss, if we check input on `FixedUpdate`: https://docs.unity3d.com/6000.2/Documentation/Manual/fixed-updates.html
            //   * physics, including velocity and forces, should be applied in `FixedUpdate`
            //   So now for simplicity we'll do everything in `Update`, as in `FixedUpdate` input loss
            //   occurs for jump, for example, as it uses `WasPerformedThisFrame` action method.
            //   But better solution should be considered for precise platforming. For example,
            //   Corgi engine doesn't use physics for player and updates player coords manually (applying
            //   gravity, etc) to be more responsive and have more control over movements (while I'm not
            //   sure about physics for other draggable objects).

            // We won't use InputSystem events, as order of their invocation is not guaranteed, but
            // in case we want to check button combinations, it's easier to check them manually.
            // Using events is better for UI controls.
            CheckGround();

            CheckJump();
            CheckHorizontalMovement();
            CheckInteraction();

            // Update animator at the end, when player state is updated.
            UpdateAnimator();
        }

        public void SetDragMode(bool dragging, float speedMultiplier) {
            // TODO: [BG] we'll need this flag later for animations
            if (dragging) {
                dragStarted = true;
                // currentMoveSpeed = baseMoveSpeed * speedMultiplier;
            } else {
                dragStarted = false;
                // currentMoveSpeed = baseMoveSpeed;
            }
        }

        private void CheckGround() {
            groundChecker.Update();
            ceilingChecker.Update();
            IsGrounded = groundChecker.IsGrounded;

            if (groundChecker.IsLeftGroundThisFrame) {
                coyoteTimer = coyoteJumpTime;
            }

            if (groundChecker.IsLandedThisFrame) {
                if (groundChecker.FallHeight > MinFallHeightForDustEffect) {
                    SpawnLandingDust();
                }
            }
        }

        private void SpawnLandingDust() {
            G.Spawner.SpawnVfx(groundDustPrefab, dustSpawnPoint.position);
        }

        private void CheckHorizontalMovement() {
            Vector2 dir = Actions.Move.ReadValue<Vector2>().normalized;

            var horzSpeed = Math.Sign(dir.x) * moveSpeed;
            rb.velocity = new Vector2(horzSpeed, rb.velocity.y);

            if (horzSpeed > 0) {
                transform.localScale = new Vector3(1, transform.localScale.y, transform.localScale.z);
            } else if (horzSpeed < 0) {
                transform.localScale = new Vector3(-1, transform.localScale.y, transform.localScale.z);
            }
        }

        private void CheckJump() {
            isJumped = false;

            var isJumpPressed = Actions.Jump.WasPerformedThisFrame();
            var isJumpReleased = Actions.Jump.WasReleasedThisFrame();

            if (isJumpPressed) {
                jumpInputBufferTimer = jumpInputBufferTime;
                isJumpPressedBuffer = true;
            } else {
                jumpInputBufferTimer -= Time.deltaTime;
                if (jumpInputBufferTimer <= 0) {
                    isJumpPressedBuffer = false;
                }
            }

            if (isJumpReleased || ceilingChecker.HasCollision) {
                jumpSustainTimer = 0;
            }

            var isSustainingJump = jumpSustainTimer > 0;

            if (isJumpPressedBuffer && CanJump()) {
                Jump();

                jumpSustainTimer = jumpSustainTime;
                isJumped = true;
                ConsumeJumpBuffer();
                G.Spawner.SpawnVfx(jumpDustPrefab, dustSpawnPoint.position);
            } else if (isSustainingJump) {
                Jump();
            }

            if (coyoteTimer > 0) {
                coyoteTimer -= Time.deltaTime;
            }

            if (jumpSustainTimer > 0) {
                jumpSustainTimer -= Time.deltaTime;
            }
        }

        private void Jump() {
            rb.velocity = new Vector2(rb.velocity.x, jumpSpeed);
        }

        private void ConsumeJumpBuffer() {
            jumpInputBufferTimer = 0;
            isJumpPressedBuffer = false;
        }

        private bool CanJump() {
            return IsGrounded || coyoteTimer > 0;
        }

        private bool IsRunning() {
            return Math.Abs(rb.velocity.x) > 0.01f;
        }

        public void OnCollected(CollectableId itemId, float value) {
            switch (itemId) {
                case CollectableId.Coin:
                    AddCoins((int)value);
                    break;

                case CollectableId.Health:
                    Debug.Log($"Player: Collected {value} health");
                    damageable.AddHealth(value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(itemId), itemId, null);
            }
        }

        private void AddCoins(int amount = 1) {
            coinsValue += amount;
            Debug.Log($"Added coin. Current value: {coinsValue}");
        }

        private void RemoveCoins(int amount = 1) {
            coinsValue = Math.Max(0, coinsValue - amount);
        }

        #region Animator

        private void UpdateAnimator() {
            animator.SetBool(HeroAnimationKeys.IsGrounded, IsGrounded);
            animator.SetBool(HeroAnimationKeys.IsRunning, IsRunning());

            if (isJumped) {
                // We're jumping on trigger, not using velocityY comparison, as we may have moving platforms,
                // in this case Y speed may be > 0, while the player is still on the ground.
                animator.SetTrigger(HeroAnimationKeys.OnJump);
            }

            var velocityY = rb.velocity.y;

            // Adjustments to compensate for floating point precision errors and physics jitter.
            if (Math.Abs(velocityY) < 0.001f) {
                velocityY = 0;
            }

            animator.SetFloat(HeroAnimationKeys.VelocityY, velocityY);

            if (damageable.IsHitThisFrame) {
                animator.SetTrigger(HeroAnimationKeys.OnHit);
            }
        }

        public void SpawnRunDust() {
            if (Math.Abs(rb.velocity.x) > 1f) {
                var instance = G.Spawner.SpawnVfx(runDustPrefab, dustSpawnPoint.position);

                // Make sure the spawned object is directed in the same direction as target object.
                instance.transform.localScale = dustSpawnPoint.lossyScale;
            }
        }

        #endregion

        #region Interaction

        private void CheckInteraction() {
            if (!Actions.Interact.WasPerformedThisFrame() || availableInteractables.Count == 0) {
                return;
            }

            var closest = GetClosestInteractable();
            if (closest != null) {
                closest.Interact();
            }
        }

        private void OnTriggerEnter2D(Collider2D other) {
            if (!other.TryGetComponent<InteractableBase>(out var interactable)) {
                return;
            }

            availableInteractables.Add(interactable);

            UpdateClosestInteractable(true);
        }

        private void OnTriggerExit2D(Collider2D other) {
            if (other.TryGetComponent<InteractableBase>(out var interactable)) {
                availableInteractables.Remove(interactable);
                interactable.IsHovered = false;

                // If there were several interactables, we should update the closest one after we remove
                // the one for which we exited trigger.
                UpdateClosestInteractable(true);
            }
        }

        private InteractableBase GetClosestInteractable() {
            return Geometry.FindClosest(availableInteractables, transform.position);
        }

        private void UpdateClosestInteractable(bool isHovered) {
            var closest = GetClosestInteractable();
            if (closest != null) {
                closest.IsHovered = isHovered;
            }
        }

        #endregion

        public void DropCoins() {
            var count = Math.Min(5, coinsValue);
            lootDropper.DropLoot(count);
            RemoveCoins(count);
        }

        // ------------------- GIZMOS -------------------

        private void OnDrawGizmosSelected() {
            GroundCheckerUtils.DrawGroundCheckerGizmos(groundChecker);
        }
    }
}
