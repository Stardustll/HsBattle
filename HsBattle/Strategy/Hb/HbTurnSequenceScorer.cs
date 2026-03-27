namespace HsBattle.Strategy.Hb
{
    internal sealed class HbTurnSequenceScorer
    {
        public int Score(HbSimulatedTurnState state)
        {
            if (state == null)
            {
                return int.MinValue;
            }

            int score = 0;

            if (state.IsEnemyHeroHealthKnown)
            {
                score += (30 - state.EnemyHeroHealth) * 6;
            }

            score += SumAttack(state.FriendlyBoard) * 15;
            score += SumHealth(state.FriendlyBoard) * 4;
            score -= SumAttack(state.EnemyBoard) * 20;
            score -= SumHealth(state.EnemyBoard) * 6;
            score += CountReadyAttackers(state.FriendlyBoard) * 10;
            score += state.RemainingMana * 2;

            if (state.EnemyBoard.Count == 0)
            {
                score += 40;
            }

            return score;
        }

        private static int SumAttack(System.Collections.Generic.List<HbBattleEntitySnapshot> board)
        {
            int total = 0;
            if (board == null)
            {
                return total;
            }

            for (int i = 0; i < board.Count; i++)
            {
                HbBattleEntitySnapshot entity = board[i];
                if (entity != null && entity.Health > 0)
                {
                    total += entity.Attack;
                }
            }

            return total;
        }

        private static int SumHealth(System.Collections.Generic.List<HbBattleEntitySnapshot> board)
        {
            int total = 0;
            if (board == null)
            {
                return total;
            }

            for (int i = 0; i < board.Count; i++)
            {
                HbBattleEntitySnapshot entity = board[i];
                if (entity != null && entity.Health > 0)
                {
                    total += entity.Health;
                }
            }

            return total;
        }

        private static int CountReadyAttackers(System.Collections.Generic.List<HbBattleEntitySnapshot> board)
        {
            int total = 0;
            if (board == null)
            {
                return total;
            }

            for (int i = 0; i < board.Count; i++)
            {
                HbBattleEntitySnapshot entity = board[i];
                if (entity != null && entity.Health > 0 && entity.CanAttack && !entity.HasAttacked)
                {
                    total++;
                }
            }

            return total;
        }
    }
}
