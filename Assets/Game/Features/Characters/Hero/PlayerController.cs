using System;
using System.Collections;
using System.Collections.Generic;
using Core.Audio;
using Core.Components.Interaction;
using Core.Utils;
using Game.Controllers;
using Game.Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Collectables;
using Game.Core.Components.Damage;
using Game.Core.Components.GameObjects;
using Game.Defs;
using Game.Models;
using Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Characters.Hero {
    public abstract class HeroAnimKeys : BaseCharacterAnimKeys {
        public static readonly int IsDead = Animator.StringToHash("isDead");
        public static readonly int OnJump = Animator.StringToHash("onJump");
        public static readonly int OnHit = Animator.StringToHash("onHit");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
        public static readonly int OnAttack = Animator.StringToHash("onAttack");
        public static readonly int OnAttack2 = Animator.StringToHash("onAttack2");
        public static readonly int OnAttack3 = Animator.StringToHash("onAttack3");
        public static readonly int OnThrowSword = Animator.StringToHash("onThrowSword");
    }

    public enum HeroAttackType {
        Slash1,
        Slash2,
        Pierce,
    } 
    
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : BaseCharacterController, ICollectableReceiver<ItemId> {
        private const string DustPositionObjectName = "DustSpawnPoint";
        private const string SwordThrowPointObjectName = "SwordThrowPoint";
        private const float MinFallHeightForDustEffect = 2.8f;
        private const float WaitBeforeRespawn = 1.5f;
        private const float WaitBeforeRestart = 2.5f;

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
        
        [SerializeField]
        private AudioCue deadGroundedSound;

        [Header("Attack")]
        [SerializeField]
        private GameObject swordAttackArea;

        [SerializeField]
        private RuntimeAnimatorController armedAnimator;

        [SerializeField]
        private RuntimeAnimatorController unarmedAnimator;

        public InputActions.PlayerActions Actions { get; private set; }
        public Damageable Damageable => damageable;

        private BoxCollider2D myCollider;
        private Damageable damageable;
        private LootDropper lootDropper;
        private PlaySfxOnCall playSfxOnCall;
        private PlayerSoundProfile sounds;

        private SafePointTracker safePointTracker;
        private float coyoteTimer;
        private bool isJumped;
        private float jumpSustainTimer;

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

        private bool isAttacking;
        private bool isAttackAnimationInitiated;
        private readonly float attackCooldownTime = 0.2f;
        private float attackCooldownTimer;

        private readonly TinyTimer throwCooldown = new TinyTimer(0.5f);
        private Transform swordThrowPoint;
        private SpawnComponent swordSpawner;
        private int CoinsCount => state.Inventory.GetCount(ItemIds.Coin);
        private int SwordCount => state.Inventory.GetCount(ItemIds.Sword);
        private bool IsArmed => SwordCount > 0;
        
        private PlayerState state;
        private HeroAttackType lastAttackType = HeroAttackType.Pierce;

        protected override void Awake() {
            base.Awake();

            Actions = G.Input.Player;
            state = G.Game.playerState;

            CloseSwordDamageWindow();

            safePointTracker = new SafePointTracker();
            damageable = GetComponent<Damageable>();
            lootDropper = GetComponent<LootDropper>();
            playSfxOnCall = GetComponent<PlaySfxOnCall>();
            sounds = GetComponent<PlayerSoundProfileLink>().Profile;

            dustSpawnPoint = transform.Find(DustPositionObjectName);
            swordThrowPoint = transform.Find(SwordThrowPointObjectName);
            swordSpawner = swordThrowPoint.GetComponent<SpawnComponent>();

            UpdateAnimatorController();

            InitFromState(state);

            G.Hero.Register(this);
        }

        private void OnDestroy() {
            G.Hero.Unregister(this);
        }

        private void InitFromState(PlayerState playerState) {
            damageable.maxHealth = playerState.GetMaxHealth();
            damageable.SetHealth(playerState.currentHealth);
        }

        private void UpdateAnimatorController() {
            MyAnimator.runtimeAnimatorController = IsArmed ? armedAnimator : unarmedAnimator;
        }

        protected override void Update() {
            base.Update();

            throwCooldown.Update(Time.deltaTime);

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

            CheckSafePoint();

            CheckJump();
            CheckHorizontalMovement();
            CheckInteraction();
            CheckAttack();
            CheckThrow();
            CheckItemUse();
        }

        private void CheckItemUse() {
            if (Actions.UseItem.WasPerformedThisFrame()
                && state.Inventory.GetCount(ItemIds.HealthPotionUsable) > 0) {
                // TODO: [BG] Move health potions healing stats to some data section, so we can get actual
                // healing value for it without hardcoding.
                Debug.Log(">>> Healing +3 HP");
                damageable.AddHealth(3f);
                state.Inventory.Remove(ItemIds.HealthPotionUsable, 1);
            }
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
            }
        }

        private bool CanAttack() {
            return IsArmed && !isAttacking && IsGrounded && attackCooldownTimer <= 0;
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

        #region ThrowSword

        private void CheckThrow() {
            if (Actions.Throw.WasPerformedThisFrame() && CanThrow()) {
                Debug.Log("Throwind sword anim!");
                // Set animation trigger and in the middle it will call `ThrowSword` method.
                MyAnimator.SetTrigger(HeroAnimKeys.OnThrowSword);
                throwCooldown.Start();
                G.Audio.Play2D(sounds.Attack.ThrowSword);
            }
        }

        private bool CanThrow() {
            return IsArmed
                   && throwCooldown.IsTimedOut
                   && SwordCount > 1; // Additional condition - don't allow throwing the last sword.
        }

        private void ThrowSword() {
            var sword = swordSpawner.SpawnInstance();
            Facing.ApplyTo(sword);
            state.Inventory.Remove(ItemIds.Sword, 1);
            UpdateAnimatorController();
            Debug.Log($"Swords left: {SwordCount}");
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

        protected override void CheckGround() {
            base.CheckGround();

            if (GroundChecker.HasExitedCollisionThisFrame) {
                coyoteTimer = coyoteJumpTime;
            }

            if (GroundChecker.HasEnteredCollisionThisFrame) {
                // Debug.Log(">>> landed" + MyRigidbody.velocity.y);
                // TODO: [BG] Figure out why sound has delay. Seems this is because we're out of sync with
                //   physics calculations. Probably CheckGround should be done on FixedUpdate.
                // Minor adjustment, as by some reason when jumping on a higher platform lending speed
                // may be something like 0.00015
                if (MyRigidbody.velocity.y < 0.0005f) {
                    playSfxOnCall.Play("landGrass");
                }

                if (FallHeight > MinFallHeightForDustEffect) {
                    SpawnLandingDust();
                }
            }
        }

        private void CheckSafePoint() {
            // TODO: [BG] Make sure that player is not standing on barrels or other platforms
            //   that are not completely stable (for example, moving platforms, disappearing platforms,
            //   or one way platforms).
            if (!isDead) {
                safePointTracker.Update(GroundChecker.IsAllCollide, transform.position, MyRigidbody.velocity,
                    Time.deltaTime);
            }
        }

        private void SpawnLandingDust() {
            G.Spawner.SpawnVfx(groundDustPrefab, dustSpawnPoint.position);
        }

        private void CheckHorizontalMovement() {
            Vector2 dir = Actions.Move.ReadValue<Vector2>().normalized;

            // Check `isAttacking` flag to prevent player from changing direction while attack effect is played,
            // otherwise the effect will turn together with player.
            SetDirection(dir, isAttacking);
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

            if (isJumpReleased || CeilingChecker.HasCollision) {
                jumpSustainTimer = 0;
            }

            var isSustainingJump = jumpSustainTimer > 0;

            if (isJumpPressedBuffer && CanJump()) {
                playSfxOnCall.Play("jump");
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
            MyRigidbody.velocity = new Vector2(MyRigidbody.velocity.x, jumpSpeed);
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

        public void OnCollected(ItemId itemId, float value) {
            if (itemId == ItemIds.HealthPotion) {
                Debug.Log($"Player: Collected {value} health");
                damageable.AddHealth(value);
            } else {
                state.Inventory.Add(itemId, (int)value);

                if (itemId == ItemIds.Sword) {
                    UpdateAnimatorController();
                }
            }
        }

        #region Animator

        private void UpdateState() {
            state.currentHealth = damageable.Health;
        }

        protected override void UpdateAnimator() {
            base.UpdateAnimator();

            if (isJumped) {
                // We're jumping on trigger, not using velocityY comparison, as we may have moving platforms,
                // in this case Y speed may be > 0, while the player is still on the ground.
                MyAnimator.SetTrigger(HeroAnimKeys.OnJump);
            }

            if (damageable.IsHitThisFrame) {
                MyAnimator.SetTrigger(HeroAnimKeys.OnHit);
            }

            if (isDiedThisFrame) {
                MyAnimator.SetTrigger(HeroAnimKeys.OnDeath);
                // TODO: [BG] Actually should be reset somewhere else, not in this method, but not it's just for POC 
                isDiedThisFrame = false;
            }

            if (isAttackAnimationInitiated) {
                lastAttackType = lastAttackType == HeroAttackType.Slash1
                    ? HeroAttackType.Slash2
                    : HeroAttackType.Slash1;
                
                var animKey = lastAttackType == HeroAttackType.Slash1 
                    ? HeroAnimKeys.OnAttack2
                    : HeroAnimKeys.OnAttack3;

                MyAnimator.SetTrigger(animKey);
                
                isAttackAnimationInitiated = false;
            }

            MyAnimator.SetBool(HeroAnimKeys.IsDead, isDead);
        }

        public void SpawnRunDust() {
            if (Math.Abs(MyRigidbody.velocity.x) > 1f) {
                var instance = G.Spawner.SpawnVfx(runDustPrefab, dustSpawnPoint.position);
                Facing.ApplyTo(instance);
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
            CancelAttack();
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

        public void OnGroundedDead() {
            G.Audio.Play2D(deadGroundedSound);
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
            var count = Math.Min(5, CoinsCount);
            lootDropper.DropLoot(count);
            state.Inventory.Remove(ItemIds.Coin, count);
        }

        // ------------------- GIZMOS -------------------

        protected override void OnDrawGizmosSelected() {
            base.OnDrawGizmosSelected();

            if (safePointTracker != null && safePointTracker.HasSafePosition) {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(safePointTracker.LastSafePosition, 0.1f);
            }
        }
    }
}
