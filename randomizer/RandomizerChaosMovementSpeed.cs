using Game;
using UnityEngine;

public class RandomizerChaosMovementSpeed : RandomizerChaosEffect
{
	public override void Clear()
	{
		Countdown = 0;
		Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Acceleration = 60f;
		Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Decceleration = 30f;
		Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed = 11.6666f;
		Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Acceleration = 26f;
		Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Decceleration = 26f;
		Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.MaxSpeed = 11.6666f;
	}

	public override void Start()
	{
		Countdown = Random.Range(360, 3600);
		int num = Random.Range(0, 16);
		if (num <= 7)
		{
			float num2 = Random.Range(0.5f, 2f);
			if (num2 < 1f)
			{
				Randomizer.showChaosEffect("Slow movement");
			}
			else
			{
				Randomizer.showChaosEffect("Fast movement");
			}
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Acceleration = 60f * num2;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Decceleration = 30f * num2;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed = 11.6666f * num2;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Acceleration = 26f * num2;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Decceleration = 26f * num2;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.MaxSpeed = 11.6666f * num2;
			return;
		}
		if (num <= 12)
		{
			float num3 = Random.Range(8f, 16f);
			Randomizer.showChaosEffect("Icy ground");
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Acceleration = 60f / num3;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Decceleration = 30f / num3;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed = 11.6666f * num3 / 8f;
			return;
		}
		if (num <= 14)
		{
			float num4 = Random.Range(1.5f, 3f);
			Randomizer.showChaosEffect("Drag racer Ori");
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed = 11.6666f * num4;
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.MaxSpeed = 11.6666f * num4;
			return;
		}
		if (num == 15)
		{
			Randomizer.showChaosEffect("Strange movement");
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Acceleration = 60f * Random.Range(0.05f, 4f);
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Decceleration = 30f * Random.Range(0.05f, 4f);
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed = 11.6666f * Random.Range(0.25f, 4f);
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Acceleration = 26f * Random.Range(0.05f, 4f);
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Decceleration = 26f * Random.Range(0.05f, 4f);
			Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.MaxSpeed = 11.6666f * Random.Range(0.25f, 4f);
		}
	}

	public override void Update()
	{
		if (Countdown == 0)
		{
			Clear();
		}
		Countdown--;
		if (Countdown < -600)
		{
			Countdown = 0;
		}
	}

	public int Countdown;
}
