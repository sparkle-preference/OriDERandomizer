using Game;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(MessageControllerB))]
    public static class MessageControllerBPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(MessageControllerB.ShowMessageBoxB))]
        public static bool SkipPatch()
        {
            return Characters.Sein.IsSuspended;
        }

    }
}