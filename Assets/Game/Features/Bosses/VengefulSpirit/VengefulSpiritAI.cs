using System.Collections;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Features.Bosses.VengefulSpirit.AI;
using Game.Features.Bosses.VengefulSpirit.AI.Patterns;
using UnityEngine;

namespace Game.Features.Bosses.VengefulSpirit {
    public enum VengefulSpiritPhase {
        One,
        Two
    }

    /// <summary>
    /// Strategic layer of the Vengeful Spirit. Pure pattern picker — owns:
    /// - <see cref="phase"/> tracking and the one-shot transition into phase 2
    ///   (forced via <see cref="phase2Opener"/>);
    /// - per-phase pattern POOLS (<see cref="phase1Patterns"/> / <see cref="phase2Patterns"/>)
    ///   — each entry is a <see cref="BossPatternSlot"/> tying a pattern component to its
    ///   per-cycle cap, max-in-a-row, and weight;
    /// - the cycle picker (filter eligible slots, weighted random pick);
    /// - the optional follow-up dispatch after each main pattern.
    ///
    /// The AI does NOT drive the boss directly. It never moves the boss, never turns it,
    /// never issues teleports. Each <see cref="VengefulSpiritPattern"/> is a complete
    /// behavior strategy: it picks positions, faces the right way, runs whatever motion
    /// it needs, and only returns when the move is fully finished. The AI then picks the
    /// next strategy. Only one strategy can be active at a time. Need a chase or a
    /// reactive escape? Add it as a pattern with the right CanRun condition.
    /// </summary>
    [RequireComponent(typeof(VengefulSpirit))]
    public class VengefulSpiritAI : VengefulSpiritControlSource {
        [Header("Wiring")]
        [SerializeField]
        private VengefulSpirit boss;

        [Header("Phase Trigger")]
        [Tooltip("HP fraction (0..1) at which the fight enters phase 2.")]
        [SerializeField, Range(0f, 1f)]
        private float phaseShiftHealthFraction = 0.5f;

        [Tooltip("Pattern run as the forced phase-2 opener after the HP threshold is crossed. " +
                 "Bypasses the cycle picker. Typical choice: a SpawnShieldPattern that " +
                 "self-teleports to a central position.")]
        [SerializeField]
        private VengefulSpiritPattern phase2Opener;

        [Tooltip("Pattern fired as a follow-up after every main pattern, if its CanRun returns true. " +
                 "Used for situational punishes (e.g. CommonAttackPattern with a punish-zone collider). " +
                 "Does NOT count against the cycle's cap or last-pattern bookkeeping.")]
        [SerializeField]
        private VengefulSpiritPattern followUpPattern;

        [Header("Pacing")]
        [Tooltip("Minimum time between picks in phase 1.")]
        [SerializeField]
        private float phase1Cadence = 2.0f;

        [Tooltip("Minimum time between picks in phase 2.")]
        [SerializeField]
        private float phase2Cadence = 1.0f;

        [Tooltip("Initial silence at fight start so the boss eases in.")]
        [SerializeField]
        private float warmupDelay = 1f;

        [Tooltip("If no pattern has been runnable for this long (seconds), the cycle is force-reset. " +
                 "Anti-deadlock for situations like the player being unreachable.")]
        [SerializeField]
        private float cycleStallDuration = 4f;

        [Header("Phase 1 Pattern Pool")]
        [Tooltip("Patterns the picker may roll in phase 1, each with its own scheduling rules.")]
        [SerializeField]
        private BossPatternSlot[] phase1Patterns;

        [Header("Phase 2 Pattern Pool")]
        [Tooltip("Patterns the picker may roll in phase 2, each with its own scheduling rules.")]
        [SerializeField]
        private BossPatternSlot[] phase2Patterns;

        // -------- Per-frame command (movement disabled — patterns drive the boss directly) --------
        private VengefulSpiritCommand? currentCommand;

        // -------- Phase / pattern state --------
        private VengefulSpiritPhase phase = VengefulSpiritPhase.One;
        private bool phaseShiftDone;
        private Coroutine activeBehavior;
        private float nextActionTime;
        private float lastRunnableTime;

        // Per-pattern usage in the current cycle, keyed by pattern reference (so two
        // components of the same subclass with different tunings track independently).
        private readonly Dictionary<VengefulSpiritPattern, int> patternUsage =
            new Dictionary<VengefulSpiritPattern, int>();

        // Singleton context — fields are mutated each tick (Phase) or per-action (CycleState).
        private VengefulSpiritPatternContext context;
        private VengefulSpiritCycleState cycleState;

        // Currently-running pattern for runtime debug display. Set when RunSlot starts a
        // main or follow-up pattern; cleared on natural completion or StopBehavior.
        private VengefulSpiritPattern activePattern;

        private void Awake() {
            if (boss == null) {
                boss = GetComponent<VengefulSpirit>();
            }

            cycleState = new VengefulSpiritCycleState();
            context = new VengefulSpiritPatternContext {
                Boss = boss,
                AI = this,
                Phase = phase,
                CycleState = cycleState,
            };
        }

        private void OnEnable() {
            // Clear stale coroutine reference left over from a previous disable —
            // disabling a MonoBehaviour stops its coroutines without running their
            // post-yield cleanup, so activeBehavior would otherwise still hold a non-null
            // (but dead) reference and the picker would refuse to start anything new.
            StopBehavior();

            nextActionTime = Time.time + warmupDelay;
            lastRunnableTime = Time.time;
            phaseShiftDone = false;
            phase = VengefulSpiritPhase.One;
            if (context != null) {
                context.Phase = phase;
            }
            cycleState?.Reset();
            ResetCycleUsage();

            // Subscribe so the current pattern can be aborted when the shield shatters —
            // the boss owns the recovery sequence (forces IsBusy on, plays the flinch),
            // we just kill our in-flight pattern so it doesn't resume after recovery.
            if (boss != null) {
                boss.OnShieldDestroyed += HandleShieldDestroyed;
            }
        }

        private void OnDisable() {
            if (boss != null) {
                boss.OnShieldDestroyed -= HandleShieldDestroyed;
            }
        }

        private void HandleShieldDestroyed() {
            // Drop the active pattern. The boss has already flipped IsBusy on for the
            // recovery; MaybeStartNewAction won't pick again until that clears.
            StopBehavior();
        }

        private void Update() {
            if (boss == null || boss.IsDead) {
                StopBehavior();
                currentCommand = null;
                return;
            }

            UpdatePhase();
            MaybeStartNewAction();

            // Movement is intentionally always zero. The AI never drives the boss; every
            // motion (positioning, facing, teleport, attack) is the active pattern's job.
            // The command pump stays only because the debug control source still uses it.
            currentCommand = new VengefulSpiritCommand(
                0, 0,
                /* attack */ false,
                /* spawnShield */ false,
                /* castSwords */ false,
                /* teleport */ false);
        }

        public override VengefulSpiritCommand? GetCommand() {
            return currentCommand;
        }

        // -------- Phase handling --------

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

        private IEnumerator PhaseShiftSequence() {
            yield return new WaitWhile(() => boss.IsBusy);

            if (phase2Opener != null) {
                yield return phase2Opener.Run(context);
            }

            phase = VengefulSpiritPhase.Two;
            context.Phase = phase;
            nextActionTime = Time.time + phase2Cadence;
            cycleState.Reset();
            ResetCycleUsage();
            lastRunnableTime = Time.time;
        }

        // -------- Pattern selection --------

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

            BossPatternSlot? pickedSlot = PickSlot();
            if (pickedSlot == null) {
                if (Time.time - lastRunnableTime > cycleStallDuration) {
                    ResetCycleUsage();
                    cycleState.Reset();
                    lastRunnableTime = Time.time;
                }
                return;
            }

            lastRunnableTime = Time.time;
            StartBehavior(RunSlot(pickedSlot.Value));
            nextActionTime = Time.time + (phase == VengefulSpiritPhase.One ? phase1Cadence : phase2Cadence);
        }

        // Filter the active phase's pool down to runnable slots, then weighted-random pick.
        // Returns null if no slot is runnable.
        private BossPatternSlot? PickSlot() {
            BossPatternSlot[] pool = phase == VengefulSpiritPhase.One ? phase1Patterns : phase2Patterns;
            if (pool == null || pool.Length == 0) {
                return null;
            }

            // Two passes: prefer slots whose pattern isn't the just-run one (so picks
            // alternate when alternatives exist); fall back to the broader set if needed.
            List<BossPatternSlot> nonLast = new List<BossPatternSlot>();
            List<BossPatternSlot> all = new List<BossPatternSlot>();

            for (int i = 0; i < pool.Length; i++) {
                BossPatternSlot slot = pool[i];
                if (!IsSlotRunnable(slot)) {
                    continue;
                }
                all.Add(slot);
                if (slot.pattern != cycleState.LastPattern) {
                    nonLast.Add(slot);
                }
            }

            List<BossPatternSlot> finalSet = nonLast.Count > 0 ? nonLast : all;
            return finalSet.Count == 0 ? (BossPatternSlot?)null : WeightedPick(finalSet);
        }

        private bool IsSlotRunnable(BossPatternSlot slot) {
            VengefulSpiritPattern p = slot.pattern;
            if (p == null) {
                return false;
            }
            if (slot.maxPerCycle == 0) {
                return false;
            }
            int used = patternUsage.TryGetValue(p, out int u) ? u : 0;
            if (slot.maxPerCycle > 0 && used >= slot.maxPerCycle) {
                return false;
            }
            // Max-in-a-row: if this same pattern just ran, only allow it again if its
            // consecutive count is below the limit.
            if (slot.maxInARow >= 0 && p == cycleState.LastPattern && cycleState.LastPatternConsecutive >= slot.maxInARow) {
                return false;
            }
            if (!p.CanRun(context)) {
                return false;
            }
            return true;
        }

        private static BossPatternSlot WeightedPick(List<BossPatternSlot> slots) {
            float total = 0f;
            for (int i = 0; i < slots.Count; i++) {
                total += SlotWeight(slots[i]);
            }
            if (total <= 0f) {
                return slots[Random.Range(0, slots.Count)];
            }
            float roll = Random.value * total;
            float acc = 0f;
            for (int i = 0; i < slots.Count; i++) {
                acc += SlotWeight(slots[i]);
                if (roll <= acc) {
                    return slots[i];
                }
            }
            return slots[slots.Count - 1];
        }

        private static float SlotWeight(BossPatternSlot s) {
            return s.weight > 0f ? s.weight : 1f;
        }

        private IEnumerator RunSlot(BossPatternSlot slot) {
            VengefulSpiritPattern p = slot.pattern;
            patternUsage[p] = (patternUsage.TryGetValue(p, out int u) ? u : 0) + 1;

            activePattern = p;
            yield return p.Run(context);
            activePattern = null;

            // Update last-run tracking only on natural completion.
            if (cycleState.LastPattern == p) {
                cycleState.LastPatternConsecutive++;
            } else {
                cycleState.LastPattern = p;
                cycleState.LastPatternConsecutive = 1;
            }

            // Follow-up hook: fires AFTER each main pattern if its CanRun reports go.
            // The follow-up is treated as an interrupt — no cycle bookkeeping, no
            // last-pattern update. Designed for situational punishes (e.g. CommonAttack
            // when player overlaps a punish zone).
            //
            // Two gates here:
            // - p.AllowFollowUp lets a pattern opt out (Blink already includes a strike,
            //   so chaining the punish would double-hit).
            // - WaitWhile(IsBusy) is a defensive flush. Patterns SHOULD leave the boss
            //   idle when their Run returns, but if anything is still in flight (e.g.
            //   a despawn fade started at the end of the main pattern) we wait for it
            //   to finish before considering the follow-up.
            if (p.AllowFollowUp && followUpPattern != null && !boss.IsDead) {
                while (boss.IsBusy && !boss.IsDead) {
                    yield return null;
                }
                if (!boss.IsDead && followUpPattern.CanRun(context)) {
                    activePattern = followUpPattern;
                    yield return followUpPattern.Run(context);
                    activePattern = null;
                }
            }

            if (AreAllCapsExhausted()) {
                ResetCycleUsage();
                cycleState.Reset();
            }
        }

        // Cycle exhaustion: only counts capped slots in the current phase pool. Slots
        // with maxPerCycle < 0 (no cap) never count toward exhaustion.
        private bool AreAllCapsExhausted() {
            BossPatternSlot[] pool = phase == VengefulSpiritPhase.One ? phase1Patterns : phase2Patterns;
            if (pool == null) {
                return false;
            }
            bool sawCapped = false;
            for (int i = 0; i < pool.Length; i++) {
                BossPatternSlot s = pool[i];
                if (s.pattern == null) {
                    continue;
                }
                if (s.maxPerCycle <= 0) {
                    // Disabled or uncapped — doesn't count.
                    continue;
                }
                sawCapped = true;
                int used = patternUsage.TryGetValue(s.pattern, out int u) ? u : 0;
                if (used < s.maxPerCycle) {
                    return false;
                }
            }
            // If there are no capped slots at all, never auto-reset (the stall timer
            // will handle deadlocks).
            return sawCapped;
        }

        private void ResetCycleUsage() {
            patternUsage.Clear();
        }

        // -------- Behavior coroutine plumbing --------

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
            activePattern = null;
        }

        // -------- Public surface for patterns --------

        /// <summary>Returns the player transform, or null if the hero service or controller is missing.</summary>
        public Transform GetPlayer() {
            return G.Hero != null && G.Hero.Controller != null
                ? G.Hero.Controller.transform
                : null;
        }

        // -------- Runtime debug surface (for the custom inspector) --------

        /// <summary>Read-only: current fight phase.</summary>
        public VengefulSpiritPhase CurrentPhase => phase;

        /// <summary>Read-only: most recently completed pattern (null at cycle start).</summary>
        public VengefulSpiritPattern LastPattern => cycleState != null ? cycleState.LastPattern : null;

        /// <summary>Read-only: consecutive count for <see cref="LastPattern"/>.</summary>
        public int LastPatternConsecutive => cycleState != null ? cycleState.LastPatternConsecutive : 0;

        /// <summary>Read-only: pattern currently executing (main or follow-up), or null when idle.</summary>
        public VengefulSpiritPattern ActivePattern => activePattern;

        /// <summary>Read-only: per-pattern usage counters in the current cycle.</summary>
        public IReadOnlyDictionary<VengefulSpiritPattern, int> PatternUsage => patternUsage;

        /// <summary>Read-only: the pool active for the current phase.</summary>
        public BossPatternSlot[] GetActivePool() {
            return phase == VengefulSpiritPhase.One ? phase1Patterns : phase2Patterns;
        }
    }
}
