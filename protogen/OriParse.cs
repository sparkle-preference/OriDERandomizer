using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Protogen {
    public static class OriParse {
        public static AreaGraph Parse(string filename, HashSet<string> logicSets) {
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

            for (var i = 1; i <= 9; i++) {
                var adjustedCount = i == 9 ? 11 : i == 8 ? 9 : i;
                var node = new Node("Map" + i, NodeType.Pickup);
                nodeDictionary[node.Name] = node;
                connections.Add(new Connection(nodeDictionary[ORIGIN], node, new Inventory { Mapstones = adjustedCount }));
            }

            return new AreaGraph(
                nodeDictionary[ORIGIN],
                nodeDictionary.Values.ToList(),
                connections
            );
        }

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
            if (AllowsAnything.Contains(parts[0])) {
                return PathBits[parts[0]];
            }

            foreach (var part in parts) {
                if (AbilitySkills.Contains(part) || part.StartsWith("Ability=")) {
                    if (PathBits.ContainsKey(parts[0] + "-abilities")) {
                        pathMask |= PathBits[parts[0] + "-abilities"];
                    } else {
                        pathMask |= InvalidPathset;
                    }
                }

                if (HealthSkills.Contains(part) || part.StartsWith("Health=")) {
                    if (PathBits.ContainsKey(parts[0] + "-dboost")) {
                        pathMask |= PathBits[parts[0] + "-dboost"];
                    } else {
                        pathMask |= InvalidPathset;
                    }
                }
            }

            if (parts.Contains("Lure")) {
                if (PathBits.ContainsKey(parts[0] + "-lure")) {
                    pathMask |= PathBits[parts[0] + "-lure"];
                } else {
                    pathMask |= InvalidPathset;
                }
            }

            if (parts[0] == "expert" && parts.Contains("DoubleBash")) {
                pathMask |= PathBits["dbash"];
            }

            if (parts.Contains("GrenadeJump")) {
                pathMask |= PathBits["gjump"];
            }

            // We only add -core now because we can allow people to have dbash or gjump without having their respective -cores selected.
            if (pathMask == 0) {
                if (PathBits.ContainsKey(parts[0] + "-core")) {
                    pathMask |= PathBits[parts[0] + "-core"];
                } else {
                    pathMask |= InvalidPathset;
                }
            }

            return pathMask;
        }

        // Returns null if invalid.
        public static HashSet<string> PathMaskToPathSet(int pathMask) {
            if (pathMask <= 0 || pathMask >= InvalidPathset) {
                return null;
            }

            var result = new HashSet<string>();
            foreach (var item in PathBits) {
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
                if (PathBits.ContainsKey(path)) {
                    pathMask |= PathBits[path];
                }
            }

            return pathMask;
        }

        public const string ORIGIN = "SunkenGladesRunaway";

        public static string[] AbilitySkills = { "ChargeFlameBurn", "ChargeDash", "RocketJump", "AirDash", "TripleJump", "UltraDefense", "Rekindle" };
        public static string[] HealthSkills = { "UltraDefense" };
        public static string[] AllowsAnything = { "glitched", "timed-level", "insane" };
        public static int InvalidPathset = 1 << 19;

        public static Dictionary<string, int> PathBits = new Dictionary<string, int> {
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
