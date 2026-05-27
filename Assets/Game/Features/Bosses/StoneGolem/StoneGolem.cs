using System;
using System.Collections;
using Core.Components.Base2D;
using Game.Core.Bootstrap;
using Game.Core.Components.Damage;
using Game.Features.Bosses._Shared;
using Game.Features.Bosses.StoneGolem.Actions;
using UnityEngine;

namespace Game.Features.Bosses.StoneGolem {
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

        [Header("Locomotion")]
        [SerializeField]
        [Tooltip("Horizontal walk speed used by MoveTowards. The golem is physics-bound and cannot fly.")]
        private float moveSpeed = 3f;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Golem animator. Resolved from this GameObject if left unset.")]
        private Animator animator;

        [SerializeField]
        [Tooltip("Trigger fired once on death to play the death animation.")]
        private string deathTrigger = "onDeath";

        [Header("Actions")]
        [SerializeField]
        private ActionRefs actions;

        private Rigidbody2D myRigidbody;
        private Facing2D facing;
        private Damageable damageable;
        private float defaultGravityScale;
        private int deathTriggerHash;

        private EnemyAction activeAction;
        private bool isDead;
        private bool isImmune;

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

            if (animator == null) {
                animator = GetComponent<Animator>();
            }

            deathTriggerHash = Animator.StringToHash(deathTrigger);
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
        /// Vertical velocity is left to gravity. No-op while busy or dead.
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

            myRigidbody.velocity = new Vector2(dir * moveSpeed, myRigidbody.velocity.y);
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
        /// </summary>
        public IEnumerator MoveByVertical(float deltaY, float duration) {
            yield return MoveTo(myRigidbody.position + new Vector2(0f, deltaY), duration);
        }

        /// <summary>
        /// Scripted move to an absolute world position over <paramref name="duration"/> seconds via the
        /// body. The caller owns gravity (call <see cref="SetGravityActive"/> first). Used by the
        /// ground-hit slam and the transition cutscene.
        /// </summary>
        public IEnumerator MoveTo(Vector2 target, float duration) {
            Vector2 start = myRigidbody.position;

            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
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
        /// Horizontal shake around the current position with amplitude ramping from 0 to
        /// <paramref name="maxAmplitude"/> over <paramref name="duration"/>, returning to the base
        /// position at the end. Caller owns gravity. Used by the transition cutscene's wind-up.
        /// </summary>
        public IEnumerator ShakeHorizontal(float duration, float maxAmplitude, float frequency) {
            Vector2 basePos = myRigidbody.position;

            float t = 0f;
            while (t < duration) {
                t += Time.deltaTime;
                float amp = duration > 0f ? maxAmplitude * (t / duration) : maxAmplitude;
                float offset = Mathf.Sin(t * frequency * Mathf.PI * 2f) * amp;
                myRigidbody.MovePosition(basePos + new Vector2(offset, 0f));
                yield return null;
            }

            myRigidbody.MovePosition(basePos);
        }
        
        public void SetIsImmune(bool value) {
            isImmune = value;
            // animator.SetBool("isImmune", value);
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
                animator.SetTrigger(deathTriggerHash);
            }
        }

        private void OnDisable() {
            // Scene-unload / deactivation safety: drop the boss bar so it never lingers into the next
            // scene if the golem is removed without going through HandleDeath. No-op if not engaged.
            if (G.BossFight != null) {
                G.BossFight.DisengageBoss();
            }
        }
    }
}
