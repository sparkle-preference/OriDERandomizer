using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// A zip file as a name->bytes dictionary, for the .bfrp practice container.
// This runtime has no ZipArchive, so the container format is done by hand:
// reads follow the central directory and accept stored or deflated entries
// (hand-edited files come back from 7-Zip and Explorer deflated); writes
// emit stored-only entries and replace the target file atomically.
public class ZipStore {
    private readonly List<string> order = new List<string>();

    private readonly Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>();

    public List<string> Names {
        get { return new List<string>(order); }
    }

    public bool Has(string name) {
        return entries.ContainsKey(name);
    }

    public byte[] Get(string name) {
        byte[] data;
        return entries.TryGetValue(name, out data) ? data : null;
    }

    public void Set(string name, byte[] data) {
        if (!entries.ContainsKey(name)) {
            order.Add(name);
        }

        entries[name] = data;
    }

    public void Remove(string name) {
        if (entries.Remove(name)) {
            order.Remove(name);
        }
    }

    public static ZipStore Read(string path) {
        var raw = File.ReadAllBytes(path);
        var store = new ZipStore();

        // the end-of-central-directory record sits at most 65535 comment bytes
        // plus its own 22 from the end; the last signature wins
        var eocd = -1;
        var floor = Math.Max(0, raw.Length - 65557);
        for (var i = raw.Length - 22; i >= floor; i--) {
            if (raw[i] == 0x50 && raw[i + 1] == 0x4b && raw[i + 2] == 0x05 && raw[i + 3] == 0x06) {
                eocd = i;
                break;
            }
        }

        if (eocd < 0) {
            throw new IOException(path + ": no end-of-central-directory record; not a zip");
        }

        int count = U16(raw, eocd + 10);
        long cd = U32(raw, eocd + 16);
        if (count == 0xFFFF || cd == 0xFFFFFFFFL) {
            throw new IOException(path + ": zip64 is not supported");
        }

        var at = (int)cd;
        for (var n = 0; n < count; n++) {
            if (U32(raw, at) != 0x02014b50) {
                throw new IOException(path + ": bad central directory entry signature");
            }

            int method = U16(raw, at + 10);
            long crc = U32(raw, at + 16);
            var csize = (int)U32(raw, at + 20);
            var size = (int)U32(raw, at + 24);
            int nameLen = U16(raw, at + 28);
            int extraLen = U16(raw, at + 30);
            int commentLen = U16(raw, at + 32);
            var local = (int)U32(raw, at + 42);
            var name = Encoding.UTF8.GetString(raw, at + 46, nameLen).Replace('\\', '/');
            at += 46 + nameLen + extraLen + commentLen;

            // a folder row carries no bytes worth keeping
            if (name.EndsWith("/")) {
                continue;
            }

            // the local header's own name/extra lengths decide where data starts
            if (U32(raw, local) != 0x04034b50) {
                throw new IOException(path + ": bad local header for " + name);
            }

            var dataAt = local + 30 + U16(raw, local + 26) + U16(raw, local + 28);
            byte[] data;
            if (method == 0) {
                data = new byte[size];
                Array.Copy(raw, dataAt, data, 0, size);
            } else if (method == 8) {
                data = Inflate(raw, dataAt, csize, size);
            } else {
                throw new IOException(path + ": " + name + " uses unsupported compression method " + method);
            }

            if (Crc32(data) != crc) {
                throw new IOException(path + ": " + name + " is corrupt (crc mismatch)");
            }

            store.Set(name, data);
        }

        return store;
    }

    public void Write(string path) {
        var buffer = new MemoryStream();
        var writer = new BinaryWriter(buffer);
        var offsets = new Dictionary<string, long>();
        var dosTime = DosTime(DateTime.Now);

        foreach (var name in order) {
            var data = entries[name];
            var nameBytes = Encoding.UTF8.GetBytes(name);
            offsets[name] = buffer.Position;
            writer.Write(0x04034b50u);
            writer.Write((ushort)20);          // version needed
            writer.Write((ushort)0x0800);      // flags: names are UTF-8
            writer.Write((ushort)0);           // stored
            writer.Write(dosTime);
            writer.Write(Crc32(data));
            writer.Write((uint)data.Length);
            writer.Write((uint)data.Length);
            writer.Write((ushort)nameBytes.Length);
            writer.Write((ushort)0);           // no extra field
            writer.Write(nameBytes);
            writer.Write(data);
        }

        var cdStart = buffer.Position;
        foreach (var name in order) {
            var data = entries[name];
            var nameBytes = Encoding.UTF8.GetBytes(name);
            writer.Write(0x02014b50u);
            writer.Write((ushort)20);          // made by
            writer.Write((ushort)20);          // version needed
            writer.Write((ushort)0x0800);
            writer.Write((ushort)0);
            writer.Write(dosTime);
            writer.Write(Crc32(data));
            writer.Write((uint)data.Length);
            writer.Write((uint)data.Length);
            writer.Write((ushort)nameBytes.Length);
            writer.Write((ushort)0);           // extra
            writer.Write((ushort)0);           // comment
            writer.Write((ushort)0);           // disk
            writer.Write((ushort)0);           // internal attrs
            writer.Write(0u);                  // external attrs
            writer.Write((uint)offsets[name]);
            writer.Write(nameBytes);
        }

        writer.Write(0x06054b50u);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)order.Count);
        writer.Write((ushort)order.Count);
        writer.Write((uint)(buffer.Position - 12 - cdStart));
        writer.Write((uint)cdStart);
        writer.Write((ushort)0);

        // never leave a half-written container where the old one was
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, buffer.ToArray());
        if (File.Exists(path)) {
            File.Replace(temp, path, null);
        } else {
            File.Move(temp, path);
        }
    }

    private static byte[] Inflate(byte[] raw, int at, int csize, int size) {
        var packed = new MemoryStream(raw, at, csize);
        var flate = new DeflateStream(packed, CompressionMode.Decompress);
        var data = new byte[size];
        var got = 0;
        while (got < size) {
            var step = flate.Read(data, got, size - got);
            if (step <= 0) {
                throw new IOException("deflated entry ended " + (size - got) + " bytes early");
            }

            got += step;
        }

        return data;
    }

    private static uint DosTime(DateTime t) {
        return (uint)(((t.Year - 1980) << 25) | (t.Month << 21) | (t.Day << 16)
            | (t.Hour << 11) | (t.Minute << 5) | (t.Second / 2));
    }

    private static int U16(byte[] b, int at) {
        return b[at] | (b[at + 1] << 8);
    }

    private static long U32(byte[] b, int at) {
        return (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));
    }

    private static uint[] crcTable;

    public static uint Crc32(byte[] data) {
        if (crcTable == null) {
            crcTable = new uint[256];
            for (uint i = 0; i < 256; i++) {
                var c = i;
                for (var k = 0; k < 8; k++) {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                crcTable[i] = c;
            }
        }

        var crc = 0xFFFFFFFFu;
        for (var i = 0; i < data.Length; i++) {
            crc = crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
