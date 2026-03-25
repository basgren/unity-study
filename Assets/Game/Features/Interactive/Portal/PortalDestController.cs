using UnityEngine;

// Currently, this controller is empty as it is used as a filter, so portal can't select other objects
// except this destination. Also here we can draw gizmos to show required space, so player is not
// teleported into a wall.
namespace Prefabs.Interactive.Portal {
    public class PortalDestController : MonoBehaviour {
    }
}
