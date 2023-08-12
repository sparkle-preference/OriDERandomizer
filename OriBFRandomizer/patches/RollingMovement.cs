using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
	[HarmonyPatch(typeof(RollingMovement))]
	public static class RollingMovementPatches
	{
		[HarmonyPatch("FixedUpdate")]
		[HarmonyPostfix]
		static void FixedUpdatePostfix(Rigidbody ___m_rigidbody)
		{
			___m_rigidbody.velocity = RandomizerBonusSkill.TimeScale(___m_rigidbody.velocity);
		}
	}
}