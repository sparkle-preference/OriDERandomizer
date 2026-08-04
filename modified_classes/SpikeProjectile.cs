using UnityEngine;

public class SpikeProjectile : Projectile
{
	public new void FixedUpdate()
	{
		base.FixedUpdate();
		if (!IsSuspended)
		{
			Rigidbody.velocity = RandomizerBonusSkill.TimeScale(SpeedOverTimeCurve.Evaluate(CurrentTime) * Direction * Speed);
		}
	}

	public override bool CanBeBashed()
	{
		return false;
	}

	public AnimationCurve SpeedOverTimeCurve;
}
