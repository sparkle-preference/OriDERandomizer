using UnityEngine;

[Category("BaseAnimator")]
public class BaseAnimatorAction : ActionMethod
{
	public new void Start()
	{
		base.Start();
		if (AnimatorsMode == FindAnimatorsMode.GameObject)
		{
			Animators = Target.GetComponents<BaseAnimator>();
		}
		if (AnimatorsMode == FindAnimatorsMode.GameObjectAndChildren)
		{
			Animators = Target.GetComponentsInChildren<BaseAnimator>();
		}
	}

	public override void Perform(IContext context)
	{
		for (var i = 0; i < Animators.Length; i++)
		{
			var baseAnimator = Animators[i];
			if (baseAnimator.enabled)
			{
				baseAnimator.Initialize();
				switch (Command)
				{
				case PlayMode.Restart:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.SetForward();
					baseAnimator.AnimatorDriver.Restart();
					break;
				case PlayMode.RestartReversed:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.SetBackwards();
					baseAnimator.AnimatorDriver.Restart();
					break;
				case PlayMode.Reverse:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.Reverse();
					break;
				case PlayMode.Stop:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.Stop();
					break;
				case PlayMode.Continue:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.Resume();
					break;
				case PlayMode.ContinueForward:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.SetForward();
					baseAnimator.AnimatorDriver.Resume();
					break;
				case PlayMode.ContinueReversed:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.SetBackwards();
					baseAnimator.AnimatorDriver.Resume();
					break;
				case PlayMode.StopAtStart:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.Pause();
					baseAnimator.AnimatorDriver.GoToStart();
					break;
				case PlayMode.StopAtEnd:
					baseAnimator.Initialize();
					baseAnimator.AnimatorDriver.Pause();
					baseAnimator.AnimatorDriver.GoToEnd();
					break;
				}
			}
		}
	}

	public override void PerformInstantly(IContext context)
	{
		foreach (var baseAnimator in Animators)
		{
			if (baseAnimator.enabled)
			{
				baseAnimator.Initialize();
				switch (Command)
				{
				case PlayMode.Restart:
					baseAnimator.AnimatorDriver.GoToEnd();
					break;
				case PlayMode.RestartReversed:
					baseAnimator.AnimatorDriver.GoToStart();
					break;
				case PlayMode.Reverse:
					baseAnimator.AnimatorDriver.GoToStart();
					break;
				case PlayMode.Stop:
					baseAnimator.AnimatorDriver.Stop();
					break;
				case PlayMode.Continue:
					if (baseAnimator.AnimatorDriver.IsReversed)
					{
						baseAnimator.AnimatorDriver.GoToStart();
					}
					else
					{
						baseAnimator.AnimatorDriver.GoToEnd();
					}
					break;
				case PlayMode.ContinueForward:
					baseAnimator.AnimatorDriver.GoToEnd();
					break;
				case PlayMode.ContinueReversed:
					baseAnimator.AnimatorDriver.GoToStart();
					break;
				case PlayMode.StopAtStart:
					baseAnimator.AnimatorDriver.GoToStart();
					break;
				case PlayMode.StopAtEnd:
					baseAnimator.AnimatorDriver.GoToEnd();
					break;
				}
			}
		}
	}

	private string TargetName => AnimatorsMode != FindAnimatorsMode.SpecifyAnimators ? !Target ? "unkown" : Target.name : Animators.Length <= 0 || !Animators[0] ? "unkown" : Animators[0].name;

	public override string GetNiceName()
	{
		switch (Command)
		{
		case PlayMode.Restart:
			return "Restart " + TargetName + " BaseAnimator";
		case PlayMode.RestartReversed:
			return "Restart reversed " + TargetName + " BaseAnimator";
		case PlayMode.Reverse:
			return "Reverse " + TargetName + " BaseAnimator";
		case PlayMode.Stop:
			return "Stop " + TargetName + " BaseAnimator";
		case PlayMode.Continue:
			return "Continue " + TargetName + " BaseAnimator";
		case PlayMode.ContinueForward:
			return "Continue forward " + TargetName + " BaseAnimator";
		case PlayMode.ContinueReversed:
			return "Continue reversed " + TargetName + " BaseAnimator";
		case PlayMode.StopAtStart:
			return "Stop at start " + TargetName + " BaseAnimator";
		case PlayMode.StopAtEnd:
			return "Stop at end " + TargetName + " BaseAnimator";
		default:
			return base.GetNiceName();
		}
	}

	[NotNull]
	public GameObject Target;

	public FindAnimatorsMode AnimatorsMode;

	public PlayMode Command;

	public BaseAnimator[] Animators;

	public enum PlayMode
	{
		Restart,
		RestartReversed,
		Reverse,
		Stop,
		Continue,
		ContinueForward,
		ContinueReversed,
		StopAtStart,
		StopAtEnd
	}

	public enum FindAnimatorsMode
	{
		GameObject,
		GameObjectAndChildren,
		SpecifyAnimators
	}
}
