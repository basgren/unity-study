using System;
using System.Collections;
using Core.Audio;
using Core.Utils;
using Game.Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Collectables;
using Game.Core.Components.Damage;
using Game.Core.Components.GameObjects;
using Game.Core.Models.Inventory;
using Game.Defs;
using Game.Features.Characters._Shared;
using Game.Features.Characters.Hero.Interaction;
using Game.Features.Characters.Hero.ItemUse;
using Game.Features.Characters.Parrot;
using Game.Features.Interactive.Bonfire;
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

        [Header("Companions")]
        [SerializeField]
        private GameObject parrotPrefab;

        [Header("Attack")]
        [SerializeField]
        private GameObject swordAttackArea;

        [SerializeField]
        private RuntimeAnimatorController armedAnimator;

        [SerializeField]
        private RuntimeAnimatorController unarmedAnimator;

        public InputActions.PlayerActions Actions { get; private set; }
        public Damageable Damageable => damageable;
        internal PlayerState State => state;
        internal PlayerSoundProfile Sounds => sounds;
        internal Animator Animator => MyAnimator;

        private BoxCollider2D myCollider;
        private Damageable damageable;
        private LootDropper lootDropper;
        private PlaySfxOnCall playSfxOnCall;
        private PlayerSoundProfile sounds;

        private SafePointTracker safePointTracker;
        private float coyoteTimer;
        private bool isJumped;
        private float jumpSustainTimer;

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

        private Transform swordThrowPoint;
        private SpawnComponent swordSpawner;
        private int CoinsCount => state.InventoryModel.GetCount(ItemIds.Coin);
        internal int SwordCount => state.InventoryModel.GetCount(ItemIds.Sword);
        internal bool IsArmed => state != null && state.IsArmed;

        private PlayerState state;
        private ItemUseService itemUseService;
        private ItemUseService perkUseService;
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
            state.InventoryModel.OnChange += OnInventoryChanged;

            UpdateAnimatorController();
            InitItemUseService();
            InitFromState(state);

            var interactionResolver = GetComponent<PlayerInteractionResolver>();
            if (interactionResolver == null) {
                Debug.LogWarning(
                    $"{nameof(PlayerController)}: {nameof(PlayerInteractionResolver)} component is missing on the hero. " +
                    "World interactions and the bottom-center hint will not work until it is added.",
                    this
                );
            }
            G.Hero.Register(this, itemUseService, perkUseService, interactionResolver);
        }

        private void Start() {
            if (G.Checkpoint.HasPendingRespawn) {
                var checkpointRef = G.Checkpoint.ConsumePendingRespawn();
                var bonfire = BonfireUtils.FindByIdInScene(gameObject.scene, checkpointRef.LocalId);
                
                if (bonfire != null) {
                    transform.position = bonfire.GetSpawnPosition();
                } else {
                    Debug.LogWarning($"Checkpoint bonfire '{checkpointRef.LocalId}' not found after scene load.");
                }
                
                RestoreHealthAfterRespawn();

                if (G.Checkpoint.IsBonfireRestTransitionActive) {
                    SetCanTakeDamage(false);
                    SetControlsEnabled(false);
                } else {
                    SetCanTakeDamage(true);
                    SetControlsEnabled(true);
                }
            }
        }

        private void OnDestroy() {
            if (state != null) {
                state.InventoryModel.OnChange -= OnInventoryChanged;
            }

            G.Hero.Unregister(this);
        }

        private void InitFromState(PlayerState playerState) {
            damageable.maxHealth = playerState.GetMaxHealth();
            damageable.SetHealth(playerState.currentHealth);
        }

        private void InitItemUseService() {
            itemUseService = new ItemUseService(state.BackpackPanelModel);
            itemUseService.Register(new SmallHealPotionStrategy(this));
            itemUseService.Register(new MediumHealPotionStrategy(this));
            itemUseService.Register(new SwordThrowStrategy(this));

            perkUseService = new ItemUseService(state.PerkPanelModel);
            perkUseService.Register(new ProtectionMaskStrategy());
            perkUseService.Register(new ParrotDeployStrategy(this, parrotPrefab));
        }

        private void UpdateAnimatorController() {
            MyAnimator.runtimeAnimatorController = IsArmed ? armedAnimator : unarmedAnimator;
        }

        private void OnInventoryChanged(InventoryChangeEvent eventInfo) {
            if (eventInfo.ItemId == ItemIds.Sword && eventInfo.CountDelta > 0 && !IsArmed) {
                // The first sword becomes the equipped weapon instead of staying in the backpack.
                SetArmedState(true);
                state.InventoryModel.Remove(ItemIds.Sword, 1);
            }
        }

        internal void RestorePersistentState(bool armed) {
            SetArmedState(armed);
        }

        private void SetArmedState(bool armed) {
            if (state == null) {
                return;
            }

            state.IsArmed = armed;
            UpdateAnimatorController();
        }

        protected override void Update() {
            base.Update();

            itemUseService.Update(Time.deltaTime);
            perkUseService.Update(Time.deltaTime);

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
            CheckAttack();
            CheckItemUse();
            CheckPerkUse();
            CheckInventory();
        }

        private void CheckInventory() {
            if (Actions.SwitchItem.WasPressedThisFrame()) {
                state.BackpackPanelModel.NextItem();
            }

            if (Actions.SwitchPerk.WasPressedThisFrame()) {
                state.PerkPanelModel.NextItem();
            }

            if (Actions.Inventory.WasPressedThisFrame()) {
                G.Menu.OpenInventory();
            }
        }

        private void CheckItemUse() {
            if (Actions.UseItem.WasPerformedThisFrame()) {
                Debug.Log("try to use item");
                itemUseService.TryUseSelectedItem();
            }
        }

        private void CheckPerkUse() {
            if (Actions.UsePerk.WasPerformedThisFrame()) {
                Debug.Log("try to use perk");
                perkUseService.TryUseSelectedItem();
            }
        }

        #region Attack

        /// <summary>
        /// Currently attack is implemented in a very simple way. Key is pressed - we activate damage window,
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
        /// Enables or disables the player action map directly.
        /// Intended for short transition windows such as bonfire rests, fades, and respawn handling.
        /// </summary>
        public void SetControlsEnabled(bool isEnabled) {
            if (isEnabled) {
                Actions.Enable();
            } else {
                Actions.Disable();
            }
        }

        /// <summary>
        /// Toggles whether the hero can receive damage.
        /// This uses <see cref="Damageable.IgnoreDamage"/> and is intended for transition states
        /// where the player should be invulnerable without triggering hit visuals.
        /// </summary>
        public void SetCanTakeDamage(bool canTakeDamage) {
            damageable.IgnoreDamage = !canTakeDamage;
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

        /// <summary>
        /// Called from animation event during throw animation.
        /// </summary>
        private void ThrowSword() {
            var sword = swordSpawner.SpawnInstance();
            Facing.ApplyTo(sword);
            state.InventoryModel.Remove(ItemIds.Sword, 1);
            UpdateAnimatorController();
        }

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
            state.InventoryModel.Add(itemId, (int)value);
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

        public void OnAfterHit(Damager damager) {
            Debug.Log($"Player: Hit by {damager.Type}. Health: {damageable.Health}");
            DropCoins();
            CancelAttack();
            UpdateState();

            if (damageable.IsDead) {
                if (G.Checkpoint.Current.HasValue) {
                    ShowHitAndRespawnAtCheckpoint();
                } else {
                    ShowHitAndRestartScene();
                }
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
            G.SceneTravel.ReloadActiveScene();
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

        private void ShowHitAndRespawnAtCheckpoint() {
            Actions.Disable();
            isDead = true;
            isDiedThisFrame = true;
            damageable.IgnoreDamage = true;
            StartCoroutine(WaitAndRespawnAtCheckpoint(WaitBeforeRestart));
        }

        private IEnumerator WaitAndRespawnAtCheckpoint(float seconds) {
            yield return new WaitForSeconds(seconds);

            var checkpointRef = G.Checkpoint.Current.Value;
            var checkpointSceneName = checkpointRef.Scene.GetSceneName();
            var currentScene = SceneManager.GetActiveScene().name;

            if (checkpointSceneName == currentScene) {
                var bonfire = BonfireUtils.FindByIdInScene(gameObject.scene, checkpointRef.LocalId);
                if (bonfire != null) {
                    RespawnAtPosition(bonfire.GetSpawnPosition());
                } else {
                    Debug.LogWarning($"Checkpoint bonfire '{checkpointRef.LocalId}' not found in scene '{currentScene}'.");
                }
            } else {
                G.Checkpoint.RequestRespawn();
                G.SceneTravel.LoadScene(checkpointSceneName);
            }
        }

        private void RespawnAtPosition(Vector2 position) {
            transform.position = position;
            RestoreHealthAfterRespawn();
            SetCanTakeDamage(true);
            SetControlsEnabled(true);
        }

        private void RestoreHealthAfterRespawn() {
            var maxHealth = state.GetMaxHealth();
            state.currentHealth = maxHealth;
            isDead = false;
            damageable.Revive();
        }

        private void DropCoins() {
            var count = Math.Min(5, CoinsCount);
            lootDropper.DropLoot(count);
            state.InventoryModel.Remove(ItemIds.Coin, count);
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
