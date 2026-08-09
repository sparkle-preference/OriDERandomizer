using Game;

public class RandomizerGoingDirectionCondition : Condition {
    public override bool Validate(IContext context) {
        if (left) {
            return Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX < 0f;
        } else {
            return Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX > 0f;
        }
    }

    public RandomizerGoingDirectionCondition() {
    }

    public bool left = false;
}
