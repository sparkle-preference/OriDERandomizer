using System;
using System.Reflection;
using Core;
using Game;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public static class BashAttackGameGameFinishedPatch
{
    public static void Postfix()
    {
        if (RandomizerRebinding.DoubleBash.Pressed && !Randomizer.BashWasQueued)
        {
            Randomizer.QueueBash = true;
        }

        Randomizer.BashWasQueued = false;
    }

    static MethodBase TargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("BashAttackGame"), "GameFinished");
    }
}

[HarmonyPatch]
public static class BashAttackUpdatePlayingStatePatch
{
    public static void Postfix(object __instance)
    {
        if (RandomizerRebinding.DoubleBash.Pressed && Randomizer.BashTap)
        {
            AccessTools.TypeByName("BashAttackGame").GetMethod("GameFinished")?.Invoke(__instance, new object[0]);
        }
    }

    static MethodBase TargetMethod()
    {
        return AccessTools.Method(AccessTools.TypeByName("BashAttackGame"), "UpdatePlayingState");
    }
}