using UnityEngine;

public class RammingRunningState : RammingEnemyState {
    public RammingRunningState(RammingEnemy rammingEnemy) : base(rammingEnemy) {
    }

    public override void OnEnter() {
        GroundEnemy.Animation.PlayLoop(RammingEnemy.Animations.Running);
        if (GroundEnemy.gameObject.activeInHierarchy) {
            GroundEnemy.PlaySound(RammingEnemy.Sounds.Run);
        }
    }

    public override void OnExit() {
        GroundEnemy.StopSound(RammingEnemy.Sounds.Run);
    }

    public override void UpdateState() {
        var accelerationDuration = RammingEnemy.Settings.AccelerationDuration;
        var runningSpeedMultipliedOverTime = RammingEnemy.Settings.RunningSpeedMultipliedOverTime;
        var runSpeed = RammingEnemy.Settings.RunSpeed;
        GroundEnemy.PlatformMovement.LocalSpeedX = RandomizerBonusSkill.TimeScale((!GroundEnemy.FaceLeft ? 1 : -1) * runSpeed * runningSpeedMultipliedOverTime.Evaluate(CurrentStateTime / accelerationDuration));
    }
}
