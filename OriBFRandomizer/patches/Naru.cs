using HarmonyLib;

namespace OriBFRandomizer.patches
{
	[HarmonyPatch(typeof(Naru))]
	[HarmonyPatch("OnDestroy")]
	public static class NaruPatches 
	{
		static bool Prefix()
		{
			Randomizer.onNaruDestroyed();
			return true;
		}
	}
}
