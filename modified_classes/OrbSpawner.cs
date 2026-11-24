using Game;
using UnityEngine;

public class OrbSpawner : MonoBehaviour {
    public void CopySettings(OrbSpawner other) {
        NumberOfExpOrbs = other.NumberOfExpOrbs;
        NumberOfGreenOrbs = other.NumberOfGreenOrbs;
        NumberOfBlueOrbs = other.NumberOfBlueOrbs;
        NumberOfRedOrbs = other.NumberOfRedOrbs;
        NumberOfYellowOrbs = other.NumberOfYellowOrbs;
    }

    public void Awake() {
        m_transform = transform;
    }

    public void SetNumberOfExpOrbs(int amount) {
        NumberOfYellowOrbs = amount / 250;
        amount -= NumberOfYellowOrbs * 250;
        NumberOfRedOrbs = amount / 50;
        amount -= NumberOfRedOrbs * 50;
        NumberOfBlueOrbs = amount / 10;
        amount -= NumberOfBlueOrbs * 10;
        NumberOfGreenOrbs = amount / 1;
    }

    private float DamageDirectionSpeed => 2f;

    public void SpawnOrbs(IContext context) {
        if (NumberOfGreenOrbs == 0 && NumberOfBlueOrbs == 0 && NumberOfRedOrbs == 0 && NumberOfYellowOrbs == 0) {
            SetNumberOfExpOrbs(NumberOfExpOrbs);
        }

        var vector = Vector2.zero;
        var damageContext = context as IDamageContext;
        if (damageContext != null) {
            var damageContext2 = damageContext;
            vector = damageContext2.Damage.Force;
        }

        var num = 0;
        var i = 0;
        while (i < NumberOfGreenOrbs) {
            SpawnPickup(OrbSpawnerManager.ItemType.GreenExpOrb, vector, num);
            i++;
            num++;
        }

        var j = 0;
        while (j < NumberOfBlueOrbs) {
            SpawnPickup(OrbSpawnerManager.ItemType.BlueExpOrb, vector, num);
            j++;
            num++;
        }

        var k = 0;
        while (k < NumberOfRedOrbs) {
            SpawnPickup(OrbSpawnerManager.ItemType.RedExpOrb, vector, num);
            k++;
            num++;
        }

        var l = 0;
        while (l < NumberOfYellowOrbs) {
            SpawnPickup(OrbSpawnerManager.ItemType.YellowExpOrb, vector, num);
            l++;
            num++;
        }

        if (DifficultyController.Instance.Difficulty == DifficultyMode.Easy) {
            if (!SpawnLoot(0)) {
                SpawnLoot(1);
            }
        } else {
            SpawnLoot(0);
        }
    }

    private bool SpawnLoot(int attempt) {
        var heartChance = LootSettings.HeartChance;
        var energyChance = LootSettings.EnergyShardChance;

        if (Characters.Sein && Characters.Sein.PlayerAbilities.MapMarkers.HasAbility) {
            heartChance *= 1.5f;
            energyChance *= 1.5f;

            if (heartChance + energyChance is var sum and > 1f) {
                heartChance /= sum;
                energyChance /= sum;
            }
        }

        var num = FixedRandom.ValueFromPosition(transform.position + new Vector3(attempt, 0, 0));
        if (LootSettings.EnergyShardChance <= 0.5f && LootSettings.HeartChance <= 0.5f && !LootOnHard && DifficultyController.Instance.Difficulty == DifficultyMode.Hard) {
            return false;
        }

        if (num < heartChance) {
            OrbSpawnerManager.Instance.Spawn(OrbSpawnerManager.ItemType.Health, m_transform.position, Vector2.zero, DropPickup.State.Hover);
            return true;
        }

        if (num < heartChance + energyChance && Characters.Sein.Energy.EnergyActive) {
            OrbSpawnerManager.Instance.Spawn(OrbSpawnerManager.ItemType.Energy, m_transform.position, Vector2.zero, DropPickup.State.Hover);
            return true;
        }

        return false;
    }

    private void SpawnPickup(OrbSpawnerManager.ItemType item, Vector2 force, int i) {
        var vector = new Vector2(HorizontalSpeed.Evaluate(FixedRandom.Values[i % FixedRandom.Values.Length]), VerticalSpeed.Evaluate(FixedRandom.Values[(i + 1) % FixedRandom.Values.Length]));
        vector += force * DamageDirectionSpeed;
        OrbSpawnerManager.Instance.Spawn(item, m_transform.position, vector, DropPickupState);
    }

    public void LimitNumberOfOrbs(int i) {
        var num = 0;
        var num2 = 0;
        var num3 = 0;
        var num4 = 0;
        for (var j = 0; j < i; j++) {
            if (NumberOfYellowOrbs > 0) {
                NumberOfYellowOrbs--;
                num3++;
            } else if (NumberOfRedOrbs > 0) {
                NumberOfRedOrbs--;
                num4++;
            } else if (NumberOfGreenOrbs > 0) {
                NumberOfGreenOrbs--;
                num++;
            } else if (NumberOfBlueOrbs > 0) {
                NumberOfBlueOrbs--;
                num2++;
            }
        }

        NumberOfRedOrbs = num4;
        NumberOfGreenOrbs = num;
        NumberOfBlueOrbs = num2;
        NumberOfYellowOrbs = num3;
    }

    public DropLootSettings LootSettings = new();

    public bool LootOnHard;

    private Transform m_transform;

    public int NumberOfExpOrbs;

    public int NumberOfGreenOrbs;

    public int NumberOfBlueOrbs;

    public int NumberOfRedOrbs;

    public int NumberOfYellowOrbs;

    public AnimationCurve HorizontalSpeed;

    public AnimationCurve VerticalSpeed;

    public DropPickup.State DropPickupState;
}
