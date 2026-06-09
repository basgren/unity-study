using Game.Configs;
using Game.Features.Characters.Hero;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests.Dialog {
    public class PlayerStateSeenNodeTests {
        private PlayerState NewState() {
            var config = ScriptableObject.CreateInstance<PlayerConfig>();
            return new PlayerState(config);
        }

        [Test]
        public void HasSeenNode_UnknownKey_ReturnsFalse() {
            var state = NewState();
            Assert.IsFalse(state.HasSeenNode("rikko.intro"));
        }

        [Test]
        public void MarkNodeSeen_ThenHasSeenNode_ReturnsTrue() {
            var state = NewState();
            state.MarkNodeSeen("rikko.intro");
            Assert.IsTrue(state.HasSeenNode("rikko.intro"));
        }

        [Test]
        public void MarkNodeSeen_IsIdempotent() {
            var state = NewState();
            state.MarkNodeSeen("rikko.intro");
            state.MarkNodeSeen("rikko.intro");
            Assert.AreEqual(1, state.SeenDialogNodes.Count);
        }

        [Test]
        public void RestoreSeenDialogNodes_ReplacesContents() {
            var state = NewState();
            state.MarkNodeSeen("old.node");
            state.RestoreSeenDialogNodes(new[] { "a.b", "c.d" });
            Assert.IsFalse(state.HasSeenNode("old.node"));
            Assert.IsTrue(state.HasSeenNode("a.b"));
            Assert.IsTrue(state.HasSeenNode("c.d"));
        }
    }
}
