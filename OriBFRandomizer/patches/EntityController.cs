using fsm;
using fsm.triggers;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
	[HarmonyPatch(typeof(EntityController))]

	public static class EntityControllerPatches
	{
		[HarmonyPatch("FixedUpdate")]
		[HarmonyPrefix]
		static bool FixedUpdatePrefix(EntityController __instance, ref TransitionManager ___m_transManager)
		{
			if (__instance.Entity.IsSuspended)
			{
				return false;
			}
			if (___m_transManager == null)
			{
				___m_transManager = __instance.StateMachine.GetTransistionManager<OnFixedUpdate>();
			}
			if (___m_transManager == null)
			{
				return false;
			}
			float num = Time.deltaTime;
			if (__instance.Entity is Enemy)
			{
				num = RandomizerBonusSkill.TimeScale(num);
			}
			__instance.StateMachine.UpdateState(num);
			__instance.StateMachine.CurrentTrigger = null;
			___m_transManager.Process(__instance.StateMachine);
			return false;
		}
	}
}