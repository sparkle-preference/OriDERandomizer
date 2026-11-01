public class GameScheduler
{
	public UberDelegate<SceneRoot> OnSceneLoaded = new UberDelegate<SceneRoot>();

	public UberDelegate<SceneRoot> OnSceneRootLoadEarlyStart = new UberDelegate<SceneRoot>();

	public UberDelegate<SceneRoot> OnSceneStartLateBeforeSerialize = new UberDelegate<SceneRoot>();

	public UberDelegate<SceneRoot> OnSceneStartLateAfterSerialize = new UberDelegate<SceneRoot>();

	public UberDelegate<SceneRoot> OnSceneRootPreEnabled = new UberDelegate<SceneRoot>();

	public UberDelegate<SceneRoot> OnSceneRootEnabledAfterSerialize = new UberDelegate<SceneRoot>();

	public UberDelegate<SceneRoot> OnSceneRootDisabled = new UberDelegate<SceneRoot>();

	public UberDelegate OnGameAwake = new UberDelegate();

	public UberDelegate OnGameStart = new UberDelegate();

	public UberDelegate OnGameStartLate = new UberDelegate();

	public UberDelegate OnGameSerializeLoad = new UberDelegate();

	public UberDelegate OnPassThroughScrollLock = new UberDelegate();

	public UberDelegate OnGameFixedUpdate = new UberDelegate();

	public UberDelegate OnGameFixedUpdateLate = new UberDelegate();

	public UberDelegate OnPlayerDeath = new UberDelegate();

	public UberDelegate OnMenuOpen = new UberDelegate();

	public UberDelegate OnMenuClose = new UberDelegate();

	public UberDelegate OnGameReset = new UberDelegate();

	public UberDelegate OnGameLanguageChange = new UberDelegate();

	public UberDelegate OnGameControlSchemeChange = new UberDelegate();
}
