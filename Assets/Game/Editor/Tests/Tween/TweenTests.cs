using System;
using Game.Core.Services.Tween;
using NUnit.Framework;

namespace Game.Editor.Tests.Tween {
    public class EaseTests {
        [Test]
        public void Linear_ReturnsInput() {
            Assert.AreEqual(0.5f, Ease.Evaluate(EaseType.Linear, 0.5f), 1e-5f);
        }

        [Test]
        public void AllTypes_ReturnZeroAtZero() {
            foreach (EaseType type in Enum.GetValues(typeof(EaseType))) {
                Assert.AreEqual(0f, Ease.Evaluate(type, 0f), 1e-5f, $"{type} at t=0");
            }
        }

        [Test]
        public void AllTypes_ReturnOneAtOne() {
            foreach (EaseType type in Enum.GetValues(typeof(EaseType))) {
                Assert.AreEqual(1f, Ease.Evaluate(type, 1f), 1e-5f, $"{type} at t=1");
            }
        }

        [Test]
        public void InQuad_MidpointValue() {
            Assert.AreEqual(0.25f, Ease.Evaluate(EaseType.InQuad, 0.5f), 1e-5f);
        }

        [Test]
        public void OutQuad_MidpointValue() {
            Assert.AreEqual(0.75f, Ease.Evaluate(EaseType.OutQuad, 0.5f), 1e-5f);
        }

        [Test]
        public void InOutQuad_MidpointValue() {
            Assert.AreEqual(0.5f, Ease.Evaluate(EaseType.InOutQuad, 0.5f), 1e-5f);
        }

        [Test]
        public void InCubic_MidpointValue() {
            Assert.AreEqual(0.125f, Ease.Evaluate(EaseType.InCubic, 0.5f), 1e-5f);
        }

        [Test]
        public void OutCubic_MidpointValue() {
            Assert.AreEqual(0.875f, Ease.Evaluate(EaseType.OutCubic, 0.5f), 1e-5f);
        }

        [Test]
        public void InOutCubic_MidpointValue() {
            Assert.AreEqual(0.5f, Ease.Evaluate(EaseType.InOutCubic, 0.5f), 1e-5f);
        }

        [Test]
        public void ClampsInputBelowZero() {
            Assert.AreEqual(0f, Ease.Evaluate(EaseType.Linear, -0.5f), 1e-5f);
        }

        [Test]
        public void ClampsInputAboveOne() {
            Assert.AreEqual(1f, Ease.Evaluate(EaseType.Linear, 1.5f), 1e-5f);
        }
    }

    public class TweenInstanceTests {
        private TweenInstance CreateInstance(
            float duration = 1f,
            EaseType easeType = EaseType.Linear,
            Action onBeforeStart = null,
            Action<float, float> onUpdate = null,
            Action onComplete = null,
            Action onStop = null
        ) {
            return new TweenInstance(new TweenParams(
                duration: duration,
                easeType: easeType,
                useUnscaledTime: false,
                onBeforeStart: onBeforeStart,
                onUpdate: onUpdate ?? ((_, _) => { }),
                onComplete: onComplete,
                onStop: onStop
            ));
        }

        [Test]
        public void OnBeforeStart_CalledOnceOnFirstTick() {
            int count = 0;
            var instance = CreateInstance(onBeforeStart: () => count++);

            instance.Tick(0.1f);
            instance.Tick(0.1f);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void OnUpdate_ReceivesCorrectProgress() {
            float lastProgress = -1f;
            float lastEased = -1f;
            var instance = CreateInstance(
                duration: 1f,
                onUpdate: (eased, progress) => {
                    lastEased = eased;
                    lastProgress = progress;
                }
            );

            instance.Tick(0.5f);

            Assert.AreEqual(0.5f, lastProgress, 1e-5f);
            Assert.AreEqual(0.5f, lastEased, 1e-5f);
        }

        [Test]
        public void OnUpdate_ReceivesEasedValue() {
            float lastEased = -1f;
            float lastProgress = -1f;
            var instance = CreateInstance(
                duration: 1f,
                easeType: EaseType.InQuad,
                onUpdate: (eased, progress) => {
                    lastEased = eased;
                    lastProgress = progress;
                }
            );

            instance.Tick(0.5f);

            Assert.AreEqual(0.5f, lastProgress, 1e-5f);
            Assert.AreEqual(0.25f, lastEased, 1e-5f);
        }

        [Test]
        public void OnComplete_FiredAtEnd() {
            int count = 0;
            var instance = CreateInstance(duration: 0.5f, onComplete: () => count++);

            instance.Tick(0.5f);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void OnComplete_NotFiredBeforeEnd() {
            int count = 0;
            var instance = CreateInstance(duration: 1f, onComplete: () => count++);

            instance.Tick(0.25f);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void OnStop_FiredOnManualStop() {
            int count = 0;
            var instance = CreateInstance(duration: 1f, onStop: () => count++);

            instance.Tick(0.1f);
            instance.Stop();

            Assert.AreEqual(1, count);
        }

        [Test]
        public void OnStop_NotFiredOnNaturalCompletion() {
            int count = 0;
            var instance = CreateInstance(duration: 0.5f, onStop: () => count++);

            instance.Tick(0.5f);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Pause_FreezesProgress() {
            float lastProgress = -1f;
            var instance = CreateInstance(
                duration: 1f,
                onUpdate: (_, progress) => lastProgress = progress
            );

            instance.Tick(0.25f);
            Assert.AreEqual(0.25f, lastProgress, 1e-5f);

            instance.Pause();
            instance.Tick(0.25f);
            Assert.AreEqual(0.25f, lastProgress, 1e-5f);
        }

        [Test]
        public void Resume_ContinuesFromPausedProgress() {
            float lastProgress = -1f;
            var instance = CreateInstance(
                duration: 1f,
                onUpdate: (_, progress) => lastProgress = progress
            );

            instance.Tick(0.25f);
            instance.Pause();
            instance.Resume();
            instance.Tick(0.25f);

            Assert.AreEqual(0.5f, lastProgress, 1e-5f);
        }

        [Test]
        public void ZeroDuration_CompletesImmediately() {
            int beforeStartCount = 0;
            int updateCount = 0;
            int completeCount = 0;
            float lastEased = -1f;
            float lastProgress = -1f;

            var instance = CreateInstance(
                duration: 0f,
                onBeforeStart: () => beforeStartCount++,
                onUpdate: (eased, progress) => {
                    updateCount++;
                    lastEased = eased;
                    lastProgress = progress;
                },
                onComplete: () => completeCount++
            );

            instance.Tick(0f);

            Assert.AreEqual(1, beforeStartCount);
            Assert.AreEqual(1, updateCount);
            Assert.AreEqual(1f, lastEased, 1e-5f);
            Assert.AreEqual(1f, lastProgress, 1e-5f);
            Assert.AreEqual(1, completeCount);
            Assert.IsTrue(instance.IsFinished);
        }

        [Test]
        public void Stop_PreventsSubsequentTicks() {
            int updateCount = 0;
            var instance = CreateInstance(
                duration: 1f,
                onUpdate: (_, _) => updateCount++
            );

            instance.Tick(0.1f);
            instance.Stop();
            instance.Tick(0.1f);

            Assert.AreEqual(1, updateCount);
        }

        [Test]
        public void IsRunning_TrueWhileActive() {
            var instance = CreateInstance(duration: 1f);

            instance.Tick(0.1f);

            Assert.IsTrue(instance.IsRunning);
        }

        [Test]
        public void IsRunning_FalseAfterComplete() {
            var instance = CreateInstance(duration: 0.5f);

            instance.Tick(0.5f);

            Assert.IsFalse(instance.IsRunning);
        }

        [Test]
        public void Stop_FiredOnlyOnce() {
            int count = 0;
            var instance = CreateInstance(duration: 1f, onStop: () => count++);

            instance.Tick(0.1f);
            instance.Stop();
            instance.Stop();

            Assert.AreEqual(1, count);
        }
    }

    public class TweenHandleTests {
        private TweenHandle CreateHandle(float duration = 1f) {
            var instance = new TweenInstance(new TweenParams(
                duration: duration,
                easeType: EaseType.Linear,
                useUnscaledTime: false,
                onBeforeStart: null,
                onUpdate: (_, _) => { },
                onComplete: null,
                onStop: null
            ));
            return new TweenHandle(instance);
        }

        [Test]
        public void IsRunning_DelegatesToInstance() {
            var handle = CreateHandle();

            handle.Instance.Tick(0.1f);

            Assert.IsTrue(handle.IsRunning);
        }

        [Test]
        public void Pause_DelegatesToInstance() {
            var handle = CreateHandle();

            handle.Instance.Tick(0.1f);
            handle.Pause();

            Assert.IsTrue(handle.IsPaused);
        }

        [Test]
        public void Stop_DelegatesToInstance() {
            var handle = CreateHandle();

            handle.Instance.Tick(0.1f);
            handle.Stop();

            Assert.IsFalse(handle.IsRunning);
        }
    }
}
