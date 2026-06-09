using Game.Configs;
using Game.Core.Models.Dialog;
using Game.Core.Services.Dialog;
using Game.Features.Characters.Hero;
using NUnit.Framework;
using UnityEngine;

namespace Game.Editor.Tests.Dialog {
    public class DialogConditionEvaluatorTests {
        private PlayerState NewState() {
            return new PlayerState(ScriptableObject.CreateInstance<PlayerConfig>());
        }

        private static DialogCondition Cond(ConditionType type, string s = null) {
            return new DialogCondition { type = type, stringParam = s };
        }

        [Test]
        public void NullConditions_AreMet() {
            Assert.IsTrue(DialogConditionEvaluator.AreConditionsMet(null, NewState()));
        }

        [Test]
        public void EmptyConditions_AreMet() {
            Assert.IsTrue(DialogConditionEvaluator.AreConditionsMet(new DialogCondition[0], NewState()));
        }

        [Test]
        public void FlagSet_TrueWhenFlagPresent() {
            var state = NewState();
            state.SetFlag("quest_started");
            Assert.IsTrue(DialogConditionEvaluator.AreConditionsMet(
                new[] { Cond(ConditionType.FlagSet, "quest_started") }, state));
        }

        [Test]
        public void FlagNotSet_TrueWhenFlagAbsent() {
            var state = NewState();
            Assert.IsTrue(DialogConditionEvaluator.AreConditionsMet(
                new[] { Cond(ConditionType.FlagNotSet, "quest_started") }, state));
        }

        [Test]
        public void IsArmed_TrueOnlyWhenArmed() {
            var state = NewState();
            var conds = new[] { Cond(ConditionType.IsArmed) };
            Assert.IsFalse(DialogConditionEvaluator.AreConditionsMet(conds, state));
            state.IsArmed = true;
            Assert.IsTrue(DialogConditionEvaluator.AreConditionsMet(conds, state));
        }

        [Test]
        public void AllConditionsMustPass() {
            var state = NewState();
            state.IsArmed = true; // first passes, second fails
            var conds = new[] { Cond(ConditionType.IsArmed), Cond(ConditionType.FlagSet, "missing") };
            Assert.IsFalse(DialogConditionEvaluator.AreConditionsMet(conds, state));
        }
    }
}
