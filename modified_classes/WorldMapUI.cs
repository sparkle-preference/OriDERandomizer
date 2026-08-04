using System;
using Game;
using UnityEngine;

public class WorldMapUI : MonoBehaviour
{
	public static bool IsReady => Instance != null;

	public static bool UseCameraSettings => !(Instance == null) && Instance.m_enabled;

	public void OnEnable()
	{
		m_enabled = true;
	}

	public void OnDisable()
	{
		m_enabled = false;
	}

	public static CameraSettings CameraSettings
	{
		get
		{
			if (Instance == null)
			{
				return null;
			}
			if (Instance.m_cameraSettings == null)
			{
				Instance.m_cameraSettings = new CameraSettings(Instance.CameraSettingsAsset, Instance.Fog);
			}
			return Instance.m_cameraSettings;
		}
	}

	public Transform FadeOutGroup => CrossFade.transform;

	public void Awake()
	{
		Instance = this;
		transform.parent = GameMapUI.Instance.Group;
		transform.position = Vector3.zero;
		NavigationManager.OptionChangeCallback = (Action)Delegate.Combine(NavigationManager.OptionChangeCallback, new Action(OnMenuItemChange));
	}

	public void OnMenuItemChange()
	{
		if (m_ignoreNavigationMenuItemChange)
		{
			return;
		}
		var currentArea = CurrentArea;
		AreaMapUI.Instance.Navigation.ScrollPosition = currentArea.ScrollPosition;
		GameMapUI.Instance.CurrentHighlightedArea = GameWorld.Instance.FindRuntimeArea(currentArea.Area);
	}

	public void OnDestroy()
	{
		if (Instance == this)
		{
			DestroyObject(Instance);
		}
		NavigationManager.OptionChangeCallback = (Action)Delegate.Remove(NavigationManager.OptionChangeCallback, new Action(OnMenuItemChange));
	}

	public void Activate()
	{
		gameObject.SetActive(true);
		if (!GameMapUI.Instance.ShowingTeleporters)
		{
			ShowAreaSelection();
		}
		m_ignoreNavigationMenuItemChange = true;
		foreach (var worldMapOverworldArea in NavigationManager.GetComponentsInChildren<WorldMapOverworldArea>())
		{
			if (worldMapOverworldArea.Area == GameMapUI.Instance.CurrentHighlightedArea.Area)
			{
				NavigationManager.SetCurrentMenuItem(worldMapOverworldArea.GetComponent<CleverMenuItem>());
			}
		}
		m_ignoreNavigationMenuItemChange = false;
	}

	public void Deactivate()
	{
		gameObject.SetActive(false);
	}

	public float ZoomTime => AreaMapUI.Instance.Navigation.ZoomTime;

	public Vector3 ScrollPosition => AreaMapUI.Instance.Navigation.ScrollPosition;

	public Vector3 WorldToProjectedPosition(Vector3 position)
	{
		return WorldMapOverworldLogic.Instance.WorldToOverworld(position);
	}

	public Vector3 WorldToUIPosition(Vector3 position)
	{
		return WorldToScreenToUI(WorldToProjectedPosition(position));
	}

	public Vector3 ClosePosition => WorldToProjectedPosition(ScrollPosition) + Vector3.back * CloseZoom;

	public Vector3 FarPosition => Vector3.back * FullZoom + CameraOffset;

	public WorldMapOverworldArea CurrentArea => NavigationManager.CurrentMenuItem.GetComponent<WorldMapOverworldArea>();

	public void UpdateCameraPosition()
	{
		if (!GameMapUI.Instance.ShowingObjective)
		{
			if (!GameMapUI.Instance.RevealingMap)
			{
				if (GameMapUI.Instance.ShowingTeleporters)
				{
					Vector3 b = GameMapUI.Instance.Teleporters.SelectedTeleporter.WorldProjectedPositon;
					b.z = -2f;
					b.x *= 0.3f;
					b.y *= 0.1f;
					b.y -= 3f;
					CameraOffset = Vector3.Lerp(CameraOffset, b, 0.03f);
				}
				else if (NavigationManager.gameObject.activeSelf)
				{
					var position = CurrentArea.transform.position;
					position.z = 0f;
					var b2 = position * 0.2f;
					CameraOffset = Vector3.Lerp(CameraOffset, b2, 0.03f);
				}
			}
		}

		Vector3 position2;
		position2.x = Mathf.Lerp(FarPosition.x, ClosePosition.x, ZoomXYCurve.Evaluate(ZoomTime));
		position2.y = Mathf.Lerp(FarPosition.y, ClosePosition.y, ZoomXYCurve.Evaluate(ZoomTime));
		position2.z = Mathf.Lerp(FarPosition.z, ClosePosition.z, ZoomZCurve.Evaluate(ZoomTime));
		Camera.transform.position = position2;
	}

	public void FixedUpdate()
	{
		if (!GameMapUI.Instance.IsVisible)
		{
			return;
		}
		NavigationManager.IsActive = GameMapTransitionManager.Instance.InWorldMapMode;
		UpdateCameraPosition();
		if (Characters.Sein)
		{
			PlayerMarker.position = WorldToUIPosition(Characters.Sein.Position);
		}
	}

	public Vector3 WorldToScreenToUI(Vector3 position)
	{
		Vector2 v = Camera.WorldToScreenPoint(position);
		var camera = UI.Cameras.System.GUICamera.Camera;
		var result = camera.ScreenToWorldPoint(v);
		result.z = 0f;
		return result;
	}

	public void ShowAreaSelection()
	{
		NavigationManager.gameObject.SetActive(true);
	}

	public void HideAreaSelection()
	{
		NavigationManager.gameObject.SetActive(false);
	}

	public static void Initialize()
	{
		if (m_isLoadingWorldMapScene)
		{
			m_cancelLoading = false;
		}
		else
		{
			m_isLoadingWorldMapScene = true;
			Application.LoadLevelAdditiveAsync("worldMapScene");
		}
	}

	public static void OnFinishedLoading(SceneRoot sceneRoot)
	{
		if (m_cancelLoading)
		{
			DestroyObject(sceneRoot.gameObject);
		}
		else
		{
			sceneRoot.EarlyStart();
			DestroyObject(sceneRoot.GetComponent<SaveSceneManager>());
			DestroyObject(sceneRoot.GetComponent<SceneSettingsComponent>());
			DestroyObject(sceneRoot);
			sceneRoot.gameObject.SetActive(true);
		}
		m_isLoadingWorldMapScene = false;
		m_cancelLoading = false;
	}

	public static void CancelLoading()
	{
		if (m_isLoadingWorldMapScene)
		{
			m_cancelLoading = true;
		}
	}

	public static WorldMapUI Instance;

	public Transform ProjectionPlane;

	public Transform PlayerMarker;

	public bool Activated;

	public float FullZoom = 20f;

	public float CloseZoom = 10f;

	public Camera Camera;

	public TransparencyAnimator CrossFade;

	public CleverMenuItemSelectionManager NavigationManager;

	public CameraSettingsAsset CameraSettingsAsset;

	public FogGradientController Fog;

	public AnimationCurve ZoomXYCurve;

	public AnimationCurve ZoomZCurve;

	public Vector3 CameraOffset;

	private CameraSettings m_cameraSettings;

	private bool m_enabled;

	private bool m_ignoreNavigationMenuItemChange;

	private static bool m_isLoadingWorldMapScene;

	private static bool m_cancelLoading;
}
