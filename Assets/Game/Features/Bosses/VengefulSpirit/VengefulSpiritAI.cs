using System.Collections;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Features.Bosses.VengefulSpirit.SpectralSwords;
using Game.Features.Bosses.VengefulSpirit.Teleport;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit {
    public enum VengefulSpiritPhase {
        One,
        Two
    }

    /// <summary>
    /// Two-phase AI control source for the Vengeful Spirit boss.
    /// Behavior is documented at design level in
    /// <c>Assets/Docs/Planning/2026-04-30-vengeful-spirit-behavior.md</c>;
    /// this comment covers <em>how the implementation works</em>.
    ///
    /// <para>
    /// <b>Integration with the boss.</b> This component is a
    /// <see cref="VengefulSpiritControlSource"/>: every frame the boss's
    /// <see cref="VengefulSpirit"/> calls <see cref="GetCommand"/> to pull a
    /// movement direction and three one-shot trigger flags
    /// (Attack / SpawnShield / CastSwords / Teleport — the AI uses the first two
    /// only). Sword casts and teleports needing a <em>specific</em> anchor go
    /// directly through <see cref="VengefulSpirit.RequestSwordCast"/> and
    /// <see cref="VengefulSpirit.RequestTeleport"/>; the command-flag path was
    /// kept for the debug input source which always fires from the "Default"
    /// sword anchor / a random teleport target.
    /// </para>
    ///
    /// <para>
    /// <b>Three concurrent concerns drive the AI each frame:</b>
    /// <list type="number">
    /// <item><description>
    ///   <b>Action picker.</b> A cadence timer (<c>nextActionTime</c>) gates how
    ///   often a new beat starts. When it elapses and the boss is idle, an
    ///   <see cref="IEnumerator"/> coroutine runs (single beat in phase 1,
    ///   multi-beat combo in phase 2). The picker is a cascade of weighted
    ///   gates — every gate that fails falls through to the next, and the
    ///   final fall-through is always a sword cast (phase 1) or sword wall
    ///   (phase 2). That is what makes sword casts the "default" beat.
    /// </description></item>
    /// <item><description>
    ///   <b>Movement.</b> A separate timer
    ///   (<c>nextMovementDecisionTime</c>) runs on a longer interval (2-4 s by
    ///   default). At each decision the AI picks a short "burst" — a direction
    ///   held for ~0.5 s — or "stand still" for the whole interval. Most
    ///   decisions in the comfortable distance band resolve to standing still,
    ///   which produces the long pauses the design calls for. Action
    ///   coroutines that need to commit to an approach (Reposition+Slash,
    ///   Shield Bash) raise <c>forceMoveActive</c> to override the burst
    ///   system temporarily.
    /// </description></item>
    /// <item><description>
    ///   <b>Phase &amp; reactivity.</b> <see cref="UpdatePhase"/> watches HP
    ///   and triggers a one-shot phase-shift sequence at the configured HP
    ///   fraction (forced teleport to <c>centerAnchorName</c> + sword wave
    ///   from there). <see cref="TryReactiveTeleport"/> runs every phase-2
    ///   frame and may interrupt idle drift if the player is in melee range
    ///   <em>and</em> has just hit the boss.
    /// </description></item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Pulse pattern for one-shot commands.</b> Pressing "Attack" via the
    /// command struct must be a single-frame edge — set the flag, the boss
    /// reads it, clear it. The AI does this by setting <c>pendingAttack</c> /
    /// <c>pendingSpawnShield</c> at the moment of decision (typically inside
    /// a coroutine), letting <see cref="Update"/> emit the command this
    /// frame, then unconditionally clearing the flags at the end of
    /// <see cref="Update"/>. Coroutines that need to wait for the boss to
    /// react use <see cref="WaitForBusyCycle"/> which waits first for
    /// <c>boss.IsBusy</c> to become true, then for it to clear — robust to
    /// either Update execution order between the AI and the boss.
    /// </para>
    ///
    /// <para>
    /// <b>Cooldown model.</b> Three parallel cooldowns keep the fight
    /// readable:
    /// <list type="bullet">
    /// <item><description><b>Melee</b> (<c>meleeCooldown</c>): gates
    /// Reposition+Slash, Teleport-Strike, and Shield Bash; <em>also</em>
    /// gates chasing in the burst-movement system, so the boss has no
    /// reason to close distance while it cannot punish.</description></item>
    /// <item><description><b>Shield</b> (<c>shieldRespawnCooldown</c>):
    /// minimum time between the destruction of one shield and the cast of
    /// another. While a shield is alive, a new cast is blocked outright.</description></item>
    /// <item><description><b>Reactive teleport</b>
    /// (<c>phase2ReactiveTeleportCooldown</c>): floor on how often the
    /// hit-triggered escape can fire, so a stunlocked player still gets
    /// readable spacing.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    [RequireComponent(typeof(VengefulSpirit))]
    public class VengefulSpiritAI : VengefulSpiritControlSource {
        [Header("Wiring")]
        [SerializeField]
        private VengefulSpirit boss;

        [Tooltip("Anchor the boss is forced to teleport to during the phase 1 -> 2 transition.")]
        [SerializeField]
        private string centerAnchorName = "Center";

        [Header("Phase Trigger")]
        [Tooltip("HP fraction (0..1) at which the fight enters phase 2.")]
        [SerializeField, Range(0f, 1f)]
        private float phaseShiftHealthFraction = 0.5f;

        [Header("Phase 1")]
        [Tooltip("Minimum time between phase 1 actions.")]
        [SerializeField]
        private float phase1Cadence = 2.0f;

        [Tooltip("HP fraction at or below which phase 1 may spawn a shield.")]
        [SerializeField, Range(0f, 1f)]
        private float phase1ShieldHealthThreshold = 0.8f;

        [Tooltip("Distance under which the player counts as crowding the boss (escape-teleport gate).")]
        [SerializeField]
        private float phase1CrowdingDistance = 3f;

        [Tooltip("How long the player must stay within the crowding distance before the boss escapes.")]
        [SerializeField]
        private float phase1CrowdingTime = 1f;

        [Header("Phase 2")]
        [Tooltip("Minimum time between phase 2 combos.")]
        [SerializeField]
        private float phase2Cadence = 1.0f;

        [Tooltip("Spacing between the two waves of the phase-2 sword-wall combo.")]
        [SerializeField]
        private float phase2SwordWallSpacing = 0.5f;

        [Tooltip("Reactive-teleport cooldown in phase 2.")]
        [SerializeField]
        private float phase2ReactiveTeleportCooldown = 4f;

        [Tooltip("Distance under which a recent boss hit triggers a reactive teleport in phase 2.")]
        [SerializeField]
        private float phase2ReactiveDistance = 1.5f;

        [Tooltip("Window after a hit during which a reactive teleport may fire.")]
        [SerializeField]
        private float phase2ReactiveHitWindow = 0.3f;

        [Header("Pacing")]
        [Tooltip("Minimum time between melee attacks (Reposition+Slash / Teleport-Strike / " +
                 "Shield Bash). While on cooldown the picker skips melee and the boss does not " +
                 "chase the player via burst movement.")]
        [SerializeField]
        private float meleeCooldown = 6f;

        [Tooltip("Drift time before a melee thrust beat (Reposition+Slash / Shield bash combos).")]
        [SerializeField]
        private float meleeApproachDuration = 0.5f;

        [Tooltip("Cooldown after the active shield is destroyed before another may be spawned. " +
                 "While a shield is alive a new one cannot be cast regardless of this value.")]
        [SerializeField]
        private float shieldRespawnCooldown = 30f;

        [Tooltip("Initial silence at fight start so the boss eases in.")]
        [SerializeField]
        private float warmupDelay = 1f;

        [Header("Movement")]
        [Tooltip("Range from the player the boss prefers to hover within. Inside this band " +
                 "it mostly stands still.")]
        [SerializeField]
        private float preferredDistanceMin = 4f;

        [SerializeField]
        private float preferredDistanceMax = 8f;

        [Tooltip("Distance under which the boss reflexively backs away from the player.")]
        [SerializeField]
        private float personalSpaceDistance = 2.5f;

        [Tooltip("Min / max time between movement decisions. Big pauses come from this — most " +
                 "decisions in the comfortable distance band resolve to 'stand still' for the " +
                 "full interval.")]
        [SerializeField]
        private Vector2 movementDecisionInterval = new Vector2(2f, 4f);

        [Tooltip("Probability of chasing the player when at the comfortable range.")]
        [SerializeField, Range(0f, 1f)]
        private float chaseChanceComfortable = 0.15f;

        [Tooltip("Probability of chasing the player when out of preferred range (too far).")]
        [SerializeField, Range(0f, 1f)]
        private float chaseChanceFar = 0.4f;

        [Tooltip("Probability of a sideways drift when in the comfortable range.")]
        [SerializeField, Range(0f, 1f)]
        private float sidewaysDriftChance = 0.15f;

        // Per-frame command state — rebuilt each Update.
        private VengefulSpiritCommand? currentCommand;
        private int currentMoveX;
        private int currentMoveY;
        private bool pendingAttack;
        private bool pendingSpawnShield;

        // Phase / scheduling.
        private VengefulSpiritPhase phase = VengefulSpiritPhase.One;
        private bool phaseShiftDone;
        private Coroutine activeBehavior;
        private float nextActionTime;
        // Stamped when a previously-active shield is destroyed; the cooldown is measured
        // from this point, not from the spawn time, so the boss "rests" for the full window
        // after losing a shield.
        private float shieldEndedTime = float.NegativeInfinity;
        private bool prevHasShield;
        private float lastMeleeTime = float.NegativeInfinity;
        private float lastReactiveTeleportTime = float.NegativeInfinity;
        private float playerCloseSince = float.PositiveInfinity;
        private float lastHitTime = float.NegativeInfinity;
        private float prevBossHealth;
        private bool warnedNoSwordAnchor;

        // Movement burst state. Decisions fire every movementDecisionInterval and produce a
        // direction held for movementBurstUntil; outside that window, the boss is still.
        private float nextMovementDecisionTime;
        private int movementBurstX;
        private int movementBurstY;
        private float movementBurstUntil;

        // Action coroutines that need to actively approach the player override the burst
        // system by raising forceMoveActive.
        private bool forceMoveActive;
        private int forceMoveX;
        private int forceMoveY;

        private void Awake() {
            if (boss == null) {
                boss = GetComponent<VengefulSpirit>();
            }
        }

        private void OnEnable() {
            nextActionTime = Time.time + warmupDelay;
            nextMovementDecisionTime = Time.time + warmupDelay;
            playerCloseSince = float.PositiveInfinity;
            shieldEndedTime = float.NegativeInfinity;
            prevHasShield = false;
            lastMeleeTime = float.NegativeInfinity;
            lastReactiveTeleportTime = float.NegativeInfinity;
            phaseShiftDone = false;
            warnedNoSwordAnchor = false;
            phase = VengefulSpiritPhase.One;
            prevBossHealth = boss != null && boss.Damageable != null ? boss.Damageable.Health : 0f;
            movementBurstX = 0;
            movementBurstY = 0;
            movementBurstUntil = 0f;
            forceMoveActive = false;
            forceMoveX = 0;
            forceMoveY = 0;
        }

        // Top-level frame loop: track context, run the phase / reactivity hooks, decide
        // movement, possibly start a new action coroutine, and finally publish a command
        // for the boss to read this frame.
        //
        // The order matters:
        //  - Tracking (proximity / hits / phase) updates state the pickers below read.
        //  - Movement is decided BEFORE the action picker so an in-flight coroutine that
        //    sets forceMoveActive earlier this frame still wins.
        //  - The command is built last and pulses are cleared after, so a coroutine that
        //    set pendingAttack this frame gets exactly one frame of "Attack=true" before
        //    the flag is dropped.
        private void Update() {
            if (boss == null || boss.IsDead) {
                StopBehavior();
                ClearForceMove();
                currentCommand = null;
                ResetPulses();
                return;
            }

            TrackPlayerProximity();
            TrackBossHits();
            TrackShield();
            UpdatePhase();

            if (phase == VengefulSpiritPhase.Two) {
                TryReactiveTeleport();
            }

            DecideMovement();
            MaybeStartNewAction();

            currentCommand = new VengefulSpiritCommand(
                currentMoveX,
                currentMoveY,
                pendingAttack,
                pendingSpawnShield,
                /* castSwords */ false,
                /* teleport */ false);

            // Pulses are one-frame: we hand them off to the boss this frame and clear so
            // the next frame's command does not re-trigger the same action. WaitForBusyCycle
            // inside coroutines covers the case where the boss reads the command on the
            // following frame instead of this one.
            ResetPulses();
        }

        public override VengefulSpiritCommand? GetCommand() {
            return currentCommand;
        }

        private void ResetPulses() {
            pendingAttack = false;
            pendingSpawnShield = false;
        }

        // -------- Phase handling --------

        // One-shot HP threshold check. Once tripped, phaseShiftDone latches true so the
        // forced sequence cannot retrigger if HP later climbs (e.g., from a heal). The
        // sequence itself sets phase = Two on completion — we do NOT flip phase up-front,
        // so action coroutines that started in phase 1 finish under phase 1 rules.
        private void UpdatePhase() {
            if (phaseShiftDone || boss.Damageable == null) {
                return;
            }

            float maxHp = boss.Damageable.maxHealth;
            float curHp = boss.Damageable.Health;
            if (maxHp <= 0f) {
                return;
            }

            if (curHp / maxHp <= phaseShiftHealthFraction) {
                phaseShiftDone = true;
                StartBehavior(PhaseShiftSequence());
            }
        }

        // The phase 1 -> 2 transition beat. This is intentionally a "cinematic" forced
        // sequence so the player gets a clear visual cue ("the fight just changed")
        // rather than a silent stat tweak: the boss teleports to the named Center anchor,
        // immediately fires a sword wave from there, and only then enters phase 2 cadence.
        // Started by UpdatePhase via StartBehavior, which means it preempts any in-flight
        // regular-pick coroutine.
        private IEnumerator PhaseShiftSequence() {
            // Wait for any in-flight action (e.g., the regular pick that StartBehavior
            // just preempted from completing on the boss side) to finish on the boss
            // before stacking a teleport on top.
            yield return new WaitWhile(() => boss.IsBusy);

            TeleportAnchor center = boss.GetTeleportAnchor(centerAnchorName);
            if (center != null) {
                boss.RequestTeleport(center);
                yield return WaitForBusyCycle();
            }

            // Sword wave from the same anchor's name (sword and teleport anchors can
            // share names — designer's call). If the sword anchor is missing the boss
            // logs an error and the cast lifecycle still completes via fallback.
            boss.RequestSwordCast(centerAnchorName);
            yield return WaitForBusyCycle();

            phase = VengefulSpiritPhase.Two;
            // Reset cadence so phase 2 doesn't roll its first combo the moment this
            // forced sequence ends — give the player a beat to read the change.
            nextActionTime = Time.time + phase2Cadence;
        }

        // -------- Movement --------

        // Burst-based movement. Every movementDecisionInterval seconds the AI rolls a
        // new "burst" — a direction held for a fraction of a second, then back to
        // standing still. In the comfortable distance band most decisions resolve to
        // "stand still" for the full interval, which is what creates the long pauses.
        //
        // Two layers, in priority order:
        //   1. forceMoveActive — set by action coroutines (Reposition+Slash, Shield Bash)
        //      that need a guaranteed approach. Overrides the burst until cleared.
        //   2. The burst window itself. While Time.time < movementBurstUntil, the chosen
        //      direction is emitted; outside that window the boss stops (movement = 0,0).
        //      The boss's Rigidbody has drag, so emitting (0,0) actually parks it.
        //
        // The boss's controller already zeros velocity during isCasting/isAttacking and
        // preserves velocity (re-applying input) during isTeleporting, so movement
        // emission during those states is harmless or beneficial.
        private void DecideMovement() {
            if (forceMoveActive) {
                currentMoveX = forceMoveX;
                currentMoveY = forceMoveY;
                return;
            }

            if (Time.time >= nextMovementDecisionTime) {
                DecideMovementBurst();
                nextMovementDecisionTime = Time.time + Random.Range(
                    movementDecisionInterval.x,
                    movementDecisionInterval.y);
            }

            if (Time.time < movementBurstUntil) {
                currentMoveX = movementBurstX;
                currentMoveY = movementBurstY;
            } else {
                currentMoveX = 0;
                currentMoveY = 0;
            }
        }

        // Distance-banded probabilistic burst picker. Three bands, each with its own
        // policy:
        //   - Personal space (dist < personalSpaceDistance): always retreat, regardless
        //     of melee readiness. Player crowding the boss is never free.
        //   - Too far (dist > preferredDistanceMax): occasional chase, gated on melee
        //     readiness. If melee is on cooldown, the boss simply holds — there's no
        //     reason to close distance when it can't punish.
        //   - Comfortable band: bias toward standing still (~70% by default), with
        //     small probabilities for sideways drift or a chase. Sideways drift is
        //     allowed even on melee cooldown so the boss doesn't look frozen.
        // After the burst is decided, the next movement decision is scheduled at
        // movementDecisionInterval seconds out — that's where the long pauses come
        // from since "stand still" decisions don't reschedule.
        private void DecideMovementBurst() {
            Transform player = GetPlayerTransform();
            if (player == null) {
                ClearMovementBurst();
                return;
            }

            Vector2 toPlayer = player.position - boss.transform.position;
            float dist = toPlayer.magnitude;
            int towardSign = toPlayer.x > 0.1f ? 1 : (toPlayer.x < -0.1f ? -1 : 0);

            // Personal space — back off reflexively. This always fires regardless of melee
            // readiness; getting in the boss's face should not be free.
            if (dist < personalSpaceDistance) {
                movementBurstX = -towardSign;
                if (movementBurstX == 0) {
                    movementBurstX = Random.value < 0.5f ? -1 : 1;
                }
                movementBurstY = 0;
                movementBurstUntil = Time.time + Random.Range(0.6f, 1.0f);
                return;
            }

            // Chasing is only allowed while melee is off cooldown — the spirit has no reason
            // to close distance if it cannot punish the player when it gets there.
            bool canChase = IsMeleeReady();

            // Too far — sometimes chase, often hold.
            if (dist > preferredDistanceMax) {
                if (canChase && Random.value < chaseChanceFar && towardSign != 0) {
                    movementBurstX = towardSign;
                    movementBurstY = 0;
                    movementBurstUntil = Time.time + Random.Range(0.5f, 1.0f);
                } else {
                    ClearMovementBurst();
                }
                return;
            }

            // Comfortable band — bias hard toward standing still.
            float roll = Random.value;
            if (canChase && roll < chaseChanceComfortable && towardSign != 0) {
                movementBurstX = towardSign;
                movementBurstY = 0;
                movementBurstUntil = Time.time + Random.Range(0.4f, 0.7f);
                return;
            }
            if (roll < chaseChanceComfortable + sidewaysDriftChance) {
                movementBurstX = Random.value < 0.5f ? -1 : 1;
                movementBurstY = 0;
                movementBurstUntil = Time.time + Random.Range(0.3f, 0.6f);
                return;
            }
            ClearMovementBurst();
        }

        private void ClearMovementBurst() {
            movementBurstX = 0;
            movementBurstY = 0;
            movementBurstUntil = 0f;
        }

        private void SetForceMoveTowardPlayer() {
            Transform player = GetPlayerTransform();
            if (player == null) {
                forceMoveActive = false;
                return;
            }
            float dx = player.position.x - boss.transform.position.x;
            forceMoveX = dx > 0f ? 1 : (dx < 0f ? -1 : 0);
            forceMoveY = 0;
            forceMoveActive = true;
        }

        private void ClearForceMove() {
            forceMoveActive = false;
            forceMoveX = 0;
            forceMoveY = 0;
        }

        // -------- Action picker --------

        // Cadence gate. Three guards must all pass before a new beat starts:
        //   - No coroutine already running (activeBehavior == null) — coroutines
        //     finish before another picks.
        //   - Boss is not busy on the controller side (no animation lock) — protects
        //     against rolling a new pick while a previous one's effect is still landing.
        //   - The cadence timer has elapsed.
        // The cadence timer is bumped by phaseNCadence whenever a pick fires, so the
        // floor between picks is the per-phase cadence value. Coroutines that take
        // longer than the cadence (sword cast charge, combos) push the next pick out
        // naturally because the IsBusy gate blocks until they finish.
        private void MaybeStartNewAction() {
            if (activeBehavior != null) {
                return;
            }
            if (boss.IsBusy) {
                return;
            }
            if (Time.time < nextActionTime) {
                return;
            }

            if (phase == VengefulSpiritPhase.One) {
                StartBehavior(PickPhase1Action());
                nextActionTime = Time.time + phase1Cadence;
            } else {
                StartBehavior(PickPhase2Combo());
                nextActionTime = Time.time + phase2Cadence;
            }
        }

        // -------- Phase 1 picks --------

        // Cascade of weighted gates. A single Random.value roll is checked against
        // increasing thresholds; each band represents one possible pick, and a band's
        // gate decides whether the roll is consumed there or falls through to the next.
        // Sword cast is the default action — it covers any roll that isn't claimed by a
        // gated pick (melee on cooldown, no crowding, no shield available). This way the
        // boss spends most of its idle time casting swords from a distance, and melee
        // becomes a punctuation rather than the dominant beat.
        //
        // Effective distribution when all gates pass:
        //   30% Reposition+Slash, 15% Escape teleport, 10% Spawn shield, 45% Sword cast.
        // When melee is on cooldown the 0-30% band falls through, so sword cast climbs
        // toward ~75%. That is the design intent: while melee is unavailable the boss
        // becomes a ranged threat by default.
        private IEnumerator PickPhase1Action() {
            float roll = Random.value;

            if (roll < 0.30f && IsMeleeReady()) {
                yield return RepositionAndSlash();
                yield break;
            }

            if (roll < 0.45f && IsPlayerCrowding()) {
                yield return EscapeTeleport();
                yield break;
            }

            if (roll < 0.55f && CanSpawnShield()) {
                yield return SpawnShieldBeat();
                yield break;
            }

            // Default 45-100% AND every fall-through from a gated pick.
            yield return StandoffSwordWave();
        }

        // -------- Phase 2 picks --------

        // Same cascade pattern as phase 1, but each pick is a multi-beat combo rather
        // than a single action. Sword wall is the default — picks that are gated out
        // (melee on cooldown, shield on cooldown) fall through to it.
        //
        // Effective distribution when all gates pass:
        //   30% Teleport-Strike, 15% Shield Bash, 10% Triple Teleport, 45% Sword Wall.
        // Reactive teleport (TryReactiveTeleport) runs in parallel and can preempt
        // any of these if the player closes to melee range and lands a hit.
        private IEnumerator PickPhase2Combo() {
            float roll = Random.value;

            if (roll < 0.30f && IsMeleeReady()) {
                yield return TeleportStrikeCombo();
                yield break;
            }
            if (roll < 0.45f && IsMeleeReady() && CanSpawnShield()) {
                yield return ShieldBashCombo();
                yield break;
            }
            if (roll < 0.55f) {
                yield return TripleTeleportCombo();
                yield break;
            }

            // Default 55-100% AND every fall-through from a gated pick.
            yield return SwordWallCombo();
        }

        // -------- Action / combo coroutines --------
        //
        // Each routine yields back through WaitForBusyCycle after pulsing a flag or
        // calling a Request* method, so the routine completes only after the boss has
        // actually finished the lifecycle (charge -> effect -> recovery). That keeps
        // the cadence honest: a slow sword-cast charge naturally pushes the next pick
        // out without the picker having to know about it.

        // Phase 1 melee: drift toward the player for meleeApproachDuration, then thrust.
        // Force-move guarantees the approach actually happens regardless of what the
        // burst-movement system would otherwise pick.
        private IEnumerator RepositionAndSlash() {
            SetForceMoveTowardPlayer();
            yield return new WaitForSeconds(meleeApproachDuration);
            ClearForceMove();

            pendingAttack = true;
            lastMeleeTime = Time.time;
            yield return WaitForBusyCycle();
        }

        // Phase 1 default action: fire one sword wave from an anchor on the boss's own
        // side (closer to the boss than to the player), so swords sweep across the gap
        // toward the player. If no anchors are wired we log once and exit silently —
        // the AI doesn't soft-lock, the missing wiring is just visible in the console.
        private IEnumerator StandoffSwordWave() {
            SpectralSwordAnchorBinding? pick = PickSwordAnchorOnBossSide();
            if (pick.HasValue) {
                boss.RequestSwordCast(pick.Value.name);
                yield return WaitForBusyCycle();
            } else {
                WarnNoSwordAnchor();
            }
        }

        // Phase 1 escape valve: only picked if IsPlayerCrowding gate passed in the picker.
        // Goes to whichever teleport anchor is farthest from the player so the relocation
        // visibly buys space, not just a side-step.
        private IEnumerator EscapeTeleport() {
            TeleportAnchor pick = PickTeleportAnchorFarthestFromPlayer();
            if (pick != null) {
                boss.RequestTeleport(pick);
                yield return WaitForBusyCycle();
            }
        }

        // Phase 1/2 shield: pulses the SpawnShield flag for a single frame. The boss's
        // cast lifecycle takes over from there (animation event spawns the shield).
        // The respawn cooldown is started from the moment the shield is destroyed
        // (TrackShield), not from the cast pulse.
        private IEnumerator SpawnShieldBeat() {
            pendingSpawnShield = true;
            yield return WaitForBusyCycle();
        }

        // Phase 2 combo. Two beats:
        //   1. Teleport behind the player (or farthest anchor as fallback if there's no
        //      anchor on the player's far side).
        //   2. Immediate thrust on reappear — no approach drift, the teleport's own
        //      hidden window IS the telegraph.
        private IEnumerator TeleportStrikeCombo() {
            TeleportAnchor behind = PickTeleportAnchorBehindPlayer();
            if (behind == null) {
                behind = PickTeleportAnchorFarthestFromPlayer();
            }
            if (behind != null) {
                boss.RequestTeleport(behind);
                yield return WaitForBusyCycle();
            }

            pendingAttack = true;
            lastMeleeTime = Time.time;
            yield return WaitForBusyCycle();
        }

        // Phase 2 combo. Two beats:
        //   1. Sword wave from the boss-side anchor.
        //   2. After phase2SwordWallSpacing seconds, second wave from an opposite-side
        //      anchor. The two waves create a "wall" that converges on the player.
        private IEnumerator SwordWallCombo() {
            SpectralSwordAnchorBinding? a = PickSwordAnchorOnBossSide();
            if (a.HasValue) {
                boss.RequestSwordCast(a.Value.name);
                yield return WaitForBusyCycle();
            } else {
                WarnNoSwordAnchor();
            }

            yield return new WaitForSeconds(phase2SwordWallSpacing);

            SpectralSwordAnchorBinding? b = PickSwordAnchorOppositeSide();
            if (b.HasValue) {
                boss.RequestSwordCast(b.Value.name);
                yield return WaitForBusyCycle();
            }
        }

        // One-time editor warning — no log spam if anchors are perpetually missing.
        private void WarnNoSwordAnchor() {
            if (warnedNoSwordAnchor) {
                return;
            }
            warnedNoSwordAnchor = true;
            Debug.LogWarning("[VengefulSpiritAI] No sword anchor available — wire at least one " +
                             "binding in 'Sword Anchors' on the VengefulSpirit component.", this);
        }

        // Phase 2 combo. Three beats:
        //   1. SpawnShield (only if cooldown allows).
        //   2. Force-move drift toward the player.
        //   3. Thrust through the active shield window — the player has to commit to a
        //      side because the shield is reflecting attacks from the opposite side.
        private IEnumerator ShieldBashCombo() {
            if (CanSpawnShield()) {
                pendingSpawnShield = true;
                yield return WaitForBusyCycle();
            }

            SetForceMoveTowardPlayer();
            yield return new WaitForSeconds(meleeApproachDuration);
            ClearForceMove();

            pendingAttack = true;
            lastMeleeTime = Time.time;
            yield return WaitForBusyCycle();
        }

        // Phase 2 combo. Three teleports in a row to scattered anchors with no other
        // action between them. Pure harassment / repositioning — disorients the player's
        // tracking. Each pick avoids the anchor closest to the boss's current position
        // so the boss actually moves on every step.
        private IEnumerator TripleTeleportCombo() {
            for (int i = 0; i < 3; i++) {
                TeleportAnchor pick = PickRandomTeleportAnchorAvoidingClosest();
                if (pick == null) {
                    yield break;
                }
                boss.RequestTeleport(pick);
                yield return WaitForBusyCycle();
                yield return new WaitForSeconds(0.15f);
            }
        }

        // -------- Reactive teleport --------

        // Phase 2 only. Runs every frame and bails fast on any failed gate; if all
        // gates pass, it bypasses the picker entirely and fires a teleport directly
        // (without going through StartBehavior, since this is reactive and shouldn't
        // preempt a deliberate combo). Gates, in order:
        //   - Boss not currently busy and no combo running — never interrupt a combo.
        //   - Reactive cooldown elapsed — floor on how often this can chain.
        //   - Hit landed within phase2ReactiveHitWindow seconds — a recent hit is
        //     what makes this "reactive".
        //   - Player within phase2ReactiveDistance — the hit landed because the player
        //     is in melee range, which is exactly the situation we want to escape.
        //   - At least one teleport anchor is reachable.
        private void TryReactiveTeleport() {
            if (boss.IsBusy || activeBehavior != null) {
                return;
            }
            if (Time.time - lastReactiveTeleportTime < phase2ReactiveTeleportCooldown) {
                return;
            }
            if (Time.time - lastHitTime > phase2ReactiveHitWindow) {
                return;
            }

            Transform player = GetPlayerTransform();
            if (player == null) {
                return;
            }
            float dist = (player.position - boss.transform.position).magnitude;
            if (dist > phase2ReactiveDistance) {
                return;
            }

            TeleportAnchor pick = PickTeleportAnchorFarthestFromPlayer();
            if (pick == null) {
                return;
            }

            lastReactiveTeleportTime = Time.time;
            // Reactive teleport interrupts the cadence — push the next scheduled action
            // out so the boss does not immediately roll into another beat on landing.
            nextActionTime = Time.time + phase2Cadence;
            boss.RequestTeleport(pick);
        }

        // -------- Behavior coroutine plumbing --------

        // RunBehavior wraps the user routine so we can null out activeBehavior on natural
        // completion (StopCoroutine takes care of the cancellation case). StartBehavior
        // also stops any in-flight routine so phase shift can preempt regular picks.
        private void StartBehavior(IEnumerator routine) {
            StopBehavior();
            activeBehavior = StartCoroutine(RunBehavior(routine));
        }

        private IEnumerator RunBehavior(IEnumerator routine) {
            yield return routine;
            activeBehavior = null;
        }

        private void StopBehavior() {
            if (activeBehavior != null) {
                StopCoroutine(activeBehavior);
                activeBehavior = null;
            }
        }

        // Two-phase wait used after every Request*/pulse to chain coroutines safely:
        //   1. Wait for the boss to BECOME busy. The boss sets isCasting/isAttacking/
        //      isTeleporting only after consuming the command, which can be 0-1 frames
        //      later than the AI's pulse depending on Update order. We bound that wait
        //      with a 0.5 s deadline so a swallowed pulse (e.g. RequestSwordCast called
        //      with a missing anchor) does not soft-lock the coroutine.
        //   2. Wait for the boss to FINISH being busy. This is the "real" wait — until
        //      the cast / attack / teleport lifecycle completes.
        // Robust to either Update execution order between this component and the boss.
        private IEnumerator WaitForBusyCycle() {
            float deadline = Time.time + 0.5f;
            while (!boss.IsBusy && Time.time < deadline) {
                yield return null;
            }
            while (boss.IsBusy) {
                yield return null;
            }
        }

        // -------- Tracking --------

        // Stamps the moment the player first crossed phase1CrowdingDistance and holds
        // that stamp until they leave. IsPlayerCrowding() then checks the dwell time.
        // The "if (playerCloseSince > Time.time)" guard means we only stamp on entry —
        // we don't keep resetting it while the player is continuously close.
        private void TrackPlayerProximity() {
            Transform player = GetPlayerTransform();
            if (player == null) {
                playerCloseSince = float.PositiveInfinity;
                return;
            }
            float dist = (player.position - boss.transform.position).magnitude;
            if (dist <= phase1CrowdingDistance) {
                if (playerCloseSince > Time.time) {
                    playerCloseSince = Time.time;
                }
            } else {
                playerCloseSince = float.PositiveInfinity;
            }
        }

        // Detects boss hits via health-drop edge detection (instead of subscribing to
        // Damageable.OnHealthChanged) so we don't have to manage subscription lifetimes
        // and so we naturally pick up health changes from any source — Damager hits,
        // scripted damage, etc. lastHitTime feeds TryReactiveTeleport's hit-window gate.
        private void TrackBossHits() {
            if (boss.Damageable == null) {
                return;
            }
            float curHp = boss.Damageable.Health;
            if (curHp < prevBossHealth) {
                lastHitTime = Time.time;
            }
            prevBossHealth = curHp;
        }

        // Edge-detects the moment the active shield is destroyed and stamps shieldEndedTime.
        // CanSpawnShield then measures the cooldown from that timestamp, so the boss waits the
        // full window after losing a shield (not after spawning one).
        private void TrackShield() {
            bool curHasShield = boss != null && boss.HasActiveShield;
            if (prevHasShield && !curHasShield) {
                shieldEndedTime = Time.time;
            }
            prevHasShield = curHasShield;
        }

        private bool IsMeleeReady() {
            return Time.time - lastMeleeTime >= meleeCooldown;
        }

        private bool IsPlayerCrowding() {
            return Time.time - playerCloseSince >= phase1CrowdingTime;
        }

        private bool CanSpawnShield() {
            if (boss.Damageable == null) {
                return false;
            }
            // Hard physical gate: only one shield at a time. The boss enforces this too,
            // but checking here keeps the AI from queueing a useless cast.
            if (boss.HasActiveShield) {
                return false;
            }
            float maxHp = boss.Damageable.maxHealth;
            float curHp = boss.Damageable.Health;
            if (maxHp <= 0f) {
                return false;
            }
            // Phase 2 ignores the HP gate — shield bash combo can fire any time off cooldown.
            bool hpGateMet = phase == VengefulSpiritPhase.Two
                ? true
                : curHp / maxHp <= phase1ShieldHealthThreshold;
            // Cooldown is measured from when the previous shield was destroyed, not spawned.
            // shieldEndedTime starts at -Infinity so the first cast is unrestricted.
            bool cooldownMet = Time.time - shieldEndedTime >= shieldRespawnCooldown;
            return hpGateMet && cooldownMet;
        }

        // -------- Anchor selection --------
        //
        // All anchor-selection helpers iterate through the boss's enumerator methods
        // (EnumerateSwordAnchors / EnumerateTeleportAnchors), which already filter out
        // null bindings. They return null on "no usable anchor"; callers either log
        // (sword cast) or silently skip (teleport).

        private Transform GetPlayerTransform() {
            return G.Hero != null && G.Hero.Controller != null
                ? G.Hero.Controller.transform
                : null;
        }

        private SpectralSwordAnchorBinding? PickSwordAnchorOnBossSide() {
            // "Same side" = anchor whose X is on the boss's side relative to the player.
            // Falls back to the anchor closest to the boss if no clear side wins.
            Transform player = GetPlayerTransform();
            float bossX = boss.transform.position.x;

            SpectralSwordAnchorBinding? best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var binding in boss.EnumerateSwordAnchors()) {
                Vector3 ap = binding.anchor.transform.position;
                bool sameSide = player == null
                    ? true
                    : Mathf.Sign(ap.x - bossX) == Mathf.Sign(bossX - player.position.x)
                      || Mathf.Approximately(ap.x, bossX);
                if (!sameSide) {
                    continue;
                }
                float dist = Mathf.Abs(ap.x - bossX);
                if (dist < bestScore) {
                    bestScore = dist;
                    best = binding;
                }
            }

            if (best.HasValue) {
                return best;
            }

            // Fallback: nearest sword anchor regardless of side.
            float nearest = float.PositiveInfinity;
            foreach (var binding in boss.EnumerateSwordAnchors()) {
                float d = (binding.anchor.transform.position - boss.transform.position).sqrMagnitude;
                if (d < nearest) {
                    nearest = d;
                    best = binding;
                }
            }
            return best;
        }

        private SpectralSwordAnchorBinding? PickSwordAnchorOppositeSide() {
            Transform player = GetPlayerTransform();
            float bossX = boss.transform.position.x;

            SpectralSwordAnchorBinding? best = null;
            float bestScore = float.PositiveInfinity;
            foreach (var binding in boss.EnumerateSwordAnchors()) {
                Vector3 ap = binding.anchor.transform.position;
                bool oppositeSide = player == null
                    ? false
                    : Mathf.Sign(ap.x - bossX) != Mathf.Sign(bossX - player.position.x);
                if (!oppositeSide) {
                    continue;
                }
                float dist = Mathf.Abs(ap.x - bossX);
                if (dist < bestScore) {
                    bestScore = dist;
                    best = binding;
                }
            }

            if (best.HasValue) {
                return best;
            }

            // Fallback: anchor farthest from the boss.
            float farthest = -1f;
            foreach (var binding in boss.EnumerateSwordAnchors()) {
                float d = (binding.anchor.transform.position - boss.transform.position).sqrMagnitude;
                if (d > farthest) {
                    farthest = d;
                    best = binding;
                }
            }
            return best;
        }

        private TeleportAnchor PickTeleportAnchorFarthestFromPlayer() {
            Transform player = GetPlayerTransform();
            if (player == null) {
                return PickRandomTeleportAnchorAvoidingClosest();
            }
            TeleportAnchor best = null;
            float bestSqr = -1f;
            foreach (var binding in boss.EnumerateTeleportAnchors()) {
                float d = (binding.anchor.transform.position - player.position).sqrMagnitude;
                if (d > bestSqr) {
                    bestSqr = d;
                    best = binding.anchor;
                }
            }
            return best;
        }

        private TeleportAnchor PickTeleportAnchorBehindPlayer() {
            Transform player = GetPlayerTransform();
            if (player == null) {
                return null;
            }

            // "Behind" = anchor whose X is on the opposite side of the player from the
            // boss's current side. Among those, pick the one closest to the player so
            // the strike actually lands.
            float bossSide = Mathf.Sign(boss.transform.position.x - player.position.x);

            TeleportAnchor best = null;
            float bestSqr = float.PositiveInfinity;
            foreach (var binding in boss.EnumerateTeleportAnchors()) {
                Vector3 ap = binding.anchor.transform.position;
                float anchorSide = Mathf.Sign(ap.x - player.position.x);
                if (anchorSide == 0f || anchorSide == bossSide) {
                    continue;
                }
                float d = (ap - player.position).sqrMagnitude;
                if (d < bestSqr) {
                    bestSqr = d;
                    best = binding.anchor;
                }
            }
            return best;
        }

        private TeleportAnchor PickRandomTeleportAnchorAvoidingClosest() {
            // Materialise to a list so we can index after rolling. Set is small (3-6).
            List<TeleportAnchor> all = new List<TeleportAnchor>();
            foreach (var binding in boss.EnumerateTeleportAnchors()) {
                all.Add(binding.anchor);
            }
            if (all.Count == 0) {
                return null;
            }
            if (all.Count == 1) {
                return all[0];
            }

            int closestIdx = 0;
            float closestSqr = float.PositiveInfinity;
            for (int i = 0; i < all.Count; i++) {
                float d = (all[i].transform.position - boss.transform.position).sqrMagnitude;
                if (d < closestSqr) {
                    closestSqr = d;
                    closestIdx = i;
                }
            }

            int pick = Random.Range(0, all.Count - 1);
            if (pick >= closestIdx) {
                pick++;
            }
            return all[pick];
        }
    }
}
