using System;
using UnityEngine;

[Serializable]
public class Kickback {
    public float KickbackDuration => KickbackCurve[KickbackCurve.length - 1].time;

    public Vector2 KickbackDirection { get; private set; }

    public float CurrentKickbackSpeed {
        get {
            if (kickbackTimeRemaining <= 0f) {
                return 0f;
            }

            return kickbackMultiplier * KickbackCurve.Evaluate(KickbackDuration - kickbackTimeRemaining);
        }
    }

    public Vector2 KickbackVector => CurrentKickbackSpeed * KickbackDirection;

    public void ApplyKickback(float kickbackMultiplier) {
        this.kickbackMultiplier = kickbackMultiplier;
        kickbackTimeRemaining = KickbackDuration;
    }

    public void ApplyKickback(float kickbackMultiplier, Vector2 kickbackDirection) {
        ApplyKickback(kickbackMultiplier);
        KickbackDirection = kickbackDirection.normalized;
    }

    public void AdvanceTime() {
        kickbackTimeRemaining -= RandomizerBonusSkill.TimeScale(Time.deltaTime);
    }

    public void Stop() {
        kickbackTimeRemaining = 0f;
    }

    public AnimationCurve KickbackCurve;

    private float kickbackTimeRemaining;

    private float kickbackMultiplier;
}
