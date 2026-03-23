using BepInEx.Logging;
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
        private bool _stylesReady;
        private bool _modeDropdownOpen;
        private bool _deckDropdownOpen;
        private float _nextDeckRefreshAt;
        private Vector2 _deckScrollPosition;
        private List<QueueDeckInfo> _deckOptions = new List<QueueDeckInfo>();

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

                float panelHeight = 156f;
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
                if (GUILayout.Button(new GUIContent(BuildAutomationLabel()), _buttonStyle, GUILayout.Width(152f), GUILayout.Height(28f)))
                {
                    bool enable = !PluginConfig.AutomationFullyEnabledValue;
                    PluginConfig.SetAutomationEnabled(enable);
                    ShowInfo(enable ? "\u5df2\u5f00\u542f\u81ea\u52a8\u5316" : "\u5df2\u6682\u505c\u81ea\u52a8\u5316");
                }

                if (GUILayout.Button(new GUIContent("\u7acb\u5373\u5339\u914d"), _buttonStyle, GUILayout.Width(132f), GUILayout.Height(28f)))
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

                QueueMode selectedMode = PluginConfig.queueMode != null ? PluginConfig.queueMode.Value : QueueMode.Standard;
                if (GUILayout.Button(new GUIContent(BuildQueueModeLabel(selectedMode)), _buttonStyle, GUILayout.Width(290f), GUILayout.Height(28f)))
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
                        if (GUILayout.Button(new GUIContent(PluginConfig.DescribeQueueMode(mode)), buttonStyle, GUILayout.Width(290f), GUILayout.Height(24f)))
                        {
                            SelectQueueMode(mode);
                        }
                    }
                }

                if (GUILayout.Button(new GUIContent(BuildQueueDeckLabel()), _buttonStyle, GUILayout.Width(290f), GUILayout.Height(28f)))
                {
                    RefreshDeckOptions(true);
                    _modeDropdownOpen = false;
                    _deckDropdownOpen = !_deckDropdownOpen;
                }

                if (_deckDropdownOpen)
                {
                    _deckScrollPosition = GUILayout.BeginScrollView(_deckScrollPosition, GUILayout.Width(300f), GUILayout.Height(160f));
                    DrawDeckOption(AutoDeckOption);

                    if (_deckOptions.Count == 0)
                    {
                        GUILayout.Label(new GUIContent("未读取到卡组"), _statusStyle, GUILayout.Width(272f));
                    }
                    else
                    {
                        for (int index = 0; index < _deckOptions.Count; index++)
                        {
                            DrawDeckOption(_deckOptions[index]);
                        }
                    }

                    GUILayout.EndScrollView();
                }

                GUILayout.Label(new GUIContent(BuildStatusLine()), _statusStyle);
                GUILayout.EndArea();
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

            _stylesReady = true;
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

            ShowInfo(newValue ? "\u5df2\u5f00\u542f\u81ea\u52a8\u5339\u914d" : "\u5df2\u5173\u95ed\u81ea\u52a8\u5339\u914d");
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
            ShowInfo(newValue ? "\u5df2\u5f00\u542f\u81ea\u52a8\u5bf9\u6218" : "\u5df2\u5173\u95ed\u81ea\u52a8\u5bf9\u6218");
        }

        private string BuildAutomationLabel()
        {
            if (!PluginConfig.EnabledValue)
            {
                return "\u72b6\u6001\u5df2\u5173\u95ed";
            }

            return PluginConfig.AutomationFullyEnabledValue
                ? "\u72b6\u6001\u5df2\u5f00\u542f"
                : "\u72b6\u6001\u90e8\u5206\u5f00\u542f";
        }

        private static string BuildQueueLabel()
        {
            return "\u81ea\u52a8\u5339\u914d\uff1a" + DescribeToggleState(PluginConfig.AutoQueueEnabledValue);
        }

        private static string BuildBattleLabel()
        {
            return "\u81ea\u52a8\u5bf9\u6218\uff1a" + DescribeToggleState(PluginConfig.AutoBattleEnabledValue);
        }

        private static string BuildQueueModeLabel(QueueMode mode)
        {
            return "\u5339\u914d\u6a21\u5f0f\uff1a" + PluginConfig.DescribeQueueMode(mode);
        }

        private string BuildQueueDeckLabel()
        {
            long deckId = PluginConfig.queueDeckId != null ? PluginConfig.queueDeckId.Value : 0L;
            return "\u5339\u914d\u5361\u7ec4\uff1a" + ShortenLabel(ResolveDeckLabel(deckId), 18);
        }

        private string BuildStatusLine()
        {
            return string.Format(
                "\u7559\u724c:{0}  \u6a21\u5f0f:{1}  {2}",
                DescribeToggleState(PluginConfig.AutoMulliganEnabledValue),
                PluginConfig.DescribeQueueMode(PluginConfig.queueMode != null ? PluginConfig.queueMode.Value : QueueMode.Standard),
                _controller != null ? _controller.GetOverlayStatusText() : "\u72b6\u6001:\u672a\u5c31\u7eea");
        }

        private static string DescribeToggleState(bool enabled)
        {
            return enabled ? "\u5f00" : "\u5173";
        }

        private void SelectQueueMode(QueueMode mode)
        {
            if (PluginConfig.queueMode != null)
            {
                PluginConfig.queueMode.Value = mode;
            }

            _modeDropdownOpen = false;
            _deckDropdownOpen = false;
            ShowInfo("\u5df2\u5207\u6362\u5339\u914d\u6a21\u5f0f\uff1a" + PluginConfig.DescribeQueueMode(mode));
        }

        private void DrawDeckOption(QueueDeckInfo deckInfo)
        {
            long selectedDeckId = PluginConfig.queueDeckId != null ? PluginConfig.queueDeckId.Value : 0L;
            GUIStyle buttonStyle = deckInfo.Id == selectedDeckId ? _buttonActiveStyle : _buttonStyle;
            if (GUILayout.Button(new GUIContent(deckInfo.DisplayName), buttonStyle, GUILayout.Width(272f), GUILayout.Height(24f)))
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
            ShowInfo("\u5df2\u5207\u6362\u5339\u914d\u5361\u7ec4\uff1a" + ResolveDeckLabel(deckInfo != null ? deckInfo.Id : 0L));
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

            Object.Destroy(texture);
            texture = null;
        }

        private static void ShowInfo(string message)
        {
            UIStatus.Get()?.AddInfo(message, 2f);
            Utils.MyLogger(LogLevel.Info, "[HsBattle] " + message);
        }
    }
}
