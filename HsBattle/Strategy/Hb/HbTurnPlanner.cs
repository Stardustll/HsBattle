using System.Collections.Generic;
using System.Linq;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbTurnPlanner
    {
        private readonly HbActionSupportClassifier _classifier;
        private readonly HbActionResolver _resolver;
        private readonly HbTurnSequenceScorer _scorer;

        public HbTurnPlanner(HbActionSupportClassifier classifier, HbActionResolver resolver, HbTurnSequenceScorer scorer)
        {
            _classifier = classifier;
            _resolver = resolver;
            _scorer = scorer;
        }

        public HbActionSequencePlan Plan(HbBattleSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsFriendlyTurn)
            {
                return null;
            }

            HbSimulatedTurnState initialState = HbSimulatedTurnState.Create(snapshot);
            List<HbSimulatedTurnState> frontier = new List<HbSimulatedTurnState> { initialState };
            HbActionSequencePlan bestSequence = null;
            int bestScore = int.MinValue;

            for (int depth = 0; depth < 6 && frontier.Count > 0; depth++)
            {
                List<HbSimulatedTurnState> next = new List<HbSimulatedTurnState>();
                foreach (HbSimulatedTurnState state in frontier)
                {
                    foreach (HbBattleOptionSnapshot option in GetCandidateOptions(state, snapshot))
                    {
                        option.SupportKind = _classifier.Classify(option);
                        if (option.SupportKind != HbActionSupportKind.SupportedExact)
                        {
                            continue;
                        }

                        if (option.Targets.Count == 0)
                        {
                            HbSimulatedTurnState clone = state.Clone();
                            if (_resolver.TryApply(clone, option, null))
                            {
                                int score = _scorer.Score(clone);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestSequence = new HbActionSequencePlan { Score = score };
                                    bestSequence.Steps.AddRange(clone.ExecutedSteps);
                                }

                                next.Add(clone);
                            }

                            continue;
                        }

                        foreach (HbBattleTargetSnapshot target in option.Targets)
                        {
                            HbSimulatedTurnState clone = state.Clone();
                            if (_resolver.TryApply(clone, option, target))
                            {
                                int score = _scorer.Score(clone);
                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestSequence = new HbActionSequencePlan { Score = score };
                                    bestSequence.Steps.AddRange(clone.ExecutedSteps);
                                }

                                next.Add(clone);
                            }
                        }
                    }
                }

                frontier = next
                    .OrderByDescending(candidate => _scorer.Score(candidate))
                    .Take(12)
                    .ToList();
            }

            return bestSequence;
        }

        private static IEnumerable<HbBattleOptionSnapshot> GetCandidateOptions(HbSimulatedTurnState state, HbBattleSnapshot snapshot)
        {
            foreach (HbBattleOptionSnapshot option in snapshot.Options)
            {
                if (option == null || !option.IsPlayable)
                {
                    continue;
                }

                if (option.Cost > state.RemainingMana)
                {
                    continue;
                }

                if (option.Kind == StrategyActionKind.HeroPower && state.HeroPowerUsed)
                {
                    continue;
                }

                if (option.Kind == StrategyActionKind.Attack)
                {
                    HbBattleEntitySnapshot attacker = state.FriendlyBoard.FirstOrDefault(entity => entity.EntityId == option.EntityId);
                    if (attacker == null || attacker.HasAttacked || !attacker.CanAttack)
                    {
                        continue;
                    }
                }

                if (option.Targets.Count > 0)
                {
                    List<HbBattleTargetSnapshot> liveTargets = new List<HbBattleTargetSnapshot>();
                    for (int i = 0; i < option.Targets.Count; i++)
                    {
                        HbBattleTargetSnapshot target = option.Targets[i];
                        if (IsTargetAvailable(state, target))
                        {
                            liveTargets.Add(target);
                        }
                    }

                    if (liveTargets.Count == 0)
                    {
                        continue;
                    }

                    HbBattleOptionSnapshot copy = CloneOption(option);
                    copy.Targets.Clear();
                    copy.Targets.AddRange(liveTargets);
                    yield return copy;
                    continue;
                }

                yield return CloneOption(option);
            }
        }

        private static bool IsTargetAvailable(HbSimulatedTurnState state, HbBattleTargetSnapshot target)
        {
            if (state == null || target == null)
            {
                return false;
            }

            if (target.IsEnemyHero)
            {
                return !state.IsEnemyHeroHealthKnown || state.EnemyHeroHealth > 0;
            }

            if (target.IsFriendlyHero)
            {
                return !state.IsFriendlyHeroHealthKnown || state.FriendlyHeroHealth > 0;
            }

            return state.EnemyBoard.Any(entity => entity.EntityId == target.EntityId)
                || state.FriendlyBoard.Any(entity => entity.EntityId == target.EntityId);
        }

        private static HbBattleOptionSnapshot CloneOption(HbBattleOptionSnapshot option)
        {
            HbBattleOptionSnapshot clone = new HbBattleOptionSnapshot
            {
                OptionIndex = option.OptionIndex,
                EntityId = option.EntityId,
                Cost = option.Cost,
                Attack = option.Attack,
                DamageAmount = option.DamageAmount,
                DrawCount = option.DrawCount,
                SourceHealth = option.SourceHealth,
                TargetCount = option.TargetCount,
                RequiresTarget = option.RequiresTarget,
                CanTargetEnemyHero = option.CanTargetEnemyHero,
                CanLethal = option.CanLethal,
                IsPlayable = option.IsPlayable,
                IsEndTurn = option.IsEndTurn,
                IsPass = option.IsPass,
                IsEntityResolved = option.IsEntityResolved,
                HasBattlecry = option.HasBattlecry,
                SupportKind = option.SupportKind,
                Kind = option.Kind,
                Description = option.Description
            };

            for (int i = 0; i < option.Targets.Count; i++)
            {
                HbBattleTargetSnapshot target = option.Targets[i];
                clone.Targets.Add(new HbBattleTargetSnapshot
                {
                    EntityId = target.EntityId,
                    Attack = target.Attack,
                    Health = target.Health,
                    MaxHealth = target.MaxHealth,
                    MissingHealth = target.MissingHealth,
                    IsDamaged = target.IsDamaged,
                    IsResolved = target.IsResolved,
                    IsEnemyHero = target.IsEnemyHero,
                    IsEnemyCharacter = target.IsEnemyCharacter,
                    IsFriendlyHero = target.IsFriendlyHero,
                    IsFriendlyCharacter = target.IsFriendlyCharacter
                });
            }

            return clone;
        }
    }
}
