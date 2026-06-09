using System;

namespace Game.Core.Models.Dialog {
    [Serializable]
    public class DialogDef {
        public string dialogId;
        public string entryNodeId;
        public DialogEntryRule[] entryRules;
        public DialogNode[] nodes;
    }
}
