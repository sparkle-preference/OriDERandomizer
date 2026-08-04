using System;
using Game;
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomizerChaosColor : RandomizerChaosEffect
{
	public override void Clear()
	{
		Countdown = 0;
		Fading = false;
		if (Activated)
		{
			Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = new Color(InitialColor.r, InitialColor.g, InitialColor.b, 0.5f);
		}
	}

	public override void Start()
	{
		Activated = true;
		Fading = false;
		Countdown = Random.Range(600, 3600);
		InitialColor = Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color;
		if (Random.Range(0, 2) == 0)
		{
			Randomizer.showChaosEffect("Invisible Ori");
			Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = new Color(InitialColor.r, InitialColor.g, InitialColor.b, 0f);
			return;
		}
		Randomizer.showChaosEffect("Ghostly Ori");
		Fading = true;
		FadeRate = Random.Range(0.5f, 2f) / Countdown;
	}

	public override void Update()
	{
		if (Countdown > 0)
		{
			if (Fading)
			{
				Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = new Color(InitialColor.r, InitialColor.g, InitialColor.b, Math.Max(0f, Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color.a - FadeRate));
			}
			Countdown--;
			if (Countdown == 0)
			{
				Clear();
			}
		}
	}

	public int Countdown;

	public Color InitialColor;

	public float FadeRate;

	public bool Fading;

	public bool Activated;
}
