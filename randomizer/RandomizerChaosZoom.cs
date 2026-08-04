using Game;
using UnityEngine;

public class RandomizerChaosZoom : RandomizerChaosEffect {
    public override void Clear() {
        Countdown = 0;
        Zooming = false;
        UI.Cameras.Current.OffsetController.AdditiveDefaultOffset = new Vector3(UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.x, UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.y, InitialOffset);
    }

    public override void Start() {
        Zooming = false;
        if (Countdown == 0) {
            InitialOffset = UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.z;
        }

        Countdown = Random.Range(360, 1200);
        var num = Random.Range(0, 16);
        if (num <= 5) {
            Randomizer.ShowChaosEffect("Zoom in");
            var z = Random.Range(-20f, -1f);
            UI.Cameras.Current.OffsetController.AdditiveDefaultOffset = new Vector3(UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.x, UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.y, z);
            return;
        }

        if (num <= 11) {
            Randomizer.ShowChaosEffect("Zoom out");
            var z2 = Random.Range(1f, 100f);
            UI.Cameras.Current.OffsetController.AdditiveDefaultOffset = new Vector3(UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.x, UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.y, z2);
            return;
        }

        if (num <= 13) {
            Randomizer.ShowChaosEffect("Zoom in");
            Zooming = true;
            ZoomRate = Random.Range(-20f, -1f) / Countdown;
            return;
        }

        if (num <= 15) {
            Randomizer.ShowChaosEffect("Zoom out");
            Zooming = true;
            ZoomRate = Random.Range(1f, 100f) / Countdown;
        }
    }

    public override void Update() {
        if (Countdown > 0) {
            if (Zooming) {
                UI.Cameras.Current.OffsetController.AdditiveDefaultOffset = new Vector3(UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.x, UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.y, UI.Cameras.Current.OffsetController.AdditiveDefaultOffset.z + ZoomRate);
            }

            Countdown--;
            if (Countdown == 0) {
                Clear();
            }
        }
    }

    public int Countdown;

    public float InitialOffset;

    public bool Zooming;

    public float ZoomRate;
}
