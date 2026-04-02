using System;

namespace Game.Core.Models.Dialog {
    [Serializable]
    public class DialogDef {
        public string dialogId;
        public string entryNodeId;
        public DialogNode[] nodes;
    }
}
