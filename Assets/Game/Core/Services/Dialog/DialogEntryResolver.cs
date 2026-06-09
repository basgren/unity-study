using Game.Core.Models.Dialog;
using Game.Features.Characters.Hero;

namespace Game.Core.Services.Dialog {
    /// <summary>
    /// Picks the node a dialog should open at, based on player state.
    /// Walks entry rules in order and returns the first whose conditions pass and
    /// whose target node is not a spent one-shot. Falls back to <see cref="DialogDef.entryNodeId"/>.
    /// </summary>
    public static class DialogEntryResolver {
        public static string Resolve(DialogDef def, PlayerState state) {
            if (def.entryRules != null) {
                for (int i = 0; i < def.entryRules.Length; i++) {
                    var rule = def.entryRules[i];
                    var node = FindNode(def, rule.nodeId);
                    if (node == null) {
                        continue;
                    }

                    if (node.once && state.HasSeenNode(SeenKey(def.dialogId, rule.nodeId))) {
                        continue;
                    }

                    if (DialogConditionEvaluator.AreConditionsMet(rule.conditions, state)) {
                        return rule.nodeId;
                    }
                }
            }

            return def.entryNodeId;
        }

        /// <summary>Global key for a one-shot node, unique across dialogs.</summary>
        public static string SeenKey(string dialogId, string nodeId) {
            return $"{dialogId}.{nodeId}";
        }

        private static DialogNode FindNode(DialogDef def, string nodeId) {
            if (def.nodes == null) {
                return null;
            }

            for (int i = 0; i < def.nodes.Length; i++) {
                if (def.nodes[i].nodeId == nodeId) {
                    return def.nodes[i];
                }
            }

            return null;
        }
    }
}
