using System;
using System.Collections;
using System.Collections.Generic;
using Components;
using Components.Interaction;
using Core.Collectables;
using Core.Components;
using Core.Services;
using Game;
using PixelCrew.Collectibles;
using PixelCrew.Controllers;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

namespace PixelCrew.Player {
    public static class HeroAnimationKeys {
        public static readonly int IsGrounded = Animator.StringToHash("isGrounded");
        public static readonly int IsRunning = Animator.StringToHash("isRunning");
        public static readonly int IsDead = Animator.StringToHash("isDead");
        public static readonly int VelocityY = Animator.StringToHash("velocityY");
        public static readonly int OnJump = Animator.StringToHash("onJump");
        public static readonly int OnHit = Animator.StringToHash("onHit");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
        public static readonly int OnAttack = Animator.StringToHash("onAttack");
    }
    
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : BaseCharacterController, ICollectableReceiver<CollectableId> {
        private const string DustPositionObjectName = "DustSpawnPoint";
        private const float MinFallHeightForDustEffect = 2.8f;
        private const float WaitBeforeRespawn = 1.5f;
        private const float WaitBeforeRestart = 2.5f;

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
        
        [Header("Attack")]
        [SerializeField]
        private GameObject swordAttackArea;
        
        [SerializeField]
        private GameObject attack1EffectPrefab;
        
        [SerializeField]
        private RuntimeAnimatorController armedAnimator;
        
        [SerializeField]
        private RuntimeAnimatorController unarmedAnimator; 

        public InputActions.PlayerActions Actions { get; private set; }
        public bool IsGrounded { get; private set; }
        
        /// <summary>
        /// Returns the height the player fell from. This should be checked only when
        /// <c>IsGrounded</c> or <c>IsLandedThisFrame</c> is true.
        /// </summary>
        private float FallHeight => fallUpperPosY - fallLowerPosY;

        private BoxCollider2D myCollider;
        private Damageable damageable;
        private LootDropper lootDropper;

        private MultiRayCaster groundChecker;
        private MultiRayCaster ceilingChecker;
        private SafePointTracker safePointTracker;
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

        // TODO: [BG] Refactor - it's getting too many flags to manage. At the same time it would be nice to
        //  keep animation state update in one place. Probably we could use FSM to store player's state and
        //  sync animation with it.
        // Flags to process animation state changes
        private bool isDiedThisFrame;
        private bool isDead;

        private float fallUpperPosY;
        private float fallLowerPosY;

        private bool isAttacking;
        private bool isAttackAnimationInitiated;
        private readonly float attackCooldownTime = 0.3f;
        private float attackCooldownTimer;
        private bool isArmed;
        
        private PlayerState state;

        protected override void Awake() {
            base.Awake();
            Actions = G.Input.Player;
            state = G.Game.PlayerState;
            
            myCollider = GetComponent<BoxCollider2D>();
            groundChecker = MultiRayCaster.CreateGroundChecker(myCollider, groundLayer)
                // Remove adjustment to prevent double jump when jumping along the wall
                .WithAdjustment(0f); 
            
            ceilingChecker = new MultiRayCaster(myCollider, groundLayer)
                .WithDirection(Direction2D.Up)
                .WithIgnoreOneWayPlatforms();

            CloseSwordDamageWindow();
            
            safePointTracker = new SafePointTracker();
            damageable = GetComponent<Damageable>();
            lootDropper = GetComponent<LootDropper>();

            dustSpawnPoint = transform.Find(DustPositionObjectName);
            UpdateAnimatorController();
            ResetFallHeight();

            InitFromState(state);
        }

        private void InitFromState(PlayerState playerState) {
            damageable.maxHealth = playerState.GetMaxHealth();
            damageable.SetHealth(playerState.currentHealth);
            coinsValue = playerState.coinsValue;
            SetArmed(playerState.isArmed);
            Debug.Log($"Initialized from state: {playerState}");
        }

        private void UpdateAnimatorController() {
            animator.runtimeAnimatorController = isArmed ? armedAnimator : unarmedAnimator;
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
            
            CheckSafePoint();

            CheckJump();
            CheckHorizontalMovement();
            CheckInteraction();
            CheckAttack();
        }

        public void TeleportTo(Vector3 targetPosition) {
            transform.position = targetPosition;
            // Reset fall height to prevent fall dust to appear when player is teleported to a lower position.
            ResetFallHeight();
        }

        private void ResetFallHeight() {
            fallUpperPosY = transform.position.y;
            fallLowerPosY = transform.position.y;
        }
        
        #region Attack
        
        /// <summary>
        /// Currently attack is implemente in a vary simple way. Key is pressed - we activate damage window,
        /// activate child object with collider and Damager components (DamageArea), it will hit everything once and
        /// when animation is finished we deactivate damage window and deactivate DamageArea.
        /// </summary>
        private void CheckAttack() {
            if (attackCooldownTimer > 0) {
                attackCooldownTimer -= Time.deltaTime;
            }
            
            // Things to consider: this method is very simple and relies on the fact that time between animation
            // events of open and close damage window is longer than fixedDeltaTime, so at least one
            // physics check iteration will be complete before DamageArea is deactivated. In case if damage
            // window is shorter than fixedDeltaTime, more robust solution should be considered with
            // activating/deactivating attack in FixedUpdate.
            if (Actions.Attack.WasPerformedThisFrame() && CanAttack()) {
                isAttacking = true; // will be used to prevent double attacks.
                isAttackAnimationInitiated = true; // used just to trigger animation event.
                
                // Spawn effect as hero's child object, so even if player moves, it will move with player.
                // But in this case we should prevent changing player direction until animation is finished.
                G.Spawner.SpawnVfx(attack1EffectPrefab, swordAttackArea.transform.position, gameObject.transform);
            }
        }

        private bool CanAttack() {
            return isArmed && !isAttacking && IsGrounded && attackCooldownTimer <= 0;
        }

        /// <summary>
        /// Should be called from animation event to enable damage window (when actial hit starts).
        /// </summary>
        private void OpenSwordDamageWindow() {
            // This will activate animation, and animation will call event which will close damage window
            // and deactivate sword damage area. 
            swordAttackArea.SetActive(true);
        }
        
        public void CancelAttack() {
            CloseSwordDamageWindow();
            FinishAttack();
        }
        
        /// <summary>
        /// Should be called from animation event to disable damage window (when hit ends).
        /// </summary>
        private void CloseSwordDamageWindow() {
            swordAttackArea.SetActive(false);
        }

        private void FinishAttack() {
            if (!isAttacking) {
                return;
            }

            // Should be called at the very end, when sword swing effect is finished, so we can
            // finish attack and allow player turning (we don't allow turning while attack is in progress).
            isAttacking = false;
            attackCooldownTimer = attackCooldownTime;
        }
        
        #endregion

        public void SetDragMode(bool dragging, float speedMultiplier) {
            // TODO: [BG] we'll need this flag later for animations
            // if (dragging) {
            //     dragStarted = true;
            //     // currentMoveSpeed = baseMoveSpeed * speedMultiplier;
            // } else {
            //     dragStarted = false;
            //     // currentMoveSpeed = baseMoveSpeed;
            // }
        }

        private void CheckGround() {
            groundChecker.Update();
            ceilingChecker.Update();
            
            UpdateFallHeight();
            
            IsGrounded = groundChecker.HasCollision;

            if (groundChecker.HasExitedCollisionThisFrame) {
                coyoteTimer = coyoteJumpTime;
            }

            if (groundChecker.HasEnteredCollisionThisFrame) {
                if (FallHeight > MinFallHeightForDustEffect) {
                    SpawnLandingDust();
                }
            }
        }
        
        private void UpdateFallHeight() {
            float y = myCollider.bounds.min.y;

            if (groundChecker.HasExitedCollisionThisFrame) {
                fallUpperPosY = y;
                fallLowerPosY = y;
            } else if (groundChecker.HasEnteredCollisionThisFrame) {
                fallLowerPosY = y;
            } else if (!IsGrounded) {
                fallUpperPosY = Mathf.Max(fallUpperPosY, y);
            }
        }

        private void CheckSafePoint() {
            // TODO: [BG] Make sure that player is not standing on barrels or other platforms
            //   that are not completely stable (for example, moving platforms, disappearing platforms,
            //   or one way platforms).
            if (!isDead) {
                safePointTracker.Update(groundChecker.IsAllCollide, transform.position, rb.velocity, Time.deltaTime);                
            }
        }

        private void SpawnLandingDust() {
            G.Spawner.SpawnVfx(groundDustPrefab, dustSpawnPoint.position);
        }

        private void CheckHorizontalMovement() {
            Vector2 dir = Actions.Move.ReadValue<Vector2>().normalized;

            var vx = rb.velocity.x;

            // Check `isAttacking` flag to prevent player from changing direction while attack effect is played,
            // otherwise the effect will turn together with player.
            if (!isAttacking) {
                if (vx > 0) {
                    transform.localScale = new Vector3(1, transform.localScale.y, transform.localScale.z);
                } else if (vx < 0) {
                    transform.localScale = new Vector3(-1, transform.localScale.y, transform.localScale.z);
                }    
            }
        }

        #region Jump
        
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
            // Do not allow to jump if we're doing ground attack.
            return (IsGrounded || coyoteTimer > 0) && !isAttacking;
        }

        #endregion
        
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
                
                case CollectableId.Sword:
                    SetArmed(true);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(itemId), itemId, null);
            }
        }

        private void SetArmed(bool newIsArmed) {
            if (newIsArmed == isArmed) {
                return;
            }

            isArmed = newIsArmed;
            UpdateState();
            UpdateAnimatorController();
        }

        private void AddCoins(int amount = 1) {
            coinsValue += amount;
            Debug.Log($"Added coin. Current value: {coinsValue}");
            UpdateState();
        }

        private void RemoveCoins(int amount = 1) {
            coinsValue = Math.Max(0, coinsValue - amount);
            UpdateState();
        }

        #region Animator

        private void UpdateState() {
            state.currentHealth = damageable.Health;
            state.coinsValue = coinsValue;
            state.isArmed = isArmed;
        }
        
        protected override void UpdateAnimator() {
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
            
            if (isDiedThisFrame) {
                animator.SetTrigger(HeroAnimationKeys.OnDeath);
                // TODO: [BG] Actually should be reset somewhere else, not in this method, but not it's just for POC 
                isDiedThisFrame = false;
            }

            if (isAttackAnimationInitiated) {
                animator.SetTrigger(HeroAnimationKeys.OnAttack);
                isAttackAnimationInitiated = false;
            }
            
            animator.SetBool(HeroAnimationKeys.IsDead, isDead);
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
        
        public void OnAfterHit(Damager damager) {
            Debug.Log($"Player: Hit by {damager.Type}. Health: {damageable.Health}");
            DropCoins();
            UpdateState();

            if (damageable.IsDead) {
                ShowHitAndRestartScene();
                return;
            }
            
            if (damager.Type == DamagerType.RespawnOnContact) {
                ShowHitAndRespawnAtSafePoint();
            }
        }

        public void OnAfterDeath(Damager damager) {
            
        }

        private void ShowHitAndRestartScene() {
            Actions.Disable();
            isDead = true;
            isDiedThisFrame = true;
            damageable.IgnoreDamage = true;
            StartCoroutine(WaitAndRestart(WaitBeforeRestart));
        }

        private void ShowHitAndRespawnAtSafePoint() {
            Actions.Disable();
            isDead = true;
            isDiedThisFrame = true;
            damageable.IgnoreDamage = true;
            StartCoroutine(WaitAndRespawn(WaitBeforeRespawn));
        }

        private IEnumerator WaitAndRestart(float seconds) {
            yield return new WaitForSeconds(seconds);
            // TODO: [BG] Leave for refactor - move to some service like game manager.
            //   player should not manage own death or even respawn. I should throw some message
            //   and game manager should decide what to do.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            isDead = false;
            damageable.IgnoreDamage = false;
            Actions.Enable();
        }
        
        private IEnumerator WaitAndRespawn(float seconds) {
            yield return new WaitForSeconds(seconds);
            RespawnAtSafePoint();
        }
        
        private void RespawnAtSafePoint() {
            isDead = false;
            transform.position = safePointTracker.LastSafePosition;
            damageable.IgnoreDamage = false;
            Actions.Enable();
        }

        private void DropCoins() {
            var count = Math.Min(5, coinsValue);
            lootDropper.DropLoot(count);
            RemoveCoins(count);
        }

        // ------------------- GIZMOS -------------------

        private void OnDrawGizmosSelected() {
            groundChecker?.DrawGizmos();
            ceilingChecker?.DrawGizmos();

            if (safePointTracker != null && safePointTracker.HasSafePosition) {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(safePointTracker.LastSafePosition, 0.1f);
            }
        }
    }
}
