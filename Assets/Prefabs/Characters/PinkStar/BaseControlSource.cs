using UnityEngine;

namespace Prefabs.Characters.PinkStar {
    public abstract class BaseControlSource<TCommandType>: MonoBehaviour where TCommandType : struct {
        public abstract TCommandType? GetCommand();
    }
}
