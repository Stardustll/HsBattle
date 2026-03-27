using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbActionResolver
    {
        public bool TryApply(HbSimulatedTurnState state, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)
        {
            if (state == null || option == null || !option.IsPlayable)
            {
                return false;
            }

            switch (option.Kind)
            {
                case StrategyActionKind.Attack:
                    return ApplyAttack(state, option, target);
                case StrategyActionKind.HeroPower:
                    return ApplyHeroPower(state, option, target);
                case StrategyActionKind.PlayCard:
                    return ApplyPlayCard(state, option, target);
                default:
                    return false;
            }
        }

        private static bool ApplyAttack(HbSimulatedTurnState state, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)
        {
            HbBattleEntitySnapshot attacker = Find(state.FriendlyBoard, option.EntityId);
            if (attacker == null || attacker.HasAttacked || !attacker.CanAttack)
            {
                return false;
            }

            attacker.HasAttacked = true;
            attacker.CanAttack = false;

            if (target == null)
            {
                if (state.IsEnemyHeroHealthKnown && option.Attack > 0)
                {
                    state.EnemyHeroHealth = System.Math.Max(0, state.EnemyHeroHealth - option.Attack);
                }

                RecordStep(state, option, null);
                return true;
            }

            if (target.IsEnemyHero)
            {
                if (state.IsEnemyHeroHealthKnown && option.Attack > 0)
                {
                    state.EnemyHeroHealth = System.Math.Max(0, state.EnemyHeroHealth - option.Attack);
                }

                RecordStep(state, option, target);
                return true;
            }

            HbBattleEntitySnapshot defender = Find(state.EnemyBoard, target.EntityId) ?? Find(state.FriendlyBoard, target.EntityId);
            if (defender == null)
            {
                return false;
            }

            attacker.Health -= System.Math.Max(0, defender.Attack);
            defender.Health -= System.Math.Max(0, option.Attack);
            RemoveDead(state.FriendlyBoard);
            RemoveDead(state.EnemyBoard);
            RecordStep(state, option, target);
            return true;
        }

        private static bool ApplyHeroPower(HbSimulatedTurnState state, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)
        {
            if (state.HeroPowerUsed || option.Cost > state.RemainingMana)
            {
                return false;
            }

            state.RemainingMana -= option.Cost;
            state.HeroPowerUsed = true;

            if (target != null)
            {
                ApplyTargetDelta(state, option.DamageAmount, target);
            }

            RecordStep(state, option, target);
            return true;
        }

        private static bool ApplyPlayCard(HbSimulatedTurnState state, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)
        {
            if (option.Cost > state.RemainingMana)
            {
                return false;
            }

            state.RemainingMana -= option.Cost;

            if (target != null && option.DamageAmount > 0)
            {
                ApplyTargetDelta(state, option.DamageAmount, target);
            }

            RecordStep(state, option, target);
            return true;
        }

        private static void ApplyTargetDelta(HbSimulatedTurnState state, int damageAmount, HbBattleTargetSnapshot target)
        {
            if (state == null || target == null || damageAmount <= 0)
            {
                return;
            }

            if (target.IsEnemyHero)
            {
                if (state.IsEnemyHeroHealthKnown)
                {
                    state.EnemyHeroHealth = System.Math.Max(0, state.EnemyHeroHealth - damageAmount);
                }

                return;
            }

            if (target.IsFriendlyHero)
            {
                if (state.IsFriendlyHeroHealthKnown)
                {
                    state.FriendlyHeroHealth = System.Math.Max(0, state.FriendlyHeroHealth - damageAmount);
                }

                return;
            }

            HbBattleEntitySnapshot entity = Find(state.EnemyBoard, target.EntityId) ?? Find(state.FriendlyBoard, target.EntityId);
            if (entity == null)
            {
                return;
            }

            entity.Health -= damageAmount;
            RemoveDead(state.FriendlyBoard);
            RemoveDead(state.EnemyBoard);
        }

        private static void RecordStep(HbSimulatedTurnState state, HbBattleOptionSnapshot option, HbBattleTargetSnapshot target)
        {
            state.ExecutedSteps.Add(new StrategyActionPlan
            {
                OptionIndex = option.OptionIndex,
                TargetId = target != null ? target.EntityId : -1,
                Description = option.Description,
                Kind = option.Kind,
                Score = 0
            });
        }

        private static HbBattleEntitySnapshot Find(List<HbBattleEntitySnapshot> board, int entityId)
        {
            if (board == null)
            {
                return null;
            }

            for (int i = 0; i < board.Count; i++)
            {
                if (board[i] != null && board[i].EntityId == entityId)
                {
                    return board[i];
                }
            }

            return null;
        }

        private static void RemoveDead(List<HbBattleEntitySnapshot> board)
        {
            if (board == null)
            {
                return;
            }

            board.RemoveAll(entity => entity == null || entity.Health <= 0);
        }
    }
}
