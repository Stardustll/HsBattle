using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace HsBattle.Strategy.Hb
{
    internal sealed class HbSnapshotAdapter
    {
        public HbBattleSnapshot CreateBattleSnapshot(StrategyContext context)
        {
            HbBattleSnapshot snapshot = new HbBattleSnapshot();
            if (context == null)
            {
                return snapshot;
            }

            snapshot.StrategyMode = context.StrategyMode;

            GameState gameState = context.GameState;
            snapshot.IsFriendlyTurn = gameState != null && gameState.IsFriendlySidePlayerTurn();
            snapshot.FriendlyHeroHealth = TryResolveFriendlyHeroHealth(gameState);
            snapshot.EnemyHeroHealth = TryResolveEnemyHeroHealth(gameState);
            snapshot.IsFriendlyHeroHealthKnown = snapshot.FriendlyHeroHealth >= 0;
            snapshot.IsEnemyHeroHealthKnown = snapshot.EnemyHeroHealth >= 0;

            Network.Options options = context.Options;
            if (options == null && gameState != null)
            {
                options = gameState.GetOptionsPacket() ?? gameState.GetLastOptions();
            }

            if (options == null || options.List == null)
            {
                return snapshot;
            }

            for (int optionIndex = 0; optionIndex < options.List.Count; optionIndex++)
            {
                HbBattleOptionSnapshot option = CreateBattleOptionSnapshot(gameState, optionIndex, options.List[optionIndex], snapshot.EnemyHeroHealth);
                if (option != null)
                {
                    snapshot.Options.Add(option);
                }
            }

            return snapshot;
        }

        public HbMulliganSnapshot CreateMulliganSnapshot(MulliganManager mulliganManager)
        {
            HbMulliganSnapshot snapshot = new HbMulliganSnapshot();
            if (mulliganManager == null)
            {
                return snapshot;
            }

            snapshot.IsActive = mulliganManager.IsMulliganActive();
            snapshot.IsIntroActive = mulliganManager.IsMulliganIntroActive();
            snapshot.WaitingForUserInput = TryReadBoolField(mulliganManager, "m_waitingForUserInput");
            snapshot.RequiresConfirmation = TryResolveMulliganRequiresConfirmation();

            // Prefer GetStartingCards() so card index -> mulligan toggle index stay in the same source/order.
            // If this source is unavailable, mark snapshot degraded and let caller fall back conservatively.
            bool resolvedFromStartingCards = TryResolveMulliganCardsFromStartingCards(mulliganManager, snapshot.Cards);
            if (!resolvedFromStartingCards)
            {
                snapshot.HasUnknownCards = true;
                foreach (HbMulliganCardSnapshot card in TryResolveMulliganCards(mulliganManager))
                {
                    snapshot.Cards.Add(card);
                }
            }

            return snapshot;
        }

        private static HbBattleOptionSnapshot CreateBattleOptionSnapshot(
            GameState gameState,
            int optionIndex,
            Network.Options.Option option,
            int enemyHeroHealth)
        {
            if (option == null || option.Main == null)
            {
                return null;
            }

            Entity entity = gameState != null ? gameState.GetEntity(option.Main.ID) : null;
            bool canTargetEnemyHero = CanTargetEnemyHero(gameState, option.Main.Targets);
            int attack = entity != null ? entity.GetRealTimeAttack() : 0;

            HbBattleOptionSnapshot battleOption = new HbBattleOptionSnapshot
            {
                OptionIndex = optionIndex,
                EntityId = option.Main.ID,
                Cost = entity != null ? entity.GetRealTimeCost() : 0,
                Attack = attack,
                SourceHealth = entity != null ? entity.GetCurrentHealth() : -1,
                TargetCount = CountPlayableTargets(option.Main.Targets),
                RequiresTarget = RequiresTarget(option),
                CanTargetEnemyHero = canTargetEnemyHero,
                CanLethal = canTargetEnemyHero && enemyHeroHealth > 0 && attack >= enemyHeroHealth,
                IsPlayable = IsPlayableOption(option),
                IsEndTurn = option.Type == Network.Options.Option.OptionType.END_TURN,
                IsPass = option.Type == Network.Options.Option.OptionType.PASS,
                IsEntityResolved = entity != null,
                Kind = ResolveActionKind(gameState, option, entity),
                Description = BuildOptionDescription(optionIndex, option, entity)
            };

            foreach (HbBattleTargetSnapshot target in CreateBattleTargetSnapshots(gameState, option.Main.Targets))
            {
                battleOption.Targets.Add(target);
            }

            return battleOption;
        }

        private static string BuildOptionDescription(int optionIndex, Network.Options.Option option, Entity entity)
        {
            string type = option != null ? option.Type.ToString() : "Unknown";
            string entityName = entity != null ? entity.GetName() : "Entity";
            return "#" + optionIndex + " " + type + " " + entityName;
        }

        private static bool IsPlayableOption(Network.Options.Option option)
        {
            if (option == null)
            {
                return false;
            }

            if (option.Type == Network.Options.Option.OptionType.END_TURN || option.Type == Network.Options.Option.OptionType.PASS)
            {
                return true;
            }

            return IsPlayableSubOption(option.Main);
        }

        private static bool IsPlayableSubOption(Network.Options.Option.SubOption subOption)
        {
            return subOption != null
                && (subOption.PlayErrorInfo == null || subOption.PlayErrorInfo.IsValid());
        }

        private static bool IsPlayableTargetOption(Network.Options.Option.TargetOption targetOption)
        {
            return targetOption != null
                && (targetOption.PlayErrorInfo == null || targetOption.PlayErrorInfo.IsValid());
        }

        private static bool HasUsableTargets(List<Network.Options.Option.TargetOption> targetOptions)
        {
            if (targetOptions == null || targetOptions.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < targetOptions.Count; i++)
            {
                if (IsPlayableTargetOption(targetOptions[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RequiresTarget(Network.Options.Option option)
        {
            if (option == null
                || option.Type == Network.Options.Option.OptionType.END_TURN
                || option.Type == Network.Options.Option.OptionType.PASS)
            {
                return false;
            }

            if (option.Main != null && option.Main.Targets != null)
            {
                return true;
            }

            if (option.Subs == null)
            {
                return false;
            }

            for (int i = 0; i < option.Subs.Count; i++)
            {
                Network.Options.Option.SubOption subOption = option.Subs[i];
                if (subOption != null && subOption.Targets != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountPlayableTargets(List<Network.Options.Option.TargetOption> targetOptions)
        {
            if (targetOptions == null || targetOptions.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < targetOptions.Count; i++)
            {
                if (IsPlayableTargetOption(targetOptions[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CanTargetEnemyHero(GameState gameState, List<Network.Options.Option.TargetOption> targetOptions)
        {
            if (gameState == null || targetOptions == null || targetOptions.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < targetOptions.Count; i++)
            {
                Network.Options.Option.TargetOption targetOption = targetOptions[i];
                if (!IsPlayableTargetOption(targetOption))
                {
                    continue;
                }

                Entity targetEntity = gameState.GetEntity(targetOption.ID);
                if (targetEntity == null || !targetEntity.IsHero())
                {
                    continue;
                }

                if (targetEntity.GetControllerSide() != Player.Side.FRIENDLY)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<HbBattleTargetSnapshot> CreateBattleTargetSnapshots(GameState gameState, List<Network.Options.Option.TargetOption> targetOptions)
        {
            if (gameState == null || targetOptions == null || targetOptions.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < targetOptions.Count; i++)
            {
                Network.Options.Option.TargetOption targetOption = targetOptions[i];
                if (!IsPlayableTargetOption(targetOption))
                {
                    continue;
                }

                Entity targetEntity = gameState.GetEntity(targetOption.ID);
                bool isEnemy = targetEntity != null && targetEntity.GetControllerSide() != Player.Side.FRIENDLY;
                bool isHero = targetEntity != null && targetEntity.IsHero();
                bool isCharacter = targetEntity != null && targetEntity.IsCharacter();

                yield return new HbBattleTargetSnapshot
                {
                    EntityId = targetOption.ID,
                    Attack = targetEntity != null ? targetEntity.GetRealTimeAttack() : 0,
                    Health = targetEntity != null ? targetEntity.GetCurrentHealth() : -1,
                    MaxHealth = targetEntity != null ? targetEntity.GetDefHealth() : -1,
                    MissingHealth = targetEntity != null ? Math.Max(0, targetEntity.GetDefHealth() - targetEntity.GetCurrentHealth()) : 0,
                    IsDamaged = targetEntity != null && targetEntity.GetCurrentHealth() < targetEntity.GetDefHealth(),
                    IsResolved = targetEntity != null,
                    IsEnemyHero = isEnemy && isHero,
                    IsEnemyCharacter = isEnemy && isCharacter,
                    IsFriendlyHero = !isEnemy && isHero,
                    IsFriendlyCharacter = !isEnemy && isCharacter
                };
            }
        }

        private static StrategyActionKind ResolveActionKind(GameState gameState, Network.Options.Option option, Entity entity)
        {
            if (option == null)
            {
                return StrategyActionKind.Other;
            }

            if (option.Type == Network.Options.Option.OptionType.END_TURN)
            {
                return StrategyActionKind.EndTurn;
            }

            if (option.Type == Network.Options.Option.OptionType.PASS)
            {
                return StrategyActionKind.Pass;
            }

            if (entity == null)
            {
                return StrategyActionKind.Other;
            }

            if ((gameState != null && gameState.IsInChoiceMode()) || entity.GetZone() == TAG_ZONE.SETASIDE)
            {
                return StrategyActionKind.Choice;
            }

            if (entity.IsHeroPower())
            {
                return StrategyActionKind.HeroPower;
            }

            if (entity.GetZone() == TAG_ZONE.HAND)
            {
                return StrategyActionKind.PlayCard;
            }

            if (entity.GetZone() == TAG_ZONE.PLAY && (entity.IsCharacter() || entity.IsWeapon()))
            {
                return StrategyActionKind.Attack;
            }

            return StrategyActionKind.Other;
        }

        private static int TryResolveFriendlyHeroHealth(GameState gameState)
        {
            if (gameState == null)
            {
                return -1;
            }

            return TryResolveHeroHealth(TryResolveFriendlyPlayer(gameState));
        }

        private static int TryResolveEnemyHeroHealth(GameState gameState)
        {
            if (gameState == null)
            {
                return -1;
            }

            return TryResolveHeroHealth(TryResolveOpposingPlayer(gameState));
        }

        private static Player TryResolveFriendlyPlayer(GameState gameState)
        {
            try
            {
                return gameState.GetFriendlySidePlayer();
            }
            catch
            {
                return null;
            }
        }

        private static Player TryResolveOpposingPlayer(GameState gameState)
        {
            if (gameState == null)
            {
                return null;
            }

            try
            {
                MethodInfo method = typeof(GameState).GetMethod("GetOpposingSidePlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return method != null ? method.Invoke(gameState, null) as Player : null;
            }
            catch
            {
                return null;
            }
        }

        private static int TryResolveHeroHealth(Player player)
        {
            if (player == null)
            {
                return -1;
            }

            string[] methodNames = { "GetHero", "GetHeroEntity", "GetHeroCard" };
            for (int i = 0; i < methodNames.Length; i++)
            {
                try
                {
                    MethodInfo method = typeof(Player).GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Entity hero = method != null ? method.Invoke(player, null) as Entity : null;
                    if (hero != null)
                    {
                        return hero.GetCurrentHealth();
                    }
                }
                catch
                {
                }
            }

            return -1;
        }

        private static bool TryResolveMulliganRequiresConfirmation()
        {
            try
            {
                GameState gameState = GameState.Get();
                return gameState != null
                    && gameState.GetBooleanGameOption(GameEntityOption.MULLIGAN_REQUIRES_CONFIRMATION);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveMulliganCardsFromStartingCards(
            MulliganManager mulliganManager,
            List<HbMulliganCardSnapshot> cards)
        {
            if (mulliganManager == null || cards == null)
            {
                return false;
            }

            List<Card> startingCards = TryGetStartingCards(mulliganManager);
            if (startingCards == null)
            {
                return false;
            }

            for (int i = 0; i < startingCards.Count; i++)
            {
                cards.Add(TryBuildCardSnapshot(i, startingCards[i]));
            }

            return true;
        }

        private static List<Card> TryGetStartingCards(MulliganManager mulliganManager)
        {
            if (mulliganManager == null)
            {
                return null;
            }

            try
            {
                return mulliganManager.GetStartingCards();
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<HbMulliganCardSnapshot> TryResolveMulliganCards(MulliganManager mulliganManager)
        {
            object cardsSource = TryReadObjectMember(mulliganManager, "m_mulliganCards")
                ?? TryReadObjectMember(mulliganManager, "m_mulliganEntities")
                ?? TryReadObjectMember(mulliganManager, "m_cards")
                ?? TryReadObjectMember(mulliganManager, "m_entities");

            IEnumerable enumerable = cardsSource as IEnumerable;
            if (enumerable == null)
            {
                yield break;
            }

            int index = 0;
            foreach (object entry in enumerable)
            {
                yield return TryBuildCardSnapshot(index, entry);
                index++;
            }
        }

        private static HbMulliganCardSnapshot TryBuildCardSnapshot(int index, object rawCard)
        {
            if (rawCard == null)
            {
                return new HbMulliganCardSnapshot
                {
                    Index = index
                };
            }

            Entity entity = TryResolveEntity(rawCard);
            int entityId = -1;
            int cost = -1;
            bool isResolved = false;
            bool isCostKnown = false;

            if (entity != null)
            {
                entityId = entity.GetEntityId();
                cost = entity.GetRealTimeCost();
                isResolved = true;
                isCostKnown = true;
            }

            if (entity == null)
            {
                int reflectedEntityId = TryReadIntMember(rawCard, "ID")
                    ?? TryReadIntMember(rawCard, "EntityID")
                    ?? TryReadIntMember(rawCard, "m_entityID")
                    ?? TryReadIntMember(rawCard, "m_entityId")
                    ?? -1;

                if (reflectedEntityId > 0)
                {
                    entityId = reflectedEntityId;
                    isResolved = true;
                    try
                    {
                        GameState gameState = GameState.Get();
                        entity = gameState != null ? gameState.GetEntity(reflectedEntityId) : null;
                    }
                    catch
                    {
                        entity = null;
                    }
                }

                if (entity != null)
                {
                    cost = entity.GetRealTimeCost();
                    isCostKnown = true;
                }
                else
                {
                    int? reflectedCost = TryReadIntMember(rawCard, "Cost")
                        ?? TryReadIntMember(rawCard, "m_cost");
                    if (reflectedCost.HasValue)
                    {
                        cost = reflectedCost.Value;
                        isCostKnown = true;
                    }
                }
            }

            bool isCoin;
            bool isCoinKnown = TryResolveIsCoin(entity, rawCard, out isCoin);

            return new HbMulliganCardSnapshot
            {
                Index = index,
                EntityId = entityId,
                Cost = cost,
                IsResolved = isResolved,
                IsCostKnown = isCostKnown,
                IsCoin = isCoin,
                IsCoinKnown = isCoinKnown
            };
        }

        private static Entity TryResolveEntity(object rawCard)
        {
            if (rawCard == null)
            {
                return null;
            }

            Entity entity = rawCard as Entity;
            if (entity != null)
            {
                return entity;
            }

            entity = TryReadObjectMember(rawCard, "Entity") as Entity
                ?? TryReadObjectMember(rawCard, "m_entity") as Entity;
            if (entity != null)
            {
                return entity;
            }

            return TryInvokeEntityMethod(rawCard, "GetEntity");
        }

        private static Entity TryInvokeEntityMethod(object source, string methodName)
        {
            if (source == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            try
            {
                MethodInfo method = source.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null || method.GetParameters().Length != 0)
                {
                    return null;
                }

                return method.Invoke(source, null) as Entity;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryReadBoolField(object instance, string fieldName)
        {
            object value = TryReadObjectMember(instance, fieldName);
            return value is bool && (bool)value;
        }

        private static bool TryResolveIsCoin(Entity entity, object rawCard, out bool isCoin)
        {
            isCoin = false;

            string cardId = TryResolveCardId(entity);
            if (string.IsNullOrEmpty(cardId) && rawCard != null && !ReferenceEquals(rawCard, entity))
            {
                cardId = TryResolveCardId(rawCard);
            }

            if (string.IsNullOrEmpty(cardId))
            {
                return false;
            }

            isCoin = string.Equals(cardId, "GAME_005", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private static string TryResolveCardId(object source)
        {
            if (source == null)
            {
                return null;
            }

            return TryInvokeStringMethod(source, "GetCardId")
                ?? TryInvokeStringMethod(source, "GetCardID")
                ?? TryReadStringMember(source, "CardID")
                ?? TryReadStringMember(source, "CardId")
                ?? TryReadStringMember(source, "m_cardId");
        }

        private static string TryInvokeStringMethod(object source, string methodName)
        {
            if (source == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            try
            {
                MethodInfo method = source.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null || method.GetParameters().Length != 0 || method.ReturnType != typeof(string))
                {
                    return null;
                }

                return method.Invoke(source, null) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string TryReadStringMember(object instance, string memberName)
        {
            object value = TryReadObjectMember(instance, memberName);
            return value as string;
        }

        private static object TryReadObjectMember(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            try
            {
                FieldInfo field = instance.GetType().GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property = instance.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null ? property.GetValue(instance, null) : null;
            }
            catch
            {
                return null;
            }
        }

        private static int? TryReadIntMember(object instance, string memberName)
        {
            object value = TryReadObjectMember(instance, memberName);
            if (value is int)
            {
                return (int)value;
            }

            return null;
        }
    }
}
