using Core.FSM.Editor;
using UnityEditor;

namespace Game.Features.Characters.PinkStar.Editor {
    [CustomEditor(typeof(PinkyController))]
    public class PinkyControllerInspector : RuntimeStateMachineInspector<PinkyController> {
    }
}
