public class RandomizerPickupAction : ActionMethod {
    public void Awake() {
        if (LocationName != null) {
            return;
        }

        if (RandomizerLocationManager.LocationsByGuid.ContainsKey(MoonGuid)) {
            LocationName = RandomizerLocationManager.LocationsByGuid[MoonGuid].Name;
        }

        if (LocationName == null) {
            LocationName = "Unknown";
        }
    }

    public override void Perform(IContext context) {
        if (!Granted) {
            RandomizerLocationManager.GivePickup(MoonGuid);
            Granted = true;
        }
    }

    public override string GetNiceName() {
        return "Give randomized pickup " + LocationName;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref Granted);
    }

    public string LocationName;

    public bool Granted;
}
