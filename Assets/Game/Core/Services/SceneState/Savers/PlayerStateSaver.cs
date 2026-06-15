using Game.Features.Characters.Hero;
using Game.Features.Hero;
using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    /// <summary>
    /// Inert — contributes nothing to the per-scene save blob.
    ///
    /// All hero state — equipped-weapon flag (<see cref="PlayerState.IsArmed"/>), stat upgrade
    /// levels, current HP, inventory, event flags, and seen dialog nodes — is global playthrough
    /// progression that lives in the in-memory <see cref="PlayerState"/> held by the
    /// DontDestroyOnLoad GameManager. That object already survives scene reloads, death, and bonfire
    /// rests, and <see cref="PlayerController"/> re-applies all of it on every scene load
    /// (Awake → InitFromState / UpdateAnimatorController).
    ///
    /// This saver previously round-tripped that global state through the per-scene save blob, which
    /// let re-entering an older scene clobber the live values with that scene's stale snapshot — e.g.
    /// picking up the sword, then walking into a scene last visited unarmed, dropped IsArmed back to
    /// false and the sword vanished (the same applied to HP and stat levels). The identical bug was
    /// already fixed for event flags and seen dialog nodes; this removes the remaining offenders.
    ///
    /// The component can be removed from the Hero prefab via the Unity Editor (Remove Component),
    /// together with its <see cref="StateRoot"/> if the hero gains no other savers. Do this in the
    /// editor — NOT by hand-editing prefab/scene YAML — so it propagates correctly to the per-scene
    /// Hero instances, whose StateRoot carries a per-instance saveId.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerStateSaver : StateSaverBase {
        public override string Slot => "player";

        // Intentionally empty: hero state is global (see class summary), never per-scene.
        public override void Capture(IStateWriter w) { }

        public override void Restore(IStateReader r) { }
    }
}
