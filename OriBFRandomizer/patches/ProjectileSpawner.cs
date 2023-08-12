using System.Collections.Generic;
using HarmonyLib;

namespace OriBFRandomizer.patches
{
	public static class ProjectileSpawnerExt
	{
		public static Dictionary<ProjectileSpawner, float> OriginalDurations =
			new Dictionary<ProjectileSpawner, float>();
	}

	[HarmonyPatch(typeof(ProjectileSpawner))]
	public static class ProjectileSpawnerPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch("Start")]
		public static void StartPatch(ProjectileSpawner __instance, TimedTrigger ___m_timedTrigger)
		{
			if (___m_timedTrigger != null)
				ProjectileSpawnerExt.OriginalDurations[__instance] = ___m_timedTrigger.Duration;
		}
	
		[HarmonyPostfix]
		[HarmonyPatch("FixedUpdate")]
		public static void FixedUpdatePatch(ProjectileSpawner __instance, TimedTrigger ___m_timedTrigger)
		{
			if (ProjectileSpawnerExt.OriginalDurations.TryGetValue(__instance, out var ogDur))
			{
				___m_timedTrigger.Duration = ogDur / RandomizerBonusSkill.TimeScale(1f);
			}
		}
	}
}