public class RandomizerResetSceneAction : ActionMethod {
    public override void Perform(IContext context) {
        sceneRoot.SaveSceneManager.LoadWithMurderousIntent(saveScene);
    }
    public SaveScene saveScene;
    public SceneRoot sceneRoot;
}
