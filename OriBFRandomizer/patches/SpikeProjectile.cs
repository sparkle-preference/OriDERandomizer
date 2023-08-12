using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
	[HarmonyPatch(typeof(SpikeProjectile))]
	public static class SpikeProjectilePatches
	{
		[HarmonyPatch("FixedUpdate")]
		[HarmonyPostfix]
		static void FixedUpdatePostfix(Rigidbody ___Rigidbody)
		{
			___Rigidbody.velocity = RandomizerBonusSkill.TimeScale(___Rigidbody.velocity);
		}
	}
}