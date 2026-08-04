using Game;
using UnityEngine.Serialization;

public class RandomizerGoingDirectionCondition : Condition {
    public override bool Validate(IContext context) {
        if (Left) {
            return Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX < 0f;
        }

        return Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX > 0f;
    }

    [FormerlySerializedAs("left")] public bool Left;
}
