namespace HsBattle.Strategy.Hb
{
    internal sealed class HbBattleDecisionService
    {
        private readonly HbHeuristicEvaluator _evaluator = new HbHeuristicEvaluator();
        private readonly HbTurnPlanner _turnPlanner = new HbTurnPlanner(
            new HbActionSupportClassifier(),
            new HbActionResolver(),
            new HbTurnSequenceScorer());

        public StrategyActionPlan Decide(HbBattleSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsFriendlyTurn || snapshot.Options == null || snapshot.Options.Count == 0)
            {
                return null;
            }

            HbActionSequencePlan sequence = _turnPlanner.Plan(snapshot);
            return sequence != null && sequence.Steps.Count > 0 ? sequence.Steps[0] : TryPickSingleStepFallback(snapshot);
        }

        private StrategyActionPlan TryPickSingleStepFallback(HbBattleSnapshot snapshot)
        {
            StrategyActionPlan bestPlan = null;
            int bestScore = int.MinValue;

            foreach (HbBattleOptionSnapshot option in snapshot.Options)
            {
                if (!CanBuildSafePlan(option))
                {
                    continue;
                }

                int score = _evaluator.ScoreBattleOption(snapshot, option);
                HbBattleTargetSnapshot bestTarget = null;

                if (option.Targets != null && option.Targets.Count > 0)
                {
                    int bestTargetScore = int.MinValue;
                    foreach (HbBattleTargetSnapshot target in option.Targets)
                    {
                        int targetScore = _evaluator.ScoreBattleTarget(snapshot, option, target);
                        if (targetScore > bestTargetScore)
                        {
                            bestTargetScore = targetScore;
                            bestTarget = target;
                        }
                    }

                    if (bestTarget == null)
                    {
                        continue;
                    }

                    score += bestTargetScore;
                }
                else if (option.RequiresTarget || option.TargetCount > 0)
                {
                    continue;
                }

                StrategyActionPlan candidatePlan = BuildPlan(option, bestTarget, score);
                if (candidatePlan == null)
                {
                    continue;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPlan = candidatePlan;
                }
            }

            return bestPlan;
        }

        private static bool CanBuildSafePlan(HbBattleOptionSnapshot option)
        {
            if (option == null || option.OptionIndex < 0 || !option.IsPlayable)
            {
                return false;
            }

            return option.Kind != StrategyActionKind.Choice;
        }

        private static StrategyActionPlan BuildPlan(HbBattleOptionSnapshot option, HbBattleTargetSnapshot bestTarget, int score)
        {
            if (option == null)
            {
                return null;
            }

            if (bestTarget == null)
            {
                return new StrategyActionPlan
                {
                    OptionIndex = option.OptionIndex,
                    TargetId = -1,
                    Score = score,
                    Description = option.Description,
                    Kind = option.Kind
                };
            }

            return new StrategyActionPlan
            {
                OptionIndex = option.OptionIndex,
                TargetId = bestTarget.EntityId,
                Score = score,
                Description = option.Description,
                Kind = option.Kind
            };
        }
    }
}
