using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HsBattle
{
    internal sealed class HsBattleOverlay
    {
        private readonly BattleController _controller;
        private static readonly QueueMode[] QueueModes =
        {
            QueueMode.Standard,
            QueueMode.Wild,
            QueueMode.Casual
        };
        private static readonly QueueDeckInfo AutoDeckOption = new QueueDeckInfo
        {
            Id = 0L,
            Name = "自动（首个可用）",
            DisplayName = "自动（首个可用）"
        };

        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonActiveTexture;
        private GUIStyle _panelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonActiveStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _textFieldStyle;
        private bool _stylesReady;
        private bool _collapsed;
        private bool _settingsOpen;
        private bool _modeDropdownOpen;
        private bool _deckDropdownOpen;
        private float _nextDeckRefreshAt;
        private Vector2 _deckScrollPosition;
        private Vector2 _settingsScrollPosition;
        private List<QueueDeckInfo> _deckOptions = new List<QueueDeckInfo>();
        private string _queueRetryInput = string.Empty;
        private string _delayMinInput = string.Empty;
        private string _delayMaxInput = string.Empty;
        private string _attackMinionChanceInput = string.Empty;
        private string _matchLogPathInput = string.Empty;

        public HsBattleOverlay(BattleController controller)
        {
            _controller = controller;
        }

        public void Draw()
        {
            if (Event.current == null)
            {
                return;
            }

            EnsureStyles();
            RefreshDeckOptions(false);

            Matrix4x4 previousMatrix = GUI.matrix;

            try
            {
                float scale = Mathf.Clamp(Screen.height / 1080f, 1f, 1.35f);
                GUI.depth = -1000;
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

                if (_collapsed)
                {
                    if (GUI.Button(new Rect(10f, 10f, 34f, 26f), new GUIContent("展"), _buttonStyle))
                    {
                        ToggleCollapsed();
                    }

                    return;
                }

                DrawMainPanel();

                if (_settingsOpen)
                {
                    DrawSettingsPanel();
                }
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }

        public void Dispose()
        {
            DestroyTexture(ref _panelTexture);
            DestroyTexture(ref _buttonTexture);
            DestroyTexture(ref _buttonActiveTexture);
            _stylesReady = false;
        }

        private void EnsureStyles()
        {
            if (_stylesReady)
            {
                return;
            }

            _panelTexture = CreateTexture(new Color32(26, 26, 26, 165));
            _buttonTexture = CreateTexture(new Color32(78, 62, 52, 210));
            _buttonActiveTexture = CreateTexture(new Color32(54, 43, 36, 235));

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = _panelTexture;
            _panelStyle.padding = new RectOffset(8, 8, 8, 8);
            _panelStyle.margin.left = 0;
            _panelStyle.margin.right = 0;
            _panelStyle.margin.top = 0;
            _panelStyle.margin.bottom = 0;
            _panelStyle.border.left = 4;
            _panelStyle.border.right = 4;
            _panelStyle.border.top = 4;
            _panelStyle.border.bottom = 4;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.normal.background = _buttonTexture;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.fontSize = 13;
            _buttonStyle.alignment = TextAnchor.MiddleCenter;
            _buttonStyle.padding = new RectOffset(6, 6, 4, 4);
            _buttonStyle.margin.left = 0;
            _buttonStyle.margin.right = 6;
            _buttonStyle.margin.top = 0;
            _buttonStyle.margin.bottom = 4;
            _buttonStyle.border.left = 3;
            _buttonStyle.border.right = 3;
            _buttonStyle.border.top = 3;
            _buttonStyle.border.bottom = 3;

            _buttonActiveStyle = new GUIStyle(_buttonStyle);
            _buttonActiveStyle.normal.background = _buttonActiveTexture;
            _buttonActiveStyle.normal.textColor = Color.white;

            _statusStyle = new GUIStyle(GUI.skin.label);
            _statusStyle.normal.textColor = Color.white;
            _statusStyle.fontSize = 12;
            _statusStyle.wordWrap = true;
            _statusStyle.margin.left = 2;
            _statusStyle.margin.right = 2;
            _statusStyle.margin.top = 2;
            _statusStyle.margin.bottom = 0;

            _titleStyle = new GUIStyle(_statusStyle);
            _titleStyle.fontSize = 14;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.alignment = TextAnchor.MiddleLeft;
            _titleStyle.margin.top = 0;
            _titleStyle.margin.bottom = 4;

            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.fontSize = 12;
            _textFieldStyle.alignment = TextAnchor.MiddleLeft;
            _textFieldStyle.normal.textColor = Color.white;
            _textFieldStyle.padding = new RectOffset(6, 6, 4, 4);
            _textFieldStyle.margin.left = 0;
            _textFieldStyle.margin.right = 6;
            _textFieldStyle.margin.top = 0;
            _textFieldStyle.margin.bottom = 4;

            _stylesReady = true;
        }

        private void DrawMainPanel()
        {
            float panelHeight = 234f;
            if (_modeDropdownOpen)
            {
                panelHeight += 82f;
            }

            if (_deckDropdownOpen)
            {
                panelHeight += 172f;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 320f, panelHeight), GUIContent.none, _panelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("HsBattle"), _titleStyle, GUILayout.Width(214f), GUILayout.Height(24f));

            if (GUILayout.Button(new GUIContent("缩小"), _buttonStyle, GUILayout.Width(68f), GUILayout.Height(24f)))
            {
                ToggleCollapsed();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(BuildAutomationLabel()), _buttonStyle, GUILayout.Width(152f), GUILayout.Height(28f)))
            {
                bool enable = !PluginConfig.AutomationFullyEnabledValue;
                PluginConfig.SetAutomationEnabled(enable);
                if (PluginConfig.isPluginEnable != null && enable)
                {
                    PluginConfig.isPluginEnable.Value = true;
                }

                ShowInfo(enable ? "已开启自动化" : "已暂停自动化");
            }

            if (GUILayout.Button(new GUIContent("立即匹配"), _buttonStyle, GUILayout.Width(132f), GUILayout.Height(28f)))
            {
                _controller?.RequestQueueNow();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent(BuildQueueLabel()), _buttonStyle, GUILayout.Width(152f), GUILayout.Height(28f)))
            {
                ToggleQueue();
            }

            if (GUILayout.Button(new GUIContent(BuildBattleLabel()), _buttonStyle, GUILayout.Width(132f), GUILayout.Height(28f)))
            {
                ToggleBattle();
            }

            GUILayout.EndHorizontal();

            if (GUILayout.Button(new GUIContent(_settingsOpen ? "关闭配置" : "打开配置"), _buttonStyle, GUILayout.Width(290f), GUILayout.Height(28f)))
            {
                ToggleSettingsPanel();
            }

            DrawQueueModeControls(290f, 290f);
            DrawDeckControls(290f, 300f, 272f);
            GUILayout.Label(new GUIContent(BuildStatusLine()), _statusStyle);
            GUILayout.EndArea();
        }

        private void DrawSettingsPanel()
        {
            GUILayout.BeginArea(new Rect(340f, 10f, 364f, 530f), GUIContent.none, _panelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("配置面板"), _titleStyle, GUILayout.Width(234f), GUILayout.Height(24f));

            if (GUILayout.Button(new GUIContent("关闭"), _buttonStyle, GUILayout.Width(68f), GUILayout.Height(24f)))
            {
                ToggleSettingsPanel();
            }

            GUILayout.EndHorizontal();

            _settingsScrollPosition = GUILayout.BeginScrollView(_settingsScrollPosition, GUILayout.Width(348f), GUILayout.Height(486f));

            DrawSectionHeader("自动化");
            DrawToggleRow("插件启用", PluginConfig.EnabledValue, TogglePluginEnabled);
            DrawToggleRow("自动匹配", PluginConfig.autoQueueEnabled != null && PluginConfig.autoQueueEnabled.Value, ToggleQueue);
            DrawToggleRow("自动对战", PluginConfig.autoBattleEnabled != null && PluginConfig.autoBattleEnabled.Value, ToggleBattle);
            DrawToggleRow("自动留牌", PluginConfig.autoMulliganEnabled != null && PluginConfig.autoMulliganEnabled.Value, ToggleMulligan);
            DrawToggleRow("防掉线踢出", PluginConfig.disableIdleKick != null && PluginConfig.disableIdleKick.Value, ToggleDisableIdleKick);
            DrawToggleRow("自动确认弹窗", PluginConfig.autoConfirmDialogs != null && PluginConfig.autoConfirmDialogs.Value, ToggleAutoConfirmDialogs);
            DrawToggleRow("错误后退出", PluginConfig.autoExitOnError != null && PluginConfig.autoExitOnError.Value, ToggleAutoExitOnError);
            DrawToggleRow("跳过英雄开场", PluginConfig.skipHeroIntro != null && PluginConfig.skipHeroIntro.Value, ToggleSkipHeroIntro);
            DrawToggleRow("记录决策日志", PluginConfig.logDecisions != null && PluginConfig.logDecisions.Value, ToggleLogDecisions);

            DrawSectionHeader("匹配");
            DrawIntInputRow("重试间隔(秒)", ref _queueRetryInput, ApplyQueueRetryInput);
            DrawButtonRow("弹窗默认响应", PluginConfig.DescribePopupResponse(PluginConfig.PopupResponseValue), CyclePopupResponse);

            DrawSectionHeader("战斗");
            DrawIntInputRow("延迟下限(ms)", ref _delayMinInput, ApplyDelayInputs);
            DrawIntInputRow("延迟上限(ms)", ref _delayMaxInput, ApplyDelayInputs);
            DrawIntInputRow("打随从概率(%)", ref _attackMinionChanceInput, ApplyAttackMinionChanceInput);

            DrawSectionHeader("日志");
            DrawTextInputRow("结果日志路径", ref _matchLogPathInput, ApplyMatchLogPathInput);

            DrawSectionHeader("热键");
            GUILayout.Label(new GUIContent("切换自动化：" + ResolveHotkeyLabel(PluginConfig.toggleAutomationKey != null ? PluginConfig.toggleAutomationKey.Value.ToString() : string.Empty)), _statusStyle);
            GUILayout.Label(new GUIContent("立即匹配：" + ResolveHotkeyLabel(PluginConfig.forceQueueKey != null ? PluginConfig.forceQueueKey.Value.ToString() : string.Empty)), _statusStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            if (_collapsed)
            {
                _settingsOpen = false;
                _modeDropdownOpen = false;
                _deckDropdownOpen = false;
            }
        }

        private void ToggleSettingsPanel()
        {
            _settingsOpen = !_settingsOpen;
            if (_settingsOpen)
            {
                SyncInputsFromConfig();
                RefreshDeckOptions(true);
                return;
            }

            _modeDropdownOpen = false;
            _deckDropdownOpen = false;
        }

        private void ToggleQueue()
        {
            bool newValue = !(PluginConfig.autoQueueEnabled != null && PluginConfig.autoQueueEnabled.Value);
            if (PluginConfig.isPluginEnable != null && newValue)
            {
                PluginConfig.isPluginEnable.Value = true;
            }

            if (PluginConfig.autoQueueEnabled != null)
            {
                PluginConfig.autoQueueEnabled.Value = newValue;
            }

            _modeDropdownOpen = false;
            _deckDropdownOpen = false;
            ShowInfo(newValue ? "已开启自动匹配" : "已关闭自动匹配");
        }

        private void ToggleBattle()
        {
            bool newValue = !(PluginConfig.autoBattleEnabled != null && PluginConfig.autoBattleEnabled.Value);
            if (PluginConfig.isPluginEnable != null && newValue)
            {
                PluginConfig.isPluginEnable.Value = true;
            }

            if (PluginConfig.autoBattleEnabled != null)
            {
                PluginConfig.autoBattleEnabled.Value = newValue;
            }

            if (PluginConfig.autoMulliganEnabled != null && newValue)
            {
                PluginConfig.autoMulliganEnabled.Value = true;
            }

            _modeDropdownOpen = false;
            _deckDropdownOpen = false;
            ShowInfo(newValue ? "已开启自动对战" : "已关闭自动对战");
        }

        private void TogglePluginEnabled()
        {
            if (PluginConfig.isPluginEnable == null)
            {
                return;
            }

            PluginConfig.isPluginEnable.Value = !PluginConfig.isPluginEnable.Value;
            ShowInfo(PluginConfig.isPluginEnable.Value ? "已启用插件" : "已停用插件");
        }

        private void ToggleMulligan()
        {
            if (PluginConfig.autoMulliganEnabled == null)
            {
                return;
            }

            bool newValue = !PluginConfig.autoMulliganEnabled.Value;
            if (PluginConfig.isPluginEnable != null && newValue)
            {
                PluginConfig.isPluginEnable.Value = true;
            }

            PluginConfig.autoMulliganEnabled.Value = newValue;
            ShowInfo(newValue ? "已开启自动留牌" : "已关闭自动留牌");
        }

        private void ToggleDisableIdleKick()
        {
            if (PluginConfig.disableIdleKick == null)
            {
                return;
            }

            PluginConfig.disableIdleKick.Value = !PluginConfig.disableIdleKick.Value;
            ShowInfo(PluginConfig.disableIdleKick.Value ? "已开启防掉线踢出" : "已关闭防掉线踢出");
        }

        private void ToggleAutoConfirmDialogs()
        {
            if (PluginConfig.autoConfirmDialogs == null)
            {
                return;
            }

            PluginConfig.autoConfirmDialogs.Value = !PluginConfig.autoConfirmDialogs.Value;
            ShowInfo(PluginConfig.autoConfirmDialogs.Value ? "已开启自动确认弹窗" : "已关闭自动确认弹窗");
        }

        private void ToggleAutoExitOnError()
        {
            if (PluginConfig.autoExitOnError == null)
            {
                return;
            }

            PluginConfig.autoExitOnError.Value = !PluginConfig.autoExitOnError.Value;
            ShowInfo(PluginConfig.autoExitOnError.Value ? "已开启错误后退出" : "已关闭错误后退出");
        }

        private void ToggleSkipHeroIntro()
        {
            if (PluginConfig.skipHeroIntro == null)
            {
                return;
            }

            PluginConfig.skipHeroIntro.Value = !PluginConfig.skipHeroIntro.Value;
            ShowInfo(PluginConfig.skipHeroIntro.Value ? "已开启跳过英雄开场" : "已关闭跳过英雄开场");
        }

        private void ToggleLogDecisions()
        {
            if (PluginConfig.logDecisions == null)
            {
                return;
            }

            PluginConfig.logDecisions.Value = !PluginConfig.logDecisions.Value;
            ShowInfo(PluginConfig.logDecisions.Value ? "已开启决策日志" : "已关闭决策日志");
        }

        private string BuildAutomationLabel()
        {
            if (!PluginConfig.EnabledValue)
            {
                return "状态已关闭";
            }

            return PluginConfig.AutomationFullyEnabledValue
                ? "状态已开启"
                : "状态部分开启";
        }

        private static string BuildQueueLabel()
        {
            return "自动匹配：" + DescribeToggleState(PluginConfig.AutoQueueEnabledValue);
        }

        private static string BuildBattleLabel()
        {
            return "自动对战：" + DescribeToggleState(PluginConfig.AutoBattleEnabledValue);
        }

        private static string BuildQueueModeLabel(QueueMode mode)
        {
            return "匹配模式：" + PluginConfig.DescribeQueueMode(mode);
        }

        private string BuildQueueDeckLabel()
        {
            long deckId = PluginConfig.queueDeckId != null ? PluginConfig.queueDeckId.Value : 0L;
            return "匹配卡组：" + ShortenLabel(ResolveDeckLabel(deckId), 18);
        }

        private string BuildStatusLine()
        {
            return string.Format(
                "留牌:{0}  模式:{1}  {2}",
                DescribeToggleState(PluginConfig.AutoMulliganEnabledValue),
                PluginConfig.DescribeQueueMode(PluginConfig.queueMode != null ? PluginConfig.queueMode.Value : QueueMode.Standard),
                _controller != null ? _controller.GetOverlayStatusText() : "状态:未就绪");
        }

        private static string DescribeToggleState(bool enabled)
        {
            return enabled ? "开" : "关";
        }

        private void DrawSectionHeader(string title)
        {
            GUILayout.Space(4f);
            GUILayout.Label(new GUIContent(title), _titleStyle, GUILayout.Width(320f), GUILayout.Height(22f));
        }

        private void DrawToggleRow(string label, bool value, Action toggleAction)
        {
            DrawButtonRow(label, value ? "开" : "关", toggleAction, value ? _buttonActiveStyle : _buttonStyle);
        }

        private void DrawButtonRow(string label, string buttonText, Action action, GUIStyle buttonStyle = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label), _statusStyle, GUILayout.Width(182f), GUILayout.Height(24f));

            if (GUILayout.Button(new GUIContent(buttonText), buttonStyle ?? _buttonStyle, GUILayout.Width(118f), GUILayout.Height(24f)))
            {
                action?.Invoke();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawIntInputRow(string label, ref string inputValue, Action applyAction)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label), _statusStyle, GUILayout.Width(150f), GUILayout.Height(24f));
            inputValue = GUILayout.TextField(inputValue ?? string.Empty, _textFieldStyle, GUILayout.Width(86f), GUILayout.Height(24f));

            if (GUILayout.Button(new GUIContent("应用"), _buttonStyle, GUILayout.Width(60f), GUILayout.Height(24f)))
            {
                applyAction?.Invoke();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawTextInputRow(string label, ref string inputValue, Action applyAction)
        {
            GUILayout.Label(new GUIContent(label), _statusStyle, GUILayout.Width(320f), GUILayout.Height(20f));

            GUILayout.BeginHorizontal();
            inputValue = GUILayout.TextField(inputValue ?? string.Empty, _textFieldStyle, GUILayout.Width(224f), GUILayout.Height(24f));

            if (GUILayout.Button(new GUIContent("应用"), _buttonStyle, GUILayout.Width(60f), GUILayout.Height(24f)))
            {
                applyAction?.Invoke();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawQueueModeControls(float buttonWidth, float optionWidth)
        {
            QueueMode selectedMode = PluginConfig.queueMode != null ? PluginConfig.queueMode.Value : QueueMode.Standard;
            if (GUILayout.Button(new GUIContent(BuildQueueModeLabel(selectedMode)), _buttonStyle, GUILayout.Width(buttonWidth), GUILayout.Height(28f)))
            {
                _deckDropdownOpen = false;
                _modeDropdownOpen = !_modeDropdownOpen;
            }

            if (_modeDropdownOpen)
            {
                for (int index = 0; index < QueueModes.Length; index++)
                {
                    QueueMode mode = QueueModes[index];
                    GUIStyle buttonStyle = mode == selectedMode ? _buttonActiveStyle : _buttonStyle;
                    if (GUILayout.Button(new GUIContent(PluginConfig.DescribeQueueMode(mode)), buttonStyle, GUILayout.Width(optionWidth), GUILayout.Height(24f)))
                    {
                        SelectQueueMode(mode);
                    }
                }
            }
        }

        private void DrawDeckControls(float buttonWidth, float scrollWidth, float optionWidth)
        {
            if (GUILayout.Button(new GUIContent(BuildQueueDeckLabel()), _buttonStyle, GUILayout.Width(buttonWidth), GUILayout.Height(28f)))
            {
                RefreshDeckOptions(true);
                _modeDropdownOpen = false;
                _deckDropdownOpen = !_deckDropdownOpen;
            }

            if (_deckDropdownOpen)
            {
                _deckScrollPosition = GUILayout.BeginScrollView(_deckScrollPosition, GUILayout.Width(scrollWidth), GUILayout.Height(156f));
                DrawDeckOption(AutoDeckOption, optionWidth);

                if (_deckOptions.Count == 0)
                {
                    GUILayout.Label(new GUIContent("未读取到卡组"), _statusStyle, GUILayout.Width(optionWidth));
                }
                else
                {
                    for (int index = 0; index < _deckOptions.Count; index++)
                    {
                        DrawDeckOption(_deckOptions[index], optionWidth);
                    }
                }

                GUILayout.EndScrollView();
            }
        }

        private void SelectQueueMode(QueueMode mode)
        {
            if (PluginConfig.queueMode != null)
            {
                PluginConfig.queueMode.Value = mode;
            }

            _modeDropdownOpen = false;
            _deckDropdownOpen = false;
            ShowInfo("已切换匹配模式：" + PluginConfig.DescribeQueueMode(mode));
        }

        private void DrawDeckOption(QueueDeckInfo deckInfo, float optionWidth)
        {
            long selectedDeckId = PluginConfig.queueDeckId != null ? PluginConfig.queueDeckId.Value : 0L;
            GUIStyle buttonStyle = deckInfo.Id == selectedDeckId ? _buttonActiveStyle : _buttonStyle;
            if (GUILayout.Button(new GUIContent(deckInfo.DisplayName), buttonStyle, GUILayout.Width(optionWidth), GUILayout.Height(24f)))
            {
                SelectQueueDeck(deckInfo);
            }
        }

        private void SelectQueueDeck(QueueDeckInfo deckInfo)
        {
            if (PluginConfig.queueDeckId != null)
            {
                PluginConfig.queueDeckId.Value = deckInfo != null ? deckInfo.Id : 0L;
            }

            _deckDropdownOpen = false;
            ShowInfo("已切换匹配卡组：" + ResolveDeckLabel(deckInfo != null ? deckInfo.Id : 0L));
        }

        private void RefreshDeckOptions(bool force)
        {
            if (!force && Time.unscaledTime < _nextDeckRefreshAt)
            {
                return;
            }

            _deckOptions = DeckUtils.GetConstructedDecks();
            _nextDeckRefreshAt = Time.unscaledTime + 2f;
        }

        private string ResolveDeckLabel(long deckId)
        {
            if (deckId <= 0L)
            {
                return AutoDeckOption.DisplayName;
            }

            for (int i = 0; i < _deckOptions.Count; i++)
            {
                if (_deckOptions[i].Id == deckId)
                {
                    return _deckOptions[i].DisplayName;
                }
            }

            return DeckUtils.DescribeQueueDeck(deckId);
        }

        private void SyncInputsFromConfig()
        {
            _queueRetryInput = Mathf.RoundToInt(PluginConfig.QueueRetrySecondsValue).ToString();
            _delayMinInput = PluginConfig.ActionDelayMinMsValue.ToString();
            _delayMaxInput = PluginConfig.ActionDelayMaxMsValue.ToString();
            _attackMinionChanceInput = PluginConfig.AttackMinionChancePercentValue.ToString();
            _matchLogPathInput = PluginConfig.MatchLogPathValue;
        }

        private void ApplyQueueRetryInput()
        {
            int value;
            if (!int.TryParse(_queueRetryInput, out value))
            {
                ShowInfo("重试间隔需输入整数");
                SyncInputsFromConfig();
                return;
            }

            PluginConfig.SetQueueRetrySeconds(value);
            SyncInputsFromConfig();
            ShowInfo("已更新匹配重试间隔");
        }

        private void ApplyDelayInputs()
        {
            int minDelay;
            int maxDelay;
            if (!int.TryParse(_delayMinInput, out minDelay) || !int.TryParse(_delayMaxInput, out maxDelay))
            {
                ShowInfo("延迟上下限需输入整数");
                SyncInputsFromConfig();
                return;
            }

            PluginConfig.SetActionDelayRangeMs(minDelay, maxDelay);
            SyncInputsFromConfig();
            ShowInfo(string.Format("动作延迟范围：{0}-{1}ms", PluginConfig.ActionDelayMinMsValue, PluginConfig.ActionDelayMaxMsValue));
        }

        private void ApplyAttackMinionChanceInput()
        {
            int value;
            if (!int.TryParse(_attackMinionChanceInput, out value))
            {
                ShowInfo("打随从概率需输入整数");
                SyncInputsFromConfig();
                return;
            }

            PluginConfig.SetAttackMinionChancePercent(value);
            SyncInputsFromConfig();
            ShowInfo(string.Format("打随从概率：{0}%", PluginConfig.AttackMinionChancePercentValue));
        }

        private void ApplyMatchLogPathInput()
        {
            PluginConfig.SetMatchLogPath(_matchLogPathInput);
            SyncInputsFromConfig();
            ShowInfo("已更新结果日志路径");
        }

        private void CyclePopupResponse()
        {
            AlertPopupResponse nextResponse;
            switch (PluginConfig.PopupResponseValue)
            {
                case AlertPopupResponse.Okay:
                    nextResponse = AlertPopupResponse.Cancel;
                    break;
                case AlertPopupResponse.Cancel:
                    nextResponse = AlertPopupResponse.Confirm;
                    break;
                default:
                    nextResponse = AlertPopupResponse.Okay;
                    break;
            }

            PluginConfig.SetPopupResponse(nextResponse);
            ShowInfo("弹窗默认响应：" + PluginConfig.DescribePopupResponse(nextResponse));
        }

        private static string ResolveHotkeyLabel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "未设置" : value;
        }

        private static string ShortenLabel(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength || maxLength <= 3)
            {
                return text;
            }

            return text.Substring(0, maxLength - 3) + "...";
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        private static void ShowInfo(string message)
        {
            UIStatus.Get()?.AddInfo(message, 2f);
            Utils.MyLogger(LogLevel.Info, "[HsBattle] " + message);
        }
    }
}
