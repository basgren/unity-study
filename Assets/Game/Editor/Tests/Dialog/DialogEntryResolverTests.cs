using Game.Configs;
using Game.Core.Models.Dialog;
using Game.Core.Services.Dialog;
using Game.Features.Characters.Hero;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests.Dialog {
    public class DialogEntryResolverTests {
        private PlayerState NewState() {
            return new PlayerState(ScriptableObject.CreateInstance<PlayerConfig>());
        }

        private static DialogNode Node(string id, bool once = false) {
            return new DialogNode { nodeId = id, once = once };
        }

        private static DialogEntryRule Rule(string nodeId, params DialogCondition[] conditions) {
            return new DialogEntryRule { nodeId = nodeId, conditions = conditions };
        }

        private static DialogCondition Cond(ConditionType type, string s = null) {
            return new DialogCondition { type = type, stringParam = s };
        }

        // dialogId "d", nodes intro(once)/sabre(once)/common, mirroring the Rikko layout.
        private static DialogDef Def(params DialogEntryRule[] rules) {
            return new DialogDef {
                dialogId = "d",
                entryNodeId = "common",
                entryRules = rules,
                nodes = new[] { Node("intro", once: true), Node("sabre", once: true), Node("common") }
            };
        }

        [Test]
        public void NoEntryRules_ReturnsEntryNodeId() {
            var def = Def();
            def.entryRules = null;
            Assert.AreEqual("common", DialogEntryResolver.Resolve(def, NewState()));
        }

        [Test]
        public void NoRuleMatches_FallsBackToEntryNodeId() {
            var def = Def(Rule("intro", Cond(ConditionType.FlagSet, "never")));
            Assert.AreEqual("common", DialogEntryResolver.Resolve(def, NewState()));
        }

        [Test]
        public void FirstUnseenUnconditionalRule_Wins() {
            var def = Def(Rule("intro"), Rule("sabre", Cond(ConditionType.IsArmed)), Rule("common"));
            Assert.AreEqual("intro", DialogEntryResolver.Resolve(def, NewState()));
        }

        [Test]
        public void SpentOnceNode_IsSkipped() {
            var def = Def(Rule("intro"), Rule("sabre", Cond(ConditionType.IsArmed)), Rule("common"));
            var state = NewState();
            state.MarkNodeSeen(DialogEntryResolver.SeenKey("d", "intro"));
            Assert.AreEqual("common", DialogEntryResolver.Resolve(def, state));
        }

        [Test]
        public void SabreRule_FiresWhenArmedAndIntroSpent() {
            var def = Def(Rule("intro"), Rule("sabre", Cond(ConditionType.IsArmed)), Rule("common"));
            var state = NewState();
            state.MarkNodeSeen(DialogEntryResolver.SeenKey("d", "intro"));
            state.IsArmed = true;
            Assert.AreEqual("sabre", DialogEntryResolver.Resolve(def, state));
        }

        [Test]
        public void SpentSabre_FallsThroughToCommon() {
            var def = Def(Rule("intro"), Rule("sabre", Cond(ConditionType.IsArmed)), Rule("common"));
            var state = NewState();
            state.MarkNodeSeen(DialogEntryResolver.SeenKey("d", "intro"));
            state.MarkNodeSeen(DialogEntryResolver.SeenKey("d", "sabre"));
            state.IsArmed = true;
            Assert.AreEqual("common", DialogEntryResolver.Resolve(def, state));
        }

        [Test]
        public void RuleWithMissingNode_IsSkipped() {
            var def = Def(Rule("ghost"), Rule("common"));
            Assert.AreEqual("common", DialogEntryResolver.Resolve(def, NewState()));
        }

        [Test]
        public void SeenKey_CombinesDialogAndNode() {
            Assert.AreEqual("d.intro", DialogEntryResolver.SeenKey("d", "intro"));
        }
    }
}
