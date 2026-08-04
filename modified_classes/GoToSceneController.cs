using System;
using Core;
using Game;
using UnityEngine;

public class GoToSceneController : MonoBehaviour
{
	public ScenesManager ScenesManager => Scenes.Manager;

	public static bool CheckStartInScene(MoonGuid guid)
	{
		return Instance == null || Instance.StartInScene == guid || Instance.StartInScene == MoonGuid.Empty;
	}

	public void Awake()
	{
		Instance = this;
	}

	public void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void GoToScene(MoonGuid sceneGuid, Vector3 position, string sceneName, Action onComplete, bool createCheckpoint, bool async)
	{
		StartInScene = sceneGuid;
		if (sceneName == "titleScreenSwallowsNest")
		{
			GameStateMachine.Instance.SetToStartScreen();
		}
		m_onCompleteLoad = onComplete;
		m_position = position;
		ScenesManager.SetTargetPositions(m_position);
		InstantLoadScenesController.Instance.LoadScenesAtPosition(null, async);
		m_createCheckpointLater = createCheckpoint;
		m_useAfterSceneLoad = true;
		ScenesManager.AllowUnloadingOnScenes(position);
	}

	private void FinishGoingToPositionImmediately()
	{
		UI.Cameras.Current.MoveCameraToTargetInstantly(false);
		ScenesManager.UnloadScenesAtPosition(true);
		ScenesManager.AutoLoadingUnloading = true;
		if (m_onCompleteImmediateLoad != null)
		{
			m_onCompleteImmediateLoad();
			m_onCompleteImmediateLoad = null;
		}
		UI.Cameras.Current.Controller.UpdateCamera();
	}

	public void OnScenesEnabled()
	{
		if (m_useAfterSceneLoad)
		{
			m_useAfterSceneLoad = false;
			CompleteGoingToAScene();
		}
	}

	public void CompleteGoingToAScene()
	{
		if (Characters.Current != null)
		{
			Characters.Current.Position = m_position;
			Characters.Current.PlaceOnGround();
		}
		UI.Cameras.Current.CameraTarget.SetTargetPosition(m_position);
		UI.Cameras.Current.Controller.PuppetController.Reset();
		UI.Cameras.Current.GoToChaseMode();
		UI.Cameras.Current.MoveCameraToTargetInstantly(false);
		UI.Cameras.Current.OffsetController.UpdateOffset(true);
		UI.Cameras.Current.MoveCameraToTargetInstantly(false);
		if (Characters.Ori)
		{
			Characters.Ori.MoveOriBackToPlayer();
		}
	}

	public void OnInstantLoadScenesControllerCompletedLoading()
	{
		if (m_onCompleteLoad != null)
		{
			m_onCompleteLoad();
			m_onCompleteLoad = null;
		}
		UI.Cameras.Current.Controller.UpdateCamera();
		if (m_createCheckpointLater)
		{
			m_createCheckpointLater = false;
			GameController.Instance.CreateCheckpoint();
			GameController.Instance.SaveGameController.PerformSave();
			GameController.Instance.PerformSaveGameSequence();
		}
	}

	public void OnScenesManagerFixedUpdate()
	{
		if (m_isMovingImmediately)
		{
			m_isMovingImmediately = false;
			ScenesManager.SetTargetPositions(m_position);
			ScenesManager.AutoLoadingUnloading = false;
			ScenesManager.EnableDisabledScenesAtPosition();
			CompleteGoingToAScene();
			LateStartHook.AddLateStartMethod(FinishGoingToPositionImmediately);
		}
	}

	public void GoToScene(SceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint)
	{
		GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.SeinPlaceholderPosition, sceneMetaData.name, onComplete, createCheckpoint, false);
	}

	public void GoToScene(RuntimeSceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint)
	{
		GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.PlaceholderPosition, sceneMetaData.Scene, onComplete, createCheckpoint, false);
	}

	public void GoToSceneAsync(SceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint)
	{
		GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.SeinPlaceholderPosition, sceneMetaData.name, onComplete, createCheckpoint, true);
	}

	public void GoToSceneAsync(RuntimeSceneMetaData sceneMetaData, Action onComplete, bool createCheckpoint)
	{
		GoToScene(sceneMetaData.SceneMoonGuid, sceneMetaData.PlaceholderPosition, sceneMetaData.Scene, onComplete, createCheckpoint, true);
	}

	public void GoToSceneImmediately(SceneMetaData scene, Action onComplete)
	{
		StartInScene = scene.SceneMoonGuid;
		m_position = scene.SeinPlaceholderPosition;
		m_onCompleteImmediateLoad = onComplete;
		m_isMovingImmediately = true;
	}

	public void GoToScene(string path)
	{
		var sceneInformation = Scenes.Manager.GetSceneInformation(path);
		if (sceneInformation == null)
		{
			Randomizer.LogError("Bad scene path: " + path);
		}
		else
		{
			GoToScene(sceneInformation, null, true);
		}
	}

	public static GoToSceneController Instance;

	public MoonGuid StartInScene;

	private Vector3 m_position;

	private bool m_useAfterSceneLoad;

	private bool m_createCheckpointLater;

	private Action m_onCompleteLoad;

	private Action m_onCompleteImmediateLoad;

	private bool m_isMovingImmediately;
}
