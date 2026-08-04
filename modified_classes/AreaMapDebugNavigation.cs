using Game;
using UnityEngine;
using Input = Core.Input;

public class AreaMapDebugNavigation : MonoBehaviour
{
	public void Awake()
	{
		m_areaMapUi = GetComponent<AreaMapUI>();
	}

	public void Advance()
	{
		if (XboxLiveController.IsContentPackage)
		{
			return;
		}
		Input.ChargeJump.Used = true;
		if (!DebugMenuB.DebugControlsEnabled)
		{
			return;
		}
		if (Input.RightShoulder.OnPressed)
		{
			if (UndiscoveredMapVisible)
			{
				ToggleUndiscoveredMap(false);
			}
			else
			{
				ToggleUndiscoveredMap(true);
			}
		}
		if (!(MoonInput.GetKey(KeyCode.LeftShift) || MoonInput.GetKey(KeyCode.RightShift)) && Input.RightClick.OnPressed)
		{
			var cursorPosition = Input.CursorPositionUI;
			Vector2 worldPosition = m_areaMapUi.Navigation.MapToWorldPosition(cursorPosition);
			if (Characters.Sein != null)
			{
				Characters.Sein.Position = worldPosition + new Vector2(0f, 0.5f);
				UI.Cameras.Current.MoveCameraToTargetInstantly();
				UI.Menu.HideMenuScreen(true);
			}
		}
	}

	public void ToggleUndiscoveredMap(bool show)
	{
		UndiscoveredMapVisible = show;
		m_areaMapUi.ResetMaps();
		m_areaMapUi.Navigation.UpdateScrollLimits();
	}

	public GameObject DebugSceneBoundsMarkerPrefab;

	public float HiddenColorAlpha;

	public float UndiscoveredColorAlpha = 0.2f;

	private AreaMapUI m_areaMapUi;

	public bool UndiscoveredMapVisible;
}
