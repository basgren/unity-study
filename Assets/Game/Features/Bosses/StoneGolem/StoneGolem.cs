using System;
using System.Collections;
using Core.Audio;
using Game.Core.Bootstrap;
using Game.Core.Components.Base2D;
using Game.Core.Components.Damage;
using Game.Core.Services.Scene;
using Game.Features.Bosses._Shared;
using Game.Features.Bosses.StoneGolem.Actions;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {

    static class StoneGolemAnimKeys {
        public static readonly int IsImmune = Animator.StringToHash("isImmune");
        public static readonly int OnDeath = Animator.StringToHash("onDeath");
    }
    
    /// <summary>
    /// Stone Golem boss controller. Acts as the action HOST: it owns the named action components
    /// (the boss's physical capabilities), runs one <see cref="EnemyAction"/> at a time, and relays
    /// animation events to it. Locomotion is exposed for selectors and patterns to drive.
    ///
    /// Action references live here (not on the selector) so every system — <see cref="StoneGolemAI"/>,
    /// debug controls, future patterns and cutscenes — asks the controller "do X" instead of holding
    /// its own wiring. The controller still holds no per-action logic: each action component owns
    /// its own animation triggers, damagers, and prefabs, and the animation-event surface is the
    /// fixed generic relay (<see cref="OnActionDo"/> / <see cref="OnActionTearDown"/>).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Facing2D))]
    public class StoneGolem : MonoBehaviour {
        /// <summary>
        /// Action slots wired from child components on the golem prefab. Grouped so the inspector
        /// shows a single collapsible "Actions" section. Populated by <see cref="AutoBind"/> on
        /// component add (<see cref="Reset"/>) and on demand via the gear-menu "Auto-bind" entry;
        /// manual assignment is preserved (empty slots only are filled).
        ///
        /// Naming convention (for hierarchy discoverability): each field <c>foo</c> should live on a
        /// child GameObject named <c>FooAction</c> — e.g. <c>melee</c> → <c>MeleeAction</c>,
        /// <c>handShoot</c> → <c>HandShootAction</c>, <c>beamShoot</c> → <c>BeamShootAction</c>.
        /// Auto-bind currently matches by type alone (each action has a unique concrete type); the
        /// naming convention still applies so the prefab structure stays self-documenting and so
        /// the name fallback can be reinstated cheaply if two slots ever share a type again.
        /// </summary>
        [Serializable]
        public class ActionRefs {
            public StoneGolemMeleeAction melee;
            public StoneGolemProjectileShootAction handShoot;
            public StoneGolemBeamShootAction beamShoot;
            public StoneGolemGroundHitAction groundHit;
            public StoneGolemLaserCrossAction laserCross;
            public StoneGolemStoneWaveAction stoneWave;
        }

        /// <summary>
        /// Per-call tuning for <see cref="SlamToGround"/>. Each caller (intro drop, ground hit,
        /// ground break) serializes its own instance, so every slam variant gets its own fall
        /// speed, camera shake, and impact sound.
        /// </summary>
        [Serializable]
        public class SlamSettings {
            [Tooltip("Gravity scale used during the fall — bigger = faster, more furious slam.")]
            public float gravityScale = 1.5f;

            [Tooltip("Camera shake fired on slam impact.")]
            public CameraShakeSettings shake = CameraShakeSettings.Default;

            [Tooltip("Optional impact sound played at the golem's position. None = silent.")]
            public AudioCue impactSound;
        }

        [Header("Locomotion")]
        [SerializeField]
        [Tooltip("Horizontal walk speed used by MoveTowards / WalkTo. The golem is physics-bound and cannot fly.")]
        private float moveSpeed = 3f;

        [SerializeField]
        [Tooltip("Seconds to ramp horizontal velocity from 0 up to full moveSpeed. 0 = instant snap.")]
        private float speedBuildUpTime = 0.25f;

        [SerializeField]
        [Tooltip("Seconds to ramp horizontal velocity back to 0 (or shed overspeed). 0 = instant stop.")]
        private float speedDecayTime = 0.2f;

        [SerializeField]
        private Collider2D normalCollider;
        
        [SerializeField]
        private Collider2D immuneCollider;

        [Header("Un-ball")]
        [SerializeField]
        [Tooltip("Time of the upward lift played while the un-ball animation runs, so the taller " +
                 "normal collider clears the ground before it swaps in.")]
        private float unballLiftTime = 0.3f;

        [SerializeField]
        [Tooltip("Safety timeout after the lift: if the un-ball animation event (OnUnballFinished) " +
                 "never arrives, the collider swap is applied anyway so the golem cannot get stuck " +
                 "in ball state.")]
        private float unballEventTimeout = 1.5f;

        [SerializeField]
        private AudioCue collapseSound;

        [SerializeField]
        [Tooltip("Played once when the golem dies. None = silent.")]
        private AudioCue deathSound;

        [Header("Actions")]
        [SerializeField]
        private ActionRefs actions;

        // Extra clearance above the measured collider delta so the swapped-in normal collider
        // never starts overlapping the ground.
        private const float UnballLiftSkin = 0.05f;

        // Hard cap on a slam: if the ground collision never fires (e.g. the golem starts flush
        // with the floor, where OnCollisionEnter2D never arrives), the slam force-finishes so a
        // cutscene cannot soft-lock.
        private const float MaxSlamDuration = 4f;

        // Absolute floor (u/s) under the braking curve — a numeric backstop so a discretized brake
        // can never command exactly zero before the arrive check trips. Note this alone cannot push
        // the body through ground friction (which absorbs roughly 0.3-0.4 u/s per physics step for
        // this golem); the actual anti-stall guarantee is that braking targets the point itself, so
        // speed at an arrive ring of radius r is sqrt(2 · decel · r) — well above the friction band.
        private const float MinWalkSpeed = 0.1f;

        private Animator animator;
        private Rigidbody2D myRigidbody;
        private Facing2D facing;
        private Damageable damageable;
        private float defaultGravityScale;
        private int groundLayer;
        private float unballLift;

        private EnemyAction activeAction;
        private bool isDead;
        private bool isImmune;
        private bool isDoingSlam;
        private bool isSlamDone;
        private SlamSettings activeSlamSettings;
        private Coroutine unballRoutine;
        private bool unballAnimFinished;
        private float walkTargetX;
        private bool isWalking;
        // Commanded walk velocity, owned here and ramped over time. NOT read back from the
        // rigidbody: contacts and ground friction shave the body's vx between writes, and a
        // read-back ramp would keep restarting from ~0 (the golem creeps instead of walking).
        private float walkVelocityCommand;

        /// <summary>True while an action is running. The selector must not start another action until this clears.</summary>
        public bool IsBusy => activeAction != null && activeAction.IsRunning;

        /// <summary>True once the golem's <see cref="Damageable"/> reports death (if one is wired).</summary>
        public bool IsDead => isDead;

        /// <summary>
        /// True if the golem's <see cref="Damageable"/> was hit this frame. Drives the reactive melee
        /// counter. Always false when no Damageable is wired. Reset in the Damageable's LateUpdate, so
        /// read it from an Update (which the selector does).
        /// </summary>
        public bool WasHitThisFrame => damageable != null && damageable.IsHitThisFrame;

        /// <summary>
        /// Current health as a 0..1 fraction of max, for phase-transition checks. Returns 1 when no
        /// Damageable is wired (so a golem without one never trips a low-health phase shift).
        /// </summary>
        public float HealthFraction =>
            damageable != null && damageable.maxHealth > 0f ? damageable.Health / damageable.maxHealth : 1f;

        /// <summary>The golem's <see cref="Damageable"/>, or null if none is wired. Used to engage the boss health bar.</summary>
        public Damageable Damageable => damageable;

        /// <summary>Current facing sign: +1 right, -1 left. Used by actions to launch projectiles.</summary>
        public int FacingSign => facing.DirSign;

        /// <summary>Step 1 of the action lifecycle: fires after <see cref="EnemyAction.Begin"/> succeeds.</summary>
        public event Action<EnemyAction> ActionStarted;

        /// <summary>Step 2: fires after the active action's <see cref="EnemyAction.Do"/> step (effect frame relayed from animation).</summary>
        public event Action<EnemyAction> ActionDo;

        /// <summary>Step 3: fires after the active action's <see cref="EnemyAction.TearDown"/> step.</summary>
        public event Action<EnemyAction> ActionTearDown;

        /// <summary>Step 4: fires after the action ends — both natural completion and forced cancel on death.</summary>
        public event Action<EnemyAction> ActionEnded;

        /// <summary>Melee swing action component, or null if unwired.</summary>
        public StoneGolemMeleeAction Melee => actions != null ? actions.melee : null;

        /// <summary>Hand projectile action component, or null if unwired.</summary>
        public StoneGolemProjectileShootAction HandShoot => actions != null ? actions.handShoot : null;

        /// <summary>Beam cast action component, or null if unwired.</summary>
        public StoneGolemBeamShootAction BeamShoot => actions != null ? actions.beamShoot : null;

        /// <summary>Ground-hit (falling stones) action component, or null if unwired.</summary>
        public StoneGolemGroundHitAction GroundHit => actions != null ? actions.groundHit : null;

        /// <summary>Laser-cross (phase 2) action component, or null if unwired.</summary>
        public StoneGolemLaserCrossAction LaserCross => actions != null ? actions.laserCross : null;

        /// <summary>Stone-wave (phase 2) action component, or null if unwired.</summary>
        public StoneGolemStoneWaveAction StoneWave => actions != null ? actions.stoneWave : null;

        private void Reset() {
            AutoBind();
        }

        /// <summary>
        /// Fills any empty <see cref="ActionRefs"/> slot by searching child components by type.
        /// Each action currently has a unique concrete type so type alone disambiguates; the
        /// <see cref="ActionRefs"/> naming convention still applies for hierarchy discoverability.
        /// </summary>
        [ContextMenu("Auto-bind")]
        private void AutoBind() {
            if (actions == null) {
                actions = new ActionRefs();
            }

            if (actions.melee == null) {
                actions.melee = GetComponentInChildren<StoneGolemMeleeAction>(true);
            }

            if (actions.handShoot == null) {
                actions.handShoot = GetComponentInChildren<StoneGolemProjectileShootAction>(true);
            }

            if (actions.beamShoot == null) {
                actions.beamShoot = GetComponentInChildren<StoneGolemBeamShootAction>(true);
            }

            if (actions.groundHit == null) {
                actions.groundHit = GetComponentInChildren<StoneGolemGroundHitAction>(true);
            }

            if (actions.laserCross == null) {
                actions.laserCross = GetComponentInChildren<StoneGolemLaserCrossAction>(true);
            }

            if (actions.stoneWave == null) {
                actions.stoneWave = GetComponentInChildren<StoneGolemStoneWaveAction>(true);
            }
        }

        private void Awake() {
            myRigidbody = GetComponent<Rigidbody2D>();
            facing = GetComponent<Facing2D>();
            damageable = GetComponent<Damageable>();
            defaultGravityScale = myRigidbody.gravityScale;
            animator = GetComponent<Animator>();
            groundLayer = LayerMask.NameToLayer("Ground");
            unballLift = ComputeUnballLift();

            SetImmune(true, false);
        }

        private void Update() {
            if (isDead) {
                return;
            }

            // Poll for death rather than subscribing: keeps the Damageable dependency optional
            // (the golem still works without one wired) and matches the VengefulSpirit pattern.
            if (damageable != null && damageable.IsDead) {
                HandleDeath();
            }
        }

        private void OnCollisionEnter2D(Collision2D other) {
            if (!isDoingSlam) {
                return;
            }

            if (other.gameObject.layer == groundLayer) {
                OnSlamImpact();
            }
        }

        /// <summary>
        /// Starts the given action if the golem is free. The action drives its own lifecycle and
        /// reports back via <see cref="EnemyAction.Completed"/>; the relays below forward animation
        /// events to it while it runs. No-op while busy or dead.
        /// </summary>
        public void RunAction(EnemyAction action) {
            if (action == null || IsBusy || isDead) {
                return;
            }

            StopMoving();
            activeAction = action;
            // Unsubscribe first so a re-run (or a prior Cancel that left the handler attached)
            // can never double-subscribe.
            action.Completed -= OnActiveActionCompleted;
            action.Completed += OnActiveActionCompleted;
            action.Begin();
            ActionStarted?.Invoke(action);
        }

        private void OnActiveActionCompleted(EnemyAction action) {
            action.Completed -= OnActiveActionCompleted;
            if (activeAction == action) {
                activeAction = null;
            }

            ActionEnded?.Invoke(action);
        }

        // ---- Animation event relays (generic; clips reference these names, not per-action ones) ----

        /// <summary>Animation event: the active action's effect frame (open window / spawn / cast).</summary>
        public void OnActionDo() {
            if (activeAction == null) {
                return;
            }

            EnemyAction current = activeAction;
            current.Do();
            ActionDo?.Invoke(current);
        }

        /// <summary>Animation event: the active action begins shutting down (e.g. close damage window).</summary>
        public void OnActionTearDown() {
            if (activeAction == null) {
                return;
            }

            EnemyAction current = activeAction;
            current.TearDown();
            ActionTearDown?.Invoke(current);
        }

        // ---- Locomotion (called by the selector) ----

        /// <summary>
        /// Walks horizontally toward the given world X at <see cref="moveSpeed"/> and faces that way.
        /// Velocity ramps up/down (same mechanism as the hero) instead of snapping, and brakes on the
        /// approach so speed reaches ~zero at the target point itself. Callers treat a small remaining
        /// distance as arrived and stop there — the brake curve keeps the residual speed low enough
        /// that the final stop reads as a settle, not a cut. Vertical velocity is left to gravity.
        /// Call every frame while chasing. No-op while busy or dead.
        /// </summary>
        public void MoveTowards(float worldX) {
            if (IsBusy || isDead) {
                return;
            }

            float dx = worldX - transform.position.x;
            int dir = dx > 0f ? 1 : (dx < 0f ? -1 : 0);
            if (dir != 0) {
                facing.SetByX(dx);
            }

            // Track the target for the walk gizmo (cleared by StopMoving).
            walkTargetX = worldX;
            isWalking = true;

            // Brake ahead of the stop point: cap the cruise speed by the curve that reaches zero
            // exactly at the target (v = sqrt(2 · decel · remaining)). The floor is only a numeric
            // backstop — see MinWalkSpeed.
            float cruise = moveSpeed;
            float decel = speedDecayTime > 0f ? moveSpeed / speedDecayTime : 0f;
            if (decel > 0f) {
                float braked = Mathf.Max(Mathf.Sqrt(2f * decel * Mathf.Abs(dx)), MinWalkSpeed);
                cruise = Mathf.Min(cruise, braked);
            }

            RampHorizontalVelocity(dir * cruise);
        }

        /// <summary>
        /// Scripted velocity-based walk to the given world X — the same ramped ground movement the
        /// hero uses, NOT a position lerp. For patterns that move the golem while an action runs
        /// (bypasses the IsBusy guard of <see cref="MoveTowards"/>). Gravity stays on: the golem
        /// walks on the ground. Braking starts ahead of the target (v = sqrt(2 · decel · distance),
        /// with decel = moveSpeed / speedDecayTime), so velocity reaches zero exactly at the target
        /// point instead of overshooting and snapping. The target is drawn as a gizmo while walking.
        /// </summary>
        public IEnumerator WalkTo(float worldX, float tolerance = 0.05f) {
            walkTargetX = worldX;
            isWalking = true;

            // Physics-step cadence so each velocity write lands right before a simulation step
            // (a per-render-frame write can let several physics steps run on a stale velocity).
            WaitForFixedUpdate wait = new WaitForFixedUpdate();
            while (!isDead) {
                float remaining = worldX - myRigidbody.position.x;
                float dist = Mathf.Abs(remaining);
                if (dist <= tolerance) {
                    break;
                }

                facing.SetByX(remaining);

                float dt = Time.fixedDeltaTime;
                float accel = speedBuildUpTime > 0f ? moveSpeed / speedBuildUpTime : float.PositiveInfinity;
                float decel = speedDecayTime > 0f ? moveSpeed / speedDecayTime : float.PositiveInfinity;

                // Ramp the owned command up toward full speed (not the body's actual vx — see
                // walkVelocityCommand)...
                float speed = Mathf.MoveTowards(Mathf.Abs(walkVelocityCommand), moveSpeed, accel * dt);
                // ...capped by the braking curve that reaches zero at the target POINT, not at the
                // tolerance ring. Braking toward the ring commands a near-zero speed just outside
                // it, ground friction absorbs that entirely within one physics step, and the golem
                // freezes short of the ring forever (observed stall at dist ≈ tolerance + 0.007).
                // Braking to the point keeps the ring-crossing speed at sqrt(2 · decel · tolerance),
                // well above the friction band, so the loop always exits...
                if (!float.IsPositiveInfinity(decel)) {
                    float braked = Mathf.Sqrt(2f * decel * dist);
                    speed = Mathf.Min(speed, Mathf.Max(braked, MinWalkSpeed));
                }

                // ...and never step past the target within a single physics step.
                speed = Mathf.Min(speed, dist / dt);

                walkVelocityCommand = Mathf.Sign(remaining) * speed;
                myRigidbody.velocity = new Vector2(walkVelocityCommand, myRigidbody.velocity.y);
                yield return wait;
            }

            StopMoving();
        }

        /// <summary>
        /// Mirrors the hero's movement ramp (see PlayerController): accelerate toward the target
        /// velocity over <see cref="speedBuildUpTime"/>, shed speed over <see cref="speedDecayTime"/>.
        /// Ramps the owned <see cref="walkVelocityCommand"/> (not the body's actual vx) and writes it
        /// to the body, so contact hits / friction can't keep knocking the ramp back to zero.
        /// </summary>
        private void RampHorizontalVelocity(float targetVx) {
            float rampTime = Mathf.Abs(walkVelocityCommand) < Mathf.Abs(targetVx) ? speedBuildUpTime : speedDecayTime;

            if (rampTime > 0f) {
                float maxDelta = (moveSpeed / rampTime) * Time.deltaTime;
                walkVelocityCommand = Mathf.MoveTowards(walkVelocityCommand, targetVx, maxDelta);
            } else {
                walkVelocityCommand = targetVx;
            }

            myRigidbody.velocity = new Vector2(walkVelocityCommand, myRigidbody.velocity.y);
        }

        /// <summary>Orients the golem toward the given world X without moving. No-op while dead or if the target is dead-ahead.</summary>
        public void FaceTowards(float worldX) {
            if (isDead) {
                return;
            }

            facing.SetByX(worldX - transform.position.x);
        }

        /// <summary>Zeroes horizontal velocity, leaving vertical motion (gravity) untouched.</summary>
        public void StopMoving() {
            // Any explicit stop also ends a scripted walk (clears the walk-target gizmo even if the
            // WalkTo coroutine was killed externally, e.g. by an action force-complete).
            isWalking = false;
            walkVelocityCommand = 0f;
            myRigidbody.velocity = new Vector2(0f, myRigidbody.velocity.y);
        }

        /// <summary>
        /// Toggles the body's gravity for scripted flight / slam moves. Disabling also zeroes velocity
        /// so the golem holds still instead of drifting; re-enabling restores the original gravity
        /// scale. Reusable by the ground-hit slam, the transition cutscene, and arena flight.
        /// </summary>
        public void SetGravityActive(bool active) {
            myRigidbody.gravityScale = active ? defaultGravityScale : 0f;
            if (!active) {
                myRigidbody.velocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Scripted vertical move by <paramref name="deltaY"/> over <paramref name="duration"/> seconds
        /// via the body. The caller owns gravity (call <see cref="SetGravityActive"/> first) since this
        /// drives position directly and would otherwise fight the gravity-accumulated velocity.
        /// Pass <paramref name="easeOut"/> for a fast-start / soft-arrival "float up" feel.
        /// </summary>
        public IEnumerator MoveByVertical(float deltaY, float duration, bool easeOut = false) {
            yield return MoveTo(myRigidbody.position + new Vector2(0f, deltaY), duration, easeOut);
        }

        /// <summary>
        /// Scripted move to an absolute world position over <paramref name="duration"/> seconds via the
        /// body. The caller owns gravity (call <see cref="SetGravityActive"/> first). Linear by default;
        /// <paramref name="easeOut"/> decelerates into the target (fast start, soft arrival).
        /// For ascents and flight only — ground moves must use the velocity-based
        /// <see cref="WalkTo"/> / <see cref="MoveTowards"/> instead of a position lerp.
        /// </summary>
        public IEnumerator MoveTo(Vector2 target, float duration, bool easeOut = false) {
            Vector2 start = myRigidbody.position;

            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                if (easeOut) {
                    // Cubic ease-out: fast start, decelerating arrival — reads as a quick float up
                    // rather than a constant-speed lerp.
                    float inv = 1f - k;
                    k = 1f - inv * inv * inv;
                }

                myRigidbody.MovePosition(Vector2.Lerp(start, target, k));
                yield return null;
            }

            myRigidbody.MovePosition(target);
        }

        /// <summary>
        /// Flies to a world position over <paramref name="duration"/> seconds with gravity disabled for
        /// the trip and restored on arrival, so the golem rests on whatever it lands on (ground or a
        /// platform). Used for arena level changes ("rises/descends by magic").
        /// </summary>
        public IEnumerator FlyTo(Vector2 target, float duration) {
            SetGravityActive(false);
            yield return MoveTo(target, duration);
            SetGravityActive(true);
        }

        /// <summary>
        /// Scripted parabolic hop from the current position to <paramref name="target"/> over
        /// <paramref name="duration"/> seconds, peaking <paramref name="arcHeight"/> above the straight
        /// line at the midpoint. Progress is eased in/out (slow start, slow finish) so the heavy golem
        /// reads as "levitating" rather than snapping. Gravity is disabled for the hop and restored on
        /// landing so the golem rests on the destination. Used for graph jumps between arena levels and
        /// the low hop across the platform gap.
        /// </summary>
        public IEnumerator JumpTo(Vector2 target, float duration, float arcHeight) {
            SetGravityActive(false);
            Vector2 start = myRigidbody.position;

            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                // Ease-in-out the travel so both the horizontal glide and the arc start/finish slowly.
                float e = Mathf.SmoothStep(0f, 1f, k);
                Vector2 pos = Vector2.Lerp(start, target, e);
                // Parabola: 0 at both ends, arcHeight at the midpoint (4 * e * (1 - e) peaks at e = 0.5).
                pos.y += arcHeight * 4f * e * (1f - e);
                myRigidbody.MovePosition(pos);
                yield return null;
            }

            myRigidbody.MovePosition(target);
            SetGravityActive(true);
        }

        /// <summary>
        /// All-direction shake around the current position with amplitude ramping from 0 to
        /// <paramref name="maxAmplitude"/> over <paramref name="duration"/>, returning to the base
        /// position at the end. Caller owns gravity. Used by the transition cutscene's wind-up.
        /// </summary>
        public IEnumerator Shake(float duration, float maxAmplitude, float frequency) {
            Vector2 basePos = myRigidbody.position;

            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float amp = duration > 0f ? maxAmplitude * (t / duration) : maxAmplitude;
                // Two sines with an irrational-ish frequency ratio trace a non-repeating 2D pattern
                // (Lissajous), so the shake covers all directions instead of a fixed axis.
                float angle = t * frequency * Mathf.PI * 2f;
                Vector2 offset = new Vector2(Mathf.Sin(angle), Mathf.Sin(angle * 1.31f)) * amp;
                myRigidbody.MovePosition(basePos + offset);
                yield return null;
            }

            myRigidbody.MovePosition(basePos);
        }
        
        /// <summary>
        /// Toggles the ball ("immune" — the animation asset's name) state. Balling up applies
        /// immediately: ball collider on, gravity zeroed. Un-balling is animation-driven: the golem
        /// first lifts by <see cref="unballLift"/> so the taller normal collider clears the ground,
        /// then the collider swap happens when the un-ball animation reports done via
        /// <see cref="OnUnballFinished"/> (with a timeout fallback).
        /// </summary>
        public void SetImmune(bool value, bool playSound = true) {
            if (isImmune == value) {
                return;
            }

            isImmune = value;
            animator.SetBool(StoneGolemAnimKeys.IsImmune, value);
            if (playSound) {
                G.Audio.Play2D(collapseSound);                
            }

            // Re-entrancy: balling up again mid-un-ball just drops the pending swap.
            if (unballRoutine != null) {
                StopCoroutine(unballRoutine);
                unballRoutine = null;
            }

            if (value) {
                SetImmuneStateEnabled(true);
            } else {
                unballAnimFinished = false;
                unballRoutine = StartCoroutine(UnballRoutine());
            }
        }

        /// <summary>
        /// Animation event: end of the un-ball clip. Signals <see cref="UnballRoutine"/> that the
        /// collider swap may be applied.
        /// </summary>
        public void OnUnballFinished() {
            unballAnimFinished = true;
        }

        /// <summary>
        /// Atomic slam: free-falls (in ball form) from the current position until the ground is
        /// hit, then fires the impact effects from <paramref name="settings"/> (camera shake,
        /// sound). Shared by the intro drop, Ground Hit, and Ground Break — each caller passes its
        /// own tuning. The golem is forced into ball form if not already there.
        /// </summary>
        public IEnumerator SlamToGround(SlamSettings settings) {
            if (isDoingSlam) {
                yield break;
            }

            isDoingSlam = true;
            isSlamDone = false;
            activeSlamSettings = settings;

            // Defensive: callers slam in ball form; force it in case one forgot.
            SetImmune(true);
            // SetImmune zeroed gravity for the ball state — override with the slam fall speed.
            myRigidbody.gravityScale = settings.gravityScale;

            float elapsed = 0f;
            while (!isSlamDone && elapsed < MaxSlamDuration) {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!isSlamDone) {
                // No ground collision arrived (started flush with the floor) — finish anyway.
                OnSlamImpact();
            }
        }

        private IEnumerator UnballRoutine() {
            // Lift so the taller normal collider won't start inside the ground when it swaps in.
            // Vertical-only on purpose: the AI may already drive a walk (the action that un-balled
            // has completed by now), so the lift must not drag X back to a captured start position.
            SetGravityActive(false);
            float startY = myRigidbody.position.y;
            float t = 0f;
            while (t < unballLiftTime) {
                t += Time.deltaTime;
                float k = unballLiftTime > 0f ? Mathf.Clamp01(t / unballLiftTime) : 1f;
                // Same cubic ease-out as the scripted ascents.
                float inv = 1f - k;
                k = 1f - inv * inv * inv;
                myRigidbody.MovePosition(new Vector2(myRigidbody.position.x, startY + unballLift * k));
                yield return null;
            }

            // Wait for the un-ball animation to finish, with a timeout fallback so a missing
            // animation event can never leave the golem stuck on the ball collider.
            float elapsed = 0f;
            while (!unballAnimFinished && elapsed < unballEventTimeout) {
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetImmuneStateEnabled(false);
            unballRoutine = null;
        }

        private void SetImmuneStateEnabled(bool value) {
            normalCollider.enabled = !value;
            immuneCollider.enabled = value;
            myRigidbody.gravityScale = value ? 0f : defaultGravityScale;
        }

        private void OnSlamImpact() {
            if (activeSlamSettings != null) {
                G.Camera.Shake(activeSlamSettings.shake);
                if (activeSlamSettings.impactSound != null) {
                    G.Audio.PlayAt(activeSlamSettings.impactSound, transform.position);
                }
            }

            isSlamDone = true;
            isDoingSlam = false;
        }

        /// <summary>
        /// How far the golem must rise before un-balling so the taller normal collider clears the
        /// ground the ball collider currently rests on. Computed analytically from the collider
        /// shapes (works while either collider is disabled, unlike Bounds).
        /// </summary>
        private float ComputeUnballLift() {
            float delta = GetBottomLocalY(immuneCollider) - GetBottomLocalY(normalCollider);
            return Mathf.Max(0f, delta * Mathf.Abs(transform.lossyScale.y)) + UnballLiftSkin;
        }

        private static float GetBottomLocalY(Collider2D col) {
            if (col is CapsuleCollider2D capsule) {
                return capsule.offset.y - capsule.size.y * 0.5f;
            }

            if (col is BoxCollider2D box) {
                return box.offset.y - box.size.y * 0.5f;
            }

            if (col is CircleCollider2D circle) {
                return circle.offset.y - circle.radius;
            }

            // Unsupported shape: fall back to the offset; the lift skin still gives some clearance.
            return col.offset.y;
        }

        private void HandleDeath() {
            isDead = true;

            if (activeAction != null) {
                // Capture before Cancel so subscribers still see which action ended.
                EnemyAction cancelled = activeAction;
                activeAction.Cancel();
                activeAction = null;
                ActionEnded?.Invoke(cancelled);
            }

            StopMoving();

            // Hide the boss health bar (the intro engaged it on fight start). DisengageBoss no-ops if
            // nothing is engaged, so it is safe even when no Damageable / BossFightService is wired.
            if (G.BossFight != null) {
                G.BossFight.DisengageBoss();
            }

            // Play the death animation last so it overrides whatever state a cancelled action left
            // the animator in.
            if (animator != null) {
                animator.SetTrigger(StoneGolemAnimKeys.OnDeath);
            }
        }
        
        public void OnPlayDeathSound() {
            G.Audio.PlayAt(deathSound, transform.position);
        }

        private void OnDisable() {
            // Scene-unload / deactivation safety: drop the boss bar so it never lingers into the next
            // scene if the golem is removed without going through HandleDeath. No-op if not engaged.
            if (G.BossFight != null) {
                G.BossFight.DisengageBoss();
            }
        }

        private void OnDrawGizmos() {
            // Live walk target while a scripted WalkTo runs (debug aid: shows where the golem is
            // actually heading and how far it got).
            if (!isWalking) {
                return;
            }

            Vector3 target = new Vector3(walkTargetX, transform.position.y, 0f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target);
            Gizmos.DrawWireSphere(target, 0.25f);
            Gizmos.DrawLine(target + Vector3.down, target + Vector3.up);
        }
    }
}
