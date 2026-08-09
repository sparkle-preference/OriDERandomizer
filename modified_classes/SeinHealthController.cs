using UnityEngine;

public class SeinHealthController : SaveSerialize, ISeinReceiver {
    public void SetAmount(float amount) {
        Amount = amount;
        VisualMinAmount = amount;
        VisualMaxAmount = amount;
    }

    public void FixedUpdate() {
        VisualMinAmount = Mathf.MoveTowards(VisualMinAmount, (int)Amount, Time.deltaTime * 4f);
        VisualMaxAmount = Mathf.MoveTowards(VisualMaxAmount, (int)Amount, Time.deltaTime * 4f);
    }

    public float VisualMinAmountNormalized => VisualMinAmount / MaxHealth;

    public float VisualMaxAmountNormalized => VisualMaxAmount / MaxHealth;

    public int HealthUpgradesCollected => MaxHealth / 4 - 3;

    public void OnRespawn() {
        InstantiateUtility.Instantiate(RespawnEffect, m_sein.Transform.position, Quaternion.identity);
        m_sein.Mortality.DamageReciever.MakeInvincible(1f);
    }

    public void LoseHealth(int amount) {
        Amount -= amount;
        if (Amount < 0f) {
            Amount = 0f;
        }

        VisualMinAmount = Amount;
    }

    public void GainHealth(int amount) {
        if (Amount > MaxHealth) {
            return;
        }

        Amount += amount;
        Amount = Mathf.Min(MaxHealth, Amount);
        VisualMaxAmount = Amount;
    }

    public void GainMaxHeartContainer() {
        MaxHealth += 4;
        RestoreAllHealth();
    }

    public void RestoreAllHealth() {
        if (Amount < MaxHealth) {
            Amount = MaxHealth;
            VisualMaxAmount = Amount;
        }
    }

    public void TakeDamage(int amount) {
        Amount -= amount;
        Amount = Mathf.Max(0f, Amount);
        VisualMinAmount = Amount;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref Amount);
        ar.Serialize(ref MaxHealth);
        if (ar.Reading) {
            VisualMaxAmount = VisualMinAmount = Amount;
        }
    }

    public bool IsFull => Amount == MaxHealth;

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
    }

    public void GainHealth(float amount) {
        if (Amount > MaxHealth) {
            return;
        }

        Amount += amount;
        Amount = Mathf.Min(MaxHealth, Amount);
        VisualMaxAmount = Amount;
    }

    public void LoseHealth(float amount) {
        Amount -= amount;
        if (Amount < 0f) {
            Amount = 0f;
        }

        VisualMinAmount = Amount;
    }

    public float Amount;

    public int MaxHealth;

    public float VisualMinAmount;

    public float VisualMaxAmount;

    public GameObject RespawnEffect;

    private SeinCharacter m_sein;
}
