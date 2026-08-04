public class RandomizerChaosSpawner : RandomizerChaosEffect
{
	public override void Clear()
	{
		Countdown = 0;
	}

	public override void Start()
	{
	}

	public override void Update()
	{
		if (Countdown > 0)
		{
			Countdown--;
			if (Countdown == 0)
			{
				Clear();
			}
		}
	}

	public int Countdown;
}
