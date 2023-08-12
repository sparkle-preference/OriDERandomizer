using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    public static class TeleporterControllerExt
    {
        public static void Activate(string identifier, bool natural)
        {
            if (natural)
            {
                RandomizerSyncManager.FoundTP(identifier);
            }

            BingoController.OnActivateTeleporter(identifier);
            foreach (GameMapTeleporter gameMapTeleporter in TeleporterController.Instance.Teleporters)
            {
                if (gameMapTeleporter.Identifier == identifier)
                {
                    gameMapTeleporter.Activated = true;
                }
            }
        }

        public static void CancelTeleport(this TeleporterController _this)
        {
            Randomizer.IsUsingRandomizerTeleportAnywhere = false;
            AccessTools.Field(typeof(TeleporterController), "m_isTeleporting").SetValue(_this, false);
            AccessTools.Field(typeof(TeleporterController), "m_isBlooming").SetValue(_this, false);
            var sound =
                AccessTools.Field(typeof(TeleporterController), "m_teleportingStartSound")
                    .GetValue(_this) as SoundPlayer;
            if (!InstantiateUtility.IsDestroyed(sound))
            {
                sound.FadeOut(0.1f, true);
                AccessTools.Field(typeof(TeleporterController), "m_teleportingStartSound").SetValue(_this, null);
            }
        }

        public static void RemoveCustomTeleporters()
        {
            if (TeleporterController.Instance != null)
            {
                TeleporterController.Instance.Teleporters.RemoveAll((GameMapTeleporter teleporter) =>
                    teleporter.Name.GetType() == typeof(RandomizerMessageProvider));
            }
        }

        public static void AddCustomTeleporter(string name, float warpX, float warpY)
        {
            if (TeleporterController.Instance == null)
            {
                return;
            }

            for (int i = 0; i < TeleporterController.Instance.Teleporters.Count; i++)
            {
                if (TeleporterController.Instance.Teleporters[i].Identifier == name)
                {
                    return;
                }
            }

            GameMapTeleporter item = GameMapTeleporterExt.GameMapTeleporter(name, warpX, warpY);
            TeleporterController.Instance.Teleporters.Add(item);
        }

        public static bool IsTeleporting
        {
            get
            {
                return !(TeleporterController.Instance == null) && (bool) AccessTools
                    .Field(typeof(TeleporterController), "m_isTeleporting").GetValue(TeleporterController.Instance);
            }
        }
    }

    [HarmonyPatch(typeof(TeleporterController))]
    public static class TeleporterControllerPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TeleporterController.BeginTeleportation))]
        public static void BeginTPHook(GameMapTeleporter selectedTeleporter)
        {
            if (selectedTeleporter.Area.Area.AreaNameString == "Forlorn Ruins")
            {
                Randomizer.NightBerryWarpPosition = selectedTeleporter.WorldPosition;
                Characters.Sein.Inventory.SetRandomizerItem(82, 1);
            }

            RandomizerHints.ShowTip();
            if (Characters.Sein.Abilities.Swimming.CurrentState != SeinSwimming.State.OutOfWater)
            {
                Characters.Sein.Abilities.Swimming.ChangeState(SeinSwimming.State.OutOfWater);
                Characters.Sein.Abilities.Swimming.HideBreathingUI();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TeleporterController.Activate))]
        public static bool ActivateHook(string identifier)
        {
            TeleporterControllerExt.Activate(identifier, true);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TeleporterController.OnGameReset))]
        public static bool ResetHook(TeleporterController __instance)
        {
            for (int i = 0; i < TeleporterController.Instance.Teleporters.Count; i++)
            {
                TeleporterController.Instance.Teleporters[i].Activated = false;
            }

            __instance.CancelTeleport();

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(TeleporterController.OnFadedToBlack))]
        public static void LastAltRHook()
        {
            if (Randomizer.IsUsingRandomizerTeleportAnywhere)
            {
                RandomizerBonusSkill.LastAltR = Characters.Sein.Position;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TeleporterController.OnFadedToBlack))]
        public static void TPAHook()
        {
            if (Randomizer.IsUsingRandomizerTeleportAnywhere)
            {
                RandomizerStatsManager.UsedTeleporter();
                if (Randomizer.IsUsingRandomizerTeleportAnywhere)
                {
                    int value = World.Events.Find(Randomizer.MistySim).Value;
                    if (value != 1 && value != 8)
                    {
                        World.Events.Find(Randomizer.MistySim).Value = 10;
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TeleporterController.OnFinishedTeleporting))]
        public static void TPFinishHook()
        {
            Randomizer.IsUsingRandomizerTeleportAnywhere = false;
            Characters.Ori.ChangeState(Ori.State.Hovering);
            Characters.Ori.EnableHoverWobbling = true;
            if (Characters.Sein.Abilities.SpiritFlame)
            {
                Characters.Sein.Abilities.SpiritFlame.RemoveLock("savePedestal");
            }
        }
    }
}