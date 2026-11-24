// Engine-bound collaborators of the netcode sources, shadowing the game dll's copies with plain state.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine {
    public static class Time {
        public static float realtimeSinceStartup;
        public static float deltaTime;
        public static float time;
    }
}

public class InventoryStub {
    public Dictionary<int, int> Items = new Dictionary<int, int>();
    public int Keystones;

    public int GetRandomizerItem(int id) {
        int v;
        return Items.TryGetValue(id, out v) ? v : 0;
    }

    public int SetRandomizerItem(int id, int value) {
        Items[id] = value;
        return value;
    }

    public int IncRandomizerItem(int id, int by) {
        return SetRandomizerItem(id, GetRandomizerItem(id) + by);
    }

    public static implicit operator bool(InventoryStub i) {
        return !ReferenceEquals(i, null);
    }
}

public class AbilitiesStub {
    public HashSet<AbilityType> Held = new HashSet<AbilityType>();

    public bool HasAbility(AbilityType a) {
        return Held.Contains(a);
    }
}

public class ControllerStub { public bool CanMove = true; }
public class ActiveStub { public bool activeInHierarchy = true; }
public class HealthStub { public float Amount = 10f; }

public class ReceiverStub {
    public int Hits;
    public void OnRecieveDamage(Damage damage) { Hits++; }
}

public class MortalityStub {
    public HealthStub Health = new HealthStub();
    public ReceiverStub DamageReciever = new ReceiverStub();
}

public class SeinStub {
    public InventoryStub Inventory = new InventoryStub();
    public AbilitiesStub PlayerAbilities = new AbilitiesStub();
    public Vector3 Position;
    public bool Active = true;
    public bool IsSuspended;
    public ControllerStub Controller = new ControllerStub();
    public ActiveStub gameObject = new ActiveStub();
    public GameObject GameObject;
    public MortalityStub Mortality = new MortalityStub();

    public static implicit operator bool(SeinStub s) {
        return !ReferenceEquals(s, null);
    }
}

public static class Characters {
    public static SeinStub Sein;
}

public static class UI {
    public static bool MainMenuVisible;
}

public class KeysanityStub {
    public bool IsActive;
    public void WantHints(List<int> needed) { }
    public void AddClue(int id, int coords, string clue) { }
}

public static class Randomizer {
    public static string VERSION = "0.0.0";
    public static bool ChaosVerbose, CluesMode, CreditsActive, ForceTrees, Shards, Sync, WorldTour, fragsEnabled;
    public static float DamageModifier = 1f;
    public static int PlayerCount, SyncMode, fragKeyFinish, GrenadeSlot = -1, StompSlot = -1;
    public static string SyncId = "", GrenadeZone, StompZone;
    public static Hashtable TeleportTable = new Hashtable();
    public static Hashtable WarpLogicLocations = new Hashtable();
    public static Dictionary<string, string> RelicZoneLookup = new Dictionary<string, string>();
    public static KeysanityStub Keysanity = new KeysanityStub();
    public static HashSet<int> HaveCoords = new HashSet<int>();
    public static List<string> Logs = new List<string>();
    public static List<string> Errors = new List<string>();
    public static List<string> Infos = new List<string>();
    public static List<string> Wins = new List<string>();
    public static List<string> Hints = new List<string>();

    public static bool HaveCoord(int coord) { return HaveCoords.Contains(coord); }
    public static void log(string text) { Logs.Add(text); }
    public static void LogError(string text) { Errors.Add(text); }
    public static void printInfo(string text) { Infos.Add(text); }
    public static void printInfo(string text, int frames) { Infos.Add(text); }
    public static void QueueWinMessage(string text) { Wins.Add(text); }
    public static void showHint(RandomizerUI.Message message) { Hints.Add(message.MessageString); }
}

public static class RandomizerUI {
    public class Message {
        public string MessageString;

        public static Message PickupMessage(string message, float baseDuration = 1f) {
            return new Message { MessageString = message };
        }
    }
}

public static class RandomizerText {
    public static string Error(string text) { return text; }
}

public static class RandomizerItems {
    public static string Name(string code, string id) { return code + "|" + id; }
    public static string ColorWrap(string input) { return input; }
    public static string ColorWrapAs(string name, string shown) { return shown; }
    public static string Cue(string text, string cue) { return text; }
}

public static class RandomizerSwitch {
    public static bool SilentMode, SeedSilent;
    public static string MessageSuffix;
    // one "code|id@coords" per grant, in order
    public static List<string> Given = new List<string>();
    public static List<string> Messages = new List<string>();

    public static void GivePickup(RandomizerAction action, int coords, bool found_locally = true) {
        Given.Add(action.Action + "|" + action.ValAsStr() + "@" + coords);
        NetGlue.Apply(action);
    }

    public static void PickupMessage(string text, int frames = 120) { Messages.Add(text); }
}

public static class RandomizerBonus {
    public static Dictionary<int, int> Counts = new Dictionary<int, int>();
    public static List<int> Calls = new List<int>();
    public static bool EscapeHint;

    public static int UpgradeCount(int id) {
        int v;
        return Counts.TryGetValue(id, out v) ? v : 0;
    }

    public static void UpgradeID(int id) {
        Calls.Add(id);
        var key = Math.Abs(id);
        Counts[key] = UpgradeCount(key) + (id < 0 ? -1 : 1);
    }

    public static bool ForlornEscapeHint() { return EscapeHint; }
    public static int WaterVeinShards() { return UpgradeCount(17); }
    public static int GumonSealShards() { return UpgradeCount(19); }
    public static int SunstoneShards() { return UpgradeCount(21); }
    public static int WarmthFrags() { return UpgradeCount(28); }
}

public static class RandomizerBonusSkill {
    public static bool HasMark;
    public static HashSet<string> CreditWarps = new HashSet<string>();
    public static bool UnlockCreditWarp(string text) { return CreditWarps.Contains(text); }
}

public static class RandomizerChaosManager {
    public static int Clears, Spawns;
    public static void ClearEffects() { Clears++; }
    public static void SpawnEffect() { Spawns++; }
}

public static class RandomizerClues {
    public static void AddClue(string clue, int index, int slot) { }
    public static void WantHints(List<int> needed) { }
}

public static class RandomizerGhostSignal {
    public static List<string> Calls = new List<string>();
    public static void Apply() { }
    public static void OnRoster(string body) { Calls.Add("ghosts:" + body); }
    public static void OnDescription(string body) { Calls.Add("ghost:" + body); }
    public static void OnIce(string body) { Calls.Add("ice:" + body); }
}

public static class RandomizerLocationManager {
    public static int Refreshes;
    public static void UpdateReachable() { Refreshes++; }
}

public static class RandomizerStatsManager {
    public static string PickupZone;
    public static string Zone = "Glades";
    public static List<int> Trees = new List<int>();
    public static List<string> Relics = new List<string>();
    public static Dictionary<string, string> ZonePrettyNames = new Dictionary<string, string>();

    public static string CurrentZone() { return Zone; }
    public static string CurrentZone(bool pretty) { return Zone; }
    public static string ZoneForPickup(int coords) { return ""; }
    public static void TreeActivated(int tree) { Trees.Add(tree); }
    public static void RelicFound(string zone) { Relics.Add(zone); }
    public static void MapstoneTurnedIn(int map) { }
    public static void WriteStatsFile() { }
}

public static class RandomizerSettings {
    public static bool Dev;

    public class Host { public string Value = "example.test"; }

    public class Dev_ {
        public bool PlainHttp;
        public Host NetcodeHost = new Host();
    }

    public static Dev_ DevSettings = new Dev_();
}

public class TeleporterStub {
    public string Identifier;
    public bool Activated;
}

public class TeleporterController {
    public static TeleporterController Instance = new TeleporterController();
    public static HashSet<string> CustomWarps = new HashSet<string>();
    public List<TeleporterStub> Teleporters = new List<TeleporterStub>();

    public static bool HasCustomWarp(string name) { return CustomWarps.Contains(name); }
}
