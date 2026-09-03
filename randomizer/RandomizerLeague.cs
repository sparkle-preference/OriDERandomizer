using System;
using System.Linq;

// TODO: delete after 10-02-2026 -- ORL season 12 ends then, and so does all of this.
//
// A beta build refuses to load a league seed. The rules require submissions on the live
// dll, and a warning was not enough: the failure mode is someone disqualifying a run they
// have already finished. Nothing here resists a determined cheater and it is not meant to.
public static class RandomizerLeague {
    private static readonly DateTime SeasonEnds = new DateTime(2026, 10, 2);

    // The season's seeds, in hex only so the file does not read as a list of live answers.
    private static readonly uint[] Seeds = {
        0x372616F4, 0x0A7F9074, 0x29AC7AD1, 0x0AF54EAD, 0x07BDE29D
    };

    // The settings every league seed shares. Checked as well as the id so a mistyped entry
    // above cannot refuse somebody's unrelated seed.
    private static readonly string[] Settings = { "master", "clues", "forcetrees" };

    public static string Refusal =>
        "@This is an ORL League seed, and this is a beta build.@\n"
        + "League rules require submissions be played on the live DLL (orirando.com/dll).";

    public static bool Refuses(string[] flags, string seed) {
        if (!Randomizer.IsBeta || DateTime.Today > SeasonEnds || flags == null) {
            return false;
        }

        uint id;
        if (!uint.TryParse((seed ?? "").Trim(), out id) || !Seeds.Contains(id)) {
            return false;
        }

        var lower = flags.Select(f => f.Trim().ToLower()).ToList();
        return Settings.All(lower.Contains);
    }

    // Reads only the flag line, so a refused seed is never parsed.
    public static bool RefusesFile(string path) {
        try {
            var lines = System.IO.File.ReadAllLines(path);
            if (lines.Length == 0) {
                return false;
            }

            var parts = lines[0].Split('|');
            return parts.Length > 1 && Refuses(parts[0].Split(','), parts[1]);
        } catch (Exception e) {
            Randomizer.log("league check skipped: " + e.Message);
            return false;
        }
    }
}
