using UnityEngine;

public class RandomizerChaosDamageModifier : RandomizerChaosEffect {
    public override void Clear() {
        Countdown = 0;
        Randomizer.DamageModifier = 1f;
    }

    public override void Start() {
        Countdown = Random.Range(360, 3600);
        int num = Random.Range(0, 8);
        if (num <= 3) {
            Randomizer.showChaosEffect("Damage vulnerability");
            Randomizer.DamageModifier = Random.Range(1.5f, 4f);
            return;
        }

        if (num <= 6) {
            Randomizer.showChaosEffect("Damage reduction");
            Randomizer.DamageModifier = Random.Range(0.25f, 0.8f);
            return;
        }

        if (num <= 7) {
            Randomizer.showChaosEffect("Invulnerability");
            Randomizer.DamageModifier = 0f;
        }
    }

    public override void Update() {
        if (Countdown > 0) {
            Countdown--;
            if (Countdown == 0) {
                Clear();
            }
        }
    }

    public int Countdown;
}
