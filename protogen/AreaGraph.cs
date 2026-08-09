using System.Collections.Generic;

namespace Protogen {
    public class AreaGraph {
        public AreaGraph(Node origin, List<Node> nodes, List<Connection> connections) {
            Origin = origin;
            Nodes = nodes;
            Connections = connections;

            foreach (Node node in Nodes) {
                NodesByName[node.Name] = node;
                OutgoingConnections[node.Name] = new List<Connection>();
            }

            foreach (Connection connection in Connections) {
                OutgoingConnections[connection.Source.Name].Add(connection);
            }
        }

        public Node Origin;

        public List<Node> Nodes;

        public List<Connection> Connections;

        public Dictionary<string, Node> NodesByName = new Dictionary<string, Node>();

        public Dictionary<string, List<Connection>> OutgoingConnections = new Dictionary<string, List<Connection>>();
    }
}
