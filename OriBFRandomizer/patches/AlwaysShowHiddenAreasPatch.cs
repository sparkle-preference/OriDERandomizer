using HarmonyLib;

namespace OriBFRandomizer.patches
{
    [HarmonyPatch(typeof(TransparentWallB))]
    [HarmonyPatch("get_HasSense")]
    public static class AlwaysShowHiddenAreasPatch
    {
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }    
}