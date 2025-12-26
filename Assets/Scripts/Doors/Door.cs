using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Doors {
    /// <summary>
    /// A scene door that can transition the player to another scene/door.
    /// The door uses a stable string ID (DoorId) that is referenced by other doors.
    /// </summary>
    public sealed class Door : MonoBehaviour {
        [SerializeField, HideInInspector]
        private string doorId;

        [SerializeField]
        private DoorLink link;

        /// <summary>
        /// Door identifier. Must be unique within a scene.
        /// Not editable directly; use the editor "Change ID" action to rename safely.
        /// </summary>
        public string DoorId => doorId;

        /// <summary>
        /// Destination link for this door.
        /// </summary>
        public DoorLink Link => link;

#if UNITY_EDITOR
        private const int DefaultGeneratedLength = 5;

        private void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            // If this component is on a prefab asset (Prefab Mode / project prefab),
            // keep DoorId empty so it does NOT propagate to instances.
            if (PrefabUtility.IsPartOfPrefabAsset(this)) {
                if (!string.IsNullOrEmpty(doorId)) {
                    doorId = string.Empty;
                    EditorUtility.SetDirty(this);
                }

                return;
            }

            // If this is a prefab instance and it still matches the prefab's stored id,
            // generate a unique one for this instance.
            var source = PrefabUtility.GetCorrespondingObjectFromSource(this) as Door;
            var isInheritedFromPrefab = source != null && string.Equals(doorId, source.doorId, System.StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(doorId) || isInheritedFromPrefab) {
                doorId = $"Door_{DoorIdUtils.GenerateId(DefaultGeneratedLength)}";
                EditorUtility.SetDirty(this);
            }
        }

        public void EditorSetDoorId(string newId) {
            doorId = newId;
        }
#endif
    }
}
