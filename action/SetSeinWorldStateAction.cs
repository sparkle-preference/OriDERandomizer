using Sein.World;

public class SetSeinWorldStateAction : ActionMethod {
    public override void Perform(IContext context) {
        GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
        switch (State) {
            case WorldState.WaterPurified:
                Randomizer.Inventory.FinishedGinsoEscape = true;
                Randomizer.NeedGinsoEscapeCleanup = true;
                RandomizerLocationManager.GivePickup(MoonGuid);
                return;
            case WorldState.GumoFree:
                Events.GumoFree = IsTrue;
                return;
            case WorldState.SpiritTreeReached:
                Events.SpiritTreeReached = IsTrue;
                return;
            case WorldState.GinsoTreeKey:
                RandomizerLocationManager.GivePickup(MoonGuid);
                return;
            case (WorldState)4:
            case (WorldState)6:
                return;
            case WorldState.GinsoTreeEntered:
                Events.GinsoTreeEntered = IsTrue;
                return;
            case WorldState.WindRestored:
                RandomizerLocationManager.GivePickup(MoonGuid);
                return;
            case WorldState.GravityActivated:
                Events.GravityActivated = IsTrue;
                return;
            case WorldState.MistLifted:
                Events.MistLifted = IsTrue;
                return;
            case WorldState.ForlornRuinsKey:
                RandomizerLocationManager.GivePickup(MoonGuid);
                return;
            case WorldState.MountHoruKey:
                RandomizerLocationManager.GivePickup(MoonGuid);
                return;
            case WorldState.WarmthReturned:
                RandomizerLocationManager.GivePickup(MoonGuid);
                return;
            case WorldState.DarknessLifted:
                Events.DarknessLifted = IsTrue;
                return;
            default:
                return;
        }
    }

    public override string GetNiceName() {
        return "Set " + ActionHelper.GetName(State.ToString()) + " to " + ActionHelper.GetName(IsTrue.ToString());
    }

    public WorldState State;

    public bool IsTrue;
}
