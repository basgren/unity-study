using Core.FSM.Editor;
using UnityEditor;

namespace Game.Features.Characters.PinkStar.Editor {
    [CustomEditor(typeof(PinkyAI))]
    public class PinkyAIInspector : RuntimeStateMachineInspector<PinkyAI> {
    }
}
