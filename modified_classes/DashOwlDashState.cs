using UnityEngine;

public class DashOwlDashState : DashOwlState {
    public DashOwlDashState(DashOwlEnemy dashOwl) : base(dashOwl) {
    }

    public override void OnEnter() {
        dashTargetOffset = (DashOwl.Controller.LastSeenSeinPosition - DashOwl.transform.position).normalized * DashOwl.Settings.DashDistance;
        DashOwl.DashSound.Play();
        DashOwl.Animation.Play(DashOwl.Animations.Dash);
        DashOwl.SpriteRotation.RotateTowardsTarget(DashOwl.PositionToPlayerPosition, DashOwl.FaceLeft);
    }

    public override void OnExit() {
        DashOwl.SpriteRotation.RotateBackToNormal();
    }

    public override void UpdateState() {
        DashOwl.FlyMovement.Kickback.Stop();
        var a = dashTargetOffset * (DashOwl.Settings.DashCurve.Evaluate(CurrentStateTime + RandomizerBonusSkill.TimeScale(Time.deltaTime)) - DashOwl.Settings.DashCurve.Evaluate(CurrentStateTime));
        DashOwl.FlyMovement.Velocity = Time.deltaTime != 0f ? a / RandomizerBonusSkill.TimeScale(Time.deltaTime) : Vector3.zero;
        base.UpdateState();
    }

    private Vector3 dashTargetOffset;
}
