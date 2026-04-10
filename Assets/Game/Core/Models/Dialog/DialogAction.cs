using System;

namespace Game.Core.Models.Dialog {
    public enum DialogActionType {
        GiveItem,
        RemoveItem,
        SetFlag,
        ClearFlag,
        OpenShop,
    }

    [Serializable]
    public class DialogAction {
        public DialogActionType type;
        public string stringParam;
        public int intParam;
    }
}
