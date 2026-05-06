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

namespace Game.Features.Bosses.VengefulSpirit {
    internal static class VengefulSpiritAnimKeys {
        public static readonly int IsCharging = Animator.StringToHash("isCharging");
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
        [SerializeField]
        private float attackThrustSpeed = 10f;

        [Tooltip("How long the boss thrusts forward during an attack. Should match the Attack animation length.")]
        [SerializeField]
        private float attackThrustDuration = 1.2f;

        [Tooltip("Time to ramp thrust speed from the boss's current speed up to attackThrustSpeed.")]
        [SerializeField]
        private float attackThrustRampUpDuration = 0.2f;

        [Tooltip("How quickly the boss decelerates after the thrust ends. Higher = faster stop.")]
        [SerializeField]
        private float attackStopDamping = 8f;

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

        private Rigidbody2D myRigidbody;
        private Animator myAnimator;
        private Facing2D facing;
        private Damageable damageable;
        private Damageable currentShieldDamageable;
        private SpriteRenderer[] currentShieldRenderers;
        // The shield's animator clips use WriteDefaultValues, so the idle clip overwrites
        // the SpriteRenderer alpha every frame. We disable these for the duration of a
        // teleport so the teleporter's fade is the sole driver of the shield's alpha.
        private Animator[] currentShieldAnimators;
        // Composed inputs to Damageable.IgnoreDamage. Both states layer onto each other —
        // a teleport that begins while a shield is active must keep the boss immune even
        // after the teleport ends, and vice-versa.
        private bool isTeleportImmune;

        private bool isCasting;
        private VengefulSpiritCastAction pendingCastAction;
        // Anchor name to use for the next sword cast. AI sets this via RequestSwordCast;
        // unset means the cast falls back to DefaultSwordAnchorName.
        private string nextSwordAnchorName;
        // Sword casts run their own charge timer instead of using the animation event so
        // the telegraph length is tunable. Held here for cancellation on death.
        private Coroutine swordCastChargeRoutine;
        private bool isAttacking;
        private bool isAttackDecelerating;
        private float attackElapsed;
        private float attackInitialSpeed;
        private bool isTeleporting;
        private bool isDead;
        private bool wasHitThisFrame;
        private bool diedThisFrame;

        public bool IsAttacking => isAttacking;
        public bool IsCasting => isCasting;
        public bool IsTeleporting => isTeleporting;
        public bool IsBusy => isAttacking || isCasting || isTeleporting;
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
            DetachShieldDamageable();
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
                pendingCastAction = VengefulSpiritCastAction.None;
                StopSwordCastChargeRoutine();
                isAttacking = false;
                isAttackDecelerating = false;
                attackElapsed = 0f;
                isTeleporting = false;
                isTeleportImmune = false;
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
            // Attack locks the boss into a self-driven thrust for attackThrustDuration,
            // then UpdateAttackThrust() damps velocity back to zero.
            if (isAttacking) {
                UpdateAttackThrust();
                return;
            }

            // Casting locks the boss until OnCastAnimationEnd() fires from the cast clip.
            if (isCasting) {
                StopMovement();
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
            isAttackDecelerating = false;
            attackElapsed = 0f;
            attackInitialSpeed = myRigidbody.velocity.magnitude;
            isCasting = false;
            myAnimator.SetTrigger(VengefulSpiritAnimKeys.OnAttack);
            myRigidbody.velocity = facing.DirVector * attackInitialSpeed;
        }

        private void UpdateAttackThrust() {
            if (!isAttackDecelerating) {
                attackElapsed += Time.deltaTime;
                // Ramp from the speed the boss had when the attack began up to attackThrustSpeed,
                // then hold max speed. Driving velocity directly each frame keeps gravity out of the thrust.
                float speed = attackThrustSpeed;
                if (attackThrustRampUpDuration > 0f && attackElapsed < attackThrustRampUpDuration) {
                    float t = attackElapsed / attackThrustRampUpDuration;
                    speed = Mathf.Lerp(attackInitialSpeed, attackThrustSpeed, t);
                }
                myRigidbody.velocity = facing.DirVector * speed;
                if (attackElapsed >= attackThrustDuration) {
                    isAttackDecelerating = true;
                }
                return;
            }

            // Damp velocity toward zero — completes the attack with a smooth stop.
            Vector2 v = Vector2.Lerp(myRigidbody.velocity, Vector2.zero, attackStopDamping * Time.deltaTime);
            myRigidbody.velocity = v;

            if (v.sqrMagnitude < 0.0001f) {
                myRigidbody.velocity = Vector2.zero;
                isAttacking = false;
                isAttackDecelerating = false;
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
            string anchorName = !string.IsNullOrEmpty(nextSwordAnchorName)
                ? nextSwordAnchorName
                : DefaultSwordAnchorName;
            nextSwordAnchorName = null;

            SpectralSwordSpawnAnchor anchor = GetSwordAnchor(anchorName);
            if (anchor == null) {
                Debug.LogError($"[VengefulSpirit] No SpectralSwordSpawnAnchor wired for name '{anchorName}'. Cast aborted — check the Sword Anchors array on this boss.", this);
                // Wiring missing — never strand the cast in isCasting=true.
                OnSwordCastComplete();
                return;
            }
            anchor.Cast(OnSwordCastComplete);
        }

        /// <summary>
        /// AI-facing: trigger a sword cast that fires from the named anchor instead of the
        /// default. Goes through the same charge-and-animate cast lifecycle as the
        /// command-driven cast — early-returns if the boss is currently busy.
        /// </summary>
        public void RequestSwordCast(string anchorName) {
            if (isCasting || isAttacking || isTeleporting || isDead) {
                return;
            }
            nextSwordAnchorName = anchorName;
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
            BeginTeleportTo(PickTeleportDestination());
        }

        private void BeginTeleportTo(TeleportAnchor target) {
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
            teleporter.Run(target.transform.position, OnTeleportDamageGraceElapsed, OnTeleportComplete);

            // Pause the shield's animator so its idle clip doesn't overwrite the fade alpha.
            SetShieldAnimatorsEnabled(false);
        }

        /// <summary>
        /// AI-facing: trigger the teleport sequence to the specified anchor, bypassing
        /// the default random selection. Early-returns if the boss is currently busy.
        /// </summary>
        public void RequestTeleport(TeleportAnchor target) {
            if (isCasting || isAttacking || isTeleporting || isDead) {
                return;
            }
            BeginTeleportTo(target);
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
            SetShieldAnimatorsEnabled(true);
        }

        // Composes shield + teleport immunity into Damageable.IgnoreDamage. Both sources can
        // overlap (e.g., a teleport that starts while a shield is active), so neither state
        // can write IgnoreDamage directly without potentially clearing the other's effect.
        private void RefreshIgnoreDamage() {
            if (damageable == null) {
                return;
            }
            damageable.IgnoreDamage = HasActiveShield || isTeleportImmune;
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

            DetachShieldDamageable();
            RefreshIgnoreDamage();
            if (G.BossFight != null) {
                G.BossFight.DisengageShield();
            }
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

            currentShieldAnimators = null;
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
            myAnimator.SetBool(VengefulSpiritAnimKeys.IsCharging, isCasting);

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