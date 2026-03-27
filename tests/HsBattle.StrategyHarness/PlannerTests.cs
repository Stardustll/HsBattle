using HsBattle.Strategy;
using HsBattle.Strategy.Hb;

namespace HsBattle.StrategyHarness
{
    internal static class PlannerTests
    {
        public static void RunAll()
        {
            ClassifierRejectsComplexBattlecry();
            ResolverKillsEnemyOnCleanTrade();
            PlannerPrefersCleanTradeBeforeFace();
            PlannerUsesHeroPowerBeforeAttackWhenThatSecuresBoard();
        }

        private static void ClassifierRejectsComplexBattlecry()
        {
            HbBattleOptionSnapshot option = new HbBattleOptionSnapshot
            {
                OptionIndex = 0,
                EntityId = 101,
                Description = "battlecry draw",
                Kind = StrategyActionKind.PlayCard,
                Cost = 2,
                HasBattlecry = true,
                DrawCount = 1
            };

            HbActionSupportClassifier classifier = new HbActionSupportClassifier();
            AssertEx.Equal(HbActionSupportKind.UnsupportedComplex, classifier.Classify(option), "Battlecry draw must not be exact-supported.");
        }

        private static void ResolverKillsEnemyOnCleanTrade()
        {
            HbSimulatedTurnState state = HbTestStateFactory.CreateCleanTradeState();
            HbBattleOptionSnapshot attack = HbTestStateFactory.CreateAttackOption(sourceId: 11, targetId: 21, attack: 3, sourceHealth: 4, targetAttack: 2, targetHealth: 3, optionIndex: 0);

            HbActionResolver resolver = new HbActionResolver();
            bool applied = resolver.TryApply(state, attack, attack.Targets[0]);

            AssertEx.True(applied, "Attack should be resolved.");
            AssertEx.True(state.EnemyBoard.Find(entity => entity.EntityId == 21) == null, "Enemy minion should die.");
            AssertEx.Equal(2, state.FriendlyBoard.Find(entity => entity.EntityId == 11).Health, "Friendly minion should survive on 2 health.");
        }

        private static void PlannerPrefersCleanTradeBeforeFace()
        {
            HbBattleSnapshot snapshot = HbTestStateFactory.CreateTradeBeforeFaceSnapshot();
            HbTurnPlanner planner = new HbTurnPlanner(new HbActionSupportClassifier(), new HbActionResolver(), new HbTurnSequenceScorer());

            HbActionSequencePlan plan = planner.Plan(snapshot);

            AssertEx.True(plan != null && plan.Steps.Count > 0, "Planner should find a sequence.");
            AssertEx.Equal(0, plan.Steps[0].OptionIndex, "Planner should start with the clean trade action.");
        }

        private static void PlannerUsesHeroPowerBeforeAttackWhenThatSecuresBoard()
        {
            HbBattleSnapshot snapshot = HbTestStateFactory.CreateHeroPowerThenAttackSnapshot();
            HbTurnPlanner planner = new HbTurnPlanner(new HbActionSupportClassifier(), new HbActionResolver(), new HbTurnSequenceScorer());

            HbActionSequencePlan plan = planner.Plan(snapshot);

            AssertEx.True(plan != null && plan.Steps.Count > 1, "Planner should produce a multi-step sequence.");
            AssertEx.Equal(0, plan.Steps[0].OptionIndex, "Planner should hero power first.");
            AssertEx.Equal(1, plan.Steps[1].OptionIndex, "Planner should attack second.");
        }
    }
}
