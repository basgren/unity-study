using System;
using System.Collections;
using System.Collections.Generic;
using Core.Components.Base2D;
using Game.Core.Bootstrap;
using Game.Core.Components.Base2D;
using Game.Core.Components.Damage;
using Game.Features.Bosses.VengefulSpirit.SpectralSwords;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using Game.Features.Characters.PinkStar;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Features.Bosses.VengefulSpirit {
    internal static class VengefulSpiritAnimKeys {
        // Bool that gates the windup phase of the charge attack (ChargeAttack1 plays while
        // true; ChargeAttack2 starts when it flips to false). Spec authoring calls this
        // 'isChargeAttackStarted'; the underlying parameter is named 'isChargingAttack' in
        // the controller — keep the existing parameter to avoid the rename + transition rewrite.
        public static readonly int IsChargingAttack = Animator.StringToHash("isChargingAttack");
        public static readonly int IsCasting = Animator.StringToHash("isCasting");
        public static readonly int OnAttack = Animator.StringToHash("onAttack");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
        public static readonly int OnHit = Animator.StringToHash("onHit");
    }

    /// <summary>
    /// Identifies which cast action should fire when the cast animation reaches its
    /// effect frame. Several cast actions reuse the same cast animation; the controller
    /// remembers the pending one and dispatches via <see cref="VengefulSpirit.OnCastEffect"/>.
    /// </summary>
    internal enum VengefulSpiritCastAction {
        None,
        SpawnShield,
        CastSwords
    }

    /// <summary>
    /// Command describing desired Vengeful Spirit actions for a single tick.
    /// </summary>
    public struct VengefulSpiritCommand {
        public readonly int XDirection;   // -1 left, 0 idle, +1 right
        public readonly int YDirection;   // -1 down, 0 idle, +1 up
        public readonly bool Attack;      // one-shot melee thrust trigger
        public readonly bool SpawnShield; // one-shot cast: spawn the spectral shield
        public readonly bool CastSwords;  // one-shot cast: launch a spectral-sword wave
        public readonly bool Teleport;    // one-shot: fade out, relocate, fade in

        public VengefulSpiritCommand(int xDirection, int yDirection, bool attack, bool spawnShield, bool castSwords, bool teleport) {
            XDirection = xDirection;
            YDirection = yDirection;
            Attack = attack;
            SpawnShield = spawnShield;
            CastSwords = castSwords;
            Teleport = teleport;
        }
    }

    public abstract class VengefulSpiritControlSource : BaseControlSource<VengefulSpiritCommand> {
    }

    /// <summary>
    /// Flying boss controller. Reads commands from a pluggable <see cref="VengefulSpiritControlSource"/>,
    /// drives 2D movement via Rigidbody2D velocity, and forwards basic abilities to the animator.
    ///
    /// NOTES:
    /// - Rigidbody2D is expected to be Dynamic with gravityScale = 0 and Z-rotation frozen. Collisions
    ///   with the Ground layer are resolved by the physics engine.
    /// - Stage 1: this is input-driven only. A proper state machine + AI will be added later.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Facing2D))]
    public class VengefulSpirit : MonoBehaviour {
        [SerializeField]
        private VengefulSpiritControlSource controlSource;

        [SerializeField]
        private float moveSpeed = 4f;

        [Header("Attack")]
        [Tooltip("How long the boss holds the attack state — animation plays, damage is dealt via " +
                 "anim events on the attack damager, then the boss becomes free to act again. " +
                 "Should match the Attack animation length. The attack is stationary; positioning " +
                 "is the responsibility of the caller (typically a pattern that teleports first).")]
        [FormerlySerializedAs("attackThrustDuration")]
        [SerializeField]
        private float attackHoldDuration = 1.2f;

        [Header("Prefabs")]
        [SerializeField]
        private GameObject swordPrefab;

        [SerializeField]
        private GameObject shieldPrefab;

        [SerializeField]
        private Transform shieldSpawnPoint;

        [Header("Spectral Swords")]
        [Tooltip("Anchors available to sword casts, keyed by name. The Default entry is fired by the input-driven debug cast; AI picks a name at runtime via GetSwordAnchor.")]
        [SerializeField]
        private SpectralSwordAnchorBinding[] swordAnchors;

        [Tooltip("Time the boss holds the Charge state before swords actually fire. Lets the " +
                 "telegraph be tuned independently of the cast clip length. The Charge animation " +
                 "loops, so any value works visually.")]
        [SerializeField]
        private float swordCastChargeDuration = 1.5f;

        [Header("Damagers")]
        [Tooltip("Damager toggled on only during the active common-attack hit frames. Driven " +
                 "by the OnAttackHitStart / OnAttackHitEnd animation events on Attack.anim. " +
                 "Leave null to keep the legacy contact-thrust behavior (no controlled window).")]
        [SerializeField]
        private Damager attackDamager;

        [Tooltip("Damager toggled on only during the horizontal dash phase of the charge attack " +
                 "(ChargeAttack2). Disabled by default; enabled by the action layer when the dash starts.")]
        [SerializeField]
        private Damager chargeDamager;

        [Header("Charge Attack")]
        [Tooltip("Time the boss holds ChargeAttack1 (windup) before the dash begins. The damager " +
                 "is OFF during this window — the boss must not damage the player before the dash.")]
        [SerializeField]
        private float chargeWindUpDuration = 0.8f;

        [Tooltip("Horizontal speed during the ChargeAttack2 dash phase.")]
        [SerializeField]
        private float chargeDashSpeed = 16f;

        [Tooltip("Idle time after the dash ends before the boss is ready for the next pattern.")]
        [SerializeField]
        private float chargeIdleDuration = 1f;

        [Tooltip("Safety cap on the dash duration. The dash also ends as soon as the boss reaches " +
                 "the destination anchor's X coordinate; this cap only fires if something blocks " +
                 "the path or the destination is unreachable.")]
        [SerializeField]
        private float chargeMaxDashDuration = 2f;

        [Header("Teleport")]
        [SerializeField]
        private SpiritTeleporter teleporter;

        [Tooltip("Predefined teleport destinations. AI / debug picks one by name; if no name " +
                 "is supplied, a random anchor different from the closest one to the boss is used.")]
        [SerializeField]
        private TeleportAnchorBinding[] teleportAnchors;

        [Header("Encounter")]
        [Tooltip("If on, the boss engages BossFightService on enable so the boss health bar appears " +
                 "automatically. Turn off when the encounter is gated by a cutscene or trigger that " +
                 "calls G.BossFight.EngageBoss explicitly.")]
        [SerializeField]
        private bool autoEngageOnEnable = true;

        [Header("Shield Destruction Recovery")]
        [Tooltip("Maximum time to wait for the spectral shield's destroy animation to complete " +
                 "before forcing the boss to advance to the hit reaction. Safety against an " +
                 "animation-event misconfiguration that would otherwise stall recovery.")]
        [SerializeField]
        private float shieldDestroyAnimMaxDuration = 3f;

        [Tooltip("Time the boss spends in the OnHit reaction after the shield finishes shattering. " +
                 "The boss is invulnerable for the entire recovery window — this is a forced " +
                 "flinch, not a real damage event.")]
        [SerializeField]
        private float shieldDestroyHitReactionDuration = 1f;

        private Rigidbody2D myRigidbody;
        private Animator myAnimator;
        private Facing2D facing;
        private Damageable damageable;
        private Damageable currentShieldDamageable;
        // Direct reference to the spectral shield's GameObject. Held alongside the
        // Damageable so the recovery sequence can poll Unity's null-check to know when
        // the shield's destroy animation has run to completion (OnDestroyAnimExit on
        // SpectralShield calls Destroy(gameObject) at that point).
        private GameObject currentShieldInstance;
        private SpriteRenderer[] currentShieldRenderers;
        // The shield's animator clips use WriteDefaultValues, so the idle clip overwrites
        // the SpriteRenderer alpha every frame. We disable these for the duration of a
        // teleport so the teleporter's fade is the sole driver of the shield's alpha.
        private Animator[] currentShieldAnimators;
        // True for the brief window between the shield's HP hitting zero and the boss's
        // OnHit reaction completing. While true the boss takes no damage and IsBusy is
        // forced on so the AI / patterns leave it alone.
        private bool isShieldDestructionRecovery;
        private Coroutine shieldRecoveryRoutine;
        // Composed inputs to Damageable.IgnoreDamage. Both states layer onto each other —
        // a teleport that begins while a shield is active must keep the boss immune even
        // after the teleport ends, and vice-versa.
        private bool isTeleportImmune;

        private bool isCasting;
        // Set true while a pattern is driving the cast lifecycle (RequestBeginShieldCast →
        // RequestFinishShieldCastAndSpawn). Suppresses the OnCastAnimationEnd hook so the
        // boss stays in the casting loop until the pattern explicitly ends it.
        private bool isCastHoldActive;
        private VengefulSpiritCastAction pendingCastAction;
        // Anchor name to use for the next sword cast (used by the input-driven debug path
        // via DefaultSwordAnchorName fallback). Pattern code goes through nextSwordAnchorRef
        // instead.
        private string nextSwordAnchorName;
        // Direct-ref override for the next sword cast — preferred by pattern code so anchors
        // can be wired in inspector without name-based lookup. Wins over nextSwordAnchorName.
        private SpectralSwordSpawnAnchor nextSwordAnchorRef;
        // Sword casts run their own charge timer instead of using the animation event so
        // the telegraph length is tunable. Held here for cancellation on death.
        private Coroutine swordCastChargeRoutine;
        private bool isAttacking;
        private float attackElapsed;
        private bool isTeleporting;
        private bool isCharging;
        private Coroutine chargeRoutine;
        // Chase mode: a pattern is driving velocity directly each frame via ChaseStep.
        // Intentionally NOT part of IsBusy so RequestCommonAttack still fires during a
        // chase — the chase pattern relies on attacking the player when it gets close.
        private bool isChasing;
        // Set true between OnAttackHitStart and OnAttackHitEnd anim events while the
        // boss is chasing. Once the swing commits to the active hit window the boss
        // anchors in place — no more chase tracking through the strike.
        private bool isAttackHitActive;
        private bool isDead;
        private bool wasHitThisFrame;
        private bool diedThisFrame;

        public bool IsAttacking => isAttacking;
        public bool IsCasting => isCasting;
        public bool IsTeleporting => isTeleporting;
        public bool IsCharging => isCharging;
        public bool IsChasing => isChasing;
        public bool IsShieldDestructionRecovery => isShieldDestructionRecovery;
        public bool IsBusy => isAttacking || isCasting || isTeleporting || isCharging || isShieldDestructionRecovery;

        /// <summary>
        /// Fires on the same frame the shield's HP reaches zero, BEFORE the shield's
        /// destroy animation actually plays. The AI subscribes so it can stop the
        /// currently running pattern; the boss handles the recovery sequence itself.
        /// </summary>
        public event Action OnShieldDestroyed;
        public bool IsDead => isDead;
        public Damageable Damageable => damageable;
        /// <summary>
        /// True while a spectral shield instance is alive in the world. The boss component
        /// gates shield spawning on this; AI may also read it to avoid stacking shield casts.
        /// </summary>
        public bool HasActiveShield => currentShieldDamageable != null;

        // Tracks the controlSource value the boss has already synced sibling enable-flags to,
        // so we only walk GetComponents when the assignment actually changes.
        private VengefulSpiritControlSource lastSyncedControlSource;

        private void Awake() {
            myRigidbody = GetComponent<Rigidbody2D>();
            myAnimator = GetComponent<Animator>();
            facing = GetComponent<Facing2D>();
            damageable = GetComponent<Damageable>();
            // Damagers must default to disabled — they are gated on by anim events
            // (attackDamager) or by the charge-attack coroutine (chargeDamager).
            // Defending against a prefab where the field was authored enabled.
            if (attackDamager != null) {
                attackDamager.enabled = false;
            }
            if (chargeDamager != null) {
                chargeDamager.enabled = false;
            }
            SyncControlSourceEnabled();
            lastSyncedControlSource = controlSource;
        }

        private void OnEnable() {
            if (!autoEngageOnEnable) {
                return;
            }

            if (damageable == null) {
                damageable = GetComponent<Damageable>();
            }

            if (damageable != null && G.BossFight != null) {
                G.BossFight.EngageBoss(damageable);
            }
        }

        private void OnDisable() {
            isTeleportImmune = false;
            isShieldDestructionRecovery = false;
            if (shieldRecoveryRoutine != null) {
                StopCoroutine(shieldRecoveryRoutine);
                shieldRecoveryRoutine = null;
            }
            DetachShieldDamageable();
            currentShieldInstance = null;
            RefreshIgnoreDamage();

            if (G.BossFight != null) {
                G.BossFight.DisengageBoss();
            }
        }

        private void Update() {
            // Pick up inspector-time / runtime swaps of the control source — the boss owns
            // which source is active, so it also owns enabling the right one and disabling
            // the rest. Sources stay naive and just don't tick when their MonoBehaviour is
            // disabled.
            if (controlSource != lastSyncedControlSource) {
                SyncControlSourceEnabled();
                lastSyncedControlSource = controlSource;
            }

            CheckDamageState();

            if (isDead) {
                return;
            }

            ExecuteCommand();
        }

        // Walks every VengefulSpiritControlSource on this GameObject and enables only the
        // one matching the assigned controlSource (others get disabled). Toggling
        // MonoBehaviour.enabled stops their Update / coroutines and triggers OnDisable /
        // OnEnable for clean state transitions.
        private void SyncControlSourceEnabled() {
            var sources = GetComponents<VengefulSpiritControlSource>();
            for (int i = 0; i < sources.Length; i++) {
                sources[i].enabled = sources[i] == controlSource;
            }
        }

        private void CheckDamageState() {
            if (damageable == null) {
                return;
            }

            // Poll instead of subscribing: Damageable.OnHealthChanged fires before IsDead is set,
            // so an event-based path would miss the death frame.
            if (damageable.IsHitThisFrame) {
                wasHitThisFrame = true;
            }

            if (damageable.IsDead && !isDead) {
                isDead = true;
                diedThisFrame = true;
                isCasting = false;
                isCastHoldActive = false;
                pendingCastAction = VengefulSpiritCastAction.None;
                StopSwordCastChargeRoutine();
                isAttacking = false;
                attackElapsed = 0f;
                isTeleporting = false;
                isTeleportImmune = false;
                isChasing = false;
                isAttackHitActive = false;
                isShieldDestructionRecovery = false;
                if (shieldRecoveryRoutine != null) {
                    StopCoroutine(shieldRecoveryRoutine);
                    shieldRecoveryRoutine = null;
                }
                StopChargeRoutine();
                // Both damagers must be off at death so the corpse never lands a posthumous hit.
                if (attackDamager != null) {
                    attackDamager.enabled = false;
                }
                if (chargeDamager != null) {
                    chargeDamager.enabled = false;
                }
                if (teleporter != null) {
                    // Snap alpha back to 1 so the death animation isn't played on an invisible sprite.
                    teleporter.Cancel();
                }
                CancelAllSwordCasts();
                StopMovement();

                DetachShieldDamageable();
                // Both immunity sources are now cleared; reapply so the death animation
                // doesn't play on an invulnerable boss.
                RefreshIgnoreDamage();

                if (G.BossFight != null) {
                    G.BossFight.DisengageBoss();
                }
            }
        }

        private void LateUpdate() {
            UpdateAnimator();
        }

        private void ExecuteCommand() {
            // Attack locks the boss in place for attackHoldDuration. The animation drives the
            // visuals; damage is gated by OnAttackHitStart / OnAttackHitEnd anim events.
            if (isAttacking) {
                UpdateAttack();
                return;
            }

            // Casting locks the boss until OnCastAnimationEnd() fires from the cast clip.
            if (isCasting) {
                StopMovement();
                return;
            }

            // Charge owns velocity during windup/dash — without this gate the per-frame
            // movement command would overwrite the dash velocity. Teleport phase of the
            // charge sets isTeleporting too; movement during teleport is harmless (boss
            // is invisible) so the gate covers all charge phases.
            if (isCharging) {
                return;
            }

            // Chase mode: a pattern is driving velocity each frame via ChaseStep. Same
            // reasoning as isCharging — bail out so ApplyMovement doesn't clobber the
            // pattern's velocity.
            if (isChasing) {
                return;
            }

            if (controlSource == null) {
                StopMovement();
                return;
            }

            VengefulSpiritCommand? command = controlSource.GetCommand();
            if (command == null) {
                StopMovement();
                return;
            }

            VengefulSpiritCommand value = command.Value;

            // While teleporting the boss still processes movement (so it may keep
            // coasting, change direction, or stop), but action flags are skipped.
            // The control source is contracted not to send actions during a
            // teleport; the !isTeleporting gate here is defensive.
            if (!isTeleporting) {
                if (value.Attack) {
                    BeginAttack();
                    return;
                }

                if (value.SpawnShield) {
                    BeginCast(VengefulSpiritCastAction.SpawnShield);
                    return;
                }

                if (value.CastSwords) {
                    BeginCast(VengefulSpiritCastAction.CastSwords);
                    return;
                }

                if (value.Teleport) {
                    BeginTeleport();
                    return;
                }
            }

            ApplyMovement(value.XDirection, value.YDirection);
        }

        private void BeginCast(VengefulSpiritCastAction action) {
            isCasting = true;
            pendingCastAction = action;
            StopMovement();

            if (action == VengefulSpiritCastAction.CastSwords) {
                // Sword cast charge is timed in code, not by the animation event. The
                // animation event is ignored for sword casts; SpawnShield still uses it.
                StopSwordCastChargeRoutine();
                swordCastChargeRoutine = StartCoroutine(SwordCastChargeRoutine());
            }
        }

        private IEnumerator SwordCastChargeRoutine() {
            float duration = Mathf.Max(0f, swordCastChargeDuration);
            if (duration > 0f) {
                yield return new WaitForSeconds(duration);
            }
            swordCastChargeRoutine = null;

            // Cancelled (death cleanup clears pendingCastAction).
            if (pendingCastAction != VengefulSpiritCastAction.CastSwords) {
                yield break;
            }
            pendingCastAction = VengefulSpiritCastAction.None;
            BeginSwordWave();
        }

        private void StopSwordCastChargeRoutine() {
            if (swordCastChargeRoutine != null) {
                StopCoroutine(swordCastChargeRoutine);
                swordCastChargeRoutine = null;
            }
        }

        private void BeginAttack() {
            isAttacking = true;
            attackElapsed = 0f;
            isCasting = false;
            isCastHoldActive = false;
            // Reset the hit-active flag so a new attack starts fresh (it'll flip on at
            // the next OnAttackHitStart anim event, if any).
            isAttackHitActive = false;
            // Defensive: if a prior attack was interrupted before OnAttackHitEnd fired,
            // the damager could still be on. Force it off so the new attack starts with
            // a clean window controlled by anim events.
            if (attackDamager != null) {
                attackDamager.enabled = false;
            }
            // While chasing, the boss keeps its momentum through the strike — the pattern
            // wants the attack to read as a lunge, not a hard stop. Other contexts (blink,
            // ground attack, debug input) still zero velocity so the strike is stationary.
            if (!isChasing) {
                StopMovement();
            }
            myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnAttack);
        }

        private void UpdateAttack() {
            // Three cases:
            // - Not chasing: stationary attack, velocity stays at zero.
            // - Chasing, swing committed (OnAttackHitStart fired): anchor in place so the
            //   strike doesn't slide; pattern's ChaseStep is gated on the same flag.
            // - Chasing, swing not yet committed: keep momentum, pattern drives velocity.
            if (!isChasing || isAttackHitActive) {
                StopMovement();
            }
            attackElapsed += Time.deltaTime;
            if (attackElapsed >= attackHoldDuration) {
                isAttacking = false;
            }
        }

        /// <summary>
        /// Animation event hook. Called from the cast animation at the moment the
        /// queued cast action should take effect. SpawnShield still uses this path;
        /// CastSwords is timed by <see cref="SwordCastChargeRoutine"/> instead, so the
        /// animation event is a no-op for sword casts (the Charge clip loops).
        /// </summary>
        public void OnCastEffect() {
            if (pendingCastAction == VengefulSpiritCastAction.SpawnShield) {
                pendingCastAction = VengefulSpiritCastAction.None;
                SpawnShield();
            }
        }

        // Anchor name used by the input-driven debug cast. AI picks other names via RequestSwordCast.
        private const string DefaultSwordAnchorName = "Default";

        private void BeginSwordWave() {
            // Direct ref wins; fall back to name-based lookup; final fallback is the
            // configured "Default" name (used by the input-driven debug cast).
            SpectralSwordSpawnAnchor anchor = nextSwordAnchorRef;
            nextSwordAnchorRef = null;
            if (anchor == null) {
                string anchorName = !string.IsNullOrEmpty(nextSwordAnchorName)
                    ? nextSwordAnchorName
                    : DefaultSwordAnchorName;
                anchor = GetSwordAnchor(anchorName);
            }
            nextSwordAnchorName = null;

            if (anchor == null) {
                Debug.LogError("[VengefulSpirit] No SpectralSwordSpawnAnchor available for cast. Cast aborted.", this);
                // Wiring missing — never strand the cast in isCasting=true.
                OnSwordCastComplete();
                return;
            }
            anchor.Cast(OnSwordCastComplete);
        }

        /// <summary>
        /// Pattern-facing: trigger a sword cast from a direct anchor reference. Preferred
        /// over the name-based overload — patterns wire their anchors in inspector and
        /// pass refs through, no global lookup table needed.
        /// </summary>
        public void RequestSwordCast(SpectralSwordSpawnAnchor anchor) {
            if (isCasting || isAttacking || isTeleporting || isDead || anchor == null) {
                return;
            }
            nextSwordAnchorRef = anchor;
            nextSwordAnchorName = null;
            BeginCast(VengefulSpiritCastAction.CastSwords);
        }

        /// <summary>
        /// AI-facing (legacy): trigger a sword cast by anchor name. Kept for the
        /// input-driven debug cast and any code still wiring by name. New pattern code
        /// should use the <see cref="RequestSwordCast(SpectralSwordSpawnAnchor)"/> overload.
        /// </summary>
        public void RequestSwordCast(string anchorName) {
            if (isCasting || isAttacking || isTeleporting || isDead) {
                return;
            }
            nextSwordAnchorName = anchorName;
            nextSwordAnchorRef = null;
            BeginCast(VengefulSpiritCastAction.CastSwords);
        }

        /// <summary>
        /// AI-facing: enumerate every wired sword anchor (skipping null bindings).
        /// Caller may inspect each anchor's <c>Position</c> to make situational picks.
        /// </summary>
        public IEnumerable<SpectralSwordAnchorBinding> EnumerateSwordAnchors() {
            if (swordAnchors == null) {
                yield break;
            }
            for (int i = 0; i < swordAnchors.Length; i++) {
                if (swordAnchors[i].anchor != null) {
                    yield return swordAnchors[i];
                }
            }
        }

        /// <summary>
        /// Returns the anchor wired for the given name, or <c>null</c> if no entry matches.
        /// AI / state code calls this to pick a situational anchor by name. Names are
        /// author-defined per boss in the inspector; comparison is case-sensitive. Silent
        /// on miss — callers that intend to fire the cast should log on null.
        /// </summary>
        public SpectralSwordSpawnAnchor GetSwordAnchor(string name) {
            if (swordAnchors == null || string.IsNullOrEmpty(name)) {
                return null;
            }
            for (int i = 0; i < swordAnchors.Length; i++) {
                if (swordAnchors[i].name == name) {
                    return swordAnchors[i].anchor;
                }
            }
            return null;
        }

        private void CancelAllSwordCasts() {
            if (swordAnchors == null) {
                return;
            }
            for (int i = 0; i < swordAnchors.Length; i++) {
                SpectralSwordSpawnAnchor a = swordAnchors[i].anchor;
                if (a != null) {
                    a.CancelActiveCast();
                }
            }
        }

        private void OnSwordCastComplete() {
            isCasting = false;
            pendingCastAction = VengefulSpiritCastAction.None;
        }

        private void BeginTeleport() {
            BeginTeleportTo(PickTeleportDestination(), -1f);
        }

        private void BeginTeleportTo(TeleportAnchor target, float hiddenDurationOverride) {
            if (target == null || teleporter == null) {
                // Wiring missing — silently no-op rather than locking the boss.
                return;
            }

            isTeleporting = true;
            // Velocity is intentionally NOT reset — the spirit may keep coasting at its
            // current speed during the fade-out, hidden hold, and fade-in.

            // IgnoreDamage is NOT flipped here — the boss can still take a final
            // hit during the first slice of the fade-out. The teleporter calls
            // OnTeleportDamageGraceElapsed() once the grace window expires.
            FacingDir destFacing = target.FacingDir;
            teleporter.Run(
                target.transform.position,
                OnTeleportDamageGraceElapsed,
                () => facing.SetDir(destFacing),
                OnTeleportComplete,
                hiddenDurationOverride);

            // Pause the shield's animator so its idle clip doesn't overwrite the fade
            // alpha, AND make the shield ignore damage for the duration — avoids the
            // shield shattering mid-fade while the boss is invisible.
            SetShieldTeleportSuspended(true);
        }

        /// <summary>
        /// AI-facing: trigger the teleport sequence to the specified anchor, bypassing
        /// the default random selection. Early-returns if the boss is currently busy.
        /// <paramref name="hiddenDurationOverride"/> overrides the teleporter's configured
        /// hidden hold for this single call (negative = use default).
        /// </summary>
        public void RequestTeleport(TeleportAnchor target, float hiddenDurationOverride = -1f) {
            if (isCasting || isAttacking || isTeleporting || isDead) {
                return;
            }
            BeginTeleportTo(target, hiddenDurationOverride);
        }

        /// <summary>
        /// Pattern-facing: fade the boss out where it stands and leave it invisible. No
        /// reposition, no fade-in. The boss stays damage-immune until the next teleport
        /// fades it back in. Used by scripted sequences (e.g. the phase-2 opener cutscene)
        /// that want the skeleton to vanish at the end without choosing an exit anchor.
        /// </summary>
        public void RequestFadeOutInPlace() {
            if (isCasting || isAttacking || isTeleporting || isDead || teleporter == null) {
                return;
            }
            isTeleporting = true;
            teleporter.FadeOutInPlace(
                OnTeleportDamageGraceElapsed,
                OnFadeOutInPlaceComplete);
            SetShieldTeleportSuspended(true);
        }

        /// <summary>
        /// AI-facing: trigger the teleport sequence to a runtime-computed world position
        /// (instead of an anchor). Used by patterns that pick the destination dynamically
        /// — e.g. BlinkAttackPattern computing a "behind the player" spot.
        /// </summary>
        public void RequestTeleport(Vector3 position, FacingDir destFacing) {
            if (isCasting || isAttacking || isTeleporting || isDead || teleporter == null) {
                return;
            }
            isTeleporting = true;
            teleporter.Run(
                position,
                OnTeleportDamageGraceElapsed,
                () => facing.SetDir(destFacing),
                OnTeleportComplete);
            SetShieldTeleportSuspended(true);
        }

        /// <summary>
        /// AI-facing: trigger a common melee attack synchronously. Equivalent to pulsing
        /// the command flag <see cref="VengefulSpiritCommand.Attack"/> on the next frame,
        /// but safe to call without worrying about Update execution order. Early-returns
        /// if the boss is currently busy.
        /// </summary>
        public void RequestCommonAttack() {
            if (IsBusy || isDead) {
                return;
            }
            BeginAttack();
        }

        /// <summary>
        /// AI-facing: orient the boss toward the given world X coordinate. Used by patterns
        /// to lock facing toward the player at the moment of a strike, in case the player
        /// has moved since the boss was repositioned. No-op when the target X equals the
        /// boss's own X.
        /// </summary>
        public void FaceTowards(float worldX) {
            facing.SetByX(worldX - transform.position.x);
        }

        /// <summary>
        /// Pattern-facing: enter chase mode. While chasing, the boss ignores the control
        /// source's movement command — the pattern is responsible for driving velocity each
        /// frame via <see cref="ChaseStep"/>. Attacks (RequestCommonAttack) still fire during
        /// chase; the pattern can interleave strikes and resume chasing afterwards. Always
        /// pair with <see cref="EndChase"/> before despawning or returning from the pattern.
        /// </summary>
        public void BeginChase() {
            if (isDead || IsBusy) {
                return;
            }
            isChasing = true;
        }

        /// <summary>
        /// Pattern-facing: drive velocity in the given world-space direction at the given
        /// speed for one frame. The direction is normalized internally; pass the raw
        /// (toPlayer) vector. Updates facing from the X component so the boss looks where
        /// it's going. No-op if not currently in chase mode or boss is dead.
        /// </summary>
        public void ChaseStep(Vector2 direction, float speed) {
            if (!isChasing || isDead) {
                return;
            }
            // Swing has committed (OnAttackHitStart fired). Hold the freeze that
            // OnAttackHitStart established — the strike must not slide.
            if (isAttackHitActive) {
                return;
            }
            if (direction.sqrMagnitude < 0.0001f) {
                myRigidbody.velocity = Vector2.zero;
                return;
            }
            Vector2 dir = direction.normalized;
            myRigidbody.velocity = dir * speed;
            if (Mathf.Abs(dir.x) > 0.01f) {
                facing.SetByX(dir.x);
            }
        }

        /// <summary>
        /// Pattern-facing: leave chase mode and zero velocity. Safe to call when not chasing.
        /// </summary>
        public void EndChase() {
            if (!isChasing) {
                return;
            }
            isChasing = false;
            if (!isDead) {
                StopMovement();
            }
        }

        /// <summary>
        /// AI-facing: trigger a spectral-shield cast synchronously. Goes through the same
        /// cast lifecycle as the command-driven path. Early-returns if the boss is busy
        /// or already has an active shield.
        /// </summary>
        public void RequestSpawnShield() {
            if (IsBusy || isDead || HasActiveShield) {
                return;
            }
            BeginCast(VengefulSpiritCastAction.SpawnShield);
        }

        /// <summary>
        /// Pattern-facing: enter the casting pose without arming an automatic shield spawn.
        /// The boss stays in the casting loop indefinitely until the pattern calls
        /// <see cref="RequestFinishShieldCastAndSpawn"/>. Used by scripted sequences that
        /// want to control the casting → spawn timing in code instead of via animation events.
        /// </summary>
        public void RequestBeginShieldCast() {
            if (IsBusy || isDead || HasActiveShield) {
                return;
            }
            isCasting = true;
            isCastHoldActive = true;
            pendingCastAction = VengefulSpiritCastAction.None;
            StopMovement();
        }

        /// <summary>
        /// Pattern-facing: end the held casting pose, instantiate the shield (its own
        /// Spawn animation plays), and release the cast lock. Pairs with
        /// <see cref="RequestBeginShieldCast"/>.
        /// </summary>
        public void RequestFinishShieldCastAndSpawn() {
            if (isDead) {
                return;
            }
            if (!HasActiveShield) {
                SpawnShield();
            }
            isCasting = false;
            isCastHoldActive = false;
            pendingCastAction = VengefulSpiritCastAction.None;
        }

        /// <summary>
        /// AI-facing: run the full charge-attack sequence — teleport to <paramref name="from"/>,
        /// hold the windup (ChargeAttack1) for <c>chargeWindUpDuration</c>, dash horizontally
        /// to <paramref name="to"/> with the dash damager active (ChargeAttack2), then idle
        /// for <c>chargeIdleDuration</c>. Early-returns if the boss is busy or either anchor
        /// is null. The dash damager is OFF during the windup — the boss never damages the
        /// player before the dash actually begins.
        /// </summary>
        public void RequestChargeAttack(TeleportAnchor from, TeleportAnchor to) {
            if (IsBusy || isDead || from == null || to == null || teleporter == null) {
                return;
            }
            StopChargeRoutine();
            chargeRoutine = StartCoroutine(ChargeAttackRoutine(from, to));
        }

        // Drives the full charge sequence as a single coroutine so cancellation on death
        // collapses to one StopCoroutine call. The four phases of the sequence are gated
        // by isCharging being true throughout, which keeps IsBusy true so the strategic
        // layer cannot start another pattern in parallel.
        private IEnumerator ChargeAttackRoutine(TeleportAnchor from, TeleportAnchor to) {
            isCharging = true;

            // Phase 1: teleport to start anchor. Reuses the standard teleport path so the
            // damage-grace + shield-fader behavior is identical to a normal teleport.
            // The at-destination callback sets facing toward the dash target — overrides
            // the start anchor's authored facingDir, which is irrelevant for charge since
            // the boss must face the dash direction by construction.
            isTeleporting = true;
            bool teleportDone = false;
            int dashDirSign = to.transform.position.x > from.transform.position.x ? 1 : -1;
            teleporter.Run(
                from.transform.position,
                OnTeleportDamageGraceElapsed,
                () => facing.SetByX(dashDirSign),
                () => { teleportDone = true; });
            SetShieldTeleportSuspended(true);
            while (!teleportDone) {
                if (isDead) {
                    yield break;
                }
                yield return null;
            }
            // Mirror OnTeleportComplete inline rather than calling it — we do not want
            // isCharging cleared here; only isTeleporting flips off.
            isTeleporting = false;
            isTeleportImmune = false;
            RefreshIgnoreDamage();
            SetShieldTeleportSuspended(false);

            StopMovement();

            // Phase 2: ChargeAttack1 windup. Damager stays OFF — the player must not be
            // damaged before the dash actually begins.
            myAnimator.SetBool(VengefulSpiritAnimKeys.IsChargingAttack, true);
            float t = 0f;
            while (t < chargeWindUpDuration) {
                if (isDead) {
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            // Phase 3: ChargeAttack2 dash. Flip the bool, enable the dash damager,
            // drive horizontal velocity until either the destination X is reached or the
            // safety cap fires.
            myAnimator.SetBool(VengefulSpiritAnimKeys.IsChargingAttack, false);
            if (chargeDamager != null) {
                chargeDamager.enabled = true;
            }
            float targetX = to.transform.position.x;
            float dashDir = Mathf.Sign(targetX - transform.position.x);
            float dashStart = Time.time;
            while (Time.time - dashStart < chargeMaxDashDuration) {
                if (isDead) {
                    if (chargeDamager != null) {
                        chargeDamager.enabled = false;
                    }
                    yield break;
                }
                float curX = transform.position.x;
                if ((dashDir > 0f && curX >= targetX) || (dashDir < 0f && curX <= targetX)) {
                    break;
                }
                myRigidbody.velocity = new Vector2(dashDir * chargeDashSpeed, 0f);
                yield return null;
            }
            if (chargeDamager != null) {
                chargeDamager.enabled = false;
            }
            StopMovement();

            // Phase 4: idle hold so the player has a beat to react before the next pattern.
            t = 0f;
            while (t < chargeIdleDuration) {
                if (isDead) {
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            isCharging = false;
            chargeRoutine = null;
        }

        // Cancellation entry point for death cleanup. Safe to call when no charge is in
        // flight. Always clears the animator bool so the boss does not freeze in
        // ChargeAttack1 if killed during windup.
        private void StopChargeRoutine() {
            if (chargeRoutine != null) {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }
            if (chargeDamager != null) {
                chargeDamager.enabled = false;
            }
            if (myAnimator != null) {
                myAnimator.SetBool(VengefulSpiritAnimKeys.IsChargingAttack, false);
            }
            isCharging = false;
        }

        /// <summary>
        /// Animation event hook on Attack.anim. Enables the attack damager at the start
        /// of the active hit frames so the player only takes damage during the controlled
        /// hit window, not for the entire attack animation. No-op if no damager is wired
        /// (legacy contact-thrust behavior).
        ///
        /// While chasing, this also anchors the boss in place for the remainder of the
        /// strike — the swing has committed, so the pattern stops tracking the player.
        /// </summary>
        public void OnAttackHitStart() {
            if (attackDamager != null && !isDead) {
                attackDamager.enabled = true;
            }
            if (isChasing) {
                isAttackHitActive = true;
                StopMovement();
            }
        }

        /// <summary>
        /// Animation event hook on Attack.anim. Closes the attack damage window. Always
        /// safe to call — also serves as a defensive "off" if the death cleanup raced the
        /// end-of-attack event.
        /// </summary>
        public void OnAttackHitEnd() {
            if (attackDamager != null) {
                attackDamager.enabled = false;
            }
            isAttackHitActive = false;
        }

        /// <summary>
        /// AI-facing: enumerate every wired teleport anchor (skipping null bindings).
        /// Caller may inspect each anchor's transform to make situational picks
        /// (closest / farthest from a target, behind player, etc.).
        /// </summary>
        public IEnumerable<TeleportAnchorBinding> EnumerateTeleportAnchors() {
            if (teleportAnchors == null) {
                yield break;
            }
            for (int i = 0; i < teleportAnchors.Length; i++) {
                if (teleportAnchors[i].anchor != null) {
                    yield return teleportAnchors[i];
                }
            }
        }

        private void OnTeleportDamageGraceElapsed() {
            isTeleportImmune = true;
            RefreshIgnoreDamage();
        }

        private void OnTeleportComplete() {
            isTeleporting = false;
            isTeleportImmune = false;
            RefreshIgnoreDamage();
            SetShieldTeleportSuspended(false);
        }

        // After fade-out-in-place: boss is at alpha 0 and no longer in transit, but stays
        // damage-immune (and the shield stays suspended) until the next teleport fades
        // the boss back in. The next teleport's OnTeleportComplete clears both flags.
        private void OnFadeOutInPlaceComplete() {
            isTeleporting = false;
            RefreshIgnoreDamage();
        }

        // Composes shield + teleport + shield-destruction immunity into Damageable.IgnoreDamage.
        // Sources can overlap (e.g., a teleport that starts while a shield is active), so
        // neither state can write IgnoreDamage directly without potentially clearing the
        // other's effect.
        private void RefreshIgnoreDamage() {
            if (damageable == null) {
                return;
            }
            damageable.IgnoreDamage = HasActiveShield || isTeleportImmune || isShieldDestructionRecovery;
        }

        /// <summary>
        /// Returns the teleport anchor wired for the given name, or <c>null</c> if no
        /// entry matches. Case-sensitive. AI / state code uses this to pick a specific
        /// destination; the input-driven path falls back to <see cref="PickTeleportDestination"/>.
        /// </summary>
        public TeleportAnchor GetTeleportAnchor(string name) {
            if (teleportAnchors == null || string.IsNullOrEmpty(name)) {
                return null;
            }
            for (int i = 0; i < teleportAnchors.Length; i++) {
                if (teleportAnchors[i].name == name) {
                    return teleportAnchors[i].anchor;
                }
            }
            return null;
        }

        // Picks a random anchor that is NOT the closest one to the boss's current position.
        // Falls back to the closest anchor if it is the only one wired.
        private TeleportAnchor PickTeleportDestination() {
            if (teleportAnchors == null || teleportAnchors.Length == 0) {
                return null;
            }

            int closestIndex = -1;
            float closestSqr = float.PositiveInfinity;
            for (int i = 0; i < teleportAnchors.Length; i++) {
                TeleportAnchor a = teleportAnchors[i].anchor;
                if (a == null) {
                    continue;
                }
                float d = (a.transform.position - transform.position).sqrMagnitude;
                if (d < closestSqr) {
                    closestSqr = d;
                    closestIndex = i;
                }
            }

            int candidateCount = 0;
            for (int i = 0; i < teleportAnchors.Length; i++) {
                if (i == closestIndex) {
                    continue;
                }
                if (teleportAnchors[i].anchor != null) {
                    candidateCount++;
                }
            }

            if (candidateCount == 0) {
                return closestIndex >= 0 ? teleportAnchors[closestIndex].anchor : null;
            }

            int pick = UnityEngine.Random.Range(0, candidateCount);
            int seen = 0;
            for (int i = 0; i < teleportAnchors.Length; i++) {
                if (i == closestIndex) {
                    continue;
                }
                TeleportAnchor a = teleportAnchors[i].anchor;
                if (a == null) {
                    continue;
                }
                if (seen == pick) {
                    return a;
                }
                seen++;
            }

            return null;
        }

        /// <summary>
        /// Animation event hook. Called at the end of the cast animation. Releases
        /// the cast lock so the boss can act again.
        ///
        /// Sword casts are timed by <see cref="SwordCastChargeRoutine"/>, which holds
        /// the cast lifecycle for <c>swordCastChargeDuration</c> regardless of clip
        /// length. The Charge clip loops, and its animation event fires every loop —
        /// if we let it clear the cast state here, the in-flight charge coroutine
        /// would wake up to see <c>pendingCastAction = None</c> and bail without
        /// spawning swords. So while a sword cast is charging, this hook is a no-op.
        /// Shield casts still flow through normally.
        /// </summary>
        public void OnCastAnimationEnd() {
            if (swordCastChargeRoutine != null) {
                return;
            }
            // Pattern is holding the casting pose — only RequestFinishShieldCastAndSpawn
            // ends it, so leave the state untouched.
            if (isCastHoldActive) {
                return;
            }
            isCasting = false;
            pendingCastAction = VengefulSpiritCastAction.None;
        }

        private void SpawnShield() {
            if (shieldPrefab == null || shieldSpawnPoint == null) {
                return;
            }

            // Physical restriction: only one shield may be active at a time. The AI guards
            // against this in CanSpawnShield, but the cast lifecycle has its own
            // entry point via OnCastEffect, so re-check here defensively.
            if (HasActiveShield) {
                return;
            }

            // Spawn unparented and follow via TransformFollow2D. Parenting under the boss
            // would mirror the shield sprite when the boss faces left (its localScale.x = -1),
            // and the shield's reflection must always render from the same side.
            GameObject instance = Instantiate(shieldPrefab, shieldSpawnPoint.position, Quaternion.identity);
            instance.AddComponent<TransformFollow2D>().Target = shieldSpawnPoint;

            currentShieldInstance = instance;
            currentShieldDamageable = instance.GetComponent<Damageable>();
            if (currentShieldDamageable != null) {
                currentShieldDamageable.OnHealthChanged += HandleShieldHealthChanged;
                if (G.BossFight != null) {
                    G.BossFight.EngageShield(currentShieldDamageable);
                }
            }

            // Register every SpriteRenderer on the shield so it fades in/out together with
            // the boss during teleports. The shield is destroyed before any next teleport
            // would otherwise re-touch it; UnregisterFader is also called on detach.
            currentShieldRenderers = instance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            if (teleporter != null && currentShieldRenderers != null) {
                for (int i = 0; i < currentShieldRenderers.Length; i++) {
                    teleporter.RegisterFader(currentShieldRenderers[i]);
                }
            }

            // Cache animators so we can pause them during teleport (see field comment).
            currentShieldAnimators = instance.GetComponentsInChildren<Animator>(includeInactive: true);

            RefreshIgnoreDamage();
        }

        private void HandleShieldHealthChanged(float newHealth) {
            if (newHealth > 0f) {
                return;
            }

            // Capture the shield's GameObject before detaching — DetachShieldDamageable
            // tears down our references, but the recovery sequence still needs to know
            // when the shield's destroy animation actually finishes (Destroy(gameObject)
            // is fired by an OnDestroyAnimExit anim event on the Destroy clip).
            GameObject instanceToWatch = currentShieldInstance;
            currentShieldInstance = null;

            DetachShieldDamageable();
            if (G.BossFight != null) {
                G.BossFight.DisengageShield();
            }

            // Stop everything in flight, become invulnerable, and run the shield-shatter
            // → boss-flinch sequence. RefreshIgnoreDamage is called from inside Begin/End.
            BeginShieldDestructionRecovery();
            if (shieldRecoveryRoutine != null) {
                StopCoroutine(shieldRecoveryRoutine);
            }
            shieldRecoveryRoutine = StartCoroutine(ShieldRecoverySequence(instanceToWatch));

            OnShieldDestroyed?.Invoke();
        }

        // Cancels everything the boss had in flight and locks it into the recovery state.
        // Damager flags are explicitly forced off so a coroutine that was killed mid-window
        // can't leave them on. The animator's charge bool is also cleared so a death cleanup
        // racing with this routine doesn't freeze the visual on ChargeAttack1.
        private void BeginShieldDestructionRecovery() {
            StopChargeRoutine();
            StopSwordCastChargeRoutine();
            isCasting = false;
            isCastHoldActive = false;
            pendingCastAction = VengefulSpiritCastAction.None;
            isAttacking = false;
            attackElapsed = 0f;
            isAttackHitActive = false;
            isChasing = false;
            StopMovement();
            if (attackDamager != null) {
                attackDamager.enabled = false;
            }
            if (chargeDamager != null) {
                chargeDamager.enabled = false;
            }
            if (myAnimator != null) {
                myAnimator.SetBool(VengefulSpiritAnimKeys.IsChargingAttack, false);
            }

            isShieldDestructionRecovery = true;
            RefreshIgnoreDamage();
        }

        private IEnumerator ShieldRecoverySequence(GameObject shieldInstance) {
            // Phase 1: wait for the shield's destroy clip to finish — the prefab's onDeath
            // UnityEvent triggers the Destroy clip which ends with Destroy(gameObject) via
            // OnDestroyAnimExit. Unity's overloaded null check flips when the GameObject
            // is actually destroyed.
            float deadline = Time.time + shieldDestroyAnimMaxDuration;
            while (shieldInstance != null && Time.time < deadline) {
                if (isDead) {
                    EndShieldDestructionRecovery();
                    yield break;
                }
                yield return null;
            }
            if (isDead) {
                EndShieldDestructionRecovery();
                yield break;
            }

            // Phase 2: forced flinch. The OnHit trigger drives the existing hit anim; the
            // boss is invulnerable so the player can't chain a real hit on top.
            if (myAnimator != null) {
                myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnHit);
            }
            float t = 0f;
            while (t < shieldDestroyHitReactionDuration) {
                if (isDead) {
                    break;
                }
                t += Time.deltaTime;
                yield return null;
            }

            EndShieldDestructionRecovery();
        }
        
        public void OnBeforeDie() {
            myRigidbody.gravityScale = 1f;
        }

        private void EndShieldDestructionRecovery() {
            isShieldDestructionRecovery = false;
            shieldRecoveryRoutine = null;
            RefreshIgnoreDamage();
        }

        private void DetachShieldDamageable() {
            if (currentShieldDamageable != null) {
                currentShieldDamageable.OnHealthChanged -= HandleShieldHealthChanged;
                currentShieldDamageable = null;
            }

            if (currentShieldRenderers != null) {
                if (teleporter != null) {
                    for (int i = 0; i < currentShieldRenderers.Length; i++) {
                        teleporter.UnregisterFader(currentShieldRenderers[i]);
                    }
                }
                currentShieldRenderers = null;
            }

            // Re-enable any animator we paused for a teleport before letting go of the
            // reference. Otherwise a shield killed while the boss was mid-teleport would
            // be left with its Animator disabled — its onDestroy trigger fires but the
            // Destroy clip never runs, OnDestroyAnimExit never executes, the GameObject
            // never gets Destroy()ed, and the shield lingers in the scene.
            if (currentShieldAnimators != null) {
                for (int i = 0; i < currentShieldAnimators.Length; i++) {
                    Animator a = currentShieldAnimators[i];
                    if (a != null) {
                        a.enabled = true;
                    }
                }
                currentShieldAnimators = null;
            }
        }

        // Toggles the shield's animators. Called at teleport start/end so the fade isn't
        // overwritten by Idle-clip default values. Safe to call when no shield is active.
        private void SetShieldAnimatorsEnabled(bool isEnabled) {
            if (currentShieldAnimators == null) {
                return;
            }
            for (int i = 0; i < currentShieldAnimators.Length; i++) {
                Animator a = currentShieldAnimators[i];
                if (a != null) {
                    a.enabled = isEnabled;
                }
            }
        }

        // Single switch for both consequences of "boss is teleporting":
        // - shield animator stays paused so the teleporter's fade controls alpha,
        // - shield ignores damage so the player can't destroy it during the fade-out
        //   (avoids the awkward case where the shield shatters while the boss is invisible).
        // Called at every teleport boundary in place of the bare animator toggle.
        private void SetShieldTeleportSuspended(bool suspended) {
            SetShieldAnimatorsEnabled(!suspended);
            if (currentShieldDamageable != null) {
                currentShieldDamageable.IgnoreDamage = suspended;
            }
        }

        private void ApplyMovement(int xDir, int yDir) {
            Vector2 input = new Vector2(xDir, yDir);
            if (input.sqrMagnitude > 1f) {
                input.Normalize();
            }

            myRigidbody.velocity = input * moveSpeed;

            if (xDir != 0) {
                facing.SetByX(xDir);
            }
        }

        private void StopMovement() {
            myRigidbody.velocity = Vector2.zero;
        }

        private void UpdateAnimator() {
            myAnimator.SetBool(VengefulSpiritAnimKeys.IsCasting, isCasting);

            if (wasHitThisFrame) {
                wasHitThisFrame = false;
                myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnHit);
            }

            if (diedThisFrame) {
                diedThisFrame = false;
                myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnDeath);
            }
        }
    }
}