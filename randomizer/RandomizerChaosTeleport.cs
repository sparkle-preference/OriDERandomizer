using Game;
using UnityEngine;

public class RandomizerChaosTeleport : RandomizerChaosEffect {
    public override void Clear() {
        Countdown = 0;
    }

    public override void Start() {
        Countdown = Random.Range(360, 3600);
        var num = Random.Range(0, 16);
        Randomizer.showChaosEffect("Teleportation");
        if (num <= 14) {
            TeleportCount = Random.Range(1, 10);
            return;
        }

        if (num <= 15) {
            Teleport(200f);
        }
    }

    public override void Update() {
        if (Countdown > 0) {
            if (Random.Range(0f, 1f) < TeleportCount / (float)Countdown) {
                Teleport(10f);
            }

            Countdown--;
            if (Countdown == 0) {
                Clear();
            }
        }
    }

    public void Teleport(float range) {
        Characters.Sein.Position = new Vector3(Characters.Sein.Position.x + Random.Range(-range, range), Characters.Sein.Position.y + Random.Range(-range, range));
        TeleportCount--;
    }

    public int Countdown;

    public int TeleportCount;
}
