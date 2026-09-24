public class RandomizerResources {
    public static byte[] ReadResource(string name) {
        using (var stream = typeof(RandomizerResources).Assembly.GetManifestResourceStream(name)) {
            if (stream == null) {
                var available = string.Join(", ", ListResources());
                Randomizer.log($"Failed to read resource '{name}'. Resource not found in assembly. Available resources: [{available}]");
                return null;
            }

            var length = (int)stream.Length;
            var buffer = new byte[length];
            int read;
            if ((read = stream.Read(buffer, 0, length)) != length) {
                // in-memory resource streams read fully in one call in practice
                Randomizer.LogError($"Failed to read resource '{name}'. Only read {read} bytes of {length}.");
                return null;
            }

            return buffer;
        }
    }

    public static string[] ListResources() {
        return typeof(RandomizerResources).Assembly.GetManifestResourceNames();
    }
}
