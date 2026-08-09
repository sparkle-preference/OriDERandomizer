using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeLaneLogic : SaveSerialize {
    public float Index {
        get { return m_index; }
    }

    public void OnEnable() {
        UpdateItems(true);
        foreach (SkillItem skillItem in Skills) {
            skillItem.LargeIconColor = LargeIconColor;
        }
    }

    public void FixedUpdate() {
        UpdateItems(false);
    }

    public void UpdateItems(bool instant) {
        int firstUnlearnedIndex = 0;
        int totalPointsNeeded = 0;
        int totalHardPointsNeeded = 0;
        for (int i = 0; i < Skills.Count; i++) {
            SkillItem skillItem = Skills[i];
            if (!skillItem.HasSkillItem) {
                if (firstUnlearnedIndex == 0) {
                    firstUnlearnedIndex = i + 1;
                }

                totalPointsNeeded += skillItem.RequiredSkillPoints;
                totalHardPointsNeeded += skillItem.RequiredHardSkillPoints;
                skillItem.TotalRequiredSkillPoints = totalPointsNeeded;
                skillItem.TotalRequiredHardSkillPoints = totalHardPointsNeeded;
            }
        }

        --firstUnlearnedIndex;
        m_index = ((!instant) ? Mathf.MoveTowards(m_index, firstUnlearnedIndex, Time.deltaTime * 3f) : firstUnlearnedIndex);
        SkillEarntAnimator.Initialize();
        SkillEarntAnimator.SampleValue(m_index, true);
        if (!m_laneAchievedAwarded && HasAllSkills) {
            OnSkillTreeDoneEvent(Type);
            m_laneAchievedAwarded = true;
        }
    }

    public bool HasAllSkills {
        get {
            bool result = true;
            for (int i = 0; i < Skills.Count; i++) {
                if (!Skills[i].HasSkillItem) {
                    result = false;
                    break;
                }
            }

            return result;
        }
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_laneAchievedAwarded);
    }

    public BaseAnimator SkillEarntAnimator;

    public List<SkillItem> Skills = new List<SkillItem>();

    private float m_index;

    public Color LargeIconColor;

    public SkillTreeType Type;

    private bool m_laneAchievedAwarded;

    public static Action<SkillTreeType> OnSkillTreeDoneEvent = delegate { };

    public enum SkillTreeType {
        Energy,
        Utility,
        Combat
    }
}
