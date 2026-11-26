using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Protogen {
    public static class OriParse {
        // entranceDoors: the shuffled doors of an Entrance seed, null on any other seed; shards: its key mode is Shards
        public static AreaGraph Parse(string filename, HashSet<string> logicSets, ICollection<string> entranceDoors = null, bool shards = false) {
            var nodeDictionary = new Dictionary<string, Node>();
            var connections = new List<Connection>();
            Node currentHome = null;
            Node currentDestination = null;
            var hasPath = false;
            var pathMask = PathSetToPathMask(logicSets);

            if (!File.Exists(filename)) {
                return null;
            }

            var logicLines = File.ReadAllLines(filename).ToList();

            foreach (var rawLine in logicLines) {
                var commStart = rawLine.IndexOf("--");
                var line = (commStart == -1 ? rawLine : rawLine.Substring(0, commStart)).Trim();
                if (line.StartsWith("--") || line == "") {
                    continue;
                }

                var segments = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var first = segments.First().Trim();
                switch (first) {
                    case "loc:":
                        nodeDictionary[segments[1]] = new Node(segments[1].Trim(), NodeType.Pickup);
                        break;
                    case "home:":
                        if (currentDestination != null && currentHome != null && !hasPath) {
                            connections.Add(new Connection(currentHome, currentDestination, new Inventory()));
                            hasPath = true;
                        }

                        currentHome = nodeDictionary.GetOrPut(segments[1].Trim(), () => new Node(segments[1].Trim(), NodeType.Anchor));
                        break;
                    case "pickup:":
                    case "conn:":
                        if (currentDestination != null && currentHome != null && !hasPath) {
                            connections.Add(new Connection(currentHome, currentDestination, new Inventory()));
                        }

                        currentDestination = nodeDictionary.GetOrPut(segments[1].Trim(), () => new Node(segments[1].Trim(), NodeType.Anchor));
                        hasPath = false;
                        break;
                    default:
                        var lineMask = GetPathMaskFromLine(segments);
                        lineMask &= ~pathMask;
                        if (lineMask == 0) {
                            var inventory = ParseRequirement(segments.Skip(1));
                            connections.Add(new Connection(currentHome, currentDestination, inventory));
                        }

                        hasPath = true;
                        break;
                }
            }

            if (entranceDoors != null) {
                connections = EntranceShuffled(connections, entranceDoors, shards);
            }

            for (var i = 1; i <= 9; i++) {
                var adjustedCount = i == 9 ? 11 : i == 8 ? 9 : i;
                var node = new Node("Map" + i, NodeType.Pickup);
                nodeDictionary[node.Name] = node;
                connections.Add(new Connection(nodeDictionary[Origin], node, new Inventory { Mapstones = adjustedCount }));
            }

            return new AreaGraph(
                nodeDictionary[Origin],
                nodeDictionary.Values.ToList(),
                connections
            );
        }

        // generator.py keysOnlyForDoors and randomize_entrances: a dungeon key gates only its own outer door
        // from a home outside it, and a shuffled door keeps no conn to any door
        private static List<Connection> EntranceShuffled(List<Connection> connections, ICollection<string> doors, bool shards) {
            var kept = new List<Connection>();
            foreach (var conn in connections) {
                if (conn.Source == null || conn.Destination == null) {
                    kept.Add(conn);
                    continue;
                }

                var home = conn.Source.Name;
                var target = conn.Destination.Name;
                if (doors.Contains(home) && (target.Contains("OuterDoor") || target.Contains("InnerDoor"))) {
                    continue;
                }

                foreach (var key in DungeonKeyDoors) {
                    if (target != key.Value || home.Contains("InnerDoor")) {
                        conn.Requirement.Unlocks.Remove(key.Key);
                    }
                }

                string token;
                if (shards && home == "TeleporterNetwork" && ShardTeleporters.TryGetValue(target, out token)) {
                    conn.Requirement.Unlocks.Add(token);
                }

                kept.Add(conn);
            }

            return kept;
        }

        // generator.py's Entrance + Shards teleporters: each also wants two of its dungeon's shards (Inventory grants the token)
        public static readonly Dictionary<string, string> ShardTeleporters = new Dictionary<string, string> {
            { "GinsoTeleporter", "TwoWaterVeinShards" },
            { "ForlornTeleporter", "TwoGumonSealShards" },
            { "HoruTeleporter", "TwoSunstoneShards" },
        };

        private static readonly Dictionary<string, string> DungeonKeyDoors = new Dictionary<string, string> {
            { "GinsoKey", "GinsoOuterDoor" },
            { "ForlornKey", "ForlornOuterDoor" },
            { "HoruKey", "HoruOuterDoor" },
        };

        private static Inventory ParseRequirement(IEnumerable<string> requirements) {
            var resultInventory = new Inventory();
            foreach (var req in requirements) {
                var trimmed = req.Trim();
                if (trimmed.Contains("=")) {
                    var parts = trimmed.Split('=');
                    var resource = parts[0].Trim();
                    var value = int.Parse(parts[1].Trim());
                    switch (resource) {
                        case "Health":
                            resultInventory.Health = value;
                            break;
                        case "Energy":
                            resultInventory.Energy = value;
                            break;
                        case "Ability":
                            resultInventory.Acs = value;
                            break;
                        case "Keystone":
                            resultInventory.Keystones = value;
                            break;
                    }
                } else {
                    resultInventory.Unlocks.Add(trimmed);
                }
            }

            return resultInventory;
        }

        private static int GetPathMaskFromLine(string[] parts) {
            var pathMask = 0;
            // Anything is allowed in insane/timed-level/glitched.
            if (allowsAnything.Contains(parts[0])) {
                return pathBits[parts[0]];
            }

            foreach (var part in parts) {
                if (abilitySkills.Contains(part) || part.StartsWith("Ability=")) {
                    if (pathBits.ContainsKey(parts[0] + "-abilities")) {
                        pathMask |= pathBits[parts[0] + "-abilities"];
                    } else {
                        pathMask |= invalidPathset;
                    }
                }

                if (healthSkills.Contains(part) || part.StartsWith("Health=")) {
                    if (pathBits.ContainsKey(parts[0] + "-dboost")) {
                        pathMask |= pathBits[parts[0] + "-dboost"];
                    } else {
                        pathMask |= invalidPathset;
                    }
                }
            }

            if (parts.Contains("Lure")) {
                if (pathBits.ContainsKey(parts[0] + "-lure")) {
                    pathMask |= pathBits[parts[0] + "-lure"];
                } else {
                    pathMask |= invalidPathset;
                }
            }

            if (parts[0] == "expert" && parts.Contains("DoubleBash")) {
                pathMask |= pathBits["dbash"];
            }

            if (parts.Contains("GrenadeJump")) {
                pathMask |= pathBits["gjump"];
            }

            // We only add -core now because we can allow people to have dbash or gjump without having their respective -cores selected.
            if (pathMask == 0) {
                if (pathBits.ContainsKey(parts[0] + "-core")) {
                    pathMask |= pathBits[parts[0] + "-core"];
                } else {
                    pathMask |= invalidPathset;
                }
            }

            return pathMask;
        }

        public static string PathMaskToString(int pathMask) {
            var results = "";
            foreach (var item in pathBits) {
                if ((pathMask & item.Value) != 0) {
                    results += item.Key + " ";
                }
            }

            if (results.Length == 0) {
                results = "NoPathSetsFound!";
            }

            return results;
        }

        // Returns null if invalid.
        public static HashSet<string> PathMaskToPathSet(int pathMask) {
            if (pathMask <= 0 || pathMask >= invalidPathset) {
                return null;
            }

            var result = new HashSet<string>();
            foreach (var item in pathBits) {
                if ((pathMask & item.Value) != 0) {
                    result.Add(item.Key);
                }
            }

            // Ensure sanity.
            result.Add("casual-core");

            return result;
        }

        public static int PathSetToPathMask(HashSet<string> pathSet) {
            var pathMask = 0;
            foreach (var path in pathSet) {
                if (pathBits.ContainsKey(path)) {
                    pathMask |= pathBits[path];
                }
            }

            return pathMask;
        }

        public const string Origin = "SunkenGladesRunaway";

        public static string[] abilitySkills = { "ChargeFlameBurn", "ChargeDash", "RocketJump", "AirDash", "TripleJump", "UltraDefense", "Rekindle" };
        public static string[] healthSkills = { "UltraDefense" };
        public static string[] allowsAnything = { "glitched", "timed-level", "insane" };
        public static int invalidPathset = 1 << 19;

        public static Dictionary<string, int> pathBits = new Dictionary<string, int> {
            { "casual-core", 1 << 0 },
            { "casual-dboost", 1 << 1 },
            { "standard-core", 1 << 2 },
            { "standard-dboost", 1 << 3 },

            { "standard-lure", 1 << 4 },
            { "standard-abilities", 1 << 5 },
            { "expert-core", 1 << 6 },
            { "expert-dboost", 1 << 7 },

            { "expert-lure", 1 << 8 },
            { "expert-abilities", 1 << 9 },
            { "dbash", 1 << 10 },
            { "master-core", 1 << 11 },

            { "master-dboost", 1 << 12 },
            { "master-lure", 1 << 13 },
            { "master-abilities", 1 << 14 },
            { "gjump", 1 << 15 },

            { "glitched", 1 << 16 },
            { "timed-level", 1 << 17 },
            { "insane", 1 << 18 },
        };
    }
}
