using System.Collections.Generic;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbSimulatedTurnState
    {
        public HbSimulatedTurnState()
        {
            FriendlyBoard = new List<HbBattleEntitySnapshot>();
            EnemyBoard = new List<HbBattleEntitySnapshot>();
            HandCards = new List<HbBattleCardSnapshot>();
            ExecutedSteps = new List<StrategyActionPlan>();
        }

        public int RemainingMana { get; set; }

        public bool HeroPowerUsed { get; set; }

        public int FriendlyHeroHealth { get; set; } = -1;

        public int EnemyHeroHealth { get; set; } = -1;

        public bool IsFriendlyHeroHealthKnown { get; set; }

        public bool IsEnemyHeroHealthKnown { get; set; }

        public List<HbBattleEntitySnapshot> FriendlyBoard { get; }

        public List<HbBattleEntitySnapshot> EnemyBoard { get; }

        public List<HbBattleCardSnapshot> HandCards { get; }

        public List<StrategyActionPlan> ExecutedSteps { get; }

        public static HbSimulatedTurnState Create(HbBattleSnapshot snapshot)
        {
            HbSimulatedTurnState state = new HbSimulatedTurnState();
            if (snapshot == null)
            {
                return state;
            }

            state.RemainingMana = snapshot.AvailableMana;
            state.HeroPowerUsed = !snapshot.HeroPowerUsable;
            state.FriendlyHeroHealth = snapshot.FriendlyHeroHealth;
            state.EnemyHeroHealth = snapshot.EnemyHeroHealth;
            state.IsFriendlyHeroHealthKnown = snapshot.IsFriendlyHeroHealthKnown;
            state.IsEnemyHeroHealthKnown = snapshot.IsEnemyHeroHealthKnown;

            CopyEntities(snapshot.FriendlyBoard, state.FriendlyBoard);
            CopyEntities(snapshot.EnemyBoard, state.EnemyBoard);
            CopyCards(snapshot.HandCards, state.HandCards);
            return state;
        }

        public HbSimulatedTurnState Clone()
        {
            HbSimulatedTurnState clone = new HbSimulatedTurnState
            {
                RemainingMana = RemainingMana,
                HeroPowerUsed = HeroPowerUsed,
                FriendlyHeroHealth = FriendlyHeroHealth,
                EnemyHeroHealth = EnemyHeroHealth,
                IsFriendlyHeroHealthKnown = IsFriendlyHeroHealthKnown,
                IsEnemyHeroHealthKnown = IsEnemyHeroHealthKnown
            };

            CopyEntities(FriendlyBoard, clone.FriendlyBoard);
            CopyEntities(EnemyBoard, clone.EnemyBoard);
            CopyCards(HandCards, clone.HandCards);

            for (int i = 0; i < ExecutedSteps.Count; i++)
            {
                StrategyActionPlan step = ExecutedSteps[i];
                clone.ExecutedSteps.Add(new StrategyActionPlan
                {
                    OptionIndex = step.OptionIndex,
                    TargetId = step.TargetId,
                    Score = step.Score,
                    Description = step.Description,
                    Kind = step.Kind
                });
            }

            return clone;
        }

        private static void CopyEntities(List<HbBattleEntitySnapshot> source, List<HbBattleEntitySnapshot> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                HbBattleEntitySnapshot entity = source[i];
                if (entity == null)
                {
                    continue;
                }

                destination.Add(new HbBattleEntitySnapshot
                {
                    EntityId = entity.EntityId,
                    IsFriendly = entity.IsFriendly,
                    IsHero = entity.IsHero,
                    IsMinion = entity.IsMinion,
                    Attack = entity.Attack,
                    Health = entity.Health,
                    MaxHealth = entity.MaxHealth,
                    CanAttack = entity.CanAttack,
                    HasAttacked = entity.HasAttacked,
                    HasTaunt = entity.HasTaunt,
                    HasDivineShield = entity.HasDivineShield,
                    IsStealthed = entity.IsStealthed,
                    IsFrozen = entity.IsFrozen
                });
            }
        }

        private static void CopyCards(List<HbBattleCardSnapshot> source, List<HbBattleCardSnapshot> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                HbBattleCardSnapshot card = source[i];
                if (card == null)
                {
                    continue;
                }

                destination.Add(new HbBattleCardSnapshot
                {
                    EntityId = card.EntityId,
                    Cost = card.Cost
                });
            }
        }
    }
}
