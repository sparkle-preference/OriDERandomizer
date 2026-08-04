using System.Collections.Generic;
using UnityEngine;

public static class RandomizerChaosManager
{
	public static void initialize()
	{
		Countdown = Random.Range(300, 1800);
		Effects = new List<RandomizerChaosEffect>();
		Frequencies = new List<int>();
		Effects.Add(new RandomizerChaosMovementSpeed());
		Frequencies.Add(19);
		Effects.Add(new RandomizerChaosGravity());
		Frequencies.Add(13);
		Effects.Add(new RandomizerChaosTeleport());
		Frequencies.Add(5);
		Effects.Add(new RandomizerChaosZoom());
		Frequencies.Add(5);
		Effects.Add(new RandomizerChaosPoison());
		Frequencies.Add(1);
		Effects.Add(new RandomizerChaosColor());
		Frequencies.Add(5);
		Effects.Add(new RandomizerChaosDamageModifier());
		Frequencies.Add(5);
		Effects.Add(new RandomizerChaosVelocityVector());
		Frequencies.Add(11);
	}

	public static void Update()
	{
		Countdown--;
		if (Countdown <= 0)
		{
			SpawnEffect();
			Countdown = Random.Range(300, 900);
		}
		for (var i = 0; i < Effects.Count; i++)
		{
			Effects[i].Update();
		}
	}

	public static void ClearEffects()
	{
		for (var i = 0; i < Effects.Count; i++)
		{
			Effects[i].Clear();
		}
	}

	public static void SpawnEffect()
	{
		var num = 0;
		var num2 = Random.Range(0, 64);
		for (var i = 0; i < Effects.Count; i++)
		{
			num += Frequencies[i];
			if (num > num2)
			{
				Effects[i].Start();
				return;
			}
		}
	}

	public static int Countdown;

	public static List<RandomizerChaosEffect> Effects;

	public static List<int> Frequencies;
}
