namespace Protogen {
    public class Node {
        public Node(string name, NodeType type) {
            Name = name;
            Type = type;
        }

        public string Name;

        public NodeType Type;
    }
}
