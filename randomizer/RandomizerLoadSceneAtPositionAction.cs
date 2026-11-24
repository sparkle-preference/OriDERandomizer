using System;
using Core;
using UnityEngine;

public class RandomizerLoadSceneAtPositionAction : ActionMethod {
    public override void Perform(IContext context) {
        Core.Scenes.Manager.AdditivelyLoadScenesAtPosition(Position, true, false, true);
    }

    public Vector3 Position;
}
