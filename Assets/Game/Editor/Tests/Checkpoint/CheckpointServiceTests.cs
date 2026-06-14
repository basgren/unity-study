using Game.Core.Services;
using Game.Features.Interactive.Bonfire;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests.Checkpoint {
    public class CheckpointServiceTests {
        private CheckpointService NewService() {
            var go = new GameObject("CheckpointService");
            return go.AddComponent<CheckpointService>();
        }

        private static CheckpointRef Ref(string localId) {
            // Default SceneReference is empty; GetSceneName() returns "" which is fine for these tests.
            return new CheckpointRef { LocalId = localId };
        }

        [Test]
        public void Reset_ClearsCurrentAndPendingRespawn() {
            var service = NewService();
            service.Activate(Ref("cp_a"));
            service.RequestRespawn();
            Assert.IsTrue(service.Current.HasValue);
            Assert.IsTrue(service.HasPendingRespawn);

            service.Reset();

            Assert.IsFalse(service.Current.HasValue);
            Assert.IsFalse(service.HasPendingRespawn);
            Assert.IsFalse(service.IsBonfireRestTransitionActive);

            Object.DestroyImmediate(service.gameObject);
        }

        [Test]
        public void Reset_ForgetsDiscoveredBonfires() {
            var service = NewService();
            service.Activate(Ref("cp_a"));
            Assert.AreEqual(BonfireState.Current, service.GetBonfireState("", "cp_a"));

            service.Reset();

            Assert.AreEqual(BonfireState.Undiscovered, service.GetBonfireState("", "cp_a"));

            Object.DestroyImmediate(service.gameObject);
        }

        [Test]
        public void Reset_FiresCheckpointChangedWithNull() {
            var service = NewService();
            service.Activate(Ref("cp_a"));

            CheckpointRef? lastValue = Ref("sentinel");
            var fired = false;
            service.OnCheckpointChanged += value => { fired = true; lastValue = value; };

            service.Reset();

            Assert.IsTrue(fired);
            Assert.IsFalse(lastValue.HasValue);

            Object.DestroyImmediate(service.gameObject);
        }
    }
}
