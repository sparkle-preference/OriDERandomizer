using System;
using System.Collections.Generic;
using System.IO;

public static class RandomizerExpNames {

	public static string FileName = "ExpNames.txt";

	public static List<String> DefaultList = new List<String> { "Apples",  "Bananas",  "Bells",  "Bits",  "Bolts",  "Boonbucks",  "Boxings",  "Brick",  "Brownie Points", 
			"Bytes",  "Cash",  "Coins",  "Comments",  "Credits",  "Crowns",  "Diamonds",  "Dollars",  "Dollerydoos",  "Doubloons",  "Drams",  "EXP",  "Echoes",  "Emeralds",  "Euros",
			"Exalted Orbs",  "Experience",  "Farthings",  "Fish",  "Fun",  "GP",  "Gallons",  "Geo",  "Gil",  "Glod",  "Gold",  "Hryvnia",  "Hugs",  "Kalganids",  "Leaves",  "Likes",
			"Marbles",  "Minerals",  "Money",  "Munny",  "Nobles",  "Notes",  "Nuts",  "Nuyen",  "Ori Money",  "Pesos",  "Pieces of Eight",  "Points",  "Poké",  "Pons",  "Pounds Sterling", 
			"Quatloos",  "Quills",  "Rings",  "Rubies",  "Runes",  "Rupees",  "Sapphires",  "Sheep",  "Shillings",  "Silver",  "Slivers",  "Socks",  "Solari",  "Souls",  "Sovereigns", 
			"Spheres",  "Spirit Bucks",  "Spirit Light",  "Stamps",  "Stonks",  "Strawberries",  "Subs",  "Tickets",  "Tokens",  "Vespene Gas",  "Wheat",  "Widgets",  "Wood",  "XP",
			"Yen",  "Zenny",  "Zloty"};

	public static List<String> ExpNames;

	public static void ParseExpNames() {
		if (!File.Exists(FileName)) {
			WriteDefaultFile();
		}

		try {
			ExpNames = new List<string>(File.ReadAllLines(FileName));
			if (ExpNames.Count == 0) {
				ExpNames = DefaultList;
			}
		}
		catch (Exception e) {
			Randomizer.LogError("Error parsing ExpNames: " + e.Message);
			ExpNames = DefaultList;
		}
	}

	public static void WriteDefaultFile() {
		using (var writer = new StreamWriter(FileName, false)) {
			foreach (var name in DefaultList) {
				writer.WriteLine(name);
			}
		}
	}

	public static string ExpName(int p)
	{
		if (RandomizerSettings.Customization.RandomizedExpNames)
		{
			return ExpNames[new System.Random(31 * Randomizer.SeedMeta.GetHashCode() + p).Next(ExpNames.Count)];
		}
		return "Experience";
	}
}