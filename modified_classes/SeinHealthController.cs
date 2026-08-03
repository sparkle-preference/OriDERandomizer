using System;
using UnityEngine;

public class SeinHealthController : SaveSerialize, ISeinReceiver
{
	public void SetAmount(float amount)
	{
		this.Amount = amount;
		this.VisualMinAmount = amount;
		this.VisualMaxAmount = amount;
	}

	public void FixedUpdate()
	{
		this.VisualMinAmount = Mathf.MoveTowards(this.VisualMinAmount, (float)((int)this.Amount), Time.deltaTime * 4f);
		this.VisualMaxAmount = Mathf.MoveTowards(this.VisualMaxAmount, (float)((int)this.Amount), Time.deltaTime * 4f);
	}

	public float VisualMinAmountNormalized
	{
		get
		{
			return this.VisualMinAmount / (float)this.MaxHealth;
		}
	}

	public float VisualMaxAmountNormalized
	{
		get
		{
			return this.VisualMaxAmount / (float)this.MaxHealth;
		}
	}

	public int HealthUpgradesCollected
	{
		get
		{
			return this.MaxHealth / 4 - 3;
		}
	}

	public void OnRespawn()
	{
		InstantiateUtility.Instantiate(this.RespawnEffect, this.m_sein.Transform.position, Quaternion.identity);
		this.m_sein.Mortality.DamageReciever.MakeInvincible(1f);
	}

	public void LoseHealth(int amount)
	{
		this.Amount -= (float)amount;
		if (this.Amount < 0f)
		{
			this.Amount = 0f;
		}
		this.VisualMinAmount = this.Amount;
	}

	public void GainHealth(int amount)
	{
		if (this.Amount > (float)this.MaxHealth)
		{
			return;
		}
		this.Amount += (float)amount;
		this.Amount = Mathf.Min((float)this.MaxHealth, this.Amount);
		this.VisualMaxAmount = this.Amount;
	}

	public void GainMaxHeartContainer()
	{
		this.MaxHealth += 4;
		this.RestoreAllHealth();
	}

	public void RestoreAllHealth()
	{
		if (this.Amount < (float)this.MaxHealth)
		{
			this.Amount = (float)this.MaxHealth;
			this.VisualMaxAmount = this.Amount;
		}
	}

	public void TakeDamage(int amount)
	{
		this.Amount -= (float)amount;
		this.Amount = Mathf.Max(0f, this.Amount);
		this.VisualMinAmount = this.Amount;
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref this.Amount);
		ar.Serialize(ref this.MaxHealth);
		if (ar.Reading)
		{
			this.VisualMaxAmount = (this.VisualMinAmount = this.Amount);
		}
	}

	public bool IsFull
	{
		get
		{
			return this.Amount == (float)this.MaxHealth;
		}
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		this.m_sein = sein;
	}

	public void GainHealth(float amount)
	{
		if (this.Amount > (float)this.MaxHealth)
		{
			return;
		}
		this.Amount += amount;
		this.Amount = Mathf.Min((float)this.MaxHealth, this.Amount);
		this.VisualMaxAmount = this.Amount;
	}

	public void LoseHealth(float amount)
	{
		this.Amount -= amount;
		if (this.Amount < 0f)
		{
			this.Amount = 0f;
		}
		this.VisualMinAmount = this.Amount;
	}

	public float Amount;

	public int MaxHealth;

	public float VisualMinAmount;

	public float VisualMaxAmount;

	public GameObject RespawnEffect;

	private SeinCharacter m_sein;
}
