using Game;

public class RandomizerGoingDirectionCondition : Condition {
    public override bool Validate(IContext context) {
        if (Left) {
            return Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX < 0f;
        }

        return Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX > 0f;
    }

    public bool Left;
}
