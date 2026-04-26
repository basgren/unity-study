using System;

namespace Game.Features.Bosses.VengefulSpirit.SpectralSwords {
    /// <summary>
    /// Editor-authored mapping from a free-form name to a concrete scene anchor.
    /// The boss serialises an array of these so designers can wire several anchors at once
    /// and the runtime can look one up by name. Names are author-defined per boss —
    /// different enemies can use different vocabularies.
    /// </summary>
    [Serializable]
    public struct SpectralSwordAnchorBinding {
        public string name;
        public SpectralSwordSpawnAnchor anchor;
    }
}
