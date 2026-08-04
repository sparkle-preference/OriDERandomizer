using System;

public class SeinInventory : SaveSerialize
{
	public event Action OnCollectKeystones = delegate
	{
	};

	public event Action OnCollectMapstone = delegate
	{
	};

	public bool HasKeystones => Keystones != 0;

	public bool HasMapstones => MapStones != 0;

	public bool CanAfford(int cost)
	{
		return Keystones >= cost;
	}

	public void SpendKeystones(int cost)
	{
		Keystones -= cost;
		if (Keystones < 0)
		{
			Keystones = 0;
		}
	}

	public void SpendMapstone(int cost)
	{
		MapStones -= cost;
		if (MapStones < 0)
		{
			MapStones = 0;
		}
	}

	public void CollectKeystones(int amount)
	{
		Keystones += amount;
		OnCollectKeystones();
	}

	public void CollectMapstone(int amount)
	{
		MapStones += amount;
		OnCollectMapstone();
	}

	public void RestoreKeystones(int amount)
	{
		CollectKeystones(amount);
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref Keystones);
		ar.Serialize(ref MapStones);
		ar.Serialize(ref SkillPointsCollected);
	}

	public int GetRandomizerItem(int code)
	{
		return Randomizer.Inventory.GetRandomizerItem(code);
	}

	public int SetRandomizerItem(int code, int value)
	{
		return Randomizer.Inventory.SetRandomizerItem(code, value);
	}

	public int IncRandomizerItem(int code, int value)
	{
		return Randomizer.Inventory.IncRandomizerItem(code, value);
	}

	public int Keystones;

	public int MapStones;

	public int SkillPointsCollected;
}
