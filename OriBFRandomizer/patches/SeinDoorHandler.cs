using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch]
    public static class EntranceShufflePatches
    {

        private static bool _interceptPositionSet;
    
        [HarmonyPatch(typeof(SeinDoorHandler))]
        [HarmonyPatch(nameof(SeinDoorHandler.OnFadedToBlack))]
        [HarmonyPrefix]
        public static void DoorHook()
        {
            _interceptPositionSet = Randomizer.Entrance;
        }
    
        [HarmonyPatch(typeof(SeinCharacter))]
        [HarmonyPatch("set_Position")]
        [HarmonyPrefix]
        public static bool EntranceShuffle(Vector3 value)
        {
            if (!_interceptPositionSet)
                return true;
            _interceptPositionSet = false;
            Randomizer.EnterDoor(value);
            return false;
        }

    }
}