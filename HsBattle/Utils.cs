using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace HsBattle
{
    public enum AlertPopupResponse
    {
        Confirm,
        Okay,
        Cancel
    }

    internal static class Utils
    {
        private static ManualLogSource _logSource;

        public static string GetWorkDirectory()
        {
            return Path.Combine(BepInEx.Paths.BepInExRootPath, PluginInfo.PLUGIN_NAME);
        }

        public static void EnsureWorkDirectory()
        {
            Directory.CreateDirectory(GetWorkDirectory());
        }

        public static string ResolvePath(string rawPath, string fallbackFileName)
        {
            EnsureWorkDirectory();

            string path = rawPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(GetWorkDirectory(), fallbackFileName);
            }
            else if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(GetWorkDirectory(), path);
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return path;
        }

        public static void MyLogger(LogLevel level, object message)
        {
            if (Plugin.Instance != null)
            {
                Plugin.Instance.PluginLogger.Log(level, message);
                return;
            }

            if (_logSource == null)
            {
                _logSource = new ManualLogSource(PluginInfo.PLUGIN_GUID + ".Fallback");
            }

            _logSource.Log(level, message);
        }

        public static void Quit(int exitCode)
        {
            try
            {
                Application.Quit(exitCode);
            }
            finally
            {
                try
                {
                    Process.GetCurrentProcess().Kill();
                }
                catch
                {
                }
            }
        }

        public static void OnHsLoginCompleted()
        {
            if (PluginConfig.DisableIdleKickValue)
            {
                InactivePlayerKicker.Get()?.SetShouldCheckForInactivity(false);
            }
        }

        public static bool TryHandlePopup(UIBButton okayButton, UIBButton confirmButton, UIBButton cancelButton)
        {
            if (!PluginConfig.AutoConfirmDialogsValue)
            {
                return false;
            }

            switch (PluginConfig.PopupResponseValue)
            {
                case AlertPopupResponse.Okay:
                    if (okayButton != null && okayButton.gameObject.activeInHierarchy)
                    {
                        okayButton.TriggerPress();
                        okayButton.TriggerRelease();
                        return true;
                    }
                    break;
                case AlertPopupResponse.Cancel:
                    if (cancelButton != null && cancelButton.gameObject.activeInHierarchy)
                    {
                        cancelButton.TriggerPress();
                        cancelButton.TriggerRelease();
                        return true;
                    }
                    break;
                default:
                    if (confirmButton != null && confirmButton.gameObject.activeInHierarchy)
                    {
                        confirmButton.TriggerPress();
                        confirmButton.TriggerRelease();
                        return true;
                    }

                    if (okayButton != null && okayButton.gameObject.activeInHierarchy)
                    {
                        okayButton.TriggerPress();
                        okayButton.TriggerRelease();
                        return true;
                    }
                    break;
            }

            return false;
        }

        public static void AppendMatchLog(TAG_PLAYSTATE playState)
        {
            string result = "UNKNOWN";
            switch (playState)
            {
                case TAG_PLAYSTATE.WON:
                case TAG_PLAYSTATE.WINNING:
                    result = "WIN";
                    break;
                case TAG_PLAYSTATE.CONCEDED:
                case TAG_PLAYSTATE.LOST:
                case TAG_PLAYSTATE.LOSING:
                    result = "LOSE";
                    break;
                case TAG_PLAYSTATE.TIED:
                    result = "TIE";
                    break;
            }

            string gameType = GameMgr.Get() != null ? GameMgr.Get().GetGameType().ToString() : "UNKNOWN";
            string line = string.Format(
                "{0},{1},{2},FRIENDLY={3},OPPOSING={4}",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                gameType,
                result,
                ResolveFriendlyName(),
                ResolveOpposingName());

            File.AppendAllText(
                ResolvePath(PluginConfig.MatchLogPathValue, "match.log"),
                line + Environment.NewLine,
                Encoding.UTF8);
        }

        private static string ResolveFriendlyName()
        {
            try
            {
                BnetPlayer player = BnetPresenceMgr.Get()?.GetMyPlayer();
                if (player?.GetBattleTag() != null)
                {
                    return player.GetBattleTag().ToString();
                }

                return player?.GetBestName() ?? "UNKNOWN";
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        private static string ResolveOpposingName()
        {
            try
            {
                Player opposing = GameState.Get()?.GetOpposingSidePlayer();
                if (opposing == null)
                {
                    return "UNKNOWN";
                }

                BnetPlayer bnetPlayer = opposing.GetBnetPlayer();
                if (bnetPlayer?.GetBattleTag() != null)
                {
                    return bnetPlayer.GetBattleTag().ToString();
                }

                BnetPlayer presencePlayer = BnetPresenceMgr.Get()?.GetPlayer(opposing.GetGameAccountId());
                if (presencePlayer?.GetBattleTag() != null)
                {
                    return presencePlayer.GetBattleTag().ToString();
                }

                string bestName = opposing.GetName();
                return string.IsNullOrWhiteSpace(bestName) ? "UNKNOWN" : bestName;
            }
            catch
            {
                return "UNKNOWN";
            }
        }
    }
}
