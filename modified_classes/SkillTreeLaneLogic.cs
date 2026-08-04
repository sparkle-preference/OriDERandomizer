using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeLaneLogic : SaveSerialize {
    public float Index => index;

    public void OnEnable() {
        UpdateItems(true);
        foreach (var skillItem in Skills) {
            skillItem.LargeIconColor = LargeIconColor;
        }
    }

    public void FixedUpdate() {
        UpdateItems(false);
    }

    public void UpdateItems(bool instant) {
        var firstUnlearnedIndex = 0;
        var totalPointsNeeded = 0;
        var totalHardPointsNeeded = 0;
        for (var i = 0; i < Skills.Count; i++) {
            var skillItem = Skills[i];
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
        index = !instant ? Mathf.MoveTowards(index, firstUnlearnedIndex, Time.deltaTime * 3f) : firstUnlearnedIndex;
        SkillEarntAnimator.Initialize();
        SkillEarntAnimator.SampleValue(index, true);
        if (!laneAchievedAwarded && HasAllSkills) {
            OnSkillTreeDoneEvent(Type);
            laneAchievedAwarded = true;
        }
    }

    public bool HasAllSkills {
        get {
            var result = true;
            for (var i = 0; i < Skills.Count; i++) {
                if (!Skills[i].HasSkillItem) {
                    result = false;
                    break;
                }
            }

            return result;
        }
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref laneAchievedAwarded);
    }

    public BaseAnimator SkillEarntAnimator;

    public List<SkillItem> Skills = new List<SkillItem>();

    private float index;

    public Color LargeIconColor;

    public SkillTreeType Type;

    private bool laneAchievedAwarded;

    public static Action<SkillTreeType> OnSkillTreeDoneEvent = delegate { };

    public enum SkillTreeType {
        Energy,
        Utility,
        Combat,
    }
}
