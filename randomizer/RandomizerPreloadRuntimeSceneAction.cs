using Core;

public class RandomizerPreloadRuntimeSceneAction : ActionMethod {
    public override void Perform(IContext context) {
        Scenes.Manager.PreloadScene(this.Scene);
    }

    public RuntimeSceneMetaData Scene;
}
