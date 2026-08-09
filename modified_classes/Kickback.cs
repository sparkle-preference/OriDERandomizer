using System;
using UnityEngine;

[Serializable]
public class Kickback {
    public float KickbackDuration {
        get { return KickbackCurve[KickbackCurve.length - 1].time; }
    }

    public Vector2 KickbackDirection { get; private set; }

    public float CurrentKickbackSpeed {
        get {
            if (m_kickbackTimeRemaining <= 0f) {
                return 0f;
            }

            return m_kickbackMultiplier * KickbackCurve.Evaluate(KickbackDuration - m_kickbackTimeRemaining);
        }
    }

    public Vector2 KickbackVector {
        get { return CurrentKickbackSpeed * KickbackDirection; }
    }

    public void ApplyKickback(float kickbackMultiplier) {
        m_kickbackMultiplier = kickbackMultiplier;
        m_kickbackTimeRemaining = KickbackDuration;
    }

    public void ApplyKickback(float kickbackMultiplier, Vector2 kickbackDirection) {
        ApplyKickback(kickbackMultiplier);
        KickbackDirection = kickbackDirection.normalized;
    }

    public void AdvanceTime() {
        m_kickbackTimeRemaining -= RandomizerBonusSkill.TimeScale(Time.deltaTime);
    }

    public void Stop() {
        m_kickbackTimeRemaining = 0f;
    }

    public AnimationCurve KickbackCurve;

    private float m_kickbackTimeRemaining;

    private float m_kickbackMultiplier;
}
