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
			var num = ar.Serialize(0);
			Deaths.Clear();
			for (var i = 0; i < num; i++)
			{
				var deathInformation = new DeathInformation();
				deathInformation.Serialize(ar);
				Deaths.Add(deathInformation);
			}
			DeathWispsManager.Refresh();
			return;
		}
		var count = Deaths.Count;
		ar.Serialize(count);
		for (var j = 0; j < count; j++)
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
		var position = Characters.Sein.Position;
		var gameTimeInSeconds = GameController.Instance.GameTimeInSeconds;
		var completionPercentage = GameWorld.Instance.CompletionPercentage;
		var count = Deaths.Count;
		Deaths.Add(new DeathInformation(position, gameTimeInSeconds, completionPercentage, count));
		SaveSceneManager.Master.Save(Game.Checkpoint.SaveGameData.Master, Instance);
	}

	public static SeinDeathsManager Instance;

	public List<DeathInformation> Deaths = new List<DeathInformation>();
}
