using UnityEngine;

#nullable enable

public class RandomizerResources {
    public static byte[]? ReadResource(string name) {
        using var stream = typeof(RandomizerResources).Assembly.GetManifestResourceStream(name);

        if (stream == null) {
            var available = string.Join(", ", ListResources());
            Randomizer.log($"Failed to read resource '{name}'. Resource not found in assembly. Available resources: [{available}]");
            return null;
        }

        var length = (int)stream.Length;
        var buffer = new byte[length];
        int read;
        if ((read = stream.Read(buffer, 0, length)) != length) {
            // Read of resource somehow failed.
            // (Resource streams should always fully read in one `Read` call, since they're already loaded in memory.
            // I'm not sure if there's a guarantee, but it has worked so far.)
            Randomizer.LogError($"Failed to read resource '{name}'. Only read {read} bytes of {length}.");
            return null;
        }

        return buffer;
    }

    public static Texture2D? LoadTexture(string name) {
        var bytes = ReadResource(name);
        if (bytes == null) {
            return null;
        }

        var texture = new Texture2D(0, 0) {
            name = name,
        };
        texture.LoadImage(bytes);
        return texture;
    }

    public static string[] ListResources() {
        return typeof(RandomizerResources).Assembly.GetManifestResourceNames();
    }
}
