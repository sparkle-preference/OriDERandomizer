using Game;
using UnityEngine;

public class RandomizerChaosVelocityVector : RandomizerChaosEffect {
    public override void Clear() {
        Countdown = 0;
        Pushing = false;
    }

    public override void Start() {
        Countdown = Random.Range(1, 300);
        Pushing = false;
        int num = Random.Range(0, 8);
        if (num <= 5) {
            Randomizer.showChaosEffect("Throw");
            Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = Random.Range(-100f, 100f);
            Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = Random.Range(-100f, 100f);
            return;
        }

        if (num <= 7) {
            Randomizer.showChaosEffect("Push");
            Pushing = true;
            Push = new Vector2(Random.Range(-40f, 40f), Random.Range(-40f, 40f));
        }
    }

    public override void Update() {
        if (Countdown > 0) {
            Countdown--;
            if (Countdown == 0) {
                Clear();
            }

            if (Pushing) {
                Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedX = Push.x;
                Characters.Sein.PlatformBehaviour.PlatformMovement.LocalSpeedY = Push.y;
            }
        }
    }

    public int Countdown;

    public bool Pushing;

    public Vector2 Push;
}
