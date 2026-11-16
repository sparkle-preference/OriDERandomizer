// Stand-ins for what RandomizerRebinding reaches outside itself; the game dll supplies the input types.
using System.Collections.Generic;

static class Randomizer {
    public static readonly List<string> Shown = new List<string>();
    public static readonly List<string> Logged = new List<string>();
    public static void printInfo(string text, int frames) { Shown.Add(text); }
    public static void log(string text) { Logged.Add(text); }
    public static void Print(string text, int seconds, bool mute, bool setMessage, bool devOnly, bool immediate) { Shown.Add(text); }
}

static class RandomizerText { public static string Error(string text) { return text; } }

// the display form is not under test, so a cap is the key's name
static class RandomizerKeyIcons { public static string Caption(UnityEngine.KeyCode key) { return key.ToString(); } }
