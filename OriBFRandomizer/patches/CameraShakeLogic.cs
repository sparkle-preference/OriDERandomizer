using System;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(CameraShakeLogic))]
[HarmonyPatch("UpdateOffset")]
public class CameraShakeLogicPatch
{
    public static void Postfix(CameraShakeLogic __instance)
    {
        __instance.Target.localPosition *= RandomizerSettings.Accessibility.CameraShakeFactor ?? 1f;
        __instance.Target.localEulerAngles *= RandomizerSettings.Accessibility.CameraShakeFactor ?? 1f;
    }
}