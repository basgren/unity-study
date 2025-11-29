using UnityEngine;

namespace Components {
    public class SpawnComponent: MonoBehaviour {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private GameObject prefab;

        [ContextMenu("Spawn")]
        public void Spawn() {
            // TODO: [BG] Think about creating some service that will spawn effects with ability to specify
            //   default parent for a scene to avoid polluting the scene hierarchy. This will also allow to
            //   control spawned effects lifetime and use object pools for better performance.
            var instance = Instantiate(prefab, target.position, Quaternion.identity);
            
            // Make sure the spawned object is directed in the same direction as target object.
            instance.transform.localScale = target.lossyScale;
        }
    }
}
