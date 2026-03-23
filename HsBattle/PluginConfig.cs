using BepInEx.Configuration;
using System;
using System.IO;
using UnityEngine;

namespace HsBattle
{
    public enum QueueMode
    {
        Standard,
        Wild,
        Casual
    }

    public static class PluginConfig
    {
        public static ConfigEntry<bool> isPluginEnable;
        public static ConfigEntry<bool> autoQueueEnabled;
        public static ConfigEntry<bool> autoBattleEnabled;
        public static ConfigEntry<bool> autoMulliganEnabled;
        public static ConfigEntry<bool> disableIdleKick;
        public static ConfigEntry<bool> autoConfirmDialogs;
        public static ConfigEntry<bool> autoExitOnError;
        public static ConfigEntry<bool> skipHeroIntro;
        public static ConfigEntry<bool> logDecisions;
        public static ConfigEntry<QueueMode> queueMode;
        public static ConfigEntry<long> queueDeckId;
        public static ConfigEntry<int> actionIntervalMs;
        public static ConfigEntry<int> queueRetrySeconds;
        public static ConfigEntry<string> matchLogPath;
        public static ConfigEntry<KeyboardShortcut> toggleAutomationKey;
        public static ConfigEntry<KeyboardShortcut> forceQueueKey;
        public static ConfigEntry<AlertPopupResponse> popupResponse;

        public static string GlobalHsUnitId = string.Empty;

        public static bool EnabledValue
        {
            get { return isPluginEnable != null && isPluginEnable.Value; }
        }

        public static bool AutoQueueEnabledValue
        {
            get { return EnabledValue && autoQueueEnabled != null && autoQueueEnabled.Value; }
        }

        public static bool AutoBattleEnabledValue
        {
            get { return EnabledValue && autoBattleEnabled != null && autoBattleEnabled.Value; }
        }

        public static bool AutoMulliganEnabledValue
        {
            get { return EnabledValue && autoMulliganEnabled != null && autoMulliganEnabled.Value; }
        }

        public static bool DisableIdleKickValue
        {
            get { return EnabledValue && disableIdleKick != null && disableIdleKick.Value; }
        }

        public static bool AutoConfirmDialogsValue
        {
            get { return EnabledValue && autoConfirmDialogs != null && autoConfirmDialogs.Value; }
        }

        public static bool AutoExitOnErrorValue
        {
            get { return EnabledValue && autoExitOnError != null && autoExitOnError.Value; }
        }

        public static bool SkipHeroIntroValue
        {
            get { return EnabledValue && skipHeroIntro != null && skipHeroIntro.Value; }
        }

        public static bool LogDecisionsValue
        {
            get { return EnabledValue && logDecisions != null && logDecisions.Value; }
        }

        public static bool AutomationFullyEnabledValue
        {
            get
            {
                return EnabledValue
                    && autoQueueEnabled != null && autoQueueEnabled.Value
                    && autoBattleEnabled != null && autoBattleEnabled.Value
                    && autoMulliganEnabled != null && autoMulliganEnabled.Value;
            }
        }

        public static float ActionIntervalSeconds
        {
            get { return Math.Max(0.2f, actionIntervalMs.Value / 1000f); }
        }

        public static float QueueRetrySecondsValue
        {
            get { return Math.Max(2f, queueRetrySeconds.Value); }
        }

        public static string MatchLogPathValue
        {
            get { return matchLogPath != null ? matchLogPath.Value : string.Empty; }
        }

        public static AlertPopupResponse PopupResponseValue
        {
            get { return popupResponse != null ? popupResponse.Value : AlertPopupResponse.Confirm; }
        }

        public static void ConfigBind(ConfigFile config)
        {
            Utils.EnsureWorkDirectory();

            isPluginEnable = config.Bind("HsBattle", "Enabled", true, "Enable HsBattle automation.");

            autoQueueEnabled = config.Bind("Automation", "AutoQueueEnabled", true, "Automatically start matchmaking when the client is idle.");
            autoBattleEnabled = config.Bind("Automation", "AutoBattleEnabled", true, "Automatically play cards, attack and end turns.");
            autoMulliganEnabled = config.Bind("Automation", "AutoMulliganEnabled", true, "Automatically confirm mulligan without replacing cards.");
            disableIdleKick = config.Bind("Automation", "DisableIdleKick", true, "Disable inactivity kick checks.");
            autoConfirmDialogs = config.Bind("Automation", "AutoConfirmDialogs", true, "Automatically confirm common popups while matching.");
            autoExitOnError = config.Bind("Automation", "AutoExitOnError", false, "Exit the client after fatal reconnect errors.");
            skipHeroIntro = config.Bind("Automation", "SkipHeroIntro", true, "Skip the intro animation before the mulligan phase.");
            logDecisions = config.Bind("Automation", "LogDecisions", true, "Write automation decisions into the BepInEx log.");

            queueMode = config.Bind("Matchmaking", "QueueMode", QueueMode.Standard, "Target queue mode.");
            queueDeckId = config.Bind("Matchmaking", "QueueDeckId", 0L, "Deck ID to queue with. 0 means use the first available deck.");
            queueRetrySeconds = config.Bind("Matchmaking", "QueueRetrySeconds", 8, "Seconds between matchmaking retries.");

            actionIntervalMs = config.Bind("Battle", "ActionIntervalMs", 900, "Delay between automated game actions.");

            matchLogPath = config.Bind(
                "Logging",
                "MatchLogPath",
                Path.Combine(Utils.GetWorkDirectory(), "match.log"),
                "Match result log path.");

            popupResponse = config.Bind("Popup", "Response", AlertPopupResponse.Confirm, "How auto-confirmed popups should be answered.");

            toggleAutomationKey = config.Bind(
                "Hotkeys",
                "ToggleAutomation",
                new KeyboardShortcut(KeyCode.F8, KeyCode.LeftControl),
                "Toggle both auto queue and auto battle.");

            forceQueueKey = config.Bind(
                "Hotkeys",
                "ForceQueueNow",
                new KeyboardShortcut(KeyCode.F9, KeyCode.LeftControl),
                "Force an immediate matchmaking attempt.");
        }

        public static bool TryParseQueueMode(string rawValue, out QueueMode mode)
        {
            string normalized = (rawValue ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-");
            switch (normalized)
            {
                case "ranked-standard":
                case "standard-ranked":
                case "standard":
                case "ranked":
                    mode = QueueMode.Standard;
                    return true;
                case "ranked-wild":
                case "wild-ranked":
                case "wild":
                    mode = QueueMode.Wild;
                    return true;
                case "casual-standard":
                case "standard-casual":
                case "casual-wild":
                case "wild-casual":
                case "casual":
                    mode = QueueMode.Casual;
                    return true;
                default:
                    mode = QueueMode.Standard;
                    return false;
            }
        }

        public static string DescribeQueueMode(QueueMode mode)
        {
            switch (mode)
            {
                case QueueMode.Wild:
                    return "狂野模式";
                case QueueMode.Casual:
                    return "休闲模式";
                default:
                    return "标准模式";
            }
        }

        public static void SetAutomationEnabled(bool enabled)
        {
            if (isPluginEnable != null)
            {
                isPluginEnable.Value = enabled;
            }

            if (autoQueueEnabled != null)
            {
                autoQueueEnabled.Value = enabled;
            }

            if (autoBattleEnabled != null)
            {
                autoBattleEnabled.Value = enabled;
            }

            if (autoMulliganEnabled != null)
            {
                autoMulliganEnabled.Value = enabled;
            }
        }
    }
}
