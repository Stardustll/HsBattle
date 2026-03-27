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

            // Experimental mode generic conservative rule:
            // prefer early cards, replace obvious late cards, and use heuristic score for middle cases.
            foreach (HbMulliganCardSnapshot card in snapshot.Cards)
            {
                if (card == null || card.Index < 0)
                {
                    continue;
                }

                int score = _evaluator.ScoreMulliganCard(snapshot, card);

                if (!card.IsCostKnown)
                {
                    result.KeepIndices.Add(card.Index);
                    continue;
                }

                if (card.Cost <= 2)
                {
                    result.KeepIndices.Add(card.Index);
                    continue;
                }

                if (card.Cost >= 5)
                {
                    result.ReplaceIndices.Add(card.Index);
                    continue;
                }

                if (score >= 0)
                {
                    result.KeepIndices.Add(card.Index);
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
