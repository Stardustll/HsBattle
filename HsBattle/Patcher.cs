using HarmonyLib;
using System;
using System.Reflection;

namespace HsBattle
{
    public static class PatchManager
    {
        private static Harmony _harmony;

        public static void ApplyAll()
        {
            if (_harmony != null)
            {
                return;
            }

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void UnapplyAll()
        {
            if (_harmony == null)
            {
                return;
            }

            _harmony.UnpatchSelf();
            _harmony = null;
        }
    }

    public static class Patches
    {
        private static readonly MethodInfo RankChangeOnClickMethod = AccessTools.Method(typeof(RankChangeTwoScoop_NEW), "OnClick");
        private static readonly MethodInfo RankedBonusStarsPopupHideMethod = AccessTools.Method(typeof(RankedBonusStarsPopup), "Hide");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LoginManager), "OnLoginComplete")]
        public static void PatchOnLoginComplete()
        {
            Utils.OnHsLoginCompleted();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InactivePlayerKicker), "SetShouldCheckForInactivity")]
        public static void PatchSetShouldCheckForInactivity(ref bool check)
        {
            check = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InactivePlayerKicker), "Update")]
        public static void PatchInactivePlayerKickerUpdate()
        {
            InactivePlayerKicker.Get()?.SetShouldCheckForInactivity(false);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DialogManager), "ShowReconnectHelperDialog")]
        [HarmonyPatch(typeof(ReconnectHelperDialog), "Show")]
        [HarmonyPatch(typeof(Network), "OnFatalBnetError")]
        public static bool PatchFatalErrorDialog()
        {
            if (!PluginConfig.AutoExitOnErrorValue)
            {
                return true;
            }

            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle exiting after a fatal reconnect error.");
            Utils.Quit(1);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameEntity), "ShowEndGameScreen")]
        public static void PatchShowEndGameScreen(ref TAG_PLAYSTATE playState)
        {
            try
            {
                Utils.AppendMatchLog(playState);
                Plugin.Instance?.Controller?.NotifyGameEnded();
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RankChangeTwoScoop_NEW), "EnableClickToContinue")]
        public static void PatchRankChangeEnableClickToContinue(RankChangeTwoScoop_NEW __instance)
        {
            if (!ShouldAutoSkipPostGameUi())
            {
                return;
            }

            InvokeUiAction(__instance, RankChangeOnClickMethod, "HsBattle auto-continued ranked summary.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RankedBonusStarsPopup), "Show")]
        public static void PatchRankedBonusStarsPopupShow(RankedBonusStarsPopup __instance)
        {
            if (!ShouldAutoSkipPostGameUi())
            {
                return;
            }

            InvokeUiAction(__instance, RankedBonusStarsPopupHideMethod, "HsBattle auto-closed ranked bonus stars popup.");
        }

        private static bool ShouldAutoSkipPostGameUi()
        {
            BattleController controller = Plugin.Instance?.Controller;
            return controller != null
                ? controller.ShouldAutoSkipPostGameUi()
                : PluginConfig.AutoQueueEnabledValue;
        }

        private static void InvokeUiAction(object instance, MethodInfo method, string successMessage)
        {
            if (instance == null || method == null)
            {
                return;
            }

            try
            {
                method.Invoke(instance, null);
                Utils.MyLogger(BepInEx.Logging.LogLevel.Info, successMessage);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "HsBattle could not auto-handle ranked post-game UI: " + ex.Message);
            }
        }

        public static class AntiCheat
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(AntiCheatSDK.AntiCheatManager), "OnLoginComplete")]
            public static bool PatchAntiCheatManagerOnLoginComplete()
            {
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AntiCheatSDK.AntiCheatManager), "Shutdown")]
            public static bool PatchAntiCheatManagerShutdown()
            {
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AntiCheatSDK.AntiCheatManager), "TryCallSDK")]
            [HarmonyPatch(typeof(AntiCheatSDK.AntiCheatManager), "CallInterfaceCallSDK")]
            public static bool PatchAntiCheatManagerTryCallSdk()
            {
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(AntiCheatSDK.AntiCheatManager), "InnerSDKMethodCall")]
            public static bool PatchAntiCheatManagerInnerSdkMethodCall()
            {
                return false;
            }
        }
    }
}
