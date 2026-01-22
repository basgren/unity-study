using System;
using UnityEngine;

namespace Core.Utils {
    public class TinyTimer {
        public float Remaining => timeRemaining;
        public float Elapsed => baseTime - timeRemaining;
        public bool IsTimedOut => timeRemaining <= 0;
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
