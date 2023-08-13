using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RandoExts;
using Logger = BFModLoader.ModLoader.Logger;
using Object = UnityEngine.Object;

namespace OriBFRandomizer.patches
{
    public static class AreaMapUIExt
    {
        public static Dictionary<AreaMapUI, MessageBox> RandomizerTooltips =
            new Dictionary<AreaMapUI, MessageBox>();
    }


    [HarmonyPatch(typeof(AreaMapUI))]
    public static class AreaMapUIPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch("Awake")]
        public static void AwakePatch(AreaMapUI __instance)
        {
            if (AreaMapUIExt.RandomizerTooltips.ContainsKey(__instance)) return;
            var gameObject =
                Object.Instantiate(__instance.transform.FindChild("legend/player")
                    .gameObject);
            gameObject.transform.parent = __instance.transform.FindChild("legend");
            var tooltip = gameObject.GetComponent<MessageBox>();
            tooltip.MessageProvider = null;
            tooltip.OverrideText = "Unknown";
            AreaMapUIExt.RandomizerTooltips[__instance] = tooltip;
        }

        [HarmonyPatch("FixedUpdate")]
        [HarmonyPostfix]
        public static void FixedUpdatePatch(AreaMapUI __instance)
        {
            if (__instance.IsSuspended)
            {
                return;
            }

            if (!GameMapUI.Instance.IsVisible)
            {
                return;
            }

            __instance.Navigation.Advance();
            __instance.DebugNavigation.Advance();
            AccessTools.Method(typeof(AreaMapUI), "UpdatePlayerPositionMarker").Invoke(__instance, new object[0]);
            AccessTools.Method(typeof(AreaMapUI), "UpdateSoulFlamePositionMarker").Invoke(__instance, new object[0]);
            __instance.UpdateCurrentArea();
            if (!GameMapUI.Instance.ShowingObjective)
            {
                string text = string.Format("#{0}#: {1}\n{2}", __instance.ObjectiveMessageProvider,
                    RandomizerText.GetObjectiveText(), RandomizerText.MapFilterText);
                if (text.Count(c => c == '\n') > 1)
                {
                    text = "\n" + text;
                }

                __instance.ObjectiveText.SetMessage(new MessageDescriptor(text));
                __instance.ObjectiveText.gameObject.SetActive(true);
            }
            else
            {
                __instance.ObjectiveText.gameObject.SetActive(false);
            }

            if (GameMapTransitionManager.Instance.InAreaMapMode)
            {
                if (Core.Input.Legend.OnPressed)
                {
                    __instance.AreaMapLegend.Toggle();
                }

                if (RandomizerRebinding.ToggleMapMode.OnPressed)
                {
                    RandomizerSettings.CurrentFilter =
                        RandomizerSettings.CurrentFilter.Next();
                    __instance.IconManager.ShowAreaIcons();
                }
            }
        }
    }
}