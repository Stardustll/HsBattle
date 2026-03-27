using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HsBattle.Strategy;
using HsBattle.Strategy.Hb;
using UnityEngine;

namespace HsBattle
{
    internal sealed class BattleController
    {
        private const float HubSceneSettleSeconds = 4f;
        private const float ModeSwitchSettleSeconds = 1.5f;
        private const float DeckSelectionSettleSeconds = 0.8f;
        private const float ChoiceSettleSeconds = 2f;
        private const float ChoiceForceSelectSeconds = 4.5f;
        private const float EndGameContinueInitialDelaySeconds = 1.2f;
        private const float EndGameContinueIntervalSeconds = 0.6f;
        private const float EndGameForceCloseSeconds = 8f;
        private const float PostGameUiWatchSeconds = 20f;
        private const float PostGameUiRetrySeconds = 0.35f;
        private const float MulliganReadySettleSeconds = 2f;
        private const float MulliganConfirmDelaySeconds = 0.4f;
        private const float PostMulliganSettleSeconds = 1.25f;
        private static readonly MethodInfo GetFriendlyChoiceStateMethod = typeof(ChoiceCardMgr).GetMethod("GetFriendlyChoiceState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo ConcealChoicesFromInputMethod = typeof(ChoiceCardMgr).GetMethod("ConcealChoicesFromInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo EndGameContinueEventsMethod = typeof(EndGameScreen).GetMethod("ContinueEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo EndGameIsDoneDisplayingRewardsMethod = typeof(EndGameScreen).GetMethod("IsDoneDisplayingRewards", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo EndGameIsInputBlockedMethod = typeof(EndGameScreen).GetMethod("IsInputBlocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo EndGameIsPlayingBlockingAnimMethod = typeof(EndGameScreen).GetMethod("IsPlayingBlockingAnim", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo EndGameBackToModeMethod = typeof(EndGameScreen).GetMethod("BackToMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo EndGameHitboxField = typeof(EndGameScreen).GetField("m_hitbox", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo RankChangeDebugClickCatcherField = typeof(RankChangeTwoScoop_NEW).GetField("m_debugClickCatcher", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo RankChangeIsReadyMethod = typeof(RankChangeTwoScoop_NEW).GetMethod("get_IsReady", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo RankChangeOnClickMethod = typeof(RankChangeTwoScoop_NEW).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo RankChangeHideMethod = typeof(RankChangeTwoScoop_NEW).GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo RankedBonusStarsPopupHideMethod = typeof(RankedBonusStarsPopup).GetMethod("Hide", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo PlayGameMethod = typeof(DeckPickerTrayDisplay).GetMethod("PlayGame", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo ShouldWaitForMulliganCardsMethod = typeof(MulliganManager).GetMethod("ShouldWaitForMulliganCardsToBeProcessed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo WaitingForUserInputField = typeof(MulliganManager).GetField("m_waitingForUserInput", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo HideChoiceUIMethod = typeof(ChoiceCardMgr).GetMethod("HideChoiceUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo GetLastChosenDeckIdMethod = typeof(DeckPickerTrayDisplay).GetMethod("GetLastChosenDeckId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo GetFirstDeckboxMethod = typeof(DeckPickerTrayDisplay).GetMethod("GetFirstDeckbox", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo GetDeckboxWithDeckIDMethod = typeof(DeckPickerTrayDisplay).GetMethod("GetDeckboxWithDeckID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(long) }, null);
        private static readonly MethodInfo SelectCustomDeckMethod = typeof(DeckPickerTrayDisplay).GetMethod("SelectCustomDeck", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(CollectionDeckBoxVisual) }, null);

        private bool _forceQueueRequested;
        private bool _queueReturnToHubPending;
        private bool _lastAutoQueueEnabled;
        private QueueMode _lastQueueMode;
        private long _lastQueueDeckId;
        private float _nextQueueAttemptAt;
        private float _nextActionAt;
        private float _postMulliganActionBlockedUntil;
        private float _choiceReadySinceAt;
        private float _mulliganReadySinceAt;
        private float _mulliganHoldIssuedAt;
        private float _mulliganConfirmReadySinceAt;
        private float _gameEndedAt;
        private float _deckSelectionWaitStartedAt;
        private float _modeSwitchWaitStartedAt;
        private float _modeSwitchSettledAt;
        private float _hubSettleWaitStartedAt;
        private float _deckReadySinceAt;
        private float _nextEndGameContinueAt;
        private float _nextNavigationAttemptAt;
        private int _choiceSignature;
        private bool _hbStrategyFallbackLogged;
        private readonly LegacyStrategyEngine _legacyStrategyEngine = new LegacyStrategyEngine();
        private readonly HbStrategyEngine _hbStrategyEngine = new HbStrategyEngine();
        private readonly HbSnapshotAdapter _hbSnapshotAdapter = new HbSnapshotAdapter();
        private readonly HbMulliganDecisionService _hbMulliganDecisionService = new HbMulliganDecisionService();

        private sealed class ActionPlan
        {
            public int OptionIndex;
            public int SubOptionIndex = -1;
            public int TargetId = -1;
            public int Position;
            public int Score;
            public string Description;
            public ActionKind Kind;
        }

        private enum ActionKind
        {
            Choice,
            PlayCard,
            HeroPower,
            Attack,
            Pass,
            EndTurn,
            Other
        }

        public BattleController()
        {
            _lastAutoQueueEnabled = PluginConfig.AutoQueueEnabledValue;
            _lastQueueMode = PluginConfig.queueMode != null ? PluginConfig.queueMode.Value : QueueMode.Standard;
            _lastQueueDeckId = GetConfiguredQueueDeckId();

            if (_lastAutoQueueEnabled)
            {
                ArmInitialQueueSetup();
            }
        }

        public void Tick()
        {
            TrackQueueConfiguration();

            TryHandleEndGameScreen();
            TryQueue();
            TryHandleMulligan();
            TryHandleBattle();
        }

        public void NotifyGameEnded()
        {
            _nextActionAt = Time.unscaledTime + 2f;
            _postMulliganActionBlockedUntil = 0f;
            ResetChoiceProgress();
            ResetMulliganProgress();
            _gameEndedAt = Time.unscaledTime;
            _nextEndGameContinueAt = Time.unscaledTime + EndGameContinueInitialDelaySeconds;

            if (PluginConfig.AutoQueueEnabledValue || _forceQueueRequested)
            {
                ArmInitialQueueSetup();
            }

            _nextQueueAttemptAt = Time.unscaledTime + 2f;
        }

        private void TryHandleEndGameScreen()
        {
            if ((!PluginConfig.AutoQueueEnabledValue && !_forceQueueRequested) || Time.unscaledTime < _nextEndGameContinueAt)
            {
                return;
            }

            if (TryHandlePostGameRankSummary())
            {
                return;
            }

            SceneMgr sceneMgr = SceneMgr.Get();
            GameState gameState = GameState.Get();
            EndGameScreen endGameScreen = EndGameScreen.Get();
            bool inFinishedGame = sceneMgr != null
                && sceneMgr.IsInGame()
                && gameState != null
                && gameState.IsGameCreated()
                && gameState.IsGameOver();

            if (!inFinishedGame || endGameScreen == null)
            {
                if (!inFinishedGame && endGameScreen == null)
                {
                    if (ShouldKeepWatchingPostGameUi())
                    {
                        _nextEndGameContinueAt = Time.unscaledTime + PostGameUiRetrySeconds;
                    }
                    else
                    {
                        ResetEndGameProgress();
                    }
                }

                return;
            }

            if (IsEndGameInputBlocked(endGameScreen) || IsEndGameBlockingAnimationPlaying(endGameScreen))
            {
                _nextEndGameContinueAt = Time.unscaledTime + PostGameUiRetrySeconds;
                return;
            }

            bool advanced = InvokeEndGameContinueEvents(endGameScreen);
            if (advanced)
            {
                _nextEndGameContinueAt = Time.unscaledTime + EndGameContinueIntervalSeconds;
                return;
            }

            if (IsEndGameDone(endGameScreen) || ShouldForceCloseEndGameScreen())
            {
                ReturnEndGameScreenToHub(endGameScreen);
                _nextEndGameContinueAt = Time.unscaledTime + 1f;
            }
            else
            {
                _nextEndGameContinueAt = Time.unscaledTime + EndGameContinueIntervalSeconds;
            }
        }

        private void ResetEndGameProgress()
        {
            _gameEndedAt = 0f;
            _nextEndGameContinueAt = 0f;
        }

        private bool TryHandlePostGameRankSummary()
        {
            return TryHandleRankChangeSummary() || TryHandleRankedBonusStarsPopup();
        }

        private bool TryHandleRankChangeSummary()
        {
            RankChangeTwoScoop_NEW rankChangeSummary = UnityEngine.Object.FindObjectOfType<RankChangeTwoScoop_NEW>();
            if (!IsActiveGameObject(rankChangeSummary))
            {
                return false;
            }

            if (TrySimulateRankChangeContinueClick(rankChangeSummary))
            {
                _nextEndGameContinueAt = Time.unscaledTime + PostGameUiRetrySeconds;
                return true;
            }

            bool isReady = IsRankChangeReady(rankChangeSummary);
            if (isReady)
            {
                if (InvokeMethod(rankChangeSummary, RankChangeOnClickMethod, "advanced ranked rank-change summary"))
                {
                    _nextEndGameContinueAt = Time.unscaledTime + EndGameContinueIntervalSeconds;
                    return true;
                }
            }
            else if (ShouldForceCloseEndGameScreen())
            {
                if (InvokeMethod(rankChangeSummary, RankChangeHideMethod, "force-closed ranked rank-change summary"))
                {
                    _nextEndGameContinueAt = Time.unscaledTime + EndGameContinueIntervalSeconds;
                    return true;
                }
            }

            _nextEndGameContinueAt = Time.unscaledTime + PostGameUiRetrySeconds;
            return true;
        }

        private bool TrySimulateRankChangeContinueClick(RankChangeTwoScoop_NEW rankChangeSummary)
        {
            bool clicked = false;
            clicked |= TryTriggerPegClick(GetPegUiElement(rankChangeSummary, RankChangeDebugClickCatcherField), "simulated ranked summary click catcher");
            clicked |= TryTriggerPegClick(GetPegUiElement(EndGameScreen.Get(), EndGameHitboxField), "simulated end-game hitbox click");
            return clicked;
        }

        private bool TryHandleRankedBonusStarsPopup()
        {
            RankedBonusStarsPopup bonusStarsPopup = UnityEngine.Object.FindObjectOfType<RankedBonusStarsPopup>();
            if (!IsActiveGameObject(bonusStarsPopup))
            {
                return false;
            }

            if (InvokeMethod(bonusStarsPopup, RankedBonusStarsPopupHideMethod, "closed ranked bonus-stars popup"))
            {
                _nextEndGameContinueAt = Time.unscaledTime + EndGameContinueIntervalSeconds;
                return true;
            }

            _nextEndGameContinueAt = Time.unscaledTime + PostGameUiRetrySeconds;
            return true;
        }

        private bool ShouldKeepWatchingPostGameUi()
        {
            return _gameEndedAt > 0f && Time.unscaledTime - _gameEndedAt < PostGameUiWatchSeconds;
        }

        private static PegUIElement GetPegUiElement(object instance, FieldInfo field)
        {
            if (instance == null || field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(instance) as PegUIElement;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsActiveGameObject(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private bool TryTriggerPegClick(PegUIElement element, string successMessage)
        {
            if (!IsActiveGameObject(element))
            {
                return false;
            }

            try
            {
                element.TriggerPress();
                element.TriggerRelease();
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, "HsBattle " + successMessage + ".");
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    element.TriggerTap();
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Info, "HsBattle " + successMessage + " via tap.");
                    return true;
                }
                catch (Exception tapEx)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle could not simulate post-game click: " + ex.Message + " / " + tapEx.Message);
                    return false;
                }
            }
        }

        private bool IsRankChangeReady(RankChangeTwoScoop_NEW rankChangeSummary)
        {
            return InvokeBoolReflection(rankChangeSummary, RankChangeIsReadyMethod);
        }

        private static bool InvokeBoolReflection(object instance, MethodInfo method)
        {
            if (instance == null || method == null)
            {
                return false;
            }

            try
            {
                object result = method.Invoke(instance, null);
                return result is bool && (bool)result;
            }
            catch
            {
                return false;
            }
        }

        private bool InvokeMethod(object instance, MethodInfo method, string successMessage)
        {
            if (instance == null || method == null)
            {
                return false;
            }

            try
            {
                method.Invoke(instance, null);
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, "HsBattle " + successMessage + ".");
                return true;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle could not handle post-game UI: " + ex.Message);
                return false;
            }
        }

        private bool InvokeEndGameContinueEvents(EndGameScreen endGameScreen)
        {
            if (endGameScreen == null || EndGameContinueEventsMethod == null)
            {
                return false;
            }

            try
            {
                object result = EndGameContinueEventsMethod.Invoke(endGameScreen, null);
                bool advanced = result is bool && (bool)result;
                if (advanced)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Info, "HsBattle advanced end game screen.");
                }

                return advanced;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle could not advance end game screen: " + ex.Message);
                return false;
            }
        }

        private bool IsEndGameDone(EndGameScreen endGameScreen)
        {
            return InvokeBoolReflection(endGameScreen, EndGameIsDoneDisplayingRewardsMethod);
        }

        private bool IsEndGameInputBlocked(EndGameScreen endGameScreen)
        {
            return InvokeBoolReflection(endGameScreen, EndGameIsInputBlockedMethod);
        }

        private bool IsEndGameBlockingAnimationPlaying(EndGameScreen endGameScreen)
        {
            return InvokeBoolReflection(endGameScreen, EndGameIsPlayingBlockingAnimMethod);
        }

        private bool ShouldForceCloseEndGameScreen()
        {
            return _gameEndedAt > 0f && Time.unscaledTime - _gameEndedAt >= EndGameForceCloseSeconds;
        }

        private void ReturnEndGameScreenToHub(EndGameScreen endGameScreen)
        {
            if (endGameScreen == null || EndGameBackToModeMethod == null)
            {
                return;
            }

            try
            {
                EndGameBackToModeMethod.Invoke(endGameScreen, new object[] { SceneMgr.Mode.HUB });
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, "HsBattle returned to hub from end game screen.");
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle could not return to hub from end game screen: " + ex.Message);
            }
        }

        public void RequestQueueNow()
        {
            ArmInitialQueueSetup();
            _forceQueueRequested = true;
            _nextQueueAttemptAt = 0f;
            UIStatus.Get()?.AddInfo("HsBattle queue requested", 2f);
        }

        public string GetOverlayStatusText()
        {
            SceneMgr sceneMgr = SceneMgr.Get();
            GameMgr gameMgr = GameMgr.Get();
            GameState gameState = GameState.Get();

            return string.Format(
                "\u573a\u666f:{0}  \u5339\u914d:{1}  \u5bf9\u6218:{2}",
                DescribeScene(sceneMgr),
                DescribeQueueStatus(gameMgr),
                DescribeBattleStatus(gameMgr, gameState));
        }

        public bool ShouldAutoSkipPostGameUi()
        {
            return PluginConfig.AutoQueueEnabledValue || _forceQueueRequested;
        }

        private void TryQueue()
        {
            if ((!PluginConfig.AutoQueueEnabledValue && !_forceQueueRequested) || Time.unscaledTime < _nextQueueAttemptAt)
            {
                return;
            }

            SceneMgr sceneMgr = SceneMgr.Get();
            GameMgr gameMgr = GameMgr.Get();
            if (sceneMgr == null || gameMgr == null)
            {
                return;
            }

            GameState gameState = GameState.Get();
            bool canExitFinishedGame = _queueReturnToHubPending
                && sceneMgr.IsInGame()
                && gameState != null
                && gameState.IsGameCreated()
                && gameState.IsGameOver();

            if (sceneMgr.IsTransitioning() || gameMgr.IsFindingGame() || gameMgr.IsSpectator() || (sceneMgr.IsInGame() && !canExitFinishedGame))
            {
                return;
            }

            long deckId;
            if (!TryPrepareQueueScene(sceneMgr, out deckId))
            {
                return;
            }

            if (!TryStartMatchmaking(deckId))
            {
                AbortQueueing("无法启动匹配，已停止匹配。");
                return;
            }

            _forceQueueRequested = false;
            _queueReturnToHubPending = false;
            _nextQueueAttemptAt = Time.unscaledTime + PluginConfig.QueueRetrySecondsValue;
        }

        private void TrackQueueConfiguration()
        {
            bool autoQueueEnabled = PluginConfig.AutoQueueEnabledValue;
            QueueMode queueMode = PluginConfig.queueMode != null ? PluginConfig.queueMode.Value : QueueMode.Standard;
            long queueDeckId = GetConfiguredQueueDeckId();

            if (autoQueueEnabled && !_lastAutoQueueEnabled)
            {
                ArmInitialQueueSetup();
            }
            else if (autoQueueEnabled && (queueMode != _lastQueueMode || queueDeckId != _lastQueueDeckId))
            {
                ArmInitialQueueSetup();
            }
            else if (!autoQueueEnabled && _lastAutoQueueEnabled)
            {
                ResetQueueNavigation();
            }

            _lastAutoQueueEnabled = autoQueueEnabled;
            _lastQueueMode = queueMode;
            _lastQueueDeckId = queueDeckId;
        }

        private void ArmInitialQueueSetup()
        {
            _queueReturnToHubPending = true;
            _deckSelectionWaitStartedAt = 0f;
            _modeSwitchWaitStartedAt = 0f;
            _modeSwitchSettledAt = 0f;
            _hubSettleWaitStartedAt = 0f;
            _deckReadySinceAt = 0f;
            _nextNavigationAttemptAt = 0f;
            _nextQueueAttemptAt = 0f;
        }

        private void ResetQueueNavigation()
        {
            _queueReturnToHubPending = false;
            _deckSelectionWaitStartedAt = 0f;
            _modeSwitchWaitStartedAt = 0f;
            _modeSwitchSettledAt = 0f;
            _hubSettleWaitStartedAt = 0f;
            _deckReadySinceAt = 0f;
            _nextNavigationAttemptAt = 0f;
        }

        private bool TryPrepareQueueScene(SceneMgr sceneMgr, out long deckId)
        {
            deckId = 0L;

            if (sceneMgr == null)
            {
                return false;
            }

            SceneMgr.Mode currentMode = sceneMgr.GetMode();
            if (currentMode != SceneMgr.Mode.HUB)
            {
                _hubSettleWaitStartedAt = 0f;
            }

            if (_queueReturnToHubPending)
            {
                if (currentMode == SceneMgr.Mode.HUB)
                {
                    if (!WaitForHubSceneToSettle())
                    {
                        return false;
                    }

                    _queueReturnToHubPending = false;
                    _nextNavigationAttemptAt = 0f;
                }
                else
                {
                    RequestSceneTransition(sceneMgr, SceneMgr.Mode.HUB, "HsBattle returning to hub before matchmaking.");
                    return false;
                }
            }

            if (currentMode == SceneMgr.Mode.HUB)
            {
                _modeSwitchWaitStartedAt = 0f;
                _modeSwitchSettledAt = 0f;
                _deckReadySinceAt = 0f;
                _deckSelectionWaitStartedAt = 0f;
                RequestSceneTransition(sceneMgr, SceneMgr.Mode.TOURNAMENT, "HsBattle opening tournament mode.");
                return false;
            }

            if (currentMode != SceneMgr.Mode.TOURNAMENT)
            {
                RequestSceneTransition(sceneMgr, SceneMgr.Mode.HUB, "HsBattle returning to hub to recover matchmaking flow.");
                return false;
            }

            if (!TryEnsureTournamentMode())
            {
                _deckSelectionWaitStartedAt = 0f;
                return false;
            }

            return TryEnsureQueueDeckReady(out deckId);
        }

        private bool TryEnsureQueueDeckReady(out long deckId)
        {
            deckId = 0L;

            DeckPickerTrayDisplay trayDisplay = DeckPickerTrayDisplay.Get();
            if (trayDisplay == null)
            {
                _deckReadySinceAt = 0f;
                return WaitForDeckSelectionOrAbort("卡组界面加载超时，已停止匹配。");
            }

            string missingDeckMessage;
            CollectionDeckBoxVisual targetDeckbox = TryGetTargetDeckbox(trayDisplay, out missingDeckMessage);
            if (targetDeckbox == null)
            {
                _deckReadySinceAt = 0f;
                return WaitForDeckSelectionOrAbort(missingDeckMessage);
            }

            long targetDeckId = targetDeckbox.GetDeckID();
            if (targetDeckId <= 0L)
            {
                AbortQueueing("目标卡组无效，已停止匹配。");
                return false;
            }

            long activeDeckId = TryResolveActiveTrayDeckId(trayDisplay);
            if (activeDeckId == targetDeckId)
            {
                if (!WaitForDeckSelectionSettle())
                {
                    return false;
                }

                _deckSelectionWaitStartedAt = 0f;
                deckId = activeDeckId;
                return true;
            }

            _deckReadySinceAt = 0f;

            BeginDeckSelectionWait();
            if (Time.unscaledTime - _deckSelectionWaitStartedAt >= 5f)
            {
                AbortQueueing("等待卡组选择完成超时，已停止匹配。");
                return false;
            }

            if (!targetDeckbox.IsDeckEnabled() || !targetDeckbox.CanSelectDeck() || !targetDeckbox.IsDeckPlayable())
            {
                AbortQueueing(GetConfiguredQueueDeckId() > 0L
                    ? "所选卡组当前不可选，已停止匹配。"
                    : "第一个卡组不可选，已停止匹配。");
                return false;
            }

            if (!TrySelectDeck(trayDisplay, targetDeckbox))
            {
                AbortQueueing(GetConfiguredQueueDeckId() > 0L
                    ? "无法自动选择所选卡组，已停止匹配。"
                    : "无法自动选择第一个卡组，已停止匹配。");
                return false;
            }

            UIStatus.Get()?.AddInfo(GetConfiguredQueueDeckId() > 0L ? "已自动选择所选卡组" : "已自动选择第一个卡组", 2f);
            BeginDeckSelectionWait();
            _nextQueueAttemptAt = Time.unscaledTime + 1f;
            return false;
        }

        private bool TryEnsureTournamentMode()
        {
            if (Time.unscaledTime < _nextNavigationAttemptAt)
            {
                return false;
            }

            DeckPickerTrayDisplay trayDisplay = DeckPickerTrayDisplay.Get();
            if (trayDisplay == null)
            {
                return WaitForModeSwitchOrAbort("对战界面加载超时，已停止匹配。");
            }

            VisualsFormatType desiredMode = GetDesiredVisualsFormatType();
            if (GetCurrentVisualsFormatType() == desiredMode)
            {
                if (!WaitForModeSwitchToSettle())
                {
                    _deckReadySinceAt = 0f;
                    return false;
                }

                return true;
            }

            try
            {
                trayDisplay.SwitchFormatTypeAndRankedPlayMode(desiredMode);
                BeginModeSwitchWait();
                _modeSwitchSettledAt = 0f;
                _deckReadySinceAt = 0f;
                _deckSelectionWaitStartedAt = 0f;
                _nextNavigationAttemptAt = Time.unscaledTime + 1f;
                _nextQueueAttemptAt = Time.unscaledTime + 1f;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, string.Format(
                    "HsBattle switching tournament mode to {0}.",
                    PluginConfig.DescribeQueueMode(GetSelectedQueueMode())));
                return false;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle could not switch tournament mode: " + ex.Message);
                AbortQueueing("无法切换到目标模式，已停止匹配。");
                return false;
            }
        }

        private void RequestSceneTransition(SceneMgr sceneMgr, SceneMgr.Mode targetMode, string logMessage)
        {
            if (sceneMgr == null || Time.unscaledTime < _nextNavigationAttemptAt)
            {
                return;
            }

            if (!sceneMgr.IsModeRequested(targetMode))
            {
                sceneMgr.SetNextMode(
                    targetMode,
                    SceneMgr.TransitionHandlerType.SCENEMGR,
                    null,
                    null,
                    false);

                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, logMessage);
            }

            _nextNavigationAttemptAt = Time.unscaledTime + 1f;
        }

        private bool TryStartMatchmaking(long deckId)
        {
            try
            {
                DeckPickerTrayDisplay trayDisplay = DeckPickerTrayDisplay.Get();
                if (trayDisplay == null)
                {
                    return false;
                }

                if (PlayGameMethod == null)
                {
                    return false;
                }

                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, string.Format(
                    "HsBattle queueing {0} with deck {1}.",
                    PluginConfig.DescribeQueueMode(GetSelectedQueueMode()),
                    deckId));

                PlayGameMethod.Invoke(trayDisplay, null);
                return true;
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle could not start matchmaking: " + ex.Message);
                return false;
            }
        }

        private void AbortQueueing(string message)
        {
            _forceQueueRequested = false;
            _queueReturnToHubPending = false;
            _deckSelectionWaitStartedAt = 0f;
            _modeSwitchWaitStartedAt = 0f;
            _modeSwitchSettledAt = 0f;
            _hubSettleWaitStartedAt = 0f;
            _deckReadySinceAt = 0f;
            _nextNavigationAttemptAt = 0f;
            _nextQueueAttemptAt = Time.unscaledTime + 2f;

            if (PluginConfig.autoQueueEnabled != null && PluginConfig.autoQueueEnabled.Value)
            {
                PluginConfig.autoQueueEnabled.Value = false;
            }

            UIStatus.Get()?.AddInfo(message, 3f);
            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "[HsBattle] " + message);
        }

        private void BeginDeckSelectionWait()
        {
            if (_deckSelectionWaitStartedAt <= 0f)
            {
                _deckSelectionWaitStartedAt = Time.unscaledTime;
            }
        }

        private bool WaitForDeckSelectionOrAbort(string message)
        {
            BeginDeckSelectionWait();

            if (Time.unscaledTime - _deckSelectionWaitStartedAt >= 5f)
            {
                AbortQueueing(message);
            }

            return false;
        }

        private bool WaitForDeckSelectionSettle()
        {
            BeginDeckSelectionWait();

            if (_deckReadySinceAt <= 0f)
            {
                _deckReadySinceAt = Time.unscaledTime;
                _nextQueueAttemptAt = Time.unscaledTime + 0.35f;
                return false;
            }

            if (Time.unscaledTime - _deckReadySinceAt < DeckSelectionSettleSeconds)
            {
                _nextQueueAttemptAt = Time.unscaledTime + 0.25f;
                return false;
            }

            _deckReadySinceAt = 0f;
            return true;
        }

        private void BeginModeSwitchWait()
        {
            if (_modeSwitchWaitStartedAt <= 0f)
            {
                _modeSwitchWaitStartedAt = Time.unscaledTime;
            }
        }

        private bool WaitForModeSwitchOrAbort(string message)
        {
            BeginModeSwitchWait();

            if (Time.unscaledTime - _modeSwitchWaitStartedAt >= 5f)
            {
                AbortQueueing(message);
            }

            return false;
        }

        private bool WaitForModeSwitchToSettle()
        {
            if (_modeSwitchWaitStartedAt <= 0f)
            {
                return true;
            }

            if (_modeSwitchSettledAt <= 0f)
            {
                _modeSwitchSettledAt = Time.unscaledTime;
                _nextQueueAttemptAt = Time.unscaledTime + 0.5f;
                return false;
            }

            if (Time.unscaledTime - _modeSwitchSettledAt < ModeSwitchSettleSeconds)
            {
                if (Time.unscaledTime - _modeSwitchWaitStartedAt >= 5f)
                {
                    AbortQueueing("切换模式后卡组界面未及时稳定，已停止匹配。");
                    return false;
                }

                _nextQueueAttemptAt = Time.unscaledTime + 0.25f;
                return false;
            }

            _modeSwitchWaitStartedAt = 0f;
            _modeSwitchSettledAt = 0f;
            return true;
        }

        private bool WaitForHubSceneToSettle()
        {
            if (_hubSettleWaitStartedAt <= 0f)
            {
                _hubSettleWaitStartedAt = Time.unscaledTime;
                _nextQueueAttemptAt = Time.unscaledTime + 0.5f;
                return false;
            }

            if (Time.unscaledTime - _hubSettleWaitStartedAt < HubSceneSettleSeconds)
            {
                _nextQueueAttemptAt = Time.unscaledTime + 0.25f;
                return false;
            }

            _hubSettleWaitStartedAt = 0f;
            return true;
        }

        private void TryHandleMulligan()
        {
            if (Time.unscaledTime < _nextActionAt)
            {
                return;
            }

            GameState gameState = GameState.Get();
            if (gameState == null || !gameState.IsMulliganManagerActive() || GameMgr.Get()?.IsSpectator() == true)
            {
                ResetMulliganProgress();
                return;
            }

            MulliganManager mulliganManager = MulliganManager.Get();
            if (mulliganManager == null || !mulliganManager.IsMulliganActive())
            {
                ResetMulliganProgress();
                return;
            }

            if (mulliganManager.IsMulliganIntroActive() || ShouldWaitForMulliganCardsToBeProcessed(mulliganManager))
            {
                ResetMulliganProgress();
                return;
            }

            if (!IsMulliganWaitingForUserInput(mulliganManager))
            {
                ResetMulliganProgress();
                return;
            }

            if (_mulliganReadySinceAt <= 0f)
            {
                _mulliganReadySinceAt = Time.unscaledTime;
                _mulliganHoldIssuedAt = 0f;
                _mulliganConfirmReadySinceAt = 0f;
                return;
            }

            if (Time.unscaledTime - _mulliganReadySinceAt < Mathf.Max(MulliganReadySettleSeconds, PluginConfig.ActionDelayMaxSeconds))
            {
                return;
            }

            if (_mulliganHoldIssuedAt <= 0f)
            {
                bool handledExperimentalMulligan = TryHandleExperimentalMulligan(gameState, mulliganManager);
                if (!handledExperimentalMulligan)
                {
                    mulliganManager.SetAllMulliganCardsToHold();
                    LogDecision("auto mulligan keep all");
                }

                _mulliganHoldIssuedAt = Time.unscaledTime;
                _mulliganConfirmReadySinceAt = 0f;
                return;
            }

            if (!gameState.GetBooleanGameOption(GameEntityOption.MULLIGAN_REQUIRES_CONFIRMATION))
            {
                if (Time.unscaledTime - _mulliganHoldIssuedAt < MulliganConfirmDelaySeconds)
                {
                    return;
                }

                mulliganManager.AutomaticContinueMulligan(false);
                ResetMulliganProgress();
                _postMulliganActionBlockedUntil = Time.unscaledTime + Mathf.Max(PostMulliganSettleSeconds, PluginConfig.RollActionDelaySeconds());
                _nextActionAt = _postMulliganActionBlockedUntil;
                return;
            }

            if (!IsMulliganConfirmButtonReady(mulliganManager))
            {
                _mulliganConfirmReadySinceAt = 0f;
                return;
            }

            if (_mulliganConfirmReadySinceAt <= 0f)
            {
                _mulliganConfirmReadySinceAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _mulliganConfirmReadySinceAt < MulliganConfirmDelaySeconds)
            {
                return;
            }

            if (!TryConfirmMulligan(mulliganManager))
            {
                _mulliganConfirmReadySinceAt = 0f;
                return;
            }

            ResetMulliganProgress();
            _postMulliganActionBlockedUntil = Time.unscaledTime + Mathf.Max(PostMulliganSettleSeconds, PluginConfig.RollActionDelaySeconds());
            _nextActionAt = _postMulliganActionBlockedUntil;
        }

        private bool TryHandleExperimentalMulligan(GameState gameState, MulliganManager mulliganManager)
        {
            if (gameState == null || mulliganManager == null)
            {
                return false;
            }

            if (PluginConfig.StrategyModeValue == StrategyMode.HbFrameworkExperimental)
            {
                try
                {
                    HbMulliganSnapshot snapshot = _hbSnapshotAdapter.CreateMulliganSnapshot(mulliganManager);
                    HbMulliganDecisionResult mulliganDecision = _hbMulliganDecisionService.Decide(snapshot);
                    if (mulliganDecision == null || mulliganDecision.ShouldFallbackToKeepAll)
                    {
                        string reason = mulliganDecision != null && !string.IsNullOrEmpty(mulliganDecision.Reason)
                            ? ": " + mulliganDecision.Reason
                            : string.Empty;
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HB experimental mulligan fell back to keep-all" + reason);
                        return false;
                    }

                    if (!TryApplyExperimentalMulliganDecision(mulliganManager, mulliganDecision))
                    {
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HB experimental mulligan fell back to keep-all: could not apply mulligan selection.");
                        return false;
                    }

                    LogDecision(string.Format(
                        "HB experimental mulligan keep {0} replace {1}",
                        mulliganDecision.KeepIndices.Count,
                        mulliganDecision.ReplaceIndices.Count));
                    return true;
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HB experimental mulligan fell back to keep-all: " + ex.Message);
                }
            }

            return false;
        }

        private static bool TryApplyExperimentalMulliganDecision(MulliganManager mulliganManager, HbMulliganDecisionResult mulliganDecision)
        {
            if (mulliganManager == null || mulliganDecision == null)
            {
                return false;
            }

            List<Card> startingCards = mulliganManager.GetStartingCards();
            if (startingCards == null)
            {
                return false;
            }

            if (mulliganDecision.KeepIndices == null)
            {
                return false;
            }

            HashSet<int> keepIndices = new HashSet<int>();
            for (int i = 0; i < mulliganDecision.KeepIndices.Count; i++)
            {
                int keepIndex = mulliganDecision.KeepIndices[i];
                if (keepIndex < 0 || keepIndex >= startingCards.Count || !keepIndices.Add(keepIndex))
                {
                    return false;
                }
            }

            // KeepIndices is the authoritative source in experimental flow.
            // ReplaceIndices is treated as redundant/derived data.
            List<int> replaceIndices = new List<int>();
            for (int index = 0; index < startingCards.Count; index++)
            {
                if (!keepIndices.Contains(index))
                {
                    replaceIndices.Add(index);
                }
            }

            mulliganManager.SetAllMulliganCardsToHold();
            if (replaceIndices.Count == 0)
            {
                return true;
            }

            HashSet<int> seenReplaceIndices = new HashSet<int>();
            for (int i = 0; i < replaceIndices.Count; i++)
            {
                int replaceIndex = replaceIndices[i];
                if (replaceIndex < 0 || replaceIndex >= startingCards.Count || !seenReplaceIndices.Add(replaceIndex))
                {
                    return false;
                }

                Card startingCard = startingCards[replaceIndex];
                if (startingCard == null)
                {
                    return false;
                }

                mulliganManager.ToggleHoldState(startingCard);
            }

            return true;
        }

        private void TryHandleBattle()
        {
            if (!PluginConfig.AutoBattleEnabledValue || Time.unscaledTime < _nextActionAt)
            {
                return;
            }

            GameMgr gameMgr = GameMgr.Get();
            GameState gameState = GameState.Get();
            if (gameMgr == null || gameState == null)
            {
                ResetChoiceProgress();
                return;
            }

            MulliganManager mulliganManager = MulliganManager.Get();
            if (mulliganManager != null && (mulliganManager.IsMulliganActive() || mulliganManager.IsMulliganIntroActive()))
            {
                ResetChoiceProgress();
                return;
            }

            if (Time.unscaledTime < _postMulliganActionBlockedUntil)
            {
                return;
            }

            if (gameMgr.IsSpectator() || gameMgr.IsBattlegrounds() || gameMgr.IsMercenaries())
            {
                ResetChoiceProgress();
                return;
            }

            if (!gameState.IsGameCreated() || gameState.IsGameOver() || gameState.IsMulliganManagerActive())
            {
                ResetChoiceProgress();
                return;
            }

            if (gameState.IsInChoiceMode())
            {
                TryHandleChoice(gameState);
                return;
            }

            ResetChoiceProgress();

            if (gameState.IsBusy())
            {
                return;
            }

            if (!gameState.IsFriendlySidePlayerTurn())
            {
                return;
            }

            Network.Options options = gameState.GetOptionsPacket() ?? gameState.GetLastOptions();
            if (options == null || options.List == null || options.List.Count == 0)
            {
                return;
            }

            StrategyActionPlan action = ResolveStrategyActionPlan(gameState, options);
            if (action == null)
            {
                return;
            }

            ExecuteStrategyAction(gameState, action);
        }

        private StrategyContext BuildStrategyContext(GameState gameState, Network.Options options)
        {
            return new StrategyContext(gameState, options, PluginConfig.StrategyModeValue);
        }

        private StrategyActionPlan ResolveStrategyActionPlan(GameState gameState, Network.Options options)
        {
            StrategyContext context = BuildStrategyContext(gameState, options);
            // Legacy regression marker: PluginConfig.StrategyModeValue == StrategyMode.HbFrameworkExperimental

            if (context.StrategyMode == StrategyMode.HbFrameworkExperimental)
            {
                try
                {
                    StrategyEngineResult hbResult = _hbStrategyEngine.TryBuildPlan(this, context);
                    if (hbResult != null && hbResult.Status == StrategyEngineStatus.Success && hbResult.Plan != null)
                    {
                        return hbResult.Plan;
                    }

                    string fallbackReason = hbResult != null && !string.IsNullOrEmpty(hbResult.Reason)
                        ? hbResult.Reason
                        : "HB strategy engine returned no plan.";
                    LogHbStrategyFallbackOnce(BepInEx.Logging.LogLevel.Info, fallbackReason);
                }
                catch (Exception ex)
                {
                    LogHbStrategyFallbackOnce(BepInEx.Logging.LogLevel.Warning, ex.Message);
                }
            }

            StrategyEngineResult legacyResult = ResolveLegacyStrategyEngineResult(context);
            return legacyResult != null && legacyResult.Status == StrategyEngineStatus.Success
                ? legacyResult.Plan
                : null;
        }

        private void LogHbStrategyFallbackOnce(BepInEx.Logging.LogLevel level, string reason)
        {
            if (_hbStrategyFallbackLogged)
            {
                return;
            }

            _hbStrategyFallbackLogged = true;
            Utils.MyLogger(level, "HB strategy engine fell back to legacy: " + reason);
        }

        private StrategyEngineResult ResolveLegacyStrategyEngineResult(StrategyContext context)
        {
            return _legacyStrategyEngine.TryBuildPlan(this, context);
        }

        private bool TryHandleChoice(GameState gameState)
        {
            if (gameState == null || !gameState.IsInChoiceMode())
            {
                ResetChoiceProgress();
                return false;
            }

            Network.EntityChoices choices = gameState.GetFriendlyEntityChoices();
            if (choices == null || choices.Entities == null || choices.Entities.Count == 0)
            {
                ResetChoiceProgress();
                return false;
            }

            int choiceSignature = ComputeChoiceSignature(choices);
            if (_choiceSignature != choiceSignature)
            {
                _choiceSignature = choiceSignature;
                _choiceReadySinceAt = Time.unscaledTime;
                return false;
            }

            if (_choiceReadySinceAt <= 0f)
            {
                _choiceReadySinceAt = Time.unscaledTime;
                return false;
            }

            float choiceAge = Time.unscaledTime - _choiceReadySinceAt;
            if (choiceAge < ChoiceSettleSeconds)
            {
                return false;
            }

            if (ShouldWaitForChoiceUi(gameState, choiceAge))
            {
                return false;
            }

            HashSet<int> blockedEntities = choices.UnchoosableEntities != null
                ? new HashSet<int>(choices.UnchoosableEntities)
                : null;

            List<Entity> rankedChoices = new List<Entity>();
            int requiredCount = Mathf.Max(1, choices.CountMin);
            int maxCount = choices.CountMax > 0 ? choices.CountMax : requiredCount;

            for (int index = 0; index < choices.Entities.Count; index++)
            {
                int entityId = choices.Entities[index];
                if (entityId <= 0 || (blockedEntities != null && blockedEntities.Contains(entityId)))
                {
                    continue;
                }

                Entity entity = gameState.GetEntity(entityId);
                if (entity != null)
                {
                    rankedChoices.Add(entity);
                }
            }

            if (rankedChoices.Count == 0)
            {
                ResetChoiceProgress();
                return false;
            }

            int selectCount = Mathf.Clamp(requiredCount, 1, Mathf.Min(maxCount, rankedChoices.Count));
            rankedChoices.Sort(delegate (Entity left, Entity right) { return EvaluateChoiceEntity(right).CompareTo(EvaluateChoiceEntity(left)); });
            ClearChosenEntities(gameState);

            List<string> selectedNames = new List<string>();
            for (int i = 0; i < selectCount; i++)
            {
                Entity entity = rankedChoices[i];
                if (entity == null || !gameState.AddChosenEntity(entity))
                {
                    continue;
                }

                selectedNames.Add(entity.GetName());
            }

            if (selectedNames.Count < requiredCount)
            {
                ClearChosenEntities(gameState);
                ResetChoiceProgress();
                return false;
            }

            gameState.SendChoices();
            TryHideChoiceUi();
            LogDecision("choice(ranked): " + string.Join(", ", selectedNames.ToArray()));
            ResetChoiceProgress();
            ScheduleNextBattleAction();
            return true;
        }

        private void ResetChoiceProgress()
        {
            _choiceReadySinceAt = 0f;
            _choiceSignature = 0;
        }

        private bool ShouldWaitForChoiceUi(GameState gameState, float choiceAge)
        {
            if (gameState == null)
            {
                return true;
            }

            if (choiceAge >= ChoiceForceSelectSeconds)
            {
                return false;
            }

            if (gameState.MustWaitForChoices())
            {
                return true;
            }

            ChoiceCardMgr choiceCardMgr = ChoiceCardMgr.Get();
            if (choiceCardMgr == null)
            {
                return false;
            }

            if (choiceCardMgr.IsFriendlyWaitingToStartChoices() || choiceCardMgr.IsWaitingToShowSubOptions())
            {
                return true;
            }

            if (!choiceCardMgr.IsFriendlyShown() && !choiceCardMgr.HasFriendlyChoices())
            {
                return true;
            }

            return false;
        }

        private int ComputeChoiceSignature(Network.EntityChoices choices)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + choices.CountMin;
                hash = hash * 31 + choices.CountMax;

                if (choices.Entities != null)
                {
                    List<int> entityIds = new List<int>(choices.Entities);
                    entityIds.Sort();
                    for (int i = 0; i < entityIds.Count; i++)
                    {
                        hash = hash * 31 + entityIds[i];
                    }
                }

                if (choices.UnchoosableEntities != null)
                {
                    List<int> blockedIds = new List<int>(choices.UnchoosableEntities);
                    blockedIds.Sort();
                    for (int i = 0; i < blockedIds.Count; i++)
                    {
                        hash = hash * 31 + blockedIds[i];
                    }
                }

                return hash;
            }
        }

        private void ShuffleEntities(List<Entity> entities)
        {
            if (entities == null || entities.Count <= 1)
            {
                return;
            }

            for (int i = entities.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                Entity current = entities[i];
                entities[i] = entities[swapIndex];
                entities[swapIndex] = current;
            }
        }

        private bool ShouldWaitForMulliganCardsToBeProcessed(MulliganManager mulliganManager)
        {
            if (mulliganManager == null)
            {
                return false;
            }

            try
            {
                if (ShouldWaitForMulliganCardsMethod == null)
                {
                    return false;
                }

                object value = ShouldWaitForMulliganCardsMethod.Invoke(mulliganManager, null);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private bool IsMulliganWaitingForUserInput(MulliganManager mulliganManager)
        {
            if (mulliganManager == null)
            {
                return false;
            }

            try
            {
                if (WaitingForUserInputField == null)
                {
                    return false;
                }

                object value = WaitingForUserInputField.GetValue(mulliganManager);
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private bool IsMulliganConfirmButtonReady(MulliganManager mulliganManager)
        {
            if (mulliganManager == null || !IsMulliganWaitingForUserInput(mulliganManager))
            {
                return false;
            }

            GameObject buttonObject = mulliganManager.GetMulliganButton();
            if (buttonObject == null || !buttonObject.activeInHierarchy)
            {
                return false;
            }

            NormalButton button = buttonObject.GetComponent<NormalButton>();
            if (button == null || !button.IsEnabled())
            {
                return false;
            }

            Collider collider = buttonObject.GetComponent<Collider>();
            return collider == null || collider.enabled;
        }

        private bool TryConfirmMulligan(MulliganManager mulliganManager)
        {
            if (mulliganManager == null)
            {
                return false;
            }

            GameObject buttonObject = mulliganManager.GetMulliganButton();
            if (buttonObject == null || !buttonObject.activeInHierarchy)
            {
                return false;
            }

            NormalButton button = buttonObject.GetComponent<NormalButton>();
            if (button == null || !button.IsEnabled())
            {
                return false;
            }

            button.TriggerPress();
            button.TriggerRelease();
            LogDecision("auto mulligan confirm");
            return true;
        }

        private void ClearChosenEntities(GameState gameState)
        {
            if (gameState == null)
            {
                return;
            }

            List<Entity> chosenEntities = gameState.GetChosenEntities();
            if (chosenEntities == null || chosenEntities.Count == 0)
            {
                return;
            }

            foreach (Entity entity in chosenEntities.ToList())
            {
                if (entity != null)
                {
                    gameState.RemoveChosenEntity(entity);
                }
            }
        }

        private void ResetMulliganProgress()
        {
            _mulliganReadySinceAt = 0f;
            _mulliganHoldIssuedAt = 0f;
            _mulliganConfirmReadySinceAt = 0f;
        }

        private ActionPlan BuildActionPlan(GameState gameState, Network.Options options)
        {
            List<ActionPlan> plans = new List<ActionPlan>();

            for (int optionIndex = 0; optionIndex < options.List.Count; optionIndex++)
            {
                Network.Options.Option option = options.List[optionIndex];
                if (option == null || option.Main == null)
                {
                    continue;
                }

                ActionPlan plan = BuildActionPlanForOption(gameState, optionIndex, option);
                if (plan != null)
                {
                    plans.Add(plan);
                }
            }

            if (plans.Count == 0)
            {
                return null;
            }

            plans.Sort(delegate (ActionPlan left, ActionPlan right) { return right.Score.CompareTo(left.Score); });
            return plans[0];
        }

        internal StrategyActionPlan BuildLegacyStrategyActionPlan(GameState gameState, Network.Options options)
        {
            ActionPlan action = BuildActionPlan(gameState, options);
            return action != null ? ToStrategyActionPlan(action) : null;
        }

        private ActionPlan BuildActionPlanForOption(GameState gameState, int optionIndex, Network.Options.Option option)
        {
            Entity mainEntity = gameState.GetEntity(option.Main.ID);
            ActionKind kind = DetermineActionKind(gameState, option, mainEntity);

            if (kind != ActionKind.EndTurn && kind != ActionKind.Pass)
            {
                if (!IsPlayableSubOption(option.Main))
                {
                    return null;
                }
            }

            int subOptionIndex = -1;
            Entity actionEntity = mainEntity;
            List<Network.Options.Option.TargetOption> targets = option.Main.Targets;

            if (option.Subs != null && option.Subs.Count > 0)
            {
                subOptionIndex = SelectSubOption(gameState, option, kind);
                if (subOptionIndex < 0)
                {
                    return null;
                }

                Network.Options.Option.SubOption subOption = option.Subs[subOptionIndex];
                actionEntity = gameState.GetEntity(subOption.ID) ?? mainEntity;
                targets = subOption.Targets;
            }

            bool requiresPosition = actionEntity != null && RequiresPosition(actionEntity);
            int position = requiresPosition ? ResolvePosition() : 0;
            if (requiresPosition && position < 0)
            {
                return null;
            }

            int targetId = ResolveTargetId(gameState, actionEntity ?? mainEntity, targets, kind);
            if (targets != null && targets.Count > 0 && targetId <= 0)
            {
                return null;
            }

            return new ActionPlan
            {
                OptionIndex = optionIndex,
                SubOptionIndex = subOptionIndex,
                TargetId = targetId,
                Position = position,
                Score = ScoreAction(gameState, actionEntity ?? mainEntity, kind, targetId),
                Description = DescribeAction(actionEntity ?? mainEntity, kind, targetId, position),
                Kind = kind
            };
        }

        private int SelectSubOption(GameState gameState, Network.Options.Option option, ActionKind kind)
        {
            int bestIndex = -1;
            int bestScore = int.MinValue;

            for (int index = 0; index < option.Subs.Count; index++)
            {
                Network.Options.Option.SubOption subOption = option.Subs[index];
                if (subOption == null)
                {
                    continue;
                }

                if (!IsPlayableSubOption(subOption))
                {
                    continue;
                }

                Entity subEntity = gameState.GetEntity(subOption.ID);
                int targetId = ResolveTargetId(gameState, subEntity, subOption.Targets, kind);
                if (HasUsableTargets(subOption.Targets) && targetId <= 0)
                {
                    continue;
                }

                int score = ScoreAction(gameState, subEntity, kind, targetId);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private ActionKind DetermineActionKind(GameState gameState, Network.Options.Option option, Entity entity)
        {
            if (option.Type == Network.Options.Option.OptionType.END_TURN)
            {
                return ActionKind.EndTurn;
            }

            if (option.Type == Network.Options.Option.OptionType.PASS)
            {
                return ActionKind.Pass;
            }

            if (entity == null)
            {
                return ActionKind.Other;
            }

            if (gameState.IsInChoiceMode() || entity.GetZone() == TAG_ZONE.SETASIDE)
            {
                return ActionKind.Choice;
            }

            if (entity.IsHeroPower())
            {
                return ActionKind.HeroPower;
            }

            if (entity.GetZone() == TAG_ZONE.HAND)
            {
                return ActionKind.PlayCard;
            }

            if (entity.GetZone() == TAG_ZONE.PLAY && (entity.IsCharacter() || entity.IsWeapon()))
            {
                return ActionKind.Attack;
            }

            return ActionKind.Other;
        }

        private int ScoreAction(GameState gameState, Entity entity, ActionKind kind, int targetId)
        {
            if (kind == ActionKind.Choice)
            {
                return 10000 + EvaluateEntity(entity);
            }

            if (kind == ActionKind.PlayCard)
            {
                int cost = entity != null ? entity.GetRealTimeCost() : 0;
                int minionBonus = entity != null && entity.IsMinion() ? 40 : 0;
                int spellBonus = entity != null && entity.IsSpell() ? 20 : 0;
                return 500 + cost * 22 + minionBonus + spellBonus;
            }

            if (kind == ActionKind.HeroPower)
            {
                return 350 + (targetId > 0 ? 10 : 0);
            }

            if (kind == ActionKind.Attack)
            {
                // Skip frozen attackers
                if (entity != null && entity.IsFrozen())
                {
                    return -1000;
                }

                int attack = entity != null ? entity.GetRealTimeAttack() : 0;
                Entity target = targetId > 0 ? gameState.GetEntity(targetId) : null;

                // Lethal detection: if this attack kills the enemy hero
                if (target != null && target.IsHero() && attack > 0)
                {
                    int heroHealth = target.GetCurrentHealth();
                    if (heroHealth > 0 && attack >= heroHealth)
                    {
                        return 100000;
                    }

                    if (heroHealth <= 10)
                    {
                        return 400 + attack * 8 + (11 - heroHealth) * 5;
                    }
                }

                int faceBonus = target != null && target.IsHero() ? 40 : 0;
                int minionBonus = 0;
                if (target != null && target.IsMinion())
                {
                    int targetHealth = target.GetCurrentHealth();
                    int targetAttack = target.GetRealTimeAttack();
                    if (targetHealth > 0 && attack >= targetHealth)
                    {
                        minionBonus += 80;
                    }

                    minionBonus += targetAttack * 6;
                }

                return 250 + faceBonus + minionBonus + attack;
            }

            if (kind == ActionKind.Pass)
            {
                return 20;
            }

            if (kind == ActionKind.EndTurn)
            {
                return 10;
            }

            return 100;
        }

        private int EvaluateEntity(Entity entity)
        {
            if (entity == null)
            {
                return 0;
            }

            return entity.GetRealTimeCost() * 10 + entity.GetRealTimeAttack() + entity.GetCurrentHealth();
        }

        private int EvaluateChoiceEntity(Entity entity)
        {
            if (entity == null)
            {
                return int.MinValue;
            }

            int score = EvaluateEntity(entity);
            if (entity.IsMinion())
            {
                score += 30;
                if (entity.HasTaunt())
                {
                    score += 20;
                }

                if (entity.HasDivineShield())
                {
                    score += 15;
                }
            }

            if (entity.IsSpell())
            {
                score += 10;
            }

            return score;
        }

        private bool RequiresPosition(Entity entity)
        {
            return entity.IsMinion() || entity.IsLocation() || entity.IsBaconSpell() || entity.IsBattlegroundTrinket();
        }

        private int ResolvePosition()
        {
            Player friendlyPlayer = GameState.Get()?.GetFriendlySidePlayer();
            Zone battlefieldZone = friendlyPlayer?.GetBattlefieldZone();
            return battlefieldZone != null ? battlefieldZone.GetCardCount() : -1;
        }

        private int ResolveTargetId(GameState gameState, Entity sourceEntity, List<Network.Options.Option.TargetOption> targetOptions, ActionKind kind = ActionKind.Other)
        {
            if (targetOptions == null || targetOptions.Count == 0)
            {
                return -1;
            }

            List<Entity> enemyTargets = new List<Entity>();
            List<Entity> friendlyTargets = new List<Entity>();

            for (int i = 0; i < targetOptions.Count; i++)
            {
                Network.Options.Option.TargetOption targetOption = targetOptions[i];
                if (!IsPlayableTargetOption(targetOption))
                {
                    continue;
                }

                Entity targetEntity = gameState.GetEntity(targetOption.ID);
                if (targetEntity == null)
                {
                    continue;
                }

                // Skip stealthed enemies
                if (targetEntity.GetControllerSide() != Player.Side.FRIENDLY && targetEntity.IsStealthed())
                {
                    continue;
                }

                if (targetEntity.GetControllerSide() == Player.Side.FRIENDLY)
                {
                    friendlyTargets.Add(targetEntity);
                }
                else
                {
                    enemyTargets.Add(targetEntity);
                }
            }

            // Taunt enforcement: if attacking and enemy has taunt minions, only allow taunt targets
            if (kind == ActionKind.Attack && enemyTargets.Count > 0)
            {
                List<Entity> tauntTargets = enemyTargets
                    .Where(delegate (Entity item) { return item.HasTaunt() && !item.IsHero(); })
                    .ToList();
                if (tauntTargets.Count > 0)
                {
                    enemyTargets = tauntTargets;
                }
            }

            if (enemyTargets.Count > 0)
            {
                Entity enemyTarget = ChooseEnemyTarget(sourceEntity, enemyTargets);
                return enemyTarget != null ? enemyTarget.GetEntityId() : -1;
            }

            if (friendlyTargets.Count > 0)
            {
                return ChooseFriendlyTarget(friendlyTargets).GetEntityId();
            }

            return -1;
        }

        private Entity ChooseEnemyTarget(Entity sourceEntity, List<Entity> enemyTargets)
        {
            Entity heroTarget = enemyTargets.FirstOrDefault(delegate (Entity item) { return item.IsHero(); });
            List<Entity> enemyMinions = enemyTargets
                .Where(delegate (Entity item) { return item.IsMinion(); })
                .ToList();

            Entity preferredMinion = ChoosePreferredEnemyMinionTarget(sourceEntity, enemyMinions);
            return ChooseConfiguredEnemyTarget(heroTarget, preferredMinion, enemyTargets);
        }

        // 0-99 prioritizes clearing legal enemy minions before face; 100 always goes face when legal.
        private Entity ChooseConfiguredEnemyTarget(Entity heroTarget, Entity preferredMinion, List<Entity> enemyTargets)
        {
            int attackFaceChancePercent = PluginConfig.AttackFaceChancePercentValue;
            if (heroTarget == null)
            {
                return preferredMinion ?? enemyTargets[0];
            }

            if (attackFaceChancePercent >= 100)
            {
                return heroTarget;
            }

            if (preferredMinion != null)
            {
                return preferredMinion;
            }

            if (attackFaceChancePercent <= 0)
            {
                return null;
            }

            return RollChance(attackFaceChancePercent) ? heroTarget : null;
        }

        private Entity ChooseFriendlyTarget(List<Entity> friendlyTargets)
        {
            Entity damagedHero = friendlyTargets.FirstOrDefault(delegate (Entity item)
            {
                return item.IsHero() && item.GetCurrentHealth() < item.GetDefHealth();
            });

            if (damagedHero != null)
            {
                return damagedHero;
            }

            Entity strongestMinion = friendlyTargets
                .Where(delegate (Entity item) { return item.IsMinion(); })
                .OrderByDescending(delegate (Entity item) { return item.GetRealTimeAttack() + item.GetCurrentHealth(); })
                .FirstOrDefault();

            return strongestMinion ?? friendlyTargets[0];
        }

        private void ExecuteAction(GameState gameState, ActionPlan action)
        {
            gameState.SetSelectedOption(action.OptionIndex);
            gameState.SetSelectedSubOption(-1);
            gameState.SetSelectedOptionTarget(0);
            gameState.SetSelectedOptionPosition(0);

            if (action.SubOptionIndex >= 0)
            {
                gameState.SetSelectedSubOption(action.SubOptionIndex);
            }

            if (action.Position > 0)
            {
                gameState.SetSelectedOptionPosition(action.Position);
            }

            if (action.TargetId > 0)
            {
                gameState.SetSelectedOptionTarget(action.TargetId);
            }

            if (!gameState.IsInChoiceMode() && action.SubOptionIndex >= 0)
            {
                NotifySubOptionSelected(gameState, action);
            }

            if (gameState.IsInChoiceMode())
            {
                gameState.SendChoices();
            }
            else
            {
                gameState.SendOption();
            }

            LogDecision(action.Description);
            ScheduleNextBattleAction();
        }

        internal void ExecuteStrategyAction(GameState gameState, StrategyActionPlan action)
        {
            if (action == null)
            {
                return;
            }

            ExecuteAction(gameState, ToLegacyActionPlan(action));
        }

        private static StrategyActionPlan ToStrategyActionPlan(ActionPlan action)
        {
            if (action == null)
            {
                return null;
            }

            return new StrategyActionPlan
            {
                OptionIndex = action.OptionIndex,
                SubOptionIndex = action.SubOptionIndex,
                TargetId = action.TargetId,
                Position = action.Position,
                Score = action.Score,
                Description = action.Description,
                Kind = ToStrategyActionKind(action.Kind)
            };
        }

        private static ActionPlan ToLegacyActionPlan(StrategyActionPlan action)
        {
            if (action == null)
            {
                return null;
            }

            return new ActionPlan
            {
                OptionIndex = action.OptionIndex,
                SubOptionIndex = action.SubOptionIndex,
                TargetId = action.TargetId,
                Position = action.Position,
                Score = action.Score,
                Description = action.Description,
                Kind = ToLegacyActionKind(action.Kind)
            };
        }

        private static StrategyActionKind ToStrategyActionKind(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.Choice:
                    return StrategyActionKind.Choice;
                case ActionKind.PlayCard:
                    return StrategyActionKind.PlayCard;
                case ActionKind.HeroPower:
                    return StrategyActionKind.HeroPower;
                case ActionKind.Attack:
                    return StrategyActionKind.Attack;
                case ActionKind.Pass:
                    return StrategyActionKind.Pass;
                case ActionKind.EndTurn:
                    return StrategyActionKind.EndTurn;
                default:
                    return StrategyActionKind.Other;
            }
        }

        private static ActionKind ToLegacyActionKind(StrategyActionKind kind)
        {
            switch (kind)
            {
                case StrategyActionKind.Choice:
                    return ActionKind.Choice;
                case StrategyActionKind.PlayCard:
                    return ActionKind.PlayCard;
                case StrategyActionKind.HeroPower:
                    return ActionKind.HeroPower;
                case StrategyActionKind.Attack:
                    return ActionKind.Attack;
                case StrategyActionKind.Pass:
                    return ActionKind.Pass;
                case StrategyActionKind.EndTurn:
                    return ActionKind.EndTurn;
                default:
                    return ActionKind.Other;
            }
        }

        private void ScheduleNextBattleAction()
        {
            _nextActionAt = Time.unscaledTime + PluginConfig.RollActionDelaySeconds();
        }

        private Entity ChoosePreferredEnemyMinionTarget(Entity sourceEntity, List<Entity> enemyMinions)
        {
            if (enemyMinions == null || enemyMinions.Count == 0)
            {
                return null;
            }

            int attack = sourceEntity != null ? sourceEntity.GetRealTimeAttack() : 0;
            Entity lethalTrade = enemyMinions
                .Where(delegate (Entity item) { return item.GetCurrentHealth() <= attack; })
                .OrderBy(delegate (Entity item) { return item.GetCurrentHealth(); })
                .ThenByDescending(delegate (Entity item) { return item.GetRealTimeAttack(); })
                .FirstOrDefault();

            if (lethalTrade != null)
            {
                return lethalTrade;
            }

            return enemyMinions
                .OrderByDescending(delegate (Entity item) { return EvaluateEnemyMinionAttackPriority(sourceEntity, item); })
                .ThenBy(delegate (Entity item) { return item.GetCurrentHealth(); })
                .FirstOrDefault();
        }

        private int EvaluateEnemyHeroAttackPriority(Entity sourceEntity, Entity heroTarget)
        {
            int attack = sourceEntity != null ? sourceEntity.GetRealTimeAttack() : 0;
            int heroHealth = heroTarget != null ? heroTarget.GetCurrentHealth() : 30;
            // Exact lethal
            if (heroHealth > 0 && attack >= heroHealth)
            {
                return 100000;
            }

            int lowHealthBonus = heroHealth <= 15 ? (16 - heroHealth) * 10 : 0;
            int pressureBonus = attack > 0 && heroHealth <= attack * 2 ? 50 : 0;
            int nearLethalBonus = heroHealth <= 5 ? 80 : 0;
            return 120 + attack * 5 + lowHealthBonus + pressureBonus + nearLethalBonus;
        }

        private int EvaluateEnemyMinionAttackPriority(Entity sourceEntity, Entity target)
        {
            if (target == null)
            {
                return int.MinValue;
            }

            int sourceAttack = sourceEntity != null ? sourceEntity.GetRealTimeAttack() : 0;
            int sourceHealth = sourceEntity != null ? sourceEntity.GetCurrentHealth() : 0;
            int targetAttack = target.GetRealTimeAttack();
            int targetHealth = target.GetCurrentHealth();
            int lethalBonus = sourceAttack > 0 && targetHealth <= sourceAttack ? 60 : 0;
            int survivalBonus = sourceHealth <= 0 || sourceHealth > targetAttack ? 25 : 0;
            int unfavorablePenalty = sourceAttack > 0 && targetHealth > sourceAttack && sourceHealth > 0 && sourceHealth <= targetAttack ? -40 : 0;
            int tauntBonus = target.HasTaunt() ? 50 : 0;
            int divineShieldBonus = target.HasDivineShield() ? 30 : 0;
            return 80 + targetAttack * 14 + targetHealth * 3 + lethalBonus + survivalBonus + unfavorablePenalty + tauntBonus + divineShieldBonus;
        }

        private bool RollChance(int percent)
        {
            if (percent <= 0)
            {
                return false;
            }

            if (percent >= 100)
            {
                return true;
            }

            return UnityEngine.Random.Range(0, 100) < percent;
        }

        private void NotifySubOptionSelected(GameState gameState, ActionPlan action)
        {
            if (gameState == null || action == null || action.SubOptionIndex < 0)
            {
                return;
            }

            Network.Options options = gameState.GetOptionsPacket() ?? gameState.GetLastOptions();
            if (options == null || options.List == null || action.OptionIndex < 0 || action.OptionIndex >= options.List.Count)
            {
                return;
            }

            Network.Options.Option option = options.List[action.OptionIndex];
            if (option == null || option.Subs == null || action.SubOptionIndex >= option.Subs.Count)
            {
                return;
            }

            Network.Options.Option.SubOption subOption = option.Subs[action.SubOptionIndex];
            if (subOption == null)
            {
                return;
            }

            Entity selectedEntity = gameState.GetEntity(subOption.ID);
            if (selectedEntity == null)
            {
                return;
            }

            ChoiceCardMgr choiceCardMgr = ChoiceCardMgr.Get();
            if (choiceCardMgr == null)
            {
                return;
            }

            choiceCardMgr.OnSubOptionClicked(selectedEntity);
            TryHideChoiceUi();
        }

        private void TryHideChoiceUi()
        {
            ChoiceCardMgr choiceCardMgr = ChoiceCardMgr.Get();
            if (choiceCardMgr == null)
            {
                return;
            }

            try
            {
                object choiceState = GetFriendlyChoiceStateMethod != null
                    ? GetFriendlyChoiceStateMethod.Invoke(choiceCardMgr, null)
                    : null;
                GameState gameState = GameState.Get();

                if (choiceState != null && gameState != null && ConcealChoicesFromInputMethod != null)
                {
                    ConcealChoicesFromInputMethod.Invoke(choiceCardMgr, new object[] { gameState.GetFriendlyPlayerId(), choiceState });
                    return;
                }

                if (HideChoiceUIMethod != null)
                {
                    HideChoiceUIMethod.Invoke(choiceCardMgr, null);
                }
            }
            catch
            {
            }
        }

        private void LogDecision(string description)
        {
            if (PluginConfig.LogDecisionsValue)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, "[HsBattle] " + description);
            }
        }

        private string DescribeAction(Entity entity, ActionKind kind, int targetId, int position)
        {
            string entityName = entity != null ? entity.GetName() : kind.ToString();
            string targetName = string.Empty;

            if (targetId > 0)
            {
                Entity targetEntity = GameState.Get()?.GetEntity(targetId);
                targetName = targetEntity != null ? targetEntity.GetName() : targetId.ToString();
            }

            return string.Format(
                "{0}: {1}{2}{3}",
                kind,
                entityName,
                targetId > 0 ? " -> " + targetName : string.Empty,
                position > 0 ? " @pos " + position.ToString() : string.Empty);
        }

        private long TryResolveActiveTrayDeckId(DeckPickerTrayDisplay trayDisplay)
        {
            if (trayDisplay == null)
            {
                return 0L;
            }

            long selectedDeckId = trayDisplay.GetSelectedDeckID();
            if (selectedDeckId > 0L)
            {
                return selectedDeckId;
            }

            long lastChosenDeckId = TryResolveLastChosenDeckId(trayDisplay);
            return lastChosenDeckId > 0L ? lastChosenDeckId : 0L;
        }

        private long TryResolveLastChosenDeckId(DeckPickerTrayDisplay trayDisplay)
        {
            if (trayDisplay == null)
            {
                return 0L;
            }

            try
            {
                if (GetLastChosenDeckIdMethod == null)
                {
                    return 0L;
                }

                object value = GetLastChosenDeckIdMethod.Invoke(trayDisplay, null);
                return value is long ? (long)value : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        private CollectionDeckBoxVisual TryGetFirstDeckbox(DeckPickerTrayDisplay trayDisplay)
        {
            if (trayDisplay == null)
            {
                return null;
            }

            try
            {
                return GetFirstDeckboxMethod != null
                    ? GetFirstDeckboxMethod.Invoke(trayDisplay, null) as CollectionDeckBoxVisual
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private CollectionDeckBoxVisual TryGetTargetDeckbox(DeckPickerTrayDisplay trayDisplay, out string missingDeckMessage)
        {
            missingDeckMessage = "未找到可用卡组，已停止匹配。";

            long configuredDeckId = GetConfiguredQueueDeckId();
            if (configuredDeckId > 0L)
            {
                missingDeckMessage = "未找到所选卡组，或该卡组在当前模式不可用，已停止匹配。";
                return TryGetDeckboxWithDeckId(trayDisplay, configuredDeckId);
            }

            return TryGetFirstDeckbox(trayDisplay);
        }

        private CollectionDeckBoxVisual TryGetDeckboxWithDeckId(DeckPickerTrayDisplay trayDisplay, long deckId)
        {
            if (trayDisplay == null || deckId <= 0L)
            {
                return null;
            }

            try
            {
                return GetDeckboxWithDeckIDMethod != null
                    ? GetDeckboxWithDeckIDMethod.Invoke(trayDisplay, new object[] { deckId }) as CollectionDeckBoxVisual
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private bool TrySelectDeck(DeckPickerTrayDisplay trayDisplay, CollectionDeckBoxVisual deckbox)
        {
            if (trayDisplay == null || deckbox == null)
            {
                return false;
            }

            try
            {
                if (SelectCustomDeckMethod == null)
                {
                    return false;
                }

                object value = SelectCustomDeckMethod.Invoke(trayDisplay, new object[] { deckbox });
                return value is bool && (bool)value;
            }
            catch
            {
                return false;
            }
        }

        private QueueMode GetSelectedQueueMode()
        {
            return PluginConfig.queueMode != null ? PluginConfig.queueMode.Value : QueueMode.Standard;
        }

        private long GetConfiguredQueueDeckId()
        {
            return PluginConfig.queueDeckId != null && PluginConfig.queueDeckId.Value > 0L
                ? PluginConfig.queueDeckId.Value
                : 0L;
        }

        private VisualsFormatType GetDesiredVisualsFormatType()
        {
            switch (GetSelectedQueueMode())
            {
                case QueueMode.Wild:
                    return VisualsFormatType.VFT_WILD;
                case QueueMode.Casual:
                    return VisualsFormatType.VFT_CASUAL;
                default:
                    return VisualsFormatType.VFT_STANDARD;
            }
        }

        private VisualsFormatType GetCurrentVisualsFormatType()
        {
            try
            {
                if (!Options.GetInRankedPlayMode())
                {
                    return VisualsFormatType.VFT_CASUAL;
                }

                switch (Options.GetFormatType())
                {
                    case PegasusShared.FormatType.FT_WILD:
                        return VisualsFormatType.VFT_WILD;
                    case PegasusShared.FormatType.FT_CLASSIC:
                        return VisualsFormatType.VFT_CLASSIC;
                    case PegasusShared.FormatType.FT_TWIST:
                        return VisualsFormatType.VFT_TWIST;
                    case PegasusShared.FormatType.FT_STANDARD:
                        return VisualsFormatType.VFT_STANDARD;
                    default:
                        return VisualsFormatType.VFT_UNKNOWN;
                }
            }
            catch
            {
                return VisualsFormatType.VFT_UNKNOWN;
            }
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

        private static string DescribeScene(SceneMgr sceneMgr)
        {
            if (sceneMgr == null)
            {
                return "\u672a\u77e5";
            }

            switch (sceneMgr.GetMode())
            {
                case SceneMgr.Mode.HUB:
                    return "\u4e3b\u754c\u9762";
                case SceneMgr.Mode.GAME_MODE:
                    return "\u6a21\u5f0f";
                case SceneMgr.Mode.TOURNAMENT:
                    return "\u5bf9\u6218";
                case SceneMgr.Mode.TAVERN_BRAWL:
                    return "\u4e71\u6597";
                default:
                    return sceneMgr.GetMode().ToString();
            }
        }

        private string DescribeQueueStatus(GameMgr gameMgr)
        {
            if (gameMgr == null)
            {
                return "\u672a\u5c31\u7eea";
            }

            if (gameMgr.IsFindingGame())
            {
                return "\u5339\u914d\u4e2d";
            }

            if (_queueReturnToHubPending)
            {
                SceneMgr pendingSceneMgr = SceneMgr.Get();
                if (pendingSceneMgr != null && !pendingSceneMgr.IsTransitioning() && pendingSceneMgr.GetMode() == SceneMgr.Mode.HUB)
                {
                    return "\u7b49\u5f85\u4e3b\u754c\u9762";
                }

                return "\u56de\u4e3b\u754c\u9762";
            }

            SceneMgr sceneMgr = SceneMgr.Get();
            if (sceneMgr != null && !sceneMgr.IsTransitioning() && sceneMgr.GetMode() != SceneMgr.Mode.TOURNAMENT)
            {
                return "\u8fdb\u5165\u6a21\u5f0f";
            }

            if (_forceQueueRequested)
            {
                return "\u5df2\u8bf7\u6c42";
            }

            if (!PluginConfig.AutoQueueEnabledValue)
            {
                return "\u624b\u52a8";
            }

            return "\u5f85\u673a";
        }

        private static string DescribeBattleStatus(GameMgr gameMgr, GameState gameState)
        {
            if (gameMgr != null && gameMgr.IsSpectator())
            {
                return "\u89c2\u6218";
            }

            if (gameState == null || !gameState.IsGameCreated())
            {
                return "\u7a7a\u95f2";
            }

            if (gameState.IsGameOver())
            {
                return "\u7ed3\u7b97";
            }

            if (gameState.IsMulliganManagerActive())
            {
                return "\u7559\u724c";
            }

            if (!PluginConfig.AutoBattleEnabledValue)
            {
                return "\u624b\u52a8";
            }

            if (gameState.IsInChoiceMode())
            {
                return "\u6293\u53d6\u9009\u62e9";
            }

            if (gameState.IsFriendlySidePlayerTurn())
            {
                return "\u6211\u65b9\u56de\u5408";
            }

            return "\u5bf9\u65b9\u56de\u5408";
        }
    }
}
