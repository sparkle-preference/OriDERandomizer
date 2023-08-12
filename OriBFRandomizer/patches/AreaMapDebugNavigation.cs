using Game;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
	[HarmonyPatch(typeof(AreaMapDebugNavigation))]
	[HarmonyPatch("Advance")]
	public class AreaMapDebugNavigationPatches
	{
		
		public static void Postfix(AreaMapUI ___m_areaMapUi)
		{
			if (XboxLiveController.IsContentPackage || !DebugMenuB.DebugControlsEnabled)
			{
				return;
			}

			if (MoonInput.GetKey(KeyCode.LeftShift) || MoonInput.GetKey(KeyCode.RightShift) ||
			    !Core.Input.RightClick.OnPressed) return;
			Vector2 cursorPositionUI = Core.Input.CursorPositionUI;
			Vector2 a = ___m_areaMapUi.Navigation.MapToWorldPosition(cursorPositionUI);
			if (Characters.Sein != null)
			{
				Characters.Sein.Position = a + new Vector2(0f, 0.5f);
				UI.Cameras.Current.MoveCameraToTargetInstantly(true);
				UI.Menu.HideMenuScreen(true);
				return;
			}
		}
	}
}
