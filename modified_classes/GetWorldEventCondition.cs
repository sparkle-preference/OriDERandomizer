using System.Collections.Generic;
using Game;

public class GetWorldEventCondition : Condition
{
	public override bool Validate(IContext context)
	{
		if (WorldEvents.UniqueID == 26 && Randomizer.Inventory.FinishedGinsoEscape)
		{
			return State != 21;
		}
		int value = World.Events.Find(WorldEvents).Value;
		if (States.Count == 0)
		{
			return State == value;
		}
		for (int i = 0; i < States.Count; i++)
		{
			int num = States[i];
			if (value == num)
			{
				return true;
			}
		}
		return false;
	}

	public WorldEvents WorldEvents;

	public int State;

	public List<int> States;
}
