using System.Collections;
using UnityEngine;

namespace Prefabs.Characters.Sharky {
    public class BaseAI : MonoBehaviour {
        private Coroutine activeCoroutine;
        
        protected void StartAction(IEnumerator action) {
            StopCurrentAction();

            activeCoroutine = StartCoroutine(action);
        }

        protected void StopCurrentAction() {
            if (activeCoroutine == null) {
                return;
            }

            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        
        protected virtual void OnDestroy() {
            StopCurrentAction();
        }
    }
}
