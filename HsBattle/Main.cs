using BepInEx;
using BepInEx.Configuration;
using System;
using System.IO;

namespace HsBattle
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        internal BattleController Controller { get; private set; }
        private HsBattleOverlay _overlay;
        internal BepInEx.Logging.ManualLogSource PluginLogger
        {
            get { return Logger; }
        }

        private void Awake()
        {
            Instance = this;

            ConfigFile configFile = ResolveConfigFile();
            PluginConfig.ConfigBind(configFile);
            ApplyCommandLineOverrides();
            Utils.EnsureWorkDirectory();

            PatchManager.ApplyAll();
            Controller = new BattleController();
            _overlay = new HsBattleOverlay(Controller);

            Utils.MyLogger(BepInEx.Logging.LogLevel.Info, string.Format(
                "{0} {1} loaded{2}.",
                PluginInfo.PLUGIN_NAME,
                PluginInfo.PLUGIN_VERSION,
                PluginConfig.AutomationFullyEnabledValue ? string.Empty : " with automation partially paused"));
        }

        private void Update()
        {
            Controller?.Tick();
        }

        private void OnGUI()
        {
            _overlay?.Draw();
        }

        private void OnDestroy()
        {
            _overlay?.Dispose();
            _overlay = null;
            PatchManager.UnapplyAll();

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private ConfigFile ResolveConfigFile()
        {
            string hsUnitId = UtilsArgu.Instance.Exists("hsunitid") ? UtilsArgu.Instance.Single("hsunitid") : string.Empty;
            PluginConfig.GlobalHsUnitId = hsUnitId ?? string.Empty;

            if (string.IsNullOrWhiteSpace(hsUnitId))
            {
                return Config;
            }

            string configDirectory = Path.Combine(BepInEx.Paths.ConfigPath, hsUnitId);
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, PluginInfo.PLUGIN_GUID + ".cfg");

            return new ConfigFile(
                configPath,
                true,
                new BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION));
        }

        private void ApplyCommandLineOverrides()
        {
            if (UtilsArgu.Instance.Exists("deckId"))
            {
                long deckId;
                if (long.TryParse(UtilsArgu.Instance.Single("deckId"), out deckId) && deckId > 0)
                {
                    PluginConfig.queueDeckId.Value = deckId;
                }
            }

            if (UtilsArgu.Instance.Exists("mode"))
            {
                QueueMode mode;
                if (PluginConfig.TryParseQueueMode(UtilsArgu.Instance.Single("mode"), out mode))
                {
                    PluginConfig.queueMode.Value = mode;
                }
            }

            if (UtilsArgu.Instance.Exists("matchPath"))
            {
                PluginConfig.matchLogPath.Value = UtilsArgu.Instance.Single("matchPath");
            }

            ApplyBoolOverride("autoqueue", PluginConfig.autoQueueEnabled);
            ApplyBoolOverride("autobattle", PluginConfig.autoBattleEnabled);

            if (UtilsArgu.Instance.Exists("afk"))
            {
                int afkMode;
                if (int.TryParse(UtilsArgu.Instance.Single("afk"), out afkMode))
                {
                    if (afkMode <= 0)
                    {
                        PluginConfig.autoQueueEnabled.Value = false;
                        PluginConfig.autoBattleEnabled.Value = false;
                    }
                    else
                    {
                        PluginConfig.autoQueueEnabled.Value = true;
                        PluginConfig.autoBattleEnabled.Value = true;
                    }
                }
            }
        }

        private void ApplyBoolOverride(string key, ConfigEntry<bool> entry)
        {
            if (!UtilsArgu.Instance.Exists(key))
            {
                return;
            }

            string rawValue = UtilsArgu.Instance.Single(key);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            bool parsed;
            if (bool.TryParse(rawValue, out parsed))
            {
                entry.Value = parsed;
                return;
            }

            int numeric;
            if (int.TryParse(rawValue, out numeric))
            {
                entry.Value = numeric != 0;
            }
        }
    }
}
