using System.Collections.Generic;
using RandoExts;
using UnityEngine;

public static class RandomizerText {
    public static RandomizerMessageProvider GetAbilityName(AbilityType ability) {
        if (!m_abilityOverrides.ContainsKey(ability)) {
            return null;
        }

        return m_abilityOverrides[ability].NameOverride;
    }

    public static RandomizerMessageProvider GetAbilityDescription(AbilityType ability) {
        if (!m_abilityOverrides.ContainsKey(ability)) {
            return null;
        }

        return m_abilityOverrides[ability].DescriptionOverride;
    }

    public static string MapFilterText {
        get {
            var text = $"Current Filter ({RandomizerRebinding.ToggleMapMode.FirstBindName()}): *{RandomizerSettings.CurrentFilter.Desc()}*";
            if (RandomizerSettings.CurrentFilter == RandomizerSettings.MapFilterMode.InLogic) {
                if (RandomizerLocationManager.Areas == null)
                    return $"{text}\n@Logic filter unavailable; areas.ori missing@";
            }

            return text;
        }
    }

    public static string GetObjectiveText() {
        if (Randomizer.canFinalEscape(false)) {
            return "Find and Restore #Mount Horu#!";
        }

        if (Randomizer.ForceTrees) {
            return "Visit all 10 #Skill Trees#";
        }

        if (Randomizer.WorldTour) {
            return string.Format("Find all {0} #Relics# hidden throughout Nibel", Randomizer.RelicCount);
        }

        if (Randomizer.ForceMaps) {
            return "Restore all 9 #Mapstones#";
        }

        if (Randomizer.fragsEnabled) {
            return string.Format("Find all {0} #Warmth Fragments# hidden throughout Nibel", Randomizer.fragKeyFinish);
        }

        return "Continue to search for #Skills# and #Resources#";
    }

    public static RandomizerMessageProvider GetDifficultyName(DifficultyMode mode) {
        switch (mode) {
            case DifficultyMode.Easy:
                return DifficultyOverrides.Easy.NameOverride;
            case DifficultyMode.Normal:
                return DifficultyOverrides.Normal.NameOverride;
            case DifficultyMode.Hard:
                return DifficultyOverrides.Hard.NameOverride;
            case DifficultyMode.OneLife:
                return DifficultyOverrides.OneLife.NameOverride;
            default:
                return null;
        }
    }

    private static Dictionary<AbilityType, AbilityTextOverrides> m_abilityOverrides = new Dictionary<AbilityType, AbilityTextOverrides> {
        {
            AbilityType.ChargeFlameEfficiency,
            new AbilityTextOverrides(null, "Allows Charge Flame to be performed without spending Energy")
        }, {
            AbilityType.ChargeDash,
            new AbilityTextOverrides(null, "Allows Ori to Charge Dash ([ChargeJumpCharge]) to attack enemies or break blue plants and walls")
        }, {
            AbilityType.MapMarkers,
            new AbilityTextOverrides("Maphacks", "Reveal the entire world map")
        }, {
            AbilityType.HealthEfficiency,
            new AbilityTextOverrides("Health Efficiency", "Health pickups will restore twice as much Health")
        }, {
            AbilityType.AbilityMarkers,
            new AbilityTextOverrides("Spirit Efficiency", "Increases all sources of Spirit Light by 50%")
        }, {
            AbilityType.SoulEfficiency,
            new AbilityTextOverrides("Spirit Potency", "Increases all sources of Spirit Light by an additional 50%")
        }, {
            AbilityType.HealthMarkers,
            new AbilityTextOverrides("Health Recovery", "Ori's Health will gradually refill (2 per minute)")
        }, {
            AbilityType.EnergyMarkers,
            new AbilityTextOverrides("Energy Recovery", "Ori's Energy will gradually refill (2 per minute)")
        }, {
            AbilityType.Sense,
            new AbilityTextOverrides("Sense Items", "Causes Ori to change color when approaching important items")
        }
    };

    public static string CostsAbilityPoint = "Costs [Amount] Ability Point ([Total] Total)";

    public static string CostsAbilityPoints = "Costs [Amount] Ability Points ([Total] Total)";

    private class AbilityTextOverrides {
        public AbilityTextOverrides(string name, string description) {
            if (name != null) {
                NameOverride = (RandomizerMessageProvider)ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
                NameOverride.SetMessage(name);
            }

            if (description != null) {
                DescriptionOverride = (RandomizerMessageProvider)ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
                DescriptionOverride.SetMessage(description);
            }
        }

        public RandomizerMessageProvider NameOverride;
        public RandomizerMessageProvider DescriptionOverride;
    }

    public class DifficultyOverrides {
        public DifficultyOverrides(string name, string description) {
            NameOverride = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
            NameOverride.SetMessage(name);

            NameOverrideUpper = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
            NameOverrideUpper.SetMessage(name.ToUpper());

            DescriptionOverride = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
            DescriptionOverride.SetMessage(description);
        }

        public RandomizerMessageProvider NameOverride;
        public RandomizerMessageProvider NameOverrideUpper;
        public RandomizerMessageProvider DescriptionOverride;

        public static DifficultyOverrides Easy = new DifficultyOverrides("Relaxed", "Suitable for all players.");
        public static DifficultyOverrides Normal = new DifficultyOverrides("Challenging", "Suitable for more competitive-minded players.");
        public static DifficultyOverrides Hard = new DifficultyOverrides("Punishing", "Suitable for players with a thirst for danger.");
        public static DifficultyOverrides OneLife = new DifficultyOverrides("One Life", "Suitable for those who are prepared to accept loss.");
    }
}
