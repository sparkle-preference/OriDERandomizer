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

    // Indigo and violet are hard to read so it's a bit of a ROYGB situation. (well more precisely it's BCGYOR)
    private static readonly string[] RainbowStops = { "4f8cff", "35cdd6", "4fd44f", "ffe94f", "ff8c2b", "ff3b3b" };

    // One gradient, a full rainbow per repeatEvery characters (at least one); ends on a stop.
    public static string Rainbowify(string text, int repeatEvery = 0) {
        var stops = RainbowStops.Length;
        if (repeatEvery > 0 && text.Length > repeatEvery) {
            stops = Mathf.RoundToInt((float)text.Length / repeatEvery * RainbowStops.Length);
        }

        var built = new System.Text.StringBuilder("<style color=");
        for (var i = 0; i < stops; i++) {
            if (i > 0) {
                built.Append(',');
            }

            built.Append(RainbowStops[i % RainbowStops.Length]);
        }

        return built.Append('>').Append(text).Append("</>").ToString();
    }

    public static string RainbowifyByChar(string text) {
        var built = new System.Text.StringBuilder();
        var i = 0;
        foreach(var c in text) {
            if(c.IsWhitespace()) {
                built.Append(c);
                continue;
            }
            built.Append($"<style color={RainbowStops[i++ % RainbowStops.Length]}>{c}</>");
        }
        return built.ToString();
    }

    public static bool Colorless {
        get => RandomizerSettings.Accessibility.ColorlessText.Value;
    }

    public static string Error(string text) {
        return RandomizerItems.Cue("@" + text + "@", "Error");
    }

    public static RandomizerMessageProvider GetAbilityDescription(AbilityType ability) {
        if (!m_abilityOverrides.ContainsKey(ability)) {
            return null;
        }

        return m_abilityOverrides[ability].DescriptionOverride;
    }

    public static string MapFilterText {
        get {
            string text = null;
            if (RandomizerLocationManager.HasLogic) {
                text = $"Current Filter ({RandomizerRebinding.ToggleMapMode.FirstBindName()}): *{RandomizerSettings.CurrentFilter.Desc()}*";
            } else if (RandomizerLocationManager.ShowNoLogicHint) {
                text = Error($"Logic Filter not available because {RandomizerLocationManager.NoLogicReason}");
            }

            var layer = RandomizerMapLayers.MistyLayerText;
            if (text == null || layer == null) {
                return text ?? layer;
            }

            return $"{text}\n{layer}";
        }
    }

    public static string GetObjectiveText() {
        if (BingoController.Active && !BingoController.Won) {
            return "Complete your #Bingo# goals!";
        }

        if (Randomizer.RelicsShort) {
            return $"Find #Relics# hidden throughout #Nibel#! ({Randomizer.RelicsFound}/{Randomizer.RelicCount})";
        }

        if (Randomizer.TreesShort) {
            var visited = RandomizerBonus.SkillTreeProgression();
            var left = visited < 8 ? null : Unfinished(RandomizerTrackedDataManager.Trees, 1, RandomizerTrackedDataManager.GetTree);
            if (left == null || left.Count == 0 || left.Count > 2) {
                return $"Visit every #Skill Tree#! ({visited}/10)";
            }

            return left.Count == 1
                ? $"Visit the #{left[0]}# #Skill Tree#!"
                : $"Visit the #{left[0]}# and #{left[1]}# #Skill Trees#!";
        }

        if (Randomizer.FragsShort) {
            return $"Find #Warmth Fragments# hidden throughout #Nibel#! ({RandomizerBonus.WarmthFrags()}/{Randomizer.fragKeyFinish})";
        }

        if (Randomizer.MapsShort) {
            var turnedIn = RandomizerBonus.MapStoneProgression();
            var left = turnedIn < 7 ? null : Unfinished(RandomizerTrackedDataManager.MapZonesByBit, 0, RandomizerTrackedDataManager.GetMapstone);
            if (left == null || left.Count == 0 || left.Count > 2) {
                return $"Turn in #Map Stones#! ({turnedIn}/9)";
            }

            return left.Count == 1
                ? $"Turn in a #Map Stone# at #{left[0]}#!"
                : $"Turn in #Map Stones# at #{left[0]}# and #{left[1]}#!";
        }

        return "Enter and escape #Mount Horu#!";
    }

    private static List<string> Unfinished(Dictionary<int, string> names, int first, System.Func<int, bool> done) {
        var left = new List<string>();
        foreach (var pair in names) {
            if (pair.Key >= first && !done(pair.Key)) {
                left.Add(pair.Value);
            }
        }

        return left;
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
            new AbilityTextOverrides("Drop Efficiency", "Enemies and plants are more likely to drop Health and Energy")
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
