// Glue for the binds half of settings_props.py: bind sets as plain values, and the bind file in and out.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

static class BindsGlue {
    public static string Use(string dir) {
        Directory.CreateDirectory(dir);
        Environment.CurrentDirectory = dir;
        return Environment.CurrentDirectory;
    }

    public static void Fresh() {
        RandomizerRebinding.TapWasBound = false;
        Randomizer.Shown.Clear();
        Randomizer.Logged.Clear();
    }

    // each bind as its inputs, each input as the physical buttons that satisfy it
    public static List<List<List<string>>> Physical(RandomizerRebinding.BindSet set) {
        return set.Binds.Select(b => b.Inputs.Select(Atoms).ToList()).ToList();
    }

    static List<string> Atoms(RandomizerRebinding.SingleInput input) {
        switch (input.Type) {
            case RandomizerRebinding.SingleInput.ActionType.CoreInput:
                return new List<string> { "core:" + input.RawStr() };
            case RandomizerRebinding.SingleInput.ActionType.ControllerButton:
                return new List<string> { "pad:" + (int)input.Button };
            case RandomizerRebinding.SingleInput.ActionType.EitherKey:
                return new List<string> { "key:" + (int)input.Key, "key:" + (int)input.KeyAlt };
            default:
                return new List<string> { "key:" + (int)input.Key };
        }
    }

    // each input as [type, key, the text it was parsed from]
    public static List<List<object>> Inputs(string action, string bindingString) {
        var raw = typeof(RandomizerRebinding.SingleInput).GetField("raw", BindingFlags.NonPublic | BindingFlags.Instance);
        return RandomizerRebinding.ParseBinds(action, bindingString).Binds.SelectMany(b => b.Inputs)
            .Select(i => new List<object> { i.Type.ToString(), (int)i.Key, raw.GetValue(i) }).ToList();
    }

    public static List<string> CoreNames() {
        return RandomizerRebinding.CoreInputMap.Keys.ToList();
    }

    public static string Text(string action, string bindingString) {
        return RandomizerRebinding.ParseBinds(action, bindingString).ToString();
    }

    // (text after one Collapse, whether it merged, text after a second Collapse, whether that merged)
    public static List<object> CollapseTwice(string action, string bindingString) {
        var set = RandomizerRebinding.ParseBinds(action, bindingString);
        var first = set.Collapse();
        var once = set.ToString();
        var second = set.Collapse();
        return new List<object> { once, first, set.ToString(), second };
    }

    public static List<List<List<string>>> PhysicalAfterCollapse(string action, string bindingString) {
        var set = RandomizerRebinding.ParseBinds(action, bindingString);
        set.Collapse();
        return Physical(set);
    }

    public static List<List<List<string>>> PhysicalOf(string action, string bindingString) {
        return Physical(RandomizerRebinding.ParseBinds(action, bindingString));
    }

    public static string OrDefault(string action, string bindingString) {
        return RandomizerRebinding.ParseOrDefault(action, bindingString, new List<string>()).ToString();
    }

    public static Dictionary<string, string> Binds() {
        return RandomizerRebinding.DefaultBinds.Keys.ToDictionary(k => k, k => RandomizerRebinding.BindNamed(k).ToString());
    }

    public static string Parse(string text) {
        File.WriteAllText(RandomizerRebinding.BindsFile, text);
        RandomizerRebinding.ParseRebinding();
        return File.ReadAllText(RandomizerRebinding.BindsFile);
    }

    public static string Rewrite() {
        RandomizerRebinding.WriteBindsToFile();
        return File.ReadAllText(RandomizerRebinding.BindsFile);
    }

    public static List<string> KeyNames() {
        return Enum.GetNames(typeof(UnityEngine.KeyCode)).ToList();
    }

    public static List<string> PadNames() {
        return Enum.GetNames(typeof(PlayerInputRebinding.ControllerButton)).ToList();
    }

    public static int KeyValue(string name) {
        return (int)Enum.Parse(typeof(UnityEngine.KeyCode), name);
    }
}
