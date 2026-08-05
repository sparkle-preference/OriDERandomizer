using Game;
using UnityEngine;

public class RandomizerChaosPoison : RandomizerChaosEffect {
    public override void Clear() {
        Countdown = 0;
    }

    public override void Start() {
        Randomizer.ShowChaosEffect("Poison");
        Countdown = Random.Range(1200, 3600);
        DamageRate = Random.Range(0.5f, 2f) * Characters.Sein.Mortality.Health.MaxHealth / Countdown;
    }

    public override void Update() {
        if (Countdown > 0) {
            Countdown--;
            Characters.Sein.Mortality.Health.LoseHealth(DamageRate);
            if (Characters.Sein.Mortality.Health.Amount <= 0f) {
                Characters.Sein.Mortality.DamageReciever.OnRecieveDamage(new Damage(1f, default, default, DamageType.Water, null));
            }

            if (Countdown == 0) {
                Clear();
            }
        }
    }

    public int Countdown;

    public float DamageRate;
}
