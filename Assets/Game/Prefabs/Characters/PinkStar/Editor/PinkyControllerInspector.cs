using Core.FSM.Editor;
using UnityEditor;

namespace Prefabs.Characters.PinkStar.Editor {
    [CustomEditor(typeof(PinkyController))]
    public class PinkyControllerInspector : RuntimeStateMachineInspector<PinkyController> {
    }
}
