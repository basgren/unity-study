using System;

namespace Game.Core.Models.Dialog {
    public enum DialogActionType {
        GiveItem,
        RemoveItem,
        SetFlag,
        ClearFlag,
        OpenShop,

        // Raises DialogService.EventRaised with this action's stringParam as the event id,
        // letting dialog nodes trigger world/scene logic (cutscenes, etc.) without the
        // dialog system taking a dependency on it. Keep last to preserve existing values.
        RaiseEvent,
    }

    [Serializable]
    public class DialogAction {
        public DialogActionType type;
        public string stringParam;
        public int intParam;
    }
}
