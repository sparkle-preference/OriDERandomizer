public class RandomizerLoadRuntimeSceneAction : ActionMethod {
    public override void Perform(IContext context) {
        if (this.UseSceneInitialValues) {
            GameController.Instance.RequireInitialValues = true;
        }
        GoToSceneController.Instance.GoToSceneAsync(this.SceneMetaData, null, this.CreateCheckpoint);
        InstantLoadScenesController.Instance.FreezeIfLoadingScenes();
    }

    public override string GetNiceName() {
        if (this.SceneMetaData != null) {
            return "Load scene: " + this.SceneMetaData.Scene;
        }
        return "Load scene: (null) ";
    }

    public RuntimeSceneMetaData SceneMetaData;

    public bool CreateCheckpoint;

    public bool UseSceneInitialValues;
}
