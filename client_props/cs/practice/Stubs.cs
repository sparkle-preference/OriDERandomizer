// Engine-bound statics the practice sources touch, as plain values the properties can read.
using System.Collections.Generic;
using UnityEngine;

static class Randomizer {
    public static readonly List<string> Errors = new List<string>();

    public static readonly List<string> Logged = new List<string>();

    public static void LogError(string text) { Keep(Errors, text); }

    public static void log(string text) { Keep(Logged, text); }

    public static bool HaveCoord(int coord) { return false; }

    public static string SyncId = "";

    public static StubInventory Inventory = new StubInventory();

    public static void Clear() {
        Errors.Clear();
        Logged.Clear();
    }

    static void Keep(List<string> into, string text) {
        if (into.Count < 2000) {
            into.Add(text);
        }
    }
}

class StubInventory {
    public int GetRandomizerItem(int id) { return 0; }

    public int SetRandomizerItem(int id, int value) { return value; }

    public int IncRandomizerItem(int id, int value) { return value; }
}

// the source RandomizerBox and RandomizerAction can't be handed to the dll's copies of these
static class RandomizerSwitch {
    public static void GivePickup(RandomizerAction action, int coords, bool found) { }
}

public class RandomizerBoxPrefab : MonoBehaviour {
    public static RandomizerBoxPrefab Create(RandomizerBox box) { return null; }

    public void UpdateActive() { }

    public void Place() { }
}
