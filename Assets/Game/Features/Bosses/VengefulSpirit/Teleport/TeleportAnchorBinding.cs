using System;

namespace Game.Features.Bosses.VengefulSpirit.Teleport {
    /// <summary>
    /// Editor-authored mapping from a free-form name to a destination transform
    /// in the boss room. The boss serialises an array of these so designers can
    /// wire several teleport spots at once and AI / debug code can look one up
    /// by name. Names are author-defined per boss; comparison is case-sensitive.
    /// </summary>
    [Serializable]
    public struct TeleportAnchorBinding {
        public string name;
        public TeleportAnchor anchor;
    }
}
