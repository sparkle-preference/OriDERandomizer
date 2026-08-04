namespace Protogen {
    public class Connection {
        public Connection(Node source, Node destination, Inventory req) {
            Source = source;

            Destination = destination;

            Requirement = req;
        }

        public Node Source;

        public Node Destination;

        public Inventory Requirement;
    }
}
