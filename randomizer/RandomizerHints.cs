using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game;

public static class RandomizerHints {
    public delegate String StringMaker();

    // Shown to everybody. Kept apart so the two lists cannot drift, which they had.
    private static readonly List<StringMaker> CommonTips = new List<StringMaker> {
        () => "Extra Double Jump lets you jump an additional time in the air.\nIt requires $Double Jump$ and stacks with #Triple Jump# in the *Blue* Ability Tree\n(See all Bonus Item Descriptions in the Bonus Item Glossary at orirando.com/faq)",
        () => "Extra Air Dash lets you dash an additional time in the air.\nIt requires $Dash$ and the ability #Air Dash#\n(See all Bonus Item Descriptions in the Bonus Item Glossary at orirando.com/faq)",
        () => "Health Regeneration restores your Health slowly over time\n(See all Bonus Item Descriptions in the Bonus Item Glossary at orirando.com/faq)",
        () => "Energy Regeneration restores your Energy slowly over time\n(See all Bonus Item Descriptions in the Bonus Item Glossary at orirando.com/faq)",
        () => "Charge Dash Efficiency halves the Energy cost of #Charge Dash#\n(See all Bonus Item Descriptions in the Bonus Item Glossary at orirando.com/faq)",
        () => "Spirit Light Efficiency grants 100% increased experience from all sources.\nIt stacks (additively) with #Spirit Efficiency# and #Spirit Potency#\n(See all Bonus Item Descriptions in the Bonus Item Glossary at orirando.com/faq)",
        () => "Join the Ori community at orirando.com/discord",
        () => "Use the Logic Helper map filter to check which pickups are currently in logic",
        () => "Some enemies are immune to some spikes, allowing them to be lured surprising distances",
        () => "The top of the Ginso escape will always teleport you to Thornfelt Swamp",
        () => "Tired of seeing these tips while you warp?\nOpen Settings -> Rando UI -> Warping Tips -> Disabled",
        () => "Warping is the perfect time to stretch your hands. Try it now!",
        () => BingoController.Active ? "Every bingo square has help text explaining how it works in detail!\nView it by clicking the ? in the bottom left of the square" : null
    };

    public static List<StringMaker> NewPlayerTips = new List<StringMaker> {
        () => "If you're unable to proceed, Warp ([[Warp]]) elsewhere and come back later",
        () => "*Charge Flame*, *Grenade*, and #Charge Dash# can all break open Blue Walls, Floors, and Petrified Plants. Collectively, these are known as Blue Breakage",
        () => "*Stomp* has a shockwave that can break some Walls from the side",
        () => "The *Blue* Ability Tree is by far the most important when playing Ori Rando",
        () => "The @Red@ Ability Tree is rarely worth investing in. (*Stomp*, *Charge Jump*, and #Charge Dash# are extremely effective against enemies)",
        () => "You can Warp ([[Warp]]) to avoid damage from enemies, poison water, or spikes",
        () => "You can Warp ([[Warp]]) out of #Kuro's Nest# to avoid getting chased",
        () => "Hold [[Double Bash]] while Bashing to *Double Bash*",
        () => "In addition to Skills and World Events, #Sense# will also help you find some teleporters! (Valley, Sorrow, and all three dungeons)",
        () => "Warmth Returned is the only pickup in the game that has no effect",
        () => "After picking up the upper Blackroot orb, you can immediately put it down and go back across the lower path instead of waiting for the platforms above,\nor even warp back to Sunken Glades! The orb will come find Ori anywhere in upper Blackroot",
        () => "There are mapstone pedestals in every zone except Ginso and Misty",
        () => "Rekindle can be used to skip some cutscenes, including the ones in Swamp where Gumo shuts a door in your face and activates deadly lasers",
        () => "Despite its name, the Swamp Teleporter is actually more useful for accessing Hollow Grove and the Ginso Tree",
        () => "Despite the name, #Charge Dash# doesn't actually have a charge time. Or a cooldown. Nyooooooom! (It does require $Dash$, though.)",
        () => "Turning in mapstones at mapstone pedestals grants pickups based on how many you've turned in, not where you do it",
        () => "The item pool contains two extra mapstones, just in case",
        () => "The #Forlorn# Escape grants a pickup right at the start. (Sometimes it's $Glide$!)\nYou can warp out and come back to finish the escape later, so it's worth starting if you can reach it, even if you can't finish it with what you have",
        () => "Every death is a lesson!\n(Sometimes the lessons are things like \"I should save more\" and \"That frog deals FIVE damage???\")",
        () => "The vanilla Glide location is considered a skill tree by the randomizer, even though it is actually a feather",

    }.Concat(CommonTips).ToList();

    public static HashSet<int> SeenNPTs = new HashSet<int>();

    public static List<StringMaker> MiscTips = new List<StringMaker> {
        () => "Once you've cleared the fog in Misty Woods, you can change the layout by interacting with the orb pedestal",
        () => "Report bugs and discuss upcoming rando features in the *dev* discord (orirando.com/discord/dev)",
        () => "The Wilhelm frog in upper Valley only spawns if you have the @Sunstone@",
        () => "You can press [[Save Select Back 10]] and [[Save Select Forward 10]] to rapidly scroll through the file select menu",
        () => "While she does talk a lot, Enhanced Sein also gives unique hints in some locations",
        () => SuggestBingo() ? "Looking to spice up your randomizer experience? Try bingo!" : null,
    }.Concat(CommonTips).ToList();

    public static List<StringMaker> EncouragementTips = new List<StringMaker> {
        () => "You've got this!",
        () => "Every in-logic check brings you one step closer to your next piece of progression",
        () => "Remember: the seed is more afraid of you than you are of it",
        () => "Never give up! Trust your instincts!",
        () => "Ori Rando, like many things in life, is what you make of it",
        () => powerSkills.Unheld().NonEmpty() ? $"{powerSkills.Unheld().ToNames().RandFrom()} might be one of the best skills in the game, but it's nothing compared to ${lifeSkills.RandFrom()}$" : null,
        () => goodSkills.Held().NonEmpty() ? $"You might not have everything you need yet, but at least you have {goodSkills.Held().ToNames().RandFrom()}" : null
    };  

    private static List<AbilityType> InvCheck(this List<AbilityType> skills, bool held = true) => Characters.Sein ? skills.Where(sk => Characters.Sein.PlayerAbilities.HasAbility(sk) == held).ToList() : new List<AbilityType>();
    private static List<AbilityType> Held(this List<AbilityType> skills) => skills.InvCheck(true);
    private static List<AbilityType> Unheld(this List<AbilityType> skills) => skills.InvCheck(false);
    private static List<String> ToNames(this List<AbilityType> skills) => skills.Select(sk => RandomizerItems.Message("SK", ((int)sk).ToString())).ToList();
    private static T RandFrom<T>(this List<T> source) => source[hintRandom.Next(source.Count)];
    private static bool NonEmpty<T>(this List<T> source) => source.Count > 0;

    private static List<AbilityType> powerSkills = new List<AbilityType>() {AbilityType.ChargeJump, AbilityType.Bash, AbilityType.Dash};
    private static List<AbilityType> goodSkills  = new List<AbilityType>() {AbilityType.ChargeJump, AbilityType.Bash, AbilityType.Dash, AbilityType.DoubleJump, AbilityType.Grenade, AbilityType.Stomp};

    private static List<string> lifeSkills = new List<string>() {"Curiosity", "Determination", "Kindness", "Hope", "Creativity", "Bravery", "Patience"}; 

    // I personally think it's very funny that this will trigger for the odd-numbered leagues
    private static bool SuggestBingo() {
        if (BingoController.Active) {
            return false;
        }

        var flags = (Randomizer.SeedMeta ?? "").Split('|')[0].ToLower();
        return flags.Contains("standard") && flags.Contains("clues")
            && flags.Contains("forcetrees") && flags.Contains("pool=competitive");
    }

    public static HashSet<int> SeenMiscs = new HashSet<int>();

    public static HashSet<int> SeenEncouragement = new HashSet<int>();

    private static Random hintRandom = new Random();

    public static void TryShowSenseHint() {
        try {
            if (File.GetLastWriteTime("RandomizerSettings.txt") > new DateTime(2023, 9, 9, 0, 0, 0)) {
                return;
            }

            Randomizer.Print("In patch 4.0.7 onwards, #Sense# only changes Ori's color from the default (or custom) color when it is detecting an item.\nIf you previously set your Sense #cold color# to be similar to your normal color, consider changing one or both.\n(Don't want to see this message anymore? Add a blank line to the bottom of your RandomizerSettings.txt file and it will stop showing up.)", 15, false, false, false, true);
        } catch (Exception e) {
            Randomizer.LogError($"TryShowSenseHint: {e.Message}");
        }
    }

    // Tell players they can skip the prologue, just in case.
    public static void TryShowPrologueHint() {
        try {
            if (shownPrologueHint
                || RandomizerSettings.Customization.HintLevel.Value == RandomizerSettings.HintLevels.Disabled) {
                return;
            }

            var naru = Characters.Naru;
            if (naru == null || naru.Controller == null
                || naru.Controller.IsSuspended || naru.Controller.LockedInput) {
                return;
            }

            shownPrologueHint = true;
            Randomizer.Print("You can skip the prologue from the pause menu ([Inventory]).", 8, false, false, false, true);
        } catch (Exception e) {
            Randomizer.LogError($"TryShowPrologueHint: {e.Message}");
        }
    }

    private static bool shownPrologueHint;
    private static int encouragementChance = 0;

    public static void ShowTip() {
        var hl = RandomizerSettings.Customization.HintLevel.Value;
        if (hl == RandomizerSettings.HintLevels.Disabled) {
            return;
        }
        // one more percent per loading screen until it lands, then back to never
        if (hintRandom.Next(0, 100) < encouragementChance++) {
            encouragementChance = 0;
            ShowFrom(EncouragementTips, SeenEncouragement);
            return;
        }

        if (hl == RandomizerSettings.HintLevels.Experienced) {
            ShowFrom(MiscTips, SeenMiscs);
        } else {
            ShowFrom(NewPlayerTips, SeenNPTs);
        }
    }


    // A tip that does not apply to this seed returns null, so the walk continues past it
    // rather than showing an empty box. Unshown tips get marked as seen so that a hint
    // not valid for the current situation can't sit unseen and prevent the refresh.
    private static void ShowFrom(List<StringMaker> tips, HashSet<int> seen) {
        if (seen.Count >= tips.Count) {
            seen.Clear();
        }

        var start = hintRandom.Next(tips.Count);
        for (var i = 0; i < tips.Count; i++) {
            var h = (start + i) % tips.Count;
            if (seen.Contains(h)) {
                continue;
            }

            var text = tips[h]();
            seen.Add(h);

            if (string.IsNullOrEmpty(text)) {
                continue;
            }

            Randomizer.Print(text, 9, false, false, false, true);
            return;
        }
    }
}
