using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(GameMapTeleporters))]
    public static class GameMapTeleportersPatches
    {
        [HarmonyPatch("TeleporterUnderMouse")]
        [HarmonyPrefix]
        static bool RandoTpsPatch(ref int __result, GameMapTeleporters __instance)
        {
            __result = -1;
            if (__instance.Teleporters.Count <= 12)
            {
                if (GameMapTransitionManager.Instance.InWorldMapMode)
                {
                    for (int i = 0; i < __instance.Teleporters.Count; i++)
                    {
                        GameMapTeleporter gameMapTeleporter = __instance.Teleporters[i];
                        if (gameMapTeleporter.Activated &&
                            Vector3.Distance(Core.Input.CursorPositionUI, gameMapTeleporter.WorldMapIconPosition) < 1f)
                        {
                            __result = i;
                        }
                    }
                }

                if (GameMapTransitionManager.Instance.InAreaMapMode)
                {
                    for (int j = 0; j < __instance.Teleporters.Count; j++)
                    {
                        GameMapTeleporter gameMapTeleporter2 = __instance.Teleporters[j];
                        if (gameMapTeleporter2.Activated && Vector3.Distance(Core.Input.CursorPositionUI,
                                gameMapTeleporter2.AreaMapIconPosition) < 1f)
                        {
                            __result = j;
                        }
                    }
                }
            }
            else
            {
                float num = 1f;
                for (int k = 0; k < __instance.Teleporters.Count; k++)
                {
                    GameMapTeleporter gameMapTeleporter3 = __instance.Teleporters[k];
                    if (gameMapTeleporter3.Activated)
                    {
                        Vector2 v;
                        if (GameMapTransitionManager.Instance.InWorldMapMode)
                        {
                            v = new Vector2(gameMapTeleporter3.WorldMapIconPosition.x,
                                gameMapTeleporter3.WorldMapIconPosition.y - 0.125f);
                        }
                        else
                        {
                            v = new Vector2(gameMapTeleporter3.AreaMapIconPosition.x,
                                gameMapTeleporter3.AreaMapIconPosition.y - 0.125f);
                        }

                        float num2 = Vector3.Distance(Core.Input.CursorPositionUI, v);
                        if (num2 < num)
                        {
                            __result = k;
                            num = num2;
                        }
                    }
                }
            }

            return false;
        }
    }
}