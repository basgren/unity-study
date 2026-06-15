using Game.Configs;
using Game.Core.Models.Shop;
using Game.Features.Characters.Hero;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests.Hero {
    /// <summary>
    /// Locks in IsArmed as global, playthrough-persistent state owned by <see cref="PlayerState"/>.
    ///
    /// The "sword disappears when switching scenes" bug was caused by PlayerStateSaver round-tripping
    /// this flag through the per-scene save blob, so re-entering a scene last captured while unarmed
    /// clobbered the live armed flag back to false. The flag must live only on the persistent
    /// PlayerState, default to false, and never reset on its own as a side effect of other progress.
    /// </summary>
    public class PlayerStateArmedTests {
        private static PlayerState NewState() {
            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            return new PlayerState(config);
        }

        [Test]
        public void NewState_IsNotArmed() {
            var state = NewState();
            Assert.IsFalse(state.IsArmed);
        }

        [Test]
        public void IsArmed_SetTrue_Persists() {
            var state = NewState();
            state.IsArmed = true;
            Assert.IsTrue(state.IsArmed);
        }

        [Test]
        public void IsArmed_SetFalse_Persists() {
            var state = NewState();
            state.IsArmed = true;
            state.IsArmed = false;
            Assert.IsFalse(state.IsArmed);
        }

        [Test]
        public void IsArmed_SurvivesUnrelatedProgressChanges() {
            var state = NewState();
            state.IsArmed = true;

            // Mutating other persistent progression must not disturb the armed flag — it is
            // independent global state, not derived from flags, seen nodes, or stat levels.
            state.SetFlag("SomeEvent");
            state.MarkNodeSeen("rikko.intro");
            state.SetStatLevel(StatId.Health, 3);

            Assert.IsTrue(state.IsArmed);
        }
    }
}
