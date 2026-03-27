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

            // Lethal: if enemy hero is dead, massive bonus
            if (state.IsEnemyHeroHealthKnown && state.EnemyHeroHealth <= 0)
            {
                return 1000000;
            }

            // Enemy hero damage pressure
            if (state.IsEnemyHeroHealthKnown)
            {
                score += (30 - state.EnemyHeroHealth) * 6;

                // Near-lethal bonus
                if (state.EnemyHeroHealth <= 10)
                {
                    score += (11 - state.EnemyHeroHealth) * 4;
                }
            }

            // Friendly board value
            score += SumAttack(state.FriendlyBoard) * 15;
            score += SumHealth(state.FriendlyBoard) * 4;
            score += CountTauntMinions(state.FriendlyBoard) * 12;
            score += CountDivineShieldMinions(state.FriendlyBoard) * 18;

            // Enemy board threat
            score -= SumAttack(state.EnemyBoard) * 20;
            score -= SumHealth(state.EnemyBoard) * 6;
            score -= CountTauntMinions(state.EnemyBoard) * 8;
            score -= CountDivineShieldMinions(state.EnemyBoard) * 14;

            // Ready attackers bonus (have attack options next turn)
            score += CountReadyAttackers(state.FriendlyBoard) * 10;

            // Mana efficiency: penalize unused mana (wasted resources)
            score -= state.RemainingMana * 3;

            // Clear board bonus
            if (state.EnemyBoard.Count == 0)
            {
                score += 40;
            }

            // Friendly hero health safety
            if (state.IsFriendlyHeroHealthKnown)
            {
                if (state.FriendlyHeroHealth <= 5)
                {
                    score -= (6 - state.FriendlyHeroHealth) * 10;
                }
            }

            // Penalize wasted attacks: if we have ready attackers with attack power, that's wasted damage
            int wastedAttack = SumReadyAttackerDamage(state.FriendlyBoard);
            if (wastedAttack > 0)
            {
                score -= wastedAttack * 8;
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

        private static int SumReadyAttackerDamage(System.Collections.Generic.List<HbBattleEntitySnapshot> board)
        {
            int total = 0;
            if (board == null)
            {
                return total;
            }

            for (int i = 0; i < board.Count; i++)
            {
                HbBattleEntitySnapshot entity = board[i];
                if (entity != null && entity.Health > 0 && entity.CanAttack && !entity.HasAttacked && entity.Attack > 0)
                {
                    total += entity.Attack;
                }
            }

            return total;
        }

        private static int CountTauntMinions(System.Collections.Generic.List<HbBattleEntitySnapshot> board)
        {
            int total = 0;
            if (board == null)
            {
                return total;
            }

            for (int i = 0; i < board.Count; i++)
            {
                HbBattleEntitySnapshot entity = board[i];
                if (entity != null && entity.Health > 0 && entity.HasTaunt)
                {
                    total++;
                }
            }

            return total;
        }

        private static int CountDivineShieldMinions(System.Collections.Generic.List<HbBattleEntitySnapshot> board)
        {
            int total = 0;
            if (board == null)
            {
                return total;
            }

            for (int i = 0; i < board.Count; i++)
            {
                HbBattleEntitySnapshot entity = board[i];
                if (entity != null && entity.Health > 0 && entity.HasDivineShield)
                {
                    total++;
                }
            }

            return total;
        }
    }
}