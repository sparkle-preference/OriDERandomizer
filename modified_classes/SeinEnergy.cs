using Core;
using Game;
using UnityEngine;

public class SeinEnergy : SaveSerialize
{
	public void SetCurrent(float current)
	{
		Current = current;
		MinVisual = Current;
		MaxVisual = Current;
	}

	public void NotifyOutOfEnergy()
	{
		UI.SeinUI.ShakeEnergyOrbBar();
		Sound.Play(OutOfEnergySound.GetSound(null), transform.position, null);
	}

	public bool CanAfford(float amount)
	{
		return Current >= amount;
	}

	public float VisualMin => MinVisual / Max;

	public float VisualMax => MaxVisual / Max;

	public void Gain(float amount)
	{
		if (Current > Max)
		{
			return;
		}
		Current += amount;
		if (Current > Max)
		{
			Current = Max;
		}
		MaxVisual = Current;
	}

	public void Spend(float amount)
	{
		Current -= amount;
		if (Current < 0f)
		{
			Current = 0f;
		}
		MinVisual = Current;
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref Current);
		ar.Serialize(ref Max);
		if (ar.Reading)
		{
			MinVisual = MaxVisual = Current;
		}
	}

	public bool EnergyActive => Max > 0f;

	public float VisualMaxNormalized => MaxVisual / Max;

	public float VisualMinNormalized => MinVisual / Max;

	public object EnergyUpgradesCollected => Max;

	public void Update()
	{
		MinVisual = Mathf.MoveTowards(MinVisual, (int)(Current * 4f) / 4f, Time.deltaTime);
		MaxVisual = Mathf.MoveTowards(MaxVisual, (int)(Current * 4f) / 4f, Time.deltaTime);
	}

	public void RestoreAllEnergy()
	{
		if (Current < Max)
		{
			Current = Max;
		}
	}

	public float MinVisual;

	public float MaxVisual;

	public float Current;

	public float Max = 3f;

	public SoundProvider OutOfEnergySound;
}
