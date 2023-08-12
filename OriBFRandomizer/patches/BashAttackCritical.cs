using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(BashAttackCritical))]
    [HarmonyPatch("UpdateCriticalState")]
    public class BashAttackCriticalPatch
    {
        public static bool Prefix(BashAttackCritical __instance, float ___m_stateCurrentTime, Vector3 ___m_localScale)
        {
            __instance.transform.localScale = ___m_localScale + Vector3.one *
                Mathf.Sin(___m_stateCurrentTime * 6.2831855f / __instance.ShakePeriod) * __instance.ShakeAmount;
            __instance.GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_MaskTexture",
                new Vector2(0.5f * (float) (Mathf.RoundToInt(___m_stateCurrentTime * 15f) % 2), 0f));
            float num = __instance.CriticalDuration;
            if (RandomizerSettings.Controls.LongerBashAimTime)
            {
                num += 3.3f;
            }

            if (___m_stateCurrentTime > num)
            {
                __instance.ChangeState(BashAttackCritical.State.Failed);
            }

            return false;
        }
    }
}