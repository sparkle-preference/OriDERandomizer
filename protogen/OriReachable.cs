using System;
using System.Collections.Generic;
using System.Linq;

namespace Protogen
{
    public static class OriReachable
    {
        public static List<Node> ReachableCollecting(AreaGraph graph, Inventory inventory,
            Dictionary<string, Inventory> placements)
        {
            List<Node> reachableOrder = new List<Node>();
            HashSet<string> lastReachable = new HashSet<string>();
            bool didUpdate;
            do
            {
                var newReachable = Reachable(graph, inventory);
                didUpdate = !newReachable.SetEquals(lastReachable);
                foreach (string nodeName in newReachable.Except(lastReachable))
                {
                    if (placements.ContainsKey(nodeName))
                    {
                        inventory += placements[nodeName];
                    }

                    reachableOrder.Add(graph.NodesByName[nodeName]);
                }

                lastReachable = newReachable;
            } while (didUpdate);

            return reachableOrder;
        }

        public static HashSet<string> Reachable(AreaGraph graph, Inventory inventory, string startNode = null, Dictionary<string, HashSet<string>> primedPaths = null)
        {
            if (startNode == null || !graph.OutgoingConnections.ContainsKey(startNode))
                startNode = graph.Origin.Name;

            if (primedPaths == null)
                primedPaths = new Dictionary<string, HashSet<string>>();

            HashSet<string> reachable = new HashSet<string>();
            Dictionary<string, int> reachedWithKeystones = new Dictionary<string, int>();
            reachable.Add(startNode);
            reachedWithKeystones[startNode] = 0;

            List<Node> accessibleMapstones = new List<Node>();
            int accessedMapstones = 0;

            HashSet<string> newNodes = new HashSet<string>();
            bool foundAny = true;

            while (foundAny)
            {
                foundAny = false;

                // First expand available primed paths until we exhaust those
                do
                {
                    newNodes.Clear();
                    foreach (string node in primedPaths.Keys)
                    {
                        if (reachable.Contains(node))
                        {
                            foreach (string target in primedPaths[node])
                            {
                                if (!reachable.Contains(target))
                                {
                                    foundAny = true;
                                    newNodes.Add(target);
                                    int destinationKeystonesUsed = reachedWithKeystones.ContainsKey(target) ? reachedWithKeystones[target] : 9999;
                                    if (reachedWithKeystones[node] < destinationKeystonesUsed)
                                        reachedWithKeystones[target]  = reachedWithKeystones[node];
                                }
                            }
                        }
                    }

                    reachable.UnionWith(newNodes);
                } while (newNodes.Count != 0);

                // Then expand all other paths (except KS doors) until we exhaust those
                do
                {
                    newNodes = new HashSet<string>(reachable.SelectMany(node => graph.OutgoingConnections[node]?.Where(conn =>
                            !reachable.Contains(conn.Destination.Name) && conn.Requirement.Mapstones == 0 &&
                            conn.Requirement.Keystones == 0 && inventory.Contains(conn.Requirement)))
                        .Select(conn =>
                        {
                            if (conn.Requirement.Unlocks.Contains("Mapstone"))
                                accessibleMapstones.Add(conn.Destination);

                            int destinationKeystonesUsed = reachedWithKeystones.ContainsKey(conn.Destination.Name) ? reachedWithKeystones[conn.Destination.Name] : 9999;

                            if (reachedWithKeystones[conn.Source.Name] < destinationKeystonesUsed)
                                reachedWithKeystones[conn.Destination.Name] = reachedWithKeystones[conn.Source.Name];

                            foundAny = true;
                            return conn.Destination.Name;
                        }));

                    reachable.UnionWith(newNodes);
                } while (newNodes.Count != 0);

                // Accumulate progressive map locations
                int mapstonesReachable = Math.Min(inventory.Mapstones, accessibleMapstones.Count);
                if (accessedMapstones < mapstonesReachable)
                {
                    foreach (var connection in graph.OutgoingConnections[startNode].Where(conn =>
                        conn.Requirement.Mapstones > accessedMapstones &&
                        conn.Requirement.Mapstones <= mapstonesReachable))
                    {
                        foundAny = true;
                        reachable.Add(connection.Destination.Name);
                    }

                    accessedMapstones = mapstonesReachable;
                }

                // Only do keystone doors if we have no other options to progress
                if (foundAny)
                {
                    continue;
                }

                // Finally, find and open keystone doors (if inventory is sufficient) that open new areas
                newNodes.Clear();
                foreach (var conn in reachable.SelectMany(node => graph.OutgoingConnections[node]?.Where(conn =>
                    !reachable.Contains(conn.Destination.Name) && conn.Requirement.Keystones > 0)))
                {
                    int keystonesNeeded = reachedWithKeystones[conn.Source.Name] + conn.Requirement.Keystones;

                    if (inventory.Keystones >= keystonesNeeded)
                    {
                        foundAny = true;
                        newNodes.Add(conn.Destination.Name);

                        int destinationKeystonesUsed = reachedWithKeystones.ContainsKey(conn.Destination.Name) ? reachedWithKeystones[conn.Destination.Name] : 9999;
                        if (keystonesNeeded < destinationKeystonesUsed)
                            reachedWithKeystones[conn.Destination.Name] = keystonesNeeded;
                    }
                }

                reachable.UnionWith(newNodes);
            }

            return reachable;
        }
    }
}