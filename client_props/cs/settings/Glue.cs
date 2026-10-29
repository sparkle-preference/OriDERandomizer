// Glue for settings_props.py: a scratch folder per test, the settings file in and out, Color.txt
// and the color tick, and the stats manager over a stub inventory.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

static class SettingsGlue {
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
    const string SettingsFile = "RandomizerSettings.txt";

    // file IO in the client is relative, so this decides where it lands
    public static string Use(string dir) {
        Directory.CreateDirectory(dir);
        Environment.CurrentDirectory = dir;
        return Environment.CurrentDirectory;
    }

    // every setting at its default (Dev too), nothing queued or said, nothing dirty
    public static void Fresh() {
        foreach (var s in RandomizerSettings.All.Values) {
            s.Reset();
        }

        ((IList)typeof(RandomizerSettings).GetField("pendingNotices", Private).GetValue(null)).Clear();
        typeof(RandomizerSettings).GetField("dirty", Private).SetValue(null, false);
        RandomizerRebinding.TapWasBound = false;
        Randomizer.MessageQueue = new object();
        Randomizer.Shown.Clear();
        Randomizer.Logged.Clear();
        Randomizer.Errors.Clear();
    }

    public static List<Dictionary<string, object>> Describe() {
        var all = new List<Dictionary<string, object>>();
        foreach (var pair in RandomizerSettings.All) {
            var s = pair.Value;
            var d = new Dictionary<string, object> {
                { "name", pair.Key }, { "kind", s.GetType().Name }, { "hidden", s.Hidden }, { "nag", s.Nag }, { "default", s.ToString() },
            };
            var f = s as RandomizerSettings.FloatSetting;
            if (f != null) {
                d["min"] = (double)f.Min;
                d["max"] = (double)f.Max;
            }

            var c = s as RandomizerSettings.ColorSetting;
            if (c != null) {
                d["divisor"] = (double)c.divisor;
            }

            var value = s.GetType().GetField("Value").GetValue(s);
            if (value is Enum) {
                d["names"] = Enum.GetNames(value.GetType()).ToList();
            }

            all.Add(d);
        }

        return all;
    }

    public static Dictionary<string, string> Snapshot() {
        return RandomizerSettings.All.ToDictionary(p => p.Key, p => p.Value.ToString());
    }

    public static bool Dirty() {
        return (bool)typeof(RandomizerSettings).GetField("dirty", Private).GetValue(null);
    }

    // the file as the player saved it, parsed the way a reload does; the file as it stands after
    public static string Parse(string text) {
        File.WriteAllText(SettingsFile, text);
        RandomizerSettings.ParseSettings();
        return File.ReadAllText(SettingsFile);
    }

    public static string Rewrite() {
        RandomizerSettings.SetDirty();
        RandomizerSettings.WriteSettings();
        return File.ReadAllText(SettingsFile);
    }

    public static string Read() {
        return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile) : null;
    }

    // one value through the setting's own Parse, trimmed as the file's are; what it holds after, or the exception's type
    public static List<object> ParseOne(string name, string value) {
        var s = RandomizerSettings.All[name];
        try {
            s.Parse(value.Trim());
            return new List<object> { true, s.ToString() };
        } catch (Exception e) {
            return new List<object> { false, e.GetType().FullName };
        }
    }

    public static bool HoldsANamedValue(string name) {
        var s = RandomizerSettings.All[name];
        var value = s.GetType().GetField("Value").GetValue(s);
        return Enum.IsDefined(value.GetType(), value);
    }

    // what a menu does: the value set directly
    public static void SetFloat(string name, float value) {
        ((RandomizerSettings.FloatSetting)RandomizerSettings.All[name]).Value = value;
    }

    public static void SetColor(string name, float r, float g, float b, float a) {
        ((RandomizerSettings.ColorSetting)RandomizerSettings.All[name]).Value = new Color(r, g, b, a);
    }

    public static void SetBool(string name, bool value) {
        ((RandomizerSettings.BoolSetting)RandomizerSettings.All[name]).Value = value;
    }

    public static void SetEnum(string name, int value) {
        var s = RandomizerSettings.All[name];
        var field = s.GetType().GetField("Value");
        field.SetValue(s, Enum.ToObject(field.FieldType, value));
    }

    public static List<double> ColorOf(string name) {
        var c = ((RandomizerSettings.ColorSetting)RandomizerSettings.All[name]).Value;
        return new List<double> { c.r, c.g, c.b, c.a };
    }
}

static class ColorGlue {
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    static object Field(string name) {
        return typeof(RandomizerColorManager).GetField(name, Private).GetValue(null);
    }

    static void SetField(string name, object value) {
        typeof(RandomizerColorManager).GetField(name, Private).SetValue(null, value);
    }

    // Color.txt (null: none) read the way boot reads it, from a clean manager
    public static Dictionary<string, object> Load(string text) {
        if (text == null) {
            File.Delete("Color.txt");
        } else {
            File.WriteAllText("Color.txt", text);
        }

        SetField("customColor", false);
        SetField("customRotation", false);
        SetField("colorIndex", 0);
        ((List<Color>)Field("colors")).Clear();
        ((List<Color>)Field("colorBeforeSense")).Clear();
        RandomizerColorManager.Initialize();
        return State();
    }

    public static Dictionary<string, object> State() {
        var colors = ((List<Color>)Field("colors")).Select(c => new List<double> { c.r, c.g, c.b, c.a }).ToList();
        return new Dictionary<string, object> {
            { "colors", colors }, { "custom", Field("customColor") }, { "rotation", Field("customRotation") }, { "index", Field("colorIndex") },
        };
    }

    // frames of the color tick with Ori `distance` from the sense target; the errors it logged
    public static List<string> Tick(int frames, float distance, bool disco) {
        Randomizer.HotCold = true;
        Randomizer.Errors.Clear();
        Characters.Sein.Position = new Vector3(distance, 0f, 0f);
        RandomizerSettings.Customization.DiscoSense.Value = disco;
        for (var i = 0; i < frames; i++) {
            RandomizerColorManager.UpdateColors();
        }

        Randomizer.HotCold = false;
        return Randomizer.Errors.ToList();
    }
}

static class StatsGlue {
    public static void Fresh() {
        if (RandomizerStatsManager.Offsets == null) {
            RandomizerStatsManager.Initialize();
        }

        Characters.Sein.Inventory.Items.Clear();
        Randomizer.Errors.Clear();
        Randomizer.ForceTrees = Randomizer.WorldTour = Randomizer.ForceMaps = false;
        Randomizer.RelicZoneLookup.Clear();
        Randomizer.SpawnWith = null;
        BingoController.Active = false;
    }

    public static void Hold(Dictionary<int, int> items) {
        foreach (var pair in items) {
            Characters.Sein.Inventory.Items[pair.Key] = pair.Value;
        }
    }

    public static void Goals(bool trees, bool relics, bool maps, List<string> relicZones) {
        Randomizer.ForceTrees = trees;
        Randomizer.WorldTour = relics;
        Randomizer.ForceMaps = maps;
        Randomizer.RelicZoneLookup.Clear();
        for (var i = 0; i < relicZones.Count; i++) {
            Randomizer.RelicZoneLookup["relic" + i] = relicZones[i];
        }
    }

    // stats.txt as WriteStatsFile leaves it, under a one-line seed file
    public static string StatsFile(string flagLine) {
        Randomizer.SeedFilePath = Path.GetFullPath("seed.txt");
        File.WriteAllText(Randomizer.SeedFilePath, flagLine + "\n");
        File.Delete("stats.txt");
        RandomizerStatsManager.WriteStatsFile();
        return File.Exists("stats.txt") ? File.ReadAllText("stats.txt") : null;
    }
}
