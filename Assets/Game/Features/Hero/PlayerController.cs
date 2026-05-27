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
using Game.Features.Characters.Hero.GrapplingHook;
using Game.Features.Characters.Parrot;
using Game.Features.Interactive.Bonfire;
using Prefabs.Effects.InfoBubble;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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

        [SerializeField, Tooltip("How long collision with a one-way platform stays suspended after a Down+Jump drop-through. Long enough for the hero to clear the platform's collider; short enough that the platform becomes solid again before the hero lands on anything below.")]
        private float dropThroughDuration = 0.3f;

        [SerializeField, Tooltip("Minimum downward magnitude on the Move input (analog stick / D-pad) required to treat a jump press as a drop-through. 0..1, higher = more deliberate down-press needed.")]
        private float dropThroughDownThreshold = 0.5f;

        [Header("Effects")]
        [SerializeField]
        private GameObject runDustPrefab;

        [SerializeField]
        private GameObject jumpDustPrefab;

        [SerializeField]
        private GameObject groundDustPrefab;
        
        [SerializeField]
        private AudioCue deadGroundedSound;

        [SerializeField, Tooltip("One-shot jingle played when the hero dies (scene-reload / checkpoint respawn).")]
        private AudioCue deathJingleCue;

        [Header("Companions")]
        [SerializeField]
        private GameObject parrotPrefab;

        [Header("Shield")]
        [SerializeField]
        private GameObject shieldAuraPrefab;

        [SerializeField]
        private Transform auraSpawnPoint;

        [SerializeField]
        private float shieldDuration = 10f;

        [SerializeField]
        private float shieldPulsateTime = 3f;

        [Header("Attack")]
        [SerializeField]
        private GameObject swordAttackArea;

        [SerializeField, Tooltip("Damager on the swordAttackArea. Used to apply melee damage stat upgrades.")]
        private Damager swordAttackDamager;

        [SerializeField, FormerlySerializedAs("attackHitPushbackImpulse"),
         Tooltip("Horizontal kickback speed in m/s added to the hero on a successful sword hit. Mass-independent — value reads as actual velocity bump. Reference: moveSpeed = 5, so 3-6 is a clear-but-modest kick, 8+ is a strong shove.")]
        private float attackHitPushbackSpeed = 5f;

        [Header("Movement")]
        [SerializeField, Tooltip("Seconds to ramp horizontal velocity from 0 up to full moveSpeed when a direction is held. Smaller = snappier. 0 = instant snap (legacy behavior).")]
        private float speedBuildUpTime = 0.08f;

        [SerializeField, Tooltip("Seconds to ramp horizontal velocity back to 0 when no direction is held. Smaller = snappier stop. 0 = instant stop (would also kill knockback / attack-pushback in the same frame).")]
        private float speedDecayTime = 0.15f;

        [Header("Hit Stun")]
        [SerializeField, Tooltip("Brief window in seconds after the hero lands a hit on something OR takes a hit. While the timer is active, movement input is ignored (decel/accel still run) and no further pushback impulse is applied. Prevents double-pushback when a swing hits multiple targets and lets the impact register.")]
        private float hitStunTime = 0.1f;

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

        private Collider2D dropThroughCollider;
        private float dropThroughTimer;

        // TODO: [BG] Refactor - it's getting too many flags to manage. At the same time it would be nice to
        //  keep animation state update in one place. Probably we could use FSM to store player's state and
        //  sync animation with it.
        // Flags to process animation state changes
        private bool isDiedThisFrame;
        private bool isDead;

        private bool isDragging;
        private bool isHookSwinging;
        private bool isAttacking;
        private bool isAttackAnimationInitiated;
        private readonly float attackCooldownTime = 0.2f;
        private float attackCooldownTimer;

        private float hitStunTimer;

        private Transform swordThrowPoint;
        private SpawnComponent swordSpawner;
        private int CoinsCount => state.InventoryModel.GetCount(ItemIds.Coin);
        internal int SwordCount => state.InventoryModel.GetCount(ItemIds.Sword);
        internal bool IsArmed => state != null && state.IsArmed;

        private PlayerState state;
        private ItemUseService itemUseService;
        private ItemUseService perkUseService;
        private HeroAttackType lastAttackType = HeroAttackType.Pierce;
        private Transform infoBubblePoint;

        // Cached prefab/base damage values, captured before any stat bonuses are applied,
        // so upgrade levels can be reapplied additively at any time.
        private int baseMeleeDamage;

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
            infoBubblePoint = transform.Find("InfoBubblePoint");

            if (swordAttackDamager != null) {
                swordAttackDamager.Hit += OnSwordAttackHit;
            }

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
                    TeleportAndNotifyCamera(bonfire.GetSpawnPosition());
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
            } else {
                // Fresh scene load (including post-death scene reload). The shared input
                // action map is disabled globally when the hero dies, so re-enable it here.
                SetControlsEnabled(true);
            }
        }

        private void OnDestroy() {
            if (state != null) {
                state.InventoryModel.OnChange -= OnInventoryChanged;
            }

            if (swordAttackDamager != null) {
                swordAttackDamager.Hit -= OnSwordAttackHit;
            }

            G.Hero.Unregister(this);
        }

        private void OnSwordAttackHit(HitInfo info) {
            if (!info.IsDamaged || attackHitPushbackSpeed <= 0f) {
                return;
            }

            // Already stunned (from a prior target this swing, or from a taken hit) — skip so
            // multi-target swings don't stack their pushbacks into an oversized kickback.
            if (hitStunTimer > 0f) {
                return;
            }

            // Push the hero away from the target — opposite to current facing — so a clean hit
            // reads as a small kickback. Additive velocity injection (not AddForce) so the tuning
            // value is in m/s and stays consistent regardless of Rigidbody2D mass.
            // Vertical velocity is left alone to keep jump/fall arcs intact.
            var pushDir = -GetFacingDirSign();
            var velocity = MyRigidbody.velocity;
            MyRigidbody.velocity = new Vector2(velocity.x + pushDir * attackHitPushbackSpeed, velocity.y);

            hitStunTimer = hitStunTime;
        }

        private void InitFromState(PlayerState playerState) {
            if (swordAttackDamager != null) {
                baseMeleeDamage = swordAttackDamager.Damage;
            }

            damageable.SetMaxHealth(playerState.GetMaxHealth());
            damageable.SetHealth(playerState.currentHealth);
            ApplyMeleeStat();
        }

        /// <summary>
        /// Re-applies all current hero stat upgrades from <see cref="PlayerState"/> to the
        /// live components (max health, melee damage). Current HP is preserved and only
        /// clamped down if the new max is lower. Called after a stat is upgraded or
        /// restored from save state.
        /// </summary>
        public void ApplyCurrentStats() {
            var newMax = state.GetMaxHealth();
            damageable.SetMaxHealth(newMax);
            if (damageable.Health > newMax) {
                damageable.SetHealth(newMax);
            }
            state.currentHealth = damageable.Health;
            ApplyMeleeStat();
        }

        /// <summary>
        /// Fully heals the player to current max health. Called by the stat shop as the
        /// reward for buying a Health upgrade.
        /// </summary>
        public void HealToFull() {
            damageable.SetHealth(damageable.maxHealth);
            state.currentHealth = damageable.Health;
        }

        private void ApplyMeleeStat() {
            if (swordAttackDamager == null) {
                return;
            }

            swordAttackDamager.SetDamage(baseMeleeDamage + state.GetMeleeDamageBonus());
        }

        private void InitItemUseService() {
            itemUseService = new ItemUseService(state.BackpackPanelModel);
            itemUseService.Register(new SmallHealPotionStrategy(this));
            itemUseService.Register(new MediumHealPotionStrategy(this));
            itemUseService.Register(new SwordThrowStrategy(this));

            perkUseService = new ItemUseService(state.PerkPanelModel);
            perkUseService.Register(new ProtectionMaskStrategy(this, shieldAuraPrefab, auraSpawnPoint, shieldDuration, shieldPulsateTime));
            perkUseService.Register(new ParrotDeployStrategy(this, parrotPrefab));

            var hookAbility = GetComponent<GrapplingHookAbility>();
            if (hookAbility != null) {
                perkUseService.Register(new GrapplingHookStrategy(this, hookAbility));
            }
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

            if (hitStunTimer > 0f) {
                hitStunTimer -= Time.deltaTime;
            }

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
            return IsArmed && !isAttacking && !isDragging && attackCooldownTimer <= 0;
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

            // Apply throw damage stat upgrade on top of the projectile prefab's base damage.
            if (sword.TryGetComponent<Damager>(out var damager)) {
                damager.SetDamage(damager.Damage + state.GetThrowDamageBonus());
            }

            Facing.ApplyTo(sword);
            state.InventoryModel.Remove(ItemIds.Sword, 1);
            UpdateAnimatorController();
        }

        public void SetHookSwingMode(bool swinging) {
            isHookSwinging = swinging;
        }

        public void SetDragMode(bool dragging, float barrelX) {
            isDragging = dragging;

            if (isDragging) {
                CancelAttack();
                // Face toward the barrel while dragging.
                Facing.SetByX(barrelX - transform.position.x);
                // Show unarmed visuals while dragging.
                MyAnimator.runtimeAnimatorController = unarmedAnimator;
            } else {
                UpdateAnimatorController();
            }
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
            // Scripted flight (e.g. portal launch arc) drives velocity directly. Bail out so the
            // accel/decel ramp doesn't strip away the horizontal component of the launch velocity.
            if (scriptedFlight) {
                return;
            }

            if (isHookSwinging) {
                // Movement velocity is handled by GrapplingHookAbility via forces,
                // but we still let horizontal input flip facing so the hero looks
                // where the player is steering.
                Vector2 swingDir = Actions.Move.ReadValue<Vector2>();
                if (Mathf.Abs(swingDir.x) > 0.1f) {
                    Facing.SetByX(swingDir.x);
                }
                return;
            }

            // Scripted walk (e.g. cinematic portal entry/exit) overrides input. Controls are
            // typically also disabled during a scripted walk so the input read would return zero
            // anyway, but the override makes the intent explicit.
            // Hit stun (timer > 0) suppresses input so decel takes over and the hit reads visibly,
            // but does NOT override scripted walk — cinematics must keep moving.
            Vector2 dir;
            if (scriptedMoveDir.HasValue) {
                dir = new Vector2(scriptedMoveDir.Value, 0f);
            } else if (hitStunTimer > 0f) {
                dir = Vector2.zero;
            } else {
                dir = Actions.Move.ReadValue<Vector2>().normalized;
            }

            // Check `isAttacking` flag to prevent player from changing direction while attack effect is played,
            // otherwise the effect will turn together with player.
            var preserveFacing = isAttacking || isDragging;

            // Capture current vx before base SetDirection overwrites it. Base call still runs so that
            // Direction and facing stay synced through the same code path; we then write the ramped vx back.
            var previousVx = MyRigidbody.velocity.x;
            SetDirection(dir, preserveFacing);

            var moveSpeed = GetMoveSpeed();
            var hasInput = Mathf.Abs(dir.x) > 0.0001f;
            var targetVx = hasInput ? Mathf.Sign(dir.x) * moveSpeed : 0f;

            // Choose accel vs decel by whether we are gaining or shedding speed relative to the
            // target — NOT by whether input is held. This way overspeed from knockback / pushback
            // bleeds off at the (slower) decel rate instead of being yanked back at the accel rate.
            var rampTime = Mathf.Abs(previousVx) < Mathf.Abs(targetVx)
                ? speedBuildUpTime
                : speedDecayTime;

            float newVx;
            if (rampTime > 0f) {
                var maxDelta = (moveSpeed / rampTime) * Time.deltaTime;
                newVx = Mathf.MoveTowards(previousVx, targetVx, maxDelta);
            } else {
                newVx = targetVx;
            }

            MyRigidbody.velocity = new Vector2(newVx, MyRigidbody.velocity.y);
        }

        // ---- Scripted movement (cinematic transitions) ----

        private int? scriptedMoveDir;
        private bool scriptedFlight;
        private float scriptedFlightSavedGravityScale;

        /// <summary>
        /// Drives the hero horizontally as if the player were holding the move key in
        /// <paramref name="dirSign"/> direction (-1 left, +1 right). Intended for cinematic
        /// transitions (e.g. walking into / out of an Entrance portal). The caller should also
        /// disable input via <see cref="SetControlsEnabled"/> so the player cannot fight the walk.
        /// </summary>
        public void BeginScriptedWalk(int dirSign) {
            scriptedMoveDir = dirSign;
        }

        /// <summary>
        /// Ends a scripted walk started by <see cref="BeginScriptedWalk"/> and immediately zeroes
        /// horizontal velocity so the hero stops on the spot.
        /// </summary>
        public void EndScriptedWalk() {
            scriptedMoveDir = null;
            SetDirection(Vector2.zero);
        }

        /// <summary>
        /// Returns true while the hero is under direct velocity control by a cinematic system
        /// (see <see cref="BeginScriptedFlight"/>). Movement and jump input handling are suspended.
        /// </summary>
        public bool IsInScriptedFlight => scriptedFlight;

        /// <summary>
        /// Begins a cinematic flight phase: suspends horizontal velocity management and (optionally)
        /// turns off gravity so a portal or other cutscene can drive the hero by writing velocity directly.
        /// Always pair with <see cref="EndScriptedFlight"/>. Has no effect if already active.
        /// </summary>
        public void BeginScriptedFlight(bool disableGravity) {
            if (scriptedFlight) {
                return;
            }

            scriptedFlight = true;
            scriptedFlightSavedGravityScale = MyRigidbody.gravityScale;
            if (disableGravity) {
                MyRigidbody.gravityScale = 0f;
            }
        }

        /// <summary>
        /// Restores normal movement and gravity after <see cref="BeginScriptedFlight"/>. Safe to call
        /// when no scripted flight is active.
        /// </summary>
        public void EndScriptedFlight() {
            if (!scriptedFlight) {
                return;
            }

            MyRigidbody.gravityScale = scriptedFlightSavedGravityScale;
            scriptedFlight = false;
        }

        /// <summary>
        /// Writes velocity directly to the hero rigidbody. Intended for cinematic phases — pair with
        /// <see cref="BeginScriptedFlight"/> so the value isn't immediately overwritten by normal
        /// horizontal velocity management.
        /// </summary>
        public void SetVelocity(Vector2 velocity) {
            MyRigidbody.velocity = velocity;
        }

        /// <summary>
        /// Sets the hero's facing direction (-1 left, +1 right). Used by cinematic transitions so the
        /// hero looks toward the action without having to drive input.
        /// </summary>
        public void SetFacing(int dirSign) {
            if (dirSign == 0) {
                return;
            }

            Facing.SetByX(dirSign);
        }

        /// <summary>
        /// Current vertical velocity (m/s). Useful for cinematic systems that need to react to landing.
        /// </summary>
        public float GetVerticalVelocity() {
            return MyRigidbody.velocity.y;
        }

        #region Jump

        private void CheckJump() {
            isJumped = false;

            // During scripted flight the hero is on a forced arc; ignore any buffered or held jump
            // so it can't reignite mid-cinematic right after controls come back on landing.
            if (scriptedFlight) {
                jumpInputBufferTimer = 0f;
                isJumpPressedBuffer = false;
                jumpSustainTimer = 0f;
                return;
            }

            UpdateDropThrough();

            // Drop-through is intercepted before the normal jump path so the same input frame
            // doesn't both ignore the platform AND launch the hero upward.
            if (TryDropThroughOneWayPlatform()) {
                return;
            }

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
            return IsGrounded || coyoteTimer > 0 || isHookSwinging;
        }

        private bool TryDropThroughOneWayPlatform() {
            if (!Actions.Jump.WasPerformedThisFrame() || !IsGrounded) {
                return false;
            }

            var moveY = Actions.Move.ReadValue<Vector2>().y;
            if (moveY > -dropThroughDownThreshold) {
                return false;
            }

            var effectors = GroundChecker.GetHitComponents<PlatformEffector2D>();
            for (var i = 0; i < effectors.Count; i++) {
                var effector = effectors[i];
                if (!effector.useOneWay) {
                    continue;
                }

                var platformCollider = effector.GetComponent<Collider2D>();
                if (platformCollider == null) {
                    continue;
                }

                Physics2D.IgnoreCollision(MyCollider, platformCollider, true);
                dropThroughCollider = platformCollider;
                dropThroughTimer = dropThroughDuration;
                return true;
            }

            return false;
        }

        private void UpdateDropThrough() {
            if (dropThroughTimer <= 0f) {
                return;
            }

            dropThroughTimer -= Time.deltaTime;
            if (dropThroughTimer > 0f) {
                return;
            }

            // Platform may have been destroyed mid-drop; Unity's == null returns true for
            // destroyed colliders, and Physics2D.IgnoreCollision throws if either side is gone.
            if (dropThroughCollider != null) {
                Physics2D.IgnoreCollision(MyCollider, dropThroughCollider, false);
            }
            dropThroughCollider = null;
        }

        #endregion

        /// <summary>
        /// True when the hero's ground contact this frame is a one-way platform
        /// (a <see cref="PlatformEffector2D"/> with one-way enabled). Used by the
        /// grappling hook to nudge the hero up when he climbs the rope onto such a platform.
        /// </summary>
        public bool IsGroundedOnOneWayPlatform() {
            var effectors = GroundChecker.GetHitComponents<PlatformEffector2D>();
            for (var i = 0; i < effectors.Count; i++) {
                if (effectors[i].useOneWay) {
                    return true;
                }
            }

            return false;
        }

        public void OnCollected(ItemId itemId, float value) {
            state.InventoryModel.Add(itemId, (int)value);
        }

        #region Animator

        private void UpdateState() {
            state.currentHealth = damageable.Health;
        }

        protected override void UpdateAnimator() {
            base.UpdateAnimator();

            if (isJumped && !isAttacking) {
                // We're jumping on trigger, not using velocityY comparison, as we may have moving platforms,
                // in this case Y speed may be > 0, while the player is still on the ground.
                // While attacking, skip the jump trigger so the slash animation plays through uninterrupted.
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
            hitStunTimer = hitStunTime;
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
            PlayDeathJingle();
            // TODO: [BG] Leave for refactor - move to some service like game manager.
            //   player should not manage own death or even respawn. I should throw some message
            //   and game manager should decide what to do.
            G.DeathEffect.Play(transform, () => G.SceneTravel.ReloadActiveScene());
        }

        private void ShowHitAndRespawnAtSafePoint() {
            Actions.Disable();
            isDead = true;
            isDiedThisFrame = true;
            damageable.IgnoreDamage = true;
            StartCoroutine(WaitAndRespawn(WaitBeforeRespawn));
        }

        private IEnumerator WaitAndRespawn(float seconds) {
            yield return new WaitForSeconds(seconds);
            RespawnAtSafePoint();
        }

        private void RespawnAtSafePoint() {
            isDead = false;
            TeleportAndNotifyCamera(safePointTracker.LastSafePosition);
            damageable.IgnoreDamage = false;
            Actions.Enable();
        }

        /// <summary>
        /// Moves the hero to <paramref name="position"/> and tells Cinemachine the follow
        /// target warped, so the camera cuts instantly instead of interpolating from the
        /// previous location (would otherwise be visible as a pan after a distant respawn).
        /// </summary>
        private void TeleportAndNotifyCamera(Vector3 position) {
            var delta = position - transform.position;
            transform.position = position;
            if (G.Camera != null) {
                G.Camera.NotifyTargetTeleported(transform, delta);
            }
        }

        private void ShowHitAndRespawnAtCheckpoint() {
            Actions.Disable();
            isDead = true;
            isDiedThisFrame = true;
            damageable.IgnoreDamage = true;
            PlayDeathJingle();
            G.DeathEffect.Play(transform, RespawnAtCheckpointNow);
        }

        /// <summary>
        /// Ducks the level music and plays the one-shot death jingle. Used by both full-death
        /// paths (scene reload and checkpoint respawn). Cross-scene reloads don't need to
        /// restart music manually — the new scene's <c>LevelEntryPoint</c> re-assigns music
        /// on its own.
        /// </summary>
        private void PlayDeathJingle() {
            G.Audio.StopLevelMusic();

            if (deathJingleCue != null) {
                StartCoroutine(DoPlayDeathJingle());
            }
        }

        private IEnumerator DoPlayDeathJingle() {
            yield return new WaitForSeconds(0.5f);
            G.Audio.Play2D(deathJingleCue);
        }

        private void RespawnAtCheckpointNow() {
            var checkpointRef = G.Checkpoint.Current.Value;
            var checkpointSceneName = checkpointRef.Scene.GetSceneName();
            var currentScene = SceneManager.GetActiveScene().name;

            if (checkpointSceneName == currentScene) {
                var bonfire = BonfireUtils.FindByIdInScene(gameObject.scene, checkpointRef.LocalId);
                if (bonfire != null) {
                    RespawnAtPosition(bonfire.GetSpawnPosition());
                    // Same-scene respawn does not trigger AfterTransition, so clear overlay manually.
                    G.DeathEffect.ResetVisuals();
                } else {
                    Debug.LogWarning($"Checkpoint bonfire '{checkpointRef.LocalId}' not found in scene '{currentScene}'.");
                }
            } else {
                G.Checkpoint.RequestRespawn();
                G.SceneTravel.LoadScene(checkpointSceneName);
            }
        }

        private void RespawnAtPosition(Vector2 position) {
            TeleportAndNotifyCamera(position);
            RestoreHealthAfterRespawn();
            SetCanTakeDamage(true);
            SetControlsEnabled(true);
            // Same-scene respawn keeps the existing music assignment; bring it back.
            G.Audio.StartLevelMusic();
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

        public void ShowConfusion() {
            G.Spawner.SpawnInfoBubble(InfoBubbleType.Question, infoBubblePoint.position, infoBubblePoint, 3f);
        }
    }
}
