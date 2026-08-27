using System;
using Core;
using UnityEngine;

public class RandomizerMoveCameraAction : ActionMethod {
    public override void Perform(IContext context) {
        Game.UI.Cameras.Current.CameraTarget.SetTargetPosition(Position);
        Game.UI.Cameras.Current.MoveCameraToTargetInstantly(true);
    }

    public Vector3 Position;
}
