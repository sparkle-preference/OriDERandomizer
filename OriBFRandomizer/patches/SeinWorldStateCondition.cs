using HarmonyLib;
using Sein.World;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(SeinWorldStateCondition))]
    public static class SeinWorldStateConditionPatches
    {
        private enum OverrideEvents
        {
            None,
            GinsoDoor,
            WaterEscapeExit,
            FinishEscapeTrigger,
            False
        }

        [HarmonyPrefix]
        [HarmonyPatch("Validate")]
        public static bool InterceptRandomizerStates(SeinWorldStateCondition __instance, ref bool __result)
        {
            if (__instance.State != WorldState.WaterPurified)
                return true;

            OverrideEvents overrideEvent = OverrideEvents.None;
            GameObject surfaceColliders = null;
            GameObject blockingWall = null;

            if (__instance.gameObject.name == "openingGinsoTree")
            {
                overrideEvent = OverrideEvents.GinsoDoor;
            }
            else if (__instance.gameObject.name == "artAfter")
            {
                Transform transform = __instance.transform.FindChild("artAfter");
                Transform transform2 = transform.FindChild("surfaceColliders");
                Transform transform3 = transform.FindChild("blockingWall");
                if (transform2 && transform3)
                {
                    overrideEvent = OverrideEvents.WaterEscapeExit;
                    surfaceColliders = transform2.gameObject;
                    blockingWall = transform3.gameObject;
                }
            }
            else
            {
                if (__instance.gameObject.name == "artBefore" && __instance.transform.parent &&
                    __instance.transform.parent.name == "ginsoTreeWaterRisingEnd")
                {
                    overrideEvent = OverrideEvents.WaterEscapeExit;
                }
                else if (__instance.name == "objectiveSetupTrigger" && __instance.transform.parent &&
                         __instance.transform.parent.name == "*objectiveSetup" && __instance.transform.parent.parent &&
                         __instance.transform.parent.parent.name == "thornfeltSwampActTwoStart")
                {
                    overrideEvent = OverrideEvents.False;
                }

                if (__instance.name == "musiczones" && (__instance.transform.Find("musicZoneDuringRising") != null ||
                                                        (__instance.transform.parent &&
                                                         __instance.transform.parent.name ==
                                                         "ginsoTreeWaterRisingEnd")))
                {
                    overrideEvent = OverrideEvents.FinishEscapeTrigger;
                }

                if (__instance.name == "activator")
                {
                    if (__instance.transform.childCount == 1 && __instance.transform.GetChild(0).name == "container" &&
                        __instance.transform.GetChild(0).childCount == 1 &&
                        __instance.transform.GetChild(0).GetChild(0).name == "musicZoneHeartWaterRising")
                    {
                        overrideEvent = OverrideEvents.FinishEscapeTrigger;
                    }
                    else if (__instance.transform.childCount == 1 &&
                             __instance.transform.GetChild(0).name == "musicZoneWaterCleansed")
                    {
                        overrideEvent = OverrideEvents.FinishEscapeTrigger;
                    }
                    else if (__instance.transform.parent &&
                             __instance.transform.parent.name == "restoringHeartWaterRising")
                    {
                        overrideEvent = OverrideEvents.FinishEscapeTrigger;
                    }
                }
            }

            if (overrideEvent == OverrideEvents.None)
            {
                __result = Events.WaterPurified;
            }
            else if (overrideEvent == OverrideEvents.GinsoDoor)
            {
                __result = false;
            }
            else if (overrideEvent == OverrideEvents.WaterEscapeExit)
            {
                bool finishedGinsoEscape = Randomizer.Inventory.FinishedGinsoEscape;
                surfaceColliders.SetActive(finishedGinsoEscape);
                blockingWall.SetActive(finishedGinsoEscape);
                if (finishedGinsoEscape)
                {
                    __result = finishedGinsoEscape;
                }
                else
                {
                    __result = Events.WaterPurified;
                }
            }
            else
            {
                if (overrideEvent == OverrideEvents.FinishEscapeTrigger)
                {
                    __result = Randomizer.Inventory.FinishedGinsoEscape;
                }
                else
                {
                    __result = overrideEvent != OverrideEvents.False && Events.WaterPurified;
                }
            }

            return false;
        }
    }
}