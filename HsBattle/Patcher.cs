using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

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
            if (PluginConfig.DisableIdleKickValue)
            {
                check = false;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InactivePlayerKicker), "Update")]
        public static void PatchInactivePlayerKickerUpdate()
        {
            if (PluginConfig.DisableIdleKickValue)
            {
                InactivePlayerKicker.Get()?.SetShouldCheckForInactivity(false);
            }
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AlertPopup), "Show")]
        public static bool PatchAlertPopupShow(ref UIBButton ___m_okayButton, ref UIBButton ___m_confirmButton, ref UIBButton ___m_cancelButton, ref AlertPopup.PopupInfo ___m_popupInfo)
        {
            if (!PluginConfig.AutoConfirmDialogsValue)
            {
                return true;
            }

            if (___m_popupInfo != null && ___m_popupInfo.m_text == GameStrings.Get("GLOBAL_RECONNECT_RECONNECTING_LOGIN"))
            {
                return true;
            }

            return !Utils.TryHandlePopup(___m_okayButton, ___m_confirmButton, ___m_cancelButton);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AlertPopup), "UpdateInfo")]
        public static void PatchAlertPopupUpdateInfo(ref UIBButton ___m_okayButton, ref UIBButton ___m_confirmButton, ref UIBButton ___m_cancelButton)
        {
            if (PluginConfig.AutoConfirmDialogsValue)
            {
                Utils.TryHandlePopup(___m_okayButton, ___m_confirmButton, ___m_cancelButton);
            }
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MulliganManager), "HandleGameStart")]
        public static IEnumerable<CodeInstruction> PatchHandleGameStart(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            int index = list.FindLastIndex(delegate (CodeInstruction instruction)
            {
                MethodInfo method = instruction.operand as MethodInfo;
                return instruction.opcode == OpCodes.Callvirt && method != null && method.Name == "ShouldSkipMulligan";
            });

            if (index <= 0)
            {
                return list;
            }

            index++;
            object originalTarget = list[index].operand;
            list.Insert(index++, new CodeInstruction(OpCodes.Brtrue_S, originalTarget));
            list.Insert(index++, new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(PluginConfig), nameof(PluginConfig.SkipHeroIntroValue))));
            return list;
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
