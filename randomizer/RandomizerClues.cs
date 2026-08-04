using System.Collections.Generic;
using Sein.World;

public static class RandomizerClues
{
	public static void initialize()
	{
		RevealOrder = new int[3];
		Clues = new List<string>();
	}

	public static void AddClue(string clue, int order)
	{
		Clues.Add(clue);
		RevealOrder[order] = Clues.Count;
	}

	public static string GetClues()
	{
		var text = "";
		var text2 = "";
		var text3 = "";
		string[] array = {
			"????",
			"????",
			"????"
		};
		if (Keys.GinsoTree)
		{
			array[0] = Clues[RevealOrder[0] - 1];
			text = "*";
		}
		if (Keys.ForlornRuins)
		{
			array[1] = Clues[RevealOrder[1] - 1];
			text2 = "#";
		}
		if (Keys.MountHoru)
		{
			array[2] = Clues[RevealOrder[2] - 1];
			text3 = "@";
		}
		for (var i = 0; i < 3; i++)
		{
			if (RandomizerBonus.SkillTreeProgression() >= RevealOrder[i] * 3)
			{
				array[i] = Clues[RevealOrder[i] - 1];
			}
		}
		return RandomizerMW.ResolveNames(string.Concat(
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
		));
	}
	public static void FinishClues()
	{
		for (var i = 0; i < 3; i++)
		{
			if (RevealOrder[i] == 0)
			{
				Clues.Add("Unknown");
				RevealOrder[i] = Clues.Count;
			}
		}
	}

	public static bool IsClueActive(string dungeonAbbr) => Randomizer.CluesMode && !GetClues().Contains($"{dungeonAbbr}: ?");

	// index 0: WV, index 1: GS, index 2: SS
	public static int[] RevealOrder;

	public static List<string> Clues;
}
