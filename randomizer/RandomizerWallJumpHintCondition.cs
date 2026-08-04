using Game;

public class RandomizerWallJumpHintCondition : Condition {
    public override bool Validate(IContext context) {
        if (RandomizerSettings.Customization.HintLevel.Value == RandomizerSettings.HintLevels.Disabled) {
            return false;
        }

        if (Characters.Sein.PlayerAbilities.HasAbility(AbilityType.WallJump)) {
            return false;
        }

        if (Characters.Sein.PlayerAbilities.HasAbility(AbilityType.Climb)) {
            return false;
        }

        if (Characters.Sein.PlayerAbilities.HasAbility(AbilityType.ChargeJump)) {
            return false;
        }

        if (Characters.Sein.PlayerAbilities.HasAbility(AbilityType.Bash) && Characters.Sein.PlayerAbilities.HasAbility(AbilityType.Grenade)) {
            return false;
        }

        return true;
    }
}
