namespace HsBattle.Strategy.Hb
{
    internal sealed class HbHeuristicEvaluator
    {
        public int ScoreBattleOption(HbBattleSnapshot snapshot, HbBattleOptionSnapshot option)
        {
            if (option == null)
            {
                return int.MinValue;
            }

            int score = 0;

            if (option.CanLethal)
            {
                score += 100000;
            }

            if (option.IsPlayable)
            {
                score += 150;
            }

            if (!option.IsEntityResolved && !option.IsEndTurn && !option.IsPass)
            {
                score -= 40;
            }

            if (option.Kind == StrategyActionKind.Attack)
            {
                score += option.Attack * 8;
            }

            if (option.Kind == StrategyActionKind.PlayCard)
            {
                score += option.Cost * 10;
            }

            if (option.RequiresTarget && option.TargetCount == 0)
            {
                score -= 800;
            }

            if (option.IsEndTurn || option.IsPass)
            {
                score -= 300;
            }

            if (snapshot != null
                && snapshot.IsEnemyHeroHealthKnown
                && snapshot.EnemyHeroHealth > 0
                && option.CanTargetEnemyHero
                && option.Attack >= snapshot.EnemyHeroHealth)
            {
                score += 2000;
            }

            return score;
        }

        public int ScoreBattleTarget(HbBattleSnapshot snapshot, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)
        {
            if (option == null || target == null)
            {
                return int.MinValue;
            }

            int score = 0;

            if (!target.IsResolved)
            {
                score -= 400;
            }

            if (target.IsEnemyHero)
            {
                score += 35;

                if (option.CanLethal)
                {
                    score += 100000;
                }

                if (snapshot != null
                    && snapshot.IsEnemyHeroHealthKnown
                    && snapshot.EnemyHeroHealth > 0
                    && option.Attack >= snapshot.EnemyHeroHealth)
                {
                    score += 3000;
                }

                if (snapshot != null && snapshot.IsEnemyHeroHealthKnown && snapshot.EnemyHeroHealth <= 12)
                {
                    score += 55;
                }

                if (option.Targets.Exists(delegate (HbBattleTargetSnapshot item) { return item.IsEnemyCharacter; }))
                {
                    score -= 65;
                }
            }

            if (target.IsEnemyCharacter)
            {
                score += 80;
                score += target.Attack * 12;

                if (target.Health > 0)
                {
                    score += target.Health * 4;
                }

                if (option.Attack > 0 && target.Health > 0 && option.Attack >= target.Health)
                {
                    score += 140;
                }

                if (option.Kind == StrategyActionKind.Attack && option.SourceHealth > 0 && target.Attack > 0)
                {
                    if (option.SourceHealth > target.Attack)
                    {
                        score += 90;
                    }

                    if (option.SourceHealth <= target.Attack)
                    {
                        score -= 75;
                    }
                }

                if (option.Kind == StrategyActionKind.Attack && option.Attack > 0 && target.Health > option.Attack && option.SourceHealth <= target.Attack)
                {
                    score -= 180;
                }
            }

            if (target.IsFriendlyHero && target.IsDamaged)
            {
                score += 60;
            }

            if (target.IsFriendlyCharacter && target.IsDamaged)
            {
                score += 40;
            }

            if (target.IsFriendlyCharacter && !target.IsDamaged && target.Attack > 0)
            {
                score -= 35;
            }

            if (!target.IsEnemyHero && !target.IsEnemyCharacter)
            {
                score -= 500;
            }

            return score;
        }

        public int ScoreMulliganCard(HbMulliganSnapshot snapshot, HbMulliganCardSnapshot card)
        {
            if (card == null)
            {
                return int.MinValue;
            }

            int score = 0;

            if (!card.IsCostKnown || card.Cost < 0)
            {
                if (card.IsCoinKnown && card.IsCoin)
                {
                    score += 2;
                }

                return score;
            }

            if (card.Cost <= 2)
            {
                score += 30;
            }

            if (card.Cost == 3)
            {
                score += 10;
            }

            if (card.Cost >= 5)
            {
                score -= 40;
            }

            if (snapshot != null && snapshot.Cards.Count >= 4 && card.Cost == 4)
            {
                score += 5;
            }

            if (card.IsCoinKnown && card.IsCoin)
            {
                score += 2;
            }

            return score;
        }
    }
}
