// Turns plain held-item lists into the client's Inventory and its reachable set, for the logic props.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Protogen;
using ProtogenStubs;

public static class LogicGlue {
    static readonly Dictionary<string, AreaGraph> Graphs = new Dictionary<string, AreaGraph>();

    // a run never rewrites a file it has parsed, so a graph is cached by file and path set
    static AreaGraph Graph(string file, List<string> sets) {
        var key = file + "|" + string.Join(",", sets.OrderBy(s => s, StringComparer.Ordinal).ToArray());
        AreaGraph graph;
        if (!Graphs.TryGetValue(key, out graph)) {
            graph = OriParse.Parse(file, new HashSet<string>(sets));
            Graphs[key] = graph;
        }
        return graph;
    }

    public static Inventory Held(List<string> skills, List<string> events, List<string> teleporters,
        int cells, int energy, int keystones, int mapstones, int points) {
        var sein = new SeinStub();
        sein.Mortality.Health.MaxHealth = cells * 4f;
        sein.Energy.Max = energy;
        sein.Inventory.Keystones = keystones;
        sein.Inventory.MapStones = mapstones;
        sein.Inventory.SkillPointsCollected = points;
        foreach (var skill in skills) {
            ((Ability)typeof(Abilities).GetField(skill).GetValue(sein.PlayerAbilities)).HasAbility = true;
        }
        Characters.Sein = sein;
        Events.WaterPurified = events.Contains("Water");
        Events.WindRestored = events.Contains("Wind");
        Keys.GinsoTree = events.Contains("GinsoKey");
        Keys.ForlornRuins = events.Contains("ForlornKey");
        Keys.MountHoru = events.Contains("HoruKey");
        TeleporterController.Instance = new TeleporterController();
        foreach (var id in teleporters) {
            TeleporterController.Instance.Teleporters.Add(new Teleporter { Activated = true, Identifier = id });
        }
        return Inventory.FromCharacter();
    }

    // UpdateReachableWorker's additions to FromCharacter; opened doors index RandomizerLocationData.KeystoneDoors
    public static List<string> Reach(string file, List<string> sets, List<string> skills, List<string> events,
        List<string> teleporters, int cells, int energy, int keystones, int mapstones, int points,
        bool open, bool openWorld, string start, List<int> openedDoors) {
        var inventory = Held(skills, events, teleporters, cells, energy, keystones, mapstones, points);
        inventory.Unlocks.Add("Mapstone");
        if (open) {
            inventory.Unlocks.Add("Open");
        }
        if (openWorld) {
            inventory.Unlocks.Add("OpenWorld");
        }
        var primed = new Dictionary<string, HashSet<string>>();
        foreach (var index in openedDoors) {
            var parts = RandomizerLocationData.KeystoneDoors[index].Split();
            primed.GetOrPut(parts[0], () => new HashSet<string>()).Add(parts[1]);
        }
        var reached = OriReachable.Reachable(Graph(file, sets), inventory, start, primed);
        return reached.OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    public static List<string> Nodes(string file, List<string> sets) {
        return Graph(file, sets).OutgoingConnections.Keys.OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    // every conn as [source, destination, requirement]; a null endpoint reads as null
    public static List<List<string>> Conns(string file, List<string> sets) {
        return Rows(Graph(file, sets));
    }

    // Conns as an Entrance seed parses them, doors being its shuffled door table
    public static List<List<string>> EntranceConns(string file, List<string> sets, List<string> doors) {
        return Rows(OriParse.Parse(file, new HashSet<string>(sets), doors));
    }

    // EntranceConns on a Shards seed
    public static List<List<string>> EntranceShardConns(string file, List<string> sets, List<string> doors) {
        return Rows(OriParse.Parse(file, new HashSet<string>(sets), doors, true));
    }

    // the shard tokens FromCharacter grants for these counts (Water Vein, Gumon Seal, Sunstone)
    public static List<string> ShardTokens(int waterVein, int gumonSeal, int sunstone) {
        RandomizerBonus.WaterVein = waterVein;
        RandomizerBonus.GumonSeal = gumonSeal;
        RandomizerBonus.Sunstone = sunstone;
        try {
            return Inventory.FromCharacter().Unlocks.Where(u => OriParse.ShardTeleporters.ContainsValue(u)).OrderBy(u => u).ToList();
        } finally {
            RandomizerBonus.WaterVein = RandomizerBonus.GumonSeal = RandomizerBonus.Sunstone = 0;
        }
    }

    static List<List<string>> Rows(AreaGraph graph) {
        return graph.Connections.Select(c => new List<string> {
            c.Source == null ? null : c.Source.Name,
            c.Destination == null ? null : c.Destination.Name,
            c.Requirement.ToString(),
        }).ToList();
    }

    // conns whose endpoint is missing from the graph it came in, after Parse of text
    public static List<string> Dangling(string text, List<string> sets) {
        var file = Path.Combine(Path.GetTempPath(), "client_props_logic_" + Guid.NewGuid().ToString("N") + ".ori");
        File.WriteAllText(file, text);
        try {
            var graph = OriParse.Parse(file, new HashSet<string>(sets));
            var bad = new List<string>();
            foreach (var c in graph.Connections) {
                foreach (var end in new[] { c.Source, c.Destination }) {
                    if (end == null || !graph.NodesByName.ContainsKey(end.Name) || !graph.OutgoingConnections.ContainsKey(end.Name)) {
                        bad.Add((c.Source == null ? "null" : c.Source.Name) + " -> " + (c.Destination == null ? "null" : c.Destination.Name));
                    }
                }
            }
            return bad;
        } finally {
            File.Delete(file);
        }
    }

    // Parse then Reachable with everything held, on text
    public static int ReachText(string text, List<string> sets) {
        var file = Path.Combine(Path.GetTempPath(), "client_props_logic_" + Guid.NewGuid().ToString("N") + ".ori");
        File.WriteAllText(file, text);
        try {
            var graph = OriParse.Parse(file, new HashSet<string>(sets));
            var all = new List<string> { "WallJump", "ChargeFlame", "DoubleJump", "Bash", "Stomp", "Glide", "Climb", "ChargeJump", "Grenade", "Dash" };
            var inventory = Held(all, new List<string> { "Water", "Wind", "GinsoKey", "ForlornKey", "HoruKey" }, new List<string>(), 15, 15, 40, 11, 33);
            inventory.Unlocks.Add("Mapstone");
            return OriReachable.Reachable(graph, inventory).Count;
        } finally {
            File.Delete(file);
        }
    }
}
