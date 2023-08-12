using Game;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    public static class UIExt
    {
        public static void HideExistingHint(bool force, HintLayer m_currentLayer, ref MessageBox m_currentHint)
        {
            if (m_currentLayer == (HintLayer) 5 && !force)
            {
                return;
            }

            if (!m_currentHint) return;
            m_currentHint.Visibility.HideMessageScreenImmediately();
            m_currentHint = null;
        }
    }


    [HarmonyPatch(typeof(UI.Hints))]
    public static class UIPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(UI.Hints.HideExistingHint))]
        public static bool HideExistingHintPatch(HintLayer ___m_currentLayer, ref MessageBox ___m_currentHint)
        {
            UIExt.HideExistingHint(false, ___m_currentLayer, ref ___m_currentHint);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(UI.Hints.Show))]
        public static void ShowRandomizerHintsDifferentlyPatch(HintLayer layer, MessageBox __result)
        {
            if (!__result)
                return;

            if (layer == (HintLayer) 5)
                __result.transform.position = new Vector3(UI.Hints.HintPosition.x, UI.Hints.HintPosition.y, -7f);
        }
    }
}