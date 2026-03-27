using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbMulliganDecisionService
    {
        private readonly HbHeuristicEvaluator _evaluator = new HbHeuristicEvaluator();

        public HbMulliganDecisionResult Decide(HbMulliganSnapshot snapshot)
        {
            HbMulliganDecisionResult result = new HbMulliganDecisionResult();
            if (snapshot == null)
            {
                result.ShouldFallbackToKeepAll = true;
                result.Reason = "Missing mulligan snapshot.";
                return result;
            }

            if (snapshot.HasUnknownCards)
            {
                result.ShouldFallbackToKeepAll = true;
                result.Reason = "Mulligan card ordering source is degraded.";
                return result;
            }

            if (!snapshot.IsActive || snapshot.Cards.Count == 0)
            {
                result.ShouldFallbackToKeepAll = true;
                result.Reason = "Mulligan snapshot is not ready.";
                return result;
            }

            // Detect coin: going second gives 4 cards + coin, so we can keep slightly higher curve
            bool hasCoin = false;
            for (int i = 0; i < snapshot.Cards.Count; i++)
            {
                if (snapshot.Cards[i] != null && snapshot.Cards[i].IsCoinKnown && snapshot.Cards[i].IsCoin)
                {
                    hasCoin = true;
                    break;
                }
            }

            // Track kept costs to avoid duplicate-cost hands
            Dictionary<int, int> keptCostCounts = new Dictionary<int, int>();

            foreach (HbMulliganCardSnapshot card in snapshot.Cards)
            {
                if (card == null || card.Index < 0)
                {
                    continue;
                }

                // Always keep coin
                if (card.IsCoinKnown && card.IsCoin)
                {
                    result.KeepIndices.Add(card.Index);
                    continue;
                }

                int score = _evaluator.ScoreMulliganCard(snapshot, card);

                if (!card.IsCostKnown)
                {
                    result.KeepIndices.Add(card.Index);
                    continue;
                }

                // Hard replace high-cost cards
                if (card.Cost >= 5)
                {
                    result.ReplaceIndices.Add(card.Index);
                    continue;
                }

                // Going first: 4-cost is too slow
                if (card.Cost == 4 && !hasCoin)
                {
                    result.ReplaceIndices.Add(card.Index);
                    continue;
                }

                // Penalize duplicate costs: if we already kept a card at this cost, lower priority
                int existingAtCost = 0;
                if (card.Cost >= 0)
                {
                    keptCostCounts.TryGetValue(card.Cost, out existingAtCost);
                }

                if (existingAtCost >= 1 && card.Cost >= 2)
                {
                    // Already have one at this cost — keep only if very good score
                    if (score < 15)
                    {
                        result.ReplaceIndices.Add(card.Index);
                        continue;
                    }
                }

                // Keep low-cost cards (1 or 2 mana)
                if (card.Cost <= 2)
                {
                    result.KeepIndices.Add(card.Index);
                    if (card.Cost >= 0)
                    {
                        keptCostCounts[card.Cost] = existingAtCost + 1;
                    }

                    continue;
                }

                // 3-cost: keep if score is reasonable
                if (card.Cost == 3 && score >= 0)
                {
                    result.KeepIndices.Add(card.Index);
                    if (card.Cost >= 0)
                    {
                        keptCostCounts[card.Cost] = existingAtCost + 1;
                    }

                    continue;
                }

                // 4-cost with coin: keep if score positive
                if (card.Cost == 4 && hasCoin && score >= 0)
                {
                    result.KeepIndices.Add(card.Index);
                    if (card.Cost >= 0)
                    {
                        keptCostCounts[card.Cost] = existingAtCost + 1;
                    }

                    continue;
                }

                if (score >= 0)
                {
                    result.KeepIndices.Add(card.Index);
                    if (card.Cost >= 0)
                    {
                        keptCostCounts[card.Cost] = existingAtCost + 1;
                    }
                }
                else
                {
                    result.ReplaceIndices.Add(card.Index);
                }
            }

            if (result.KeepIndices.Count == 0 && result.ReplaceIndices.Count == 0)
            {
                result.ShouldFallbackToKeepAll = true;
                result.Reason = "No mulligan decision could be produced.";
            }

            return result;
        }
    }
}
