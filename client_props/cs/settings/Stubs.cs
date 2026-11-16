// Stand-ins for what RandomizerSettings, RandomizerColorManager and RandomizerStatsManager reach outside
// themselves: plain state the properties set, and lists that record what was said.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core {
    public class Button { public bool IsPressed, OnPressed, OnReleased; }
    public static class Input { public static Button Jump = new Button(); }
}

namespace Game {
    class GameStub { }
}

static class Randomizer {
    public static string NETCODE_HOST = "host.example.test";
    public static object MessageQueue;
    public static readonly List<string> Shown = new List<string>();
    public static readonly List<string> Logged = new List<string>();
    public static readonly List<string> Errors = new List<string>();
    public static void printInfo(string text, int frames) { Shown.Add(text); }
    public static void log(string text) { Logged.Add(text); }
    public static void LogError(string text) { Errors.Add(text); }
    public static void PrintImmediately(string text, int seconds, bool mute, bool setMessage, bool devOnly) { }
    public static bool HaveCoord(int coord) { return false; }

    public static bool HotCold;
    public static Dictionary<int, RandomizerHotColdItem> HotColdItems = new Dictionary<int, RandomizerHotColdItem>();
    public static Dictionary<int, RandomizerHotColdItem> HotColdFrags = new Dictionary<int, RandomizerHotColdItem>();
    public static List<int> HotColdMaps = new List<int>();
    public static List<int> HotColdMapsWithFrags = new List<int>();

    public static string SpawnWith;
    public static Hashtable TeleportTable = new Hashtable();
    public static bool ForceTrees, WorldTour, ForceMaps;
    public static Dictionary<string, string> RelicZoneLookup = new Dictionary<string, string>();
    public static string SeedFilePath;
}

static class RandomizerRebinding {
    public static bool TapWasBound;
    public static void WriteBindsToFile() { }
}

static class RandomizerText { public static string Error(string text) { return text; } }
static class RandomizerGhostSignal { public static void Apply() { } }
static class RandomizerPerf { public static void Apply() { } }

public class RandomizerHotColdItem {
    public Vector3 Position;
    public int Id;
}

class SeinInventory {
    public Dictionary<int, int> Items = new Dictionary<int, int>();
    public int GetRandomizerItem(int code) { int v; return Items.TryGetValue(code, out v) ? v : 0; }
    public int SetRandomizerItem(int code, int value) { Items[code] = value; return value; }
    public int IncRandomizerItem(int code, int value) { return SetRandomizerItem(code, GetRandomizerItem(code) + value); }
    public static implicit operator bool(SeinInventory i) { return !ReferenceEquals(i, null); }
}

class SenseAbility { public bool HasAbility; }
class SeinAbilities { public SenseAbility Sense = new SenseAbility(); }
class StubMaterial { public Color color; }
class StubRenderer { public StubMaterial material = new StubMaterial(); }
class StubVisuals { public StubRenderer SpriteRenderer = new StubRenderer(); }
class StubPlatform { public StubVisuals Visuals = new StubVisuals(); }

class SeinCharacter {
    public SeinInventory Inventory = new SeinInventory();
    public Vector3 Position;
    public SeinAbilities PlayerAbilities = new SeinAbilities();
    public StubPlatform PlatformBehaviour = new StubPlatform();
    public static implicit operator bool(SeinCharacter s) { return !ReferenceEquals(s, null); }
}

class StubOri { public bool InsideMapstone; }

static class Characters {
    public static SeinCharacter Sein = new SeinCharacter();
    public static StubOri Ori = new StubOri();
}

class GameWorldArea { public string AreaIdentifier; }

class GameWorld {
    public static GameWorld Instance;
    public GameWorldArea WorldAreaAtPosition(Vector3 p) { return null; }
    public static implicit operator bool(GameWorld g) { return !ReferenceEquals(g, null); }
}

class SceneInfo { public string Scene; }
class SceneManagerStub { public SceneInfo CurrentScene; }
static class Scenes { public static SceneManagerStub Manager = new SceneManagerStub(); }

public enum DamageType { LevelUp, Lava }

static class RandomizerBonus {
    public static bool SenseFragsActive;
    public static bool SenseFragsEnabled;
    public static int MapStoneProgression() { return 0; }
    public static int WarmthFrags() { return 0; }
}

static class BingoController {
    public static bool Active;
    public static string GetJson() { return ""; }
    public static readonly string[] Teleporters = {
        "swamp", "sorrowPass", "sunkenGlades", "moonGrotto", "mangroveFalls", "valleyOfTheWind",
        "spiritTree", "mangroveB", "horuFields", "ginsoTree", "forlorn", "mountHoru"
    };
}

static class RandomizerMapWarp { public static string ShortName(string id) { return id; } }

static class RandomizerTrackedDataManager {
    public static Dictionary<int, string> Trees = new Dictionary<int, string> {
        { 0, "Spirit Flame" }, { 1, "Wall Jump" }, { 2, "Charge Flame" }, { 3, "Double Jump" }, { 4, "Bash" },
        { 5, "Stomp" }, { 6, "Glide" }, { 7, "Climb" }, { 8, "Charge Jump" }, { 9, "Grenade" }, { 10, "Dash" },
    };
    public static Dictionary<string, int> RelicFound = new Dictionary<string, int> {
        { "Glades", 0 }, { "Grove", 1 }, { "Grotto", 2 }, { "Blackroot", 3 }, { "Swamp", 4 }, { "Ginso", 5 },
        { "Valley", 6 }, { "Misty", 7 }, { "Forlorn", 8 }, { "Sorrow", 9 }, { "Horu", 10 },
    };
    public static Dictionary<string, int> MapBitsByArea = new Dictionary<string, int> {
        { "sunkenGlades", 0 }, { "mangrove", 1 }, { "hollowGrove", 2 }, { "moonGrotto", 3 }, { "thornfeltSwamp", 4 },
        { "valleyOfTheWind", 5 }, { "forlornRuins", 6 }, { "sorrowPass", 7 }, { "mountHoru", 8 },
    };
}

static class RandomizerLocationManager {
    public class Location { public string Zone; }
    public static Dictionary<int, Location> LocationsByKey = new Dictionary<int, Location>();
}
