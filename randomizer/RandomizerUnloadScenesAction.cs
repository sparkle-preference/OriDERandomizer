using Core;

public class RandomizerUnloadScenesAction : ActionMethod {
    public override void Perform(IContext context) {

        foreach (string sceneName in scenes) {
            var scene = Scenes.Manager.GetSceneManagerScene(sceneName);
            if (scene is not null) {
                Scenes.Manager.UnloadScene(scene, keepInMemory, unloadInstantly);
            }
        }
    }

    public string[] scenes;

    public bool unloadInstantly;

    public bool keepInMemory;
}
