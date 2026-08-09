using System;
using System.Collections.Generic;
using Sein.World;

public static class RandomizerClues {
    public static void initialize() {
        RandomizerClues.RevealOrder = new int[3];
        RandomizerClues.Clues = new List<string>();
        RandomizerClues.ClueSlots = new List<int>();
    }

    // slot is the multiworld manifest slot this clue's key sits in, or -1 for
    // a key that stayed in our own world (nothing to buy a hint for)
    public static void AddClue(string clue, int order, int slot = -1) {
        RandomizerClues.Clues.Add(clue);
        RandomizerClues.ClueSlots.Add(slot);
        RandomizerClues.RevealOrder[order] = RandomizerClues.Clues.Count;
    }

    // index 0: WV, 1: GS, 2: SS. Ori reveals a clue once you hold the key or
    // have lit enough trees; that moment is also when an AP hint is worth
    // buying, so the predicate has exactly one home.
    public static bool Revealed(int i) {
        if (i == 0 && Keys.GinsoTree)
            return true;
        if (i == 1 && Keys.ForlornRuins)
            return true;
        if (i == 2 && Keys.MountHoru)
            return true;
        return RandomizerBonus.SkillTreeProgression() >= RandomizerClues.RevealOrder[i] * 3;
    }

    public static int SlotFor(int i) {
        int index = RandomizerClues.RevealOrder[i] - 1;
        return (RandomizerClues.ClueSlots != null && index >= 0 && index < RandomizerClues.ClueSlots.Count)
            ? RandomizerClues.ClueSlots[index]
            : -1;
    }

    public static string ClueFor(int i) {
        return RandomizerMW.ApHintOr(SlotFor(i), RandomizerClues.Clues[RandomizerClues.RevealOrder[i] - 1]);
    }

    public static void WantHints(List<int> needed) {
        if (RandomizerClues.Clues == null || RandomizerClues.RevealOrder == null)
            return;
        for (int i = 0; i < 3; i++)
            if (Revealed(i))
                RandomizerMW.WantHint(needed, SlotFor(i));
    }

    public static string GetClues() {
        string text = "";
        string text2 = "";
        string text3 = "";
        string[] array = new string[] {
            "????",
            "????",
            "????"
        };
        if (Keys.GinsoTree) {
            text = "*";
        }

        if (Keys.ForlornRuins) {
            text2 = "#";
        }

        if (Keys.MountHoru) {
            text3 = "@";
        }

        for (int i = 0; i < 3; i++) {
            if (Revealed(i)) {
                array[i] = ClueFor(i);
            }
        }

        return RandomizerMW.ResolveNames(
            string.Concat(
                new string[] {
                    text,
                    "WV: ",
                    array[0],
                    text,
                    "  ",
                    text2,
                    "GS: ",
                    array[1],
                    text2,
                    "  ",
                    text3,
                    "SS: ",
                    array[2],
                    text3
                }
            )
        );
    }

    public static void FinishClues() {
        for (int i = 0; i < 3; i++) {
            if (RandomizerClues.RevealOrder[i] == 0) {
                RandomizerClues.Clues.Add("Unknown");
                RandomizerClues.ClueSlots.Add(-1);
                RandomizerClues.RevealOrder[i] = RandomizerClues.Clues.Count;
            }
        }
    }

    public static bool IsClueActive(string dungeonAbbr) => Randomizer.CluesMode && !GetClues().Contains($"{dungeonAbbr}: ?");

    // index 0: WV, index 1: GS, index 2: SS
    public static int[] RevealOrder;

    public static List<string> Clues;

    // parallel to Clues: the manifest slot each clue's key sits in, -1 if it
    // never left our world
    public static List<int> ClueSlots;
}
