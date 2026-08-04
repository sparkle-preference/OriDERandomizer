public class ShowWorldMapObjectiveAction : PerformingAction
{
	public override void Perform(IContext context)
	{
	}

	public override void Stop()
	{
	}

	public override bool IsPerforming => m_isPerforming;

	public void OnFinish()
	{
		m_isPerforming = false;
	}

	public Objective Objective;

	private bool m_isPerforming;
}
