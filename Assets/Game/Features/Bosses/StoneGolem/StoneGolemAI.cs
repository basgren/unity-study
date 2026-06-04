using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core.Bootstrap;
using Game.Features.Bosses._Shared;
using Game.Features.Bosses.StoneGolem.Actions;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
    /// <summary>The Stone Golem's two fight phases. Phase 2 begins once HP drops below the threshold.</summary>
    public enum StoneGolemPhase {
        One,
        Two
    }

    /// <summary>Identifies one of the golem's attacks for the weighted selection pools.</summary>
    public enum StoneGolemAttack {
        Melee,
        Hand,
        Beam,
        GroundHit,
        LaserCross,
        StoneWave
    }

    /// <summary>
    /// Strategic selector for the Stone Golem. Each phase has a weighted pool of attacks; when the golem
    /// is free the selector rolls one and runs an <see cref="ActionSequence"/> that first POSITIONS the
    /// golem, then triggers the attack:
    /// <list type="bullet">
    /// <item><description>Melee / Hand / Beam — match the player's level (fly up/down to the nearest
    /// anchor on that level), and for melee also chase into range.</description></item>
    /// <item><description>Ground Hit — fly to the nearest level-1 anchor first (slam is level-1 only).</description></item>
    /// <item><description>Laser Cross — walk to the anchor nearest the cross centre first; the action
    /// then ascends vertically. Stone Wave — self-positioned (casts in place).</description></item>
    /// </list>
    /// A reactive melee counter still interrupts when the golem is struck while the player is inside
    /// its melee trigger area. Levels are derived from the player's / golem's Y against the lowest level-2 anchor;
    /// when no level-2 anchors exist (phase 2's flat room) everything reads as level 1.
    ///
    /// Anchors are scene markers wired into <see cref="anchors"/> by hand (no global lookup), matching
    /// the VengefulSpirit anchor convention.
    /// </summary>
    [RequireComponent(typeof(StoneGolem))]
    public class StoneGolemAI : MonoBehaviour {
        /// <summary>One weighted entry in a phase pool. Weight 0 disables the entry.</summary>
        [Serializable]
        public struct WeightedAttack {
            public StoneGolemAttack attack;

            [Min(0f)]
            public float weight;
        }

        [Header("Wiring")]
        [SerializeField]
        private StoneGolem golem;

        [SerializeField]
        [Tooltip("Movement points placed in the scene (drag-and-drop). Level 1 = ground / ground-hit, " +
                 "Level 2 = platforms. Used to match the player's level and to position Ground Hit.")]
        private StoneGolemAnchorPoint[] anchors;

        [Header("Phase pools")]
        [SerializeField]
        [Tooltip("Attacks the golem may roll in phase 1, with relative weights.")]
        private WeightedAttack[] phase1Pool;

        [SerializeField]
        [Tooltip("Attacks the golem may roll in phase 2, with relative weights.")]
        private WeightedAttack[] phase2Pool;

        [Header("Pacing")]
        [SerializeField]
        [Tooltip("Minimum time between action picks in phase 1.")]
        private float phase1Cadence = 1.5f;

        [SerializeField]
        [Tooltip("Minimum time between action picks in phase 2.")]
        private float phase2Cadence = 1.0f;

        [SerializeField]
        [Tooltip("Initial delay before the golem starts acting.")]
        private float warmupDelay = 1f;

        [Header("Melee")]
        [SerializeField]
        [Tooltip("Trigger collider in front of the golem (a child that flips with facing). The golem " +
                 "can swing melee when the player overlaps it — this drives both the reactive counter " +
                 "and the chase stop, replacing fixed range distances.")]
        private Collider2D meleeTriggerArea;

        [SerializeField]
        [Tooltip("Shared cooldown gating both the reactive counter and a rolled melee, so the player " +
                 "always gets a guaranteed safe window after a swing.")]
        private float meleeCooldown = 1.5f;

        [SerializeField]
        [Tooltip("Max time the golem spends directly chasing the player (within one walkable segment) " +
                 "before giving up and ending the melee. Cross-level / cross-platform travel is not " +
                 "counted here.")]
        private float chaseTimeout = 3f;

        [SerializeField]
        [Tooltip("How often the melee pursuit re-plans its route while chasing, so it follows the " +
                 "player to a new platform instead of committing to where the player started.")]
        private float repathInterval = 0.3f;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Fallback fly time when no graph route exists between the current and target anchor.")]
        private float flyDuration = 2f;

        [SerializeField]
        [Tooltip("Duration of a cross-level graph jump (Jump edge) between anchors.")]
        private float jumpDuration = 0.8f;

        [SerializeField]
        [Tooltip("Peak height of a cross-level jump arc above the straight line between anchors.")]
        private float jumpArcHeight = 3f;

        [SerializeField]
        [Tooltip("Duration of the low horizontal hop across the platform gap (HorzJump edge).")]
        private float horzJumpDuration = 0.6f;

        [SerializeField]
        [Tooltip("Peak height of the horizontal gap hop above the straight line between the platforms.")]
        private float horzJumpArcHeight = 1.5f;

        [SerializeField]
        [Tooltip("How close (world units) the golem must get to an anchor on a Move edge to count as arrived. " +
                 "Movement always aims at the anchor itself; this only decides when to cut the last sliver and proceed.")]
        private float anchorArriveThreshold = 0.05f;

        [SerializeField]
        [Tooltip("Safety cap (seconds) on a single Move (walk) edge, in case the golem can't reach the anchor.")]
        private float moveEdgeTimeout = 5f;

        [SerializeField]
        [Tooltip("Vertical slack when deciding which level the player / golem is on.")]
        private float levelTolerance = 1f;

        [Header("Phase trigger")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("HP fraction at or below which the golem enters phase 2.")]
        private float phaseShiftHealthFraction = 0.5f;

        [SerializeField]
        [Tooltip("Action run once when phase 2 begins (the empowered ground-hit cutscene). Optional.")]
        private EnemyAction phaseTransitionAction;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Debug only: begin the fight already in phase 2 (phase-2 pool + cadence). Engages the " +
                 "boss, shatters the floor and swaps the camera confiner immediately (skipping the intro " +
                 "drop-in and the transition cutscene), so phase-2 attacks can be tested in the lower arena.")]
        private bool debugStartInPhase2;

        private float nextActionTime;
        private float nextMeleeTime;
        private StoneGolemPhase phase = StoneGolemPhase.One;
        private bool phaseShiftDone;
        private bool transitioning;
        private bool executing;

        private bool hasLevel2;
        private float level2MinY = float.PositiveInfinity;
        private Collider2D cachedPlayerCollider;
        private StoneGolemNavGraph navGraph;

        // Safety cap on graph hops in one pursuit, so a misconfigured graph can't loop forever.
        private const int PursueNodeCap = 16;

        /// <summary>Current fight phase (read-only, for debug/UI).</summary>
        public StoneGolemPhase CurrentPhase => phase;

        /// <summary>Debug: true when configured to begin already in phase 2. Read by the intro so it
        /// steps aside (the AI engages and breaks the floor itself) instead of holding the golem inert.</summary>
        public bool DebugStartInPhase2 => debugStartInPhase2;

        private float CurrentCadence => phase == StoneGolemPhase.One ? phase1Cadence : phase2Cadence;

        private void Reset() {
            if (golem == null) {
                golem = GetComponent<StoneGolem>();
            }
        }

        private void Awake() {
            if (golem == null) {
                golem = GetComponent<StoneGolem>();
            }

            ComputeLevels();
            navGraph = new StoneGolemNavGraph(anchors);

            if (debugStartInPhase2) {
                // Debug: jump straight to phase 2 (phase-2 pool + cadence). Latch phaseShiftDone so
                // UpdatePhase never runs the transition cutscene. The floor stays intact.
                phase = StoneGolemPhase.Two;
                phaseShiftDone = true;
            }
        }

        // Start (not Awake): G.BossFight and the transition action's Awake are ready by now, and Start
        // runs the first time this component becomes enabled, so the bring-up still fires if the AI
        // starts disabled (e.g. StoneGolemDebugControls.startWithAi = false) and is resumed later.
        private void Start() {
            if (debugStartInPhase2) {
                DebugBringUpPhase2();
            }
        }

        private void OnEnable() {
            nextActionTime = Time.time + warmupDelay;
        }

        // Debug bring-up: the normal intro (engage boss) and transition cutscene (shatter floor + swap
        // camera confiner) are both skipped when starting straight in phase 2, so do them at once here.
        // The intro reads DebugStartInPhase2 and steps aside so it does not hold the golem inert.
        private void DebugBringUpPhase2() {
            if (golem != null && golem.Damageable != null && G.BossFight != null) {
                G.BossFight.EngageBoss(golem.Damageable);
            }

            StoneGolemGroundBreakAction breakAction = phaseTransitionAction as StoneGolemGroundBreakAction;
            if (breakAction != null) {
                breakAction.ApplyBrokenStateImmediate();
            } else if (phaseTransitionAction != null) {
                Debug.LogWarning("StoneGolemAI.debugStartInPhase2: phaseTransitionAction is not a " +
                                 "StoneGolemGroundBreakAction, so the floor/confiner were not switched.", this);
            }
        }

        private void Update() {
            if (golem == null || golem.IsDead) {
                return;
            }

            // Phase shift owns the golem while it runs (waits for the current action, plays the
            // transition); skip normal selection until it completes.
            UpdatePhase();
            if (transitioning || executing) {
                return;
            }

            Transform player = GetPlayer();
            if (player == null) {
                return;
            }

            // While an action runs the selector keeps hands off — the action owns the golem.
            if (golem.IsBusy) {
                return;
            }

            // Reactive melee counter: if the golem was just struck at close range (same level), swing
            // back immediately (bypassing the cadence) so hitting the boss has a felt consequence.
            if (TryReactiveMelee(player)) {
                return;
            }

            if (Time.time < nextActionTime) {
                return;
            }

            StoneGolemAttack? attack = PickAttack();
            if (attack.HasValue) {
                StartCoroutine(ActionSequence(attack.Value));
                nextActionTime = Time.time + CurrentCadence;
            }
        }

        // Reactive counter: only when the golem actually took a hit this frame, the player is inside the
        // melee trigger area, and the shared melee cooldown has elapsed.
        private bool TryReactiveMelee(Transform player) {
            if (golem.Melee == null || Time.time < nextMeleeTime || !golem.WasHitThisFrame) {
                return false;
            }

            // Face the player first so the trigger area (which flips with facing) covers their side,
            // then test overlap. Overlap also implies the same level — the trigger box is short.
            golem.FaceTowards(player.position.x);
            if (!PlayerInMeleeReach(player)) {
                return false;
            }

            golem.RunAction(golem.Melee);
            nextMeleeTime = Time.time + meleeCooldown;
            nextActionTime = Time.time + CurrentCadence;
            return true;
        }

        // -------- Phase handling --------

        private void UpdatePhase() {
            if (phaseShiftDone || phase == StoneGolemPhase.Two) {
                return;
            }

            if (golem.HealthFraction > phaseShiftHealthFraction) {
                return;
            }

            phaseShiftDone = true;
            StartCoroutine(PhaseTransition());
        }

        private IEnumerator PhaseTransition() {
            transitioning = true;

            while (golem.IsBusy) {
                yield return null;
            }

            if (phaseTransitionAction != null) {
                bool ended = false;
                Action<EnemyAction> onEnded = a => {
                    if (a == phaseTransitionAction) {
                        ended = true;
                    }
                };

                golem.ActionEnded += onEnded;
                golem.RunAction(phaseTransitionAction);
                while (!ended) {
                    yield return null;
                }

                golem.ActionEnded -= onEnded;
            }

            phase = StoneGolemPhase.Two;
            nextActionTime = Time.time + phase2Cadence;
            transitioning = false;
        }

        // -------- Selection --------

        private StoneGolemAttack? PickAttack() {
            WeightedAttack[] pool = phase == StoneGolemPhase.One ? phase1Pool : phase2Pool;
            if (pool == null || pool.Length == 0) {
                return null;
            }

            // Sum eligible weights (entry enabled, weight > 0, and the action actually wired).
            float total = 0f;
            for (int i = 0; i < pool.Length; i++) {
                if (pool[i].weight > 0f && ResolveAttack(pool[i].attack) != null) {
                    total += pool[i].weight;
                }
            }

            if (total <= 0f) {
                return null;
            }

            float roll = UnityEngine.Random.value * total;
            float acc = 0f;
            for (int i = 0; i < pool.Length; i++) {
                if (pool[i].weight <= 0f || ResolveAttack(pool[i].attack) == null) {
                    continue;
                }

                acc += pool[i].weight;
                if (roll <= acc) {
                    return pool[i].attack;
                }
            }

            return null;
        }

        private EnemyAction ResolveAttack(StoneGolemAttack attack) {
            switch (attack) {
                case StoneGolemAttack.Melee: return golem.Melee;
                case StoneGolemAttack.Hand: return golem.HandShoot;
                case StoneGolemAttack.Beam: return golem.BeamShoot;
                case StoneGolemAttack.GroundHit: return golem.GroundHit;
                case StoneGolemAttack.LaserCross: return golem.LaserCross;
                case StoneGolemAttack.StoneWave: return golem.StoneWave;
                default: return null;
            }
        }

        // -------- Action sequence: position, then attack --------

        private IEnumerator ActionSequence(StoneGolemAttack attack) {
            executing = true;

            EnemyAction action = ResolveAttack(attack);
            Transform player = GetPlayer();

            if (action != null && player != null && !golem.IsDead) {
                yield return Position(attack, player);

                if (!golem.IsDead) {
                    golem.FaceTowards(player.position.x);

                    // Melee can finish its chase without reaching the player (e.g. the player jumped to
                    // another level). Skip the swing so the golem doesn't flail at air or burn the
                    // cooldown; the next pick re-routes toward the player.
                    if (attack != StoneGolemAttack.Melee || PlayerInMeleeReach(player)) {
                        golem.RunAction(action);
                        if (attack == StoneGolemAttack.Melee) {
                            nextMeleeTime = Time.time + meleeCooldown;
                        }

                        // Let RunAction arm IsBusy, then hold the selector until the action finishes.
                        yield return null;
                        while (golem.IsBusy && !golem.IsDead) {
                            yield return null;
                        }
                    }
                }
            }

            executing = false;
        }

        private IEnumerator Position(StoneGolemAttack attack, Transform player) {
            switch (attack) {
                case StoneGolemAttack.GroundHit:
                    // Slam is level-1 only — route to the nearest level-1 anchor (down from a platform if needed).
                    yield return TravelTo(NearestAnchor(StoneGolemLevel.Level1, player.position));
                    break;

                case StoneGolemAttack.Melee:
                    yield return PursuePlayer(player);
                    break;

                case StoneGolemAttack.Hand:
                case StoneGolemAttack.Beam:
                    yield return TravelToPlayerLevel(player);
                    break;

                case StoneGolemAttack.LaserCross:
                    // Walk to the ground anchor nearest the cross centre (same graph movement as the
                    // other attacks), so the action's vertical ascent starts from under the pivot
                    // instead of flying diagonally across the room.
                    yield return TravelTo(NearestAnchor(StoneGolemLevel.Level1, golem.LaserCross.CenterPosition));
                    break;

                // StoneWave positions itself (casts in place).
            }
        }

        // Routes to the anchor nearest the player on the player's level, but only when the golem is on a
        // different level — same level needs no repositioning (melee then chases; hand/beam have range).
        private IEnumerator TravelToPlayerLevel(Transform player) {
            bool playerUp = IsOnLevel2(player.position.y);
            bool golemUp = IsOnLevel2(golem.transform.position.y);
            if (playerUp == golemUp) {
                yield break;
            }

            StoneGolemLevel targetLevel = playerUp ? StoneGolemLevel.Level2 : StoneGolemLevel.Level1;
            yield return TravelTo(NearestAnchor(targetLevel, player.position));
        }

        // Builds a route from the golem's current anchor to dest along the authored navigation graph and
        // walks / jumps each edge in turn. Falls back to a direct fly when no route exists (graph not
        // wired between those nodes) so the golem still reaches its destination.
        private IEnumerator TravelTo(StoneGolemAnchorPoint dest) {
            if (dest == null || golem.IsDead) {
                yield break;
            }

            // Start from the nearest anchor ON THE GOLEM'S OWN LEVEL — not the nearest of any level.
            // When the golem stands directly under a platform, a level-2 anchor can be closer in 2D
            // than the level-1 anchor it is actually on, which would route a bogus straight-up jump.
            StoneGolemLevel golemLevel = IsOnLevel2(golem.transform.position.y)
                ? StoneGolemLevel.Level2
                : StoneGolemLevel.Level1;
            StoneGolemAnchorPoint start = NearestAnchor(golemLevel, golem.transform.position);
            IReadOnlyList<StoneGolemNavGraph.NavStep> path =
                start != null && navGraph != null ? navGraph.FindPath(start, dest) : null;

            if (path == null) {
                Debug.LogWarning($"StoneGolemAI: no navigation route to '{dest.name}'; flying directly.", this);
                yield return golem.FlyTo(dest.Position, flyDuration);
                yield break;
            }

            // Walk onto the takeoff anchor first so a leading Jump launches from the anchor, not mid-gap.
            yield return WalkTo(start.Position.x);

            for (int i = 0; i < path.Count && !golem.IsDead; i++) {
                yield return ExecuteStep(path[i]);
            }
        }

        // Pursues the player while RE-PLANNING the route on each node arrival and on a short interval —
        // the standard "replan at every waypoint" chase. Because the destination is recomputed from the
        // player's LIVE position, the golem follows the player to a new platform instead of committing to
        // where the player started. It steers directly toward the player while on the same walkable
        // segment, and only consults the graph (walk-to-takeoff + jump) to cross between segments.
        // Returns when the player is within melee reach (ActionSequence then swings) or when it gives up.
        private IEnumerator PursuePlayer(Transform player) {
            float chaseElapsed = 0f;

            for (int guard = 0; guard < PursueNodeCap && !golem.IsDead; guard++) {
                if (PlayerInMeleeReach(player)) {
                    yield break;
                }

                StoneGolemAnchorPoint golemNode = NearestAnchor(LevelOf(golem.transform.position.y), golem.transform.position);
                StoneGolemAnchorPoint targetNode = NearestAnchor(LevelOf(player.position.y), player.position);
                IReadOnlyList<StoneGolemNavGraph.NavStep> path =
                    golemNode != null && targetNode != null && navGraph != null
                        ? navGraph.FindPath(golemNode, targetNode)
                        : null;

                if (path == null) {
                    if (targetNode != null) {
                        Debug.LogWarning($"StoneGolemAI: no navigation route to '{targetNode.name}'; flying directly.", this);
                        yield return golem.FlyTo(targetNode.Position, flyDuration);
                    }

                    yield break;
                }

                int jumpIndex = FirstNonMoveIndex(path);
                if (jumpIndex < 0) {
                    // Player is on the golem's walkable segment — steer straight at them for one interval,
                    // then loop to re-plan (they may have left the segment, or be in reach).
                    float burst = 0f;
                    while (burst < repathInterval
                           && chaseElapsed < chaseTimeout
                           && !golem.IsDead
                           && IsOnLevel2(player.position.y) == IsOnLevel2(golem.transform.position.y)
                           && !PlayerInMeleeReach(player)) {
                        golem.MoveTowards(player.position.x);
                        burst += Time.deltaTime;
                        chaseElapsed += Time.deltaTime;
                        yield return null;
                    }

                    // Stop only when the pursuit actually ends. Stopping before every re-plan zeroed
                    // the ramped walk command each repathInterval (0.3 s vs a 0.25 s build-up), so the
                    // chase pulsed: accelerate → hard brake → accelerate. Keep rolling into the next
                    // re-plan instead; WalkTo / RunAction own their stops on the other paths.
                    if (golem.IsDead || PlayerInMeleeReach(player) || chaseElapsed >= chaseTimeout) {
                        golem.StopMoving();
                        yield break;
                    }

                    continue;
                }

                // Different segment: if the route starts with a jump, walk onto the takeoff anchor first
                // so it launches cleanly. Execute up to and including the first jump, then re-plan.
                if (jumpIndex == 0) {
                    yield return WalkTo(golemNode.Position.x);
                }

                for (int i = 0; i <= jumpIndex && !golem.IsDead; i++) {
                    yield return ExecuteStep(path[i]);
                }
            }
        }

        // Executes one navigation edge: walk a Move edge, parabolic-jump a Jump / HorzJump edge.
        private IEnumerator ExecuteStep(StoneGolemNavGraph.NavStep step) {
            switch (step.type) {
                case StoneGolemMovementType.Move:
                    yield return WalkTo(step.node.Position.x);
                    break;

                case StoneGolemMovementType.Jump:
                    golem.FaceTowards(step.node.Position.x);
                    yield return golem.JumpTo(step.node.Position, jumpDuration, jumpArcHeight);
                    break;

                case StoneGolemMovementType.HorzJump:
                    golem.FaceTowards(step.node.Position.x);
                    yield return golem.JumpTo(step.node.Position, horzJumpDuration, horzJumpArcHeight);
                    break;
            }
        }

        // Index of the first non-Move (Jump / HorzJump) step, or -1 if the whole route is walkable —
        // i.e. the target sits on the golem's current contiguous segment.
        private static int FirstNonMoveIndex(IReadOnlyList<StoneGolemNavGraph.NavStep> path) {
            for (int i = 0; i < path.Count; i++) {
                if (path[i].type != StoneGolemMovementType.Move) {
                    return i;
                }
            }

            return -1;
        }

        // Velocity-based ground walk to a world X for Move edges. MoveTowards always brakes toward
        // the target point itself; once the golem is within anchorArriveThreshold the point counts
        // as reached — stop and proceed (cutting the tiny residual speed instead of decaying through
        // micro distances). moveEdgeTimeout is a safety cap. Sibling to Chase.
        private IEnumerator WalkTo(float targetX) {
            float elapsed = 0f;
            while (elapsed < moveEdgeTimeout
                   && !golem.IsDead
                   && Mathf.Abs(targetX - golem.transform.position.x) > anchorArriveThreshold) {
                golem.MoveTowards(targetX);
                elapsed += Time.deltaTime;
                yield return null;
            }

            golem.StopMoving();
        }

        // True when the player overlaps the golem's melee trigger area. The trigger sits in front of
        // the golem and flips with its facing; since facing may have changed this frame and 2D physics
        // does not auto-sync transforms, sync first, then test geometric overlap (works regardless of
        // the layer collision matrix). Replaces the old fixed horizontal-range checks.
        private bool PlayerInMeleeReach(Transform player) {
            if (meleeTriggerArea == null) {
                return false;
            }

            Collider2D playerCollider = ResolvePlayerCollider(player);
            if (playerCollider == null) {
                return false;
            }

            Physics2D.SyncTransforms();
            return meleeTriggerArea.Distance(playerCollider).isOverlapped;
        }

        // The player Transform is fetched fresh each frame; cache its collider and refresh only when
        // the underlying object changes (it is stable in practice).
        private Collider2D ResolvePlayerCollider(Transform player) {
            if (player == null) {
                return null;
            }

            if (cachedPlayerCollider == null || cachedPlayerCollider.transform != player) {
                cachedPlayerCollider = player.GetComponent<Collider2D>();
            }

            return cachedPlayerCollider;
        }

        // -------- Level helpers --------

        // Anchors are static scene objects, so the level-2 height is resolved once at Awake.
        private void ComputeLevels() {
            hasLevel2 = false;
            level2MinY = float.PositiveInfinity;
            if (anchors == null) {
                return;
            }

            for (int i = 0; i < anchors.Length; i++) {
                if (anchors[i] != null && anchors[i].Level == StoneGolemLevel.Level2) {
                    hasLevel2 = true;
                    level2MinY = Mathf.Min(level2MinY, anchors[i].Position.y);
                }
            }
        }

        private bool IsOnLevel2(float y) {
            return hasLevel2 && y >= level2MinY - levelTolerance;
        }

        private StoneGolemLevel LevelOf(float y) {
            return IsOnLevel2(y) ? StoneGolemLevel.Level2 : StoneGolemLevel.Level1;
        }

        // Nearest by 2D distance to the reference (the player). Ranking on full distance — not just X —
        // is what keeps the golem in the player's room: the two arenas are stacked vertically, so a
        // same-room anchor is always closer than a same-X anchor one room up/down. No per-anchor room
        // tag is needed; the player is always in the active room.
        private StoneGolemAnchorPoint NearestAnchor(StoneGolemLevel level, Vector2 reference) {
            StoneGolemAnchorPoint best = null;
            float bestSqr = float.PositiveInfinity;
            if (anchors == null) {
                return null;
            }

            for (int i = 0; i < anchors.Length; i++) {
                StoneGolemAnchorPoint a = anchors[i];
                if (a == null || !a.isActiveAndEnabled || a.Level != level) {
                    continue;
                }

                float sqr = ((Vector2)a.Position - reference).sqrMagnitude;
                if (sqr < bestSqr) {
                    bestSqr = sqr;
                    best = a;
                }
            }

            return best;
        }

        private Transform GetPlayer() {
            return G.Hero != null && G.Hero.Controller != null
                ? G.Hero.Controller.transform
                : null;
        }
    }
}
