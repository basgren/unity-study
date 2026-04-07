using UnityEngine;

namespace Game.Core.Services.SceneState.Savers {
    [RequireComponent(typeof(StateRoot))]
    public abstract class StateSaverBase : MonoBehaviour, IStateSaver {
        public abstract string Slot { get; }
        public abstract void Capture(IStateWriter w);
        public abstract void Restore(IStateReader r);
    }
}
