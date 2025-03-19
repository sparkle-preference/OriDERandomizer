using System;
using Game;

public class NewGameAction : ActionMethod
{
	public override void Perform(IContext context)
	{
		Game.Checkpoint.SaveGameData = new SaveGameData();
		try {
			Randomizer.initialize();
			Randomizer.showSeedInfo();
			RandomizerStatsManager.Activate();
		} catch(Exception e) {
			Randomizer.LogError("New Game Action: " + e.Message);
		}
		GameController.Instance.RequireInitialValues = true;
		GameStateMachine.Instance.SetToGame();
	}
}
