using System.Collections.Generic;
using Game;
using UnityEngine;

public class SeinDeathsManager : SaveSerialize
{
	[ContextMenu("Fake a death here")]
	public void FakeADeathHere()
	{
		RecordDeath();
	}

	public override void Awake()
	{
		base.Awake();
		Instance = this;
		Events.Scheduler.OnGameReset.Add(OnGameReset);
	}

	public override void OnDestroy()
	{
		base.OnDestroy();
		if (Instance == this)
		{
			Instance = null;
		}
		Events.Scheduler.OnGameReset.Remove(OnGameReset);
	}

	public void OnGameReset()
	{
		Deaths.Clear();
	}

	public override void Serialize(Archive ar)
	{
		if (ar.Reading)
		{
			int num = ar.Serialize(0);
			Deaths.Clear();
			for (int i = 0; i < num; i++)
			{
				DeathInformation deathInformation = new DeathInformation();
				deathInformation.Serialize(ar);
				Deaths.Add(deathInformation);
			}
			DeathWispsManager.Refresh();
			return;
		}
		int count = Deaths.Count;
		ar.Serialize(count);
		for (int j = 0; j < count; j++)
		{
			Deaths[j].Serialize(ar);
		}
	}

	public static void OnDeath()
	{
		Randomizer.OnDeath();
		if (Instance && DifficultyController.Instance.Difficulty == DifficultyMode.OneLife)
		{
			Instance.Deaths.Clear();
			Instance.RecordDeath();
		}
	}

	public void RecordDeath()
	{
		Vector3 position = Characters.Sein.Position;
		int gameTimeInSeconds = GameController.Instance.GameTimeInSeconds;
		int completionPercentage = GameWorld.Instance.CompletionPercentage;
		int count = Deaths.Count;
		Deaths.Add(new DeathInformation(position, gameTimeInSeconds, completionPercentage, count));
		SaveSceneManager.Master.Save(Game.Checkpoint.SaveGameData.Master, Instance);
	}

	public static SeinDeathsManager Instance;

	public List<DeathInformation> Deaths = new List<DeathInformation>();
}
