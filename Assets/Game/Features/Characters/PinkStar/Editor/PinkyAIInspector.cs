using Core.FSM.Editor;
using UnityEditor;

namespace Prefabs.Characters.PinkStar.Editor {
    [CustomEditor(typeof(PinkyAI))]
    public class PinkyAIInspector : RuntimeStateMachineInspector<PinkyAI> {
    }
}
