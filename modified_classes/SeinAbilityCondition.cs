using Core;
using Game;

public class SeinAbilityCondition : Condition {
    public override bool Validate(IContext context) {
        if (Characters.Sein != null) {
            if (Ability == AbilityType.Stomp) {
                if (Randomizer.Inventory.FinishedGinsoEscape && Scenes.Manager.CurrentScene != null) {
                    string scene = Scenes.Manager.CurrentScene.Scene;
                    if (scene == "ginsoTreeTurrets")
                        return true;
                    if (scene == "kuroMomentTreeDuplicate")
                        return false;
                }

                if (Randomizer.OpenWorld) {
                    return false;
                }

                if (!Randomizer.StompTriggers) {
                    return true;
                }
            }

            return Characters.Sein.PlayerAbilities.HasAbility(Ability);
        }

        return false;
    }

    public AbilityType Ability;
}
