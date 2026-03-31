using System;
using UnityEngine;

namespace Game.Core.Utils {
    public class TinyTimer {
        public float Remaining => timeRemaining;
        public float Elapsed => baseTime - timeRemaining;
        public bool IsTimedOut => timeRemaining <= 0;
        public float Progress => baseTime > 0 ? 1f - (timeRemaining / baseTime) : 1f;
        public event Action OnTimeout;
        
        private float baseTime;
        private float timeRemaining;

        public TinyTimer(float baseTime) {
            this.baseTime = baseTime;
        }

        public void Update(float deltaTime) {
            if (timeRemaining > 0) {
                timeRemaining = Mathf.Max(0, timeRemaining - deltaTime);

                if (IsTimedOut) {
                    OnTimeout?.Invoke();
                }
            }
        }

        public void Start() {
            timeRemaining = baseTime;
        }

        public void Stop() {
            timeRemaining = 0;
        }
    }
}
