using HsBattle.Strategy;
using HsBattle.Strategy.Hb;

namespace HsBattle.StrategyHarness
{
    internal static class HbTestStateFactory
    {
        public static HbSimulatedTurnState CreateCleanTradeState()
        {
            HbBattleSnapshot snapshot = CreateTradeBeforeFaceSnapshot();
            return HbSimulatedTurnState.Create(snapshot);
        }

        public static HbSimulatedTurnState CreateHeroPowerState()
        {
            HbBattleSnapshot snapshot = CreateHeroPowerThenAttackSnapshot();
            return HbSimulatedTurnState.Create(snapshot);
        }

        public static HbBattleSnapshot CreateTradeBeforeFaceSnapshot()
        {
            HbBattleSnapshot snapshot = new HbBattleSnapshot
            {
                IsFriendlyTurn = true,
                AvailableMana = 2,
                EnemyHeroHealth = 30,
                IsEnemyHeroHealthKnown = true
            };

            snapshot.FriendlyBoard.Add(new HbBattleEntitySnapshot { EntityId = 11, IsFriendly = true, IsMinion = true, Attack = 3, Health = 4, MaxHealth = 4, CanAttack = true });
            snapshot.EnemyBoard.Add(new HbBattleEntitySnapshot { EntityId = 21, IsFriendly = false, IsMinion = true, Attack = 2, Health = 3, MaxHealth = 3 });
            snapshot.Options.Add(CreateAttackOption(sourceId: 11, targetId: 21, attack: 3, sourceHealth: 4, targetAttack: 2, targetHealth: 3, optionIndex: 0));
            snapshot.Options.Add(new HbBattleOptionSnapshot { OptionIndex = 1, EntityId = 11, Description = "face attack", Kind = StrategyActionKind.Attack, Attack = 3, SourceHealth = 4, IsPlayable = true });
            return snapshot;
        }

        public static HbBattleSnapshot CreateHeroPowerThenAttackSnapshot()
        {
            HbBattleSnapshot snapshot = new HbBattleSnapshot
            {
                IsFriendlyTurn = true,
                AvailableMana = 2,
                EnemyHeroHealth = 30,
                IsEnemyHeroHealthKnown = true,
                HeroPowerUsable = true
            };

            snapshot.FriendlyBoard.Add(new HbBattleEntitySnapshot { EntityId = 11, IsFriendly = true, IsMinion = true, Attack = 2, Health = 3, MaxHealth = 3, CanAttack = true });
            snapshot.EnemyBoard.Add(new HbBattleEntitySnapshot { EntityId = 21, IsFriendly = false, IsMinion = true, Attack = 2, Health = 3, MaxHealth = 3 });
            snapshot.Options.Add(CreateHeroPowerOption(sourceId: 51, targetId: 21, cost: 2, damage: 1, optionIndex: 0, targetAttack: 2, targetHealth: 3));
            snapshot.Options.Add(CreateAttackOption(sourceId: 11, targetId: 21, attack: 2, sourceHealth: 3, targetAttack: 2, targetHealth: 3, optionIndex: 1));
            return snapshot;
        }

        public static HbBattleOptionSnapshot CreateAttackOption(int sourceId, int targetId, int attack, int sourceHealth, int targetAttack, int targetHealth, int optionIndex = 0)
        {
            HbBattleOptionSnapshot option = new HbBattleOptionSnapshot
            {
                OptionIndex = optionIndex,
                EntityId = sourceId,
                Description = "trade attack",
                Kind = StrategyActionKind.Attack,
                Attack = attack,
                SourceHealth = sourceHealth,
                IsPlayable = true
            };

            option.Targets.Add(new HbBattleTargetSnapshot
            {
                EntityId = targetId,
                Attack = targetAttack,
                Health = targetHealth,
                IsResolved = true,
                IsEnemyCharacter = true
            });

            return option;
        }

        public static HbBattleOptionSnapshot CreateHeroPowerOption(int sourceId, int targetId, int cost, int damage, int optionIndex = 0, int targetAttack = 2, int targetHealth = 3)
        {
            HbBattleOptionSnapshot option = new HbBattleOptionSnapshot
            {
                OptionIndex = optionIndex,
                EntityId = sourceId,
                Description = "hero power ping",
                Kind = StrategyActionKind.HeroPower,
                Cost = cost,
                DamageAmount = damage,
                RequiresTarget = true,
                IsPlayable = true
            };

            option.Targets.Add(new HbBattleTargetSnapshot
            {
                EntityId = targetId,
                Attack = targetAttack,
                Health = targetHealth,
                IsResolved = true,
                IsEnemyCharacter = true
            });

            return option;
        }

        public static HbSimulatedTurnState CreateScoredState(int enemyAttack, int friendlyAttack, int remainingMana)
        {
            HbBattleSnapshot snapshot = new HbBattleSnapshot
            {
                IsFriendlyTurn = true,
                AvailableMana = remainingMana
            };

            snapshot.FriendlyBoard.Add(new HbBattleEntitySnapshot { EntityId = 11, IsFriendly = true, IsMinion = true, Attack = friendlyAttack, Health = 3, MaxHealth = 3 });
            if (enemyAttack > 0)
            {
                snapshot.EnemyBoard.Add(new HbBattleEntitySnapshot { EntityId = 21, IsFriendly = false, IsMinion = true, Attack = enemyAttack, Health = 3, MaxHealth = 3 });
            }

            HbSimulatedTurnState state = HbSimulatedTurnState.Create(snapshot);
            state.RemainingMana = remainingMana;
            return state;
        }
    }
}
