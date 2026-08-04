using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Archive {
    public Archive() {
        MemoryStream = new MemoryStream();
    }

    public MemoryStream MemoryStream {
        get => memoryStream;
        set {
            if (memoryStream != null) {
                ((IDisposable)memoryStream).Dispose();
            }

            if (binaryReader != null) {
                ((IDisposable)binaryReader).Dispose();
            }

            if (binaryWriter != null) {
                ((IDisposable)binaryWriter).Dispose();
            }

            memoryStream = value;
            binaryReader = new BinaryReader(memoryStream);
            binaryWriter = new BinaryWriter(memoryStream);
        }
    }

    public void WriteMemoryStreamToBinaryWriter(BinaryWriter binaryWriter) {
        binaryWriter.Write((int)MemoryStream.Length);
        MemoryStream.WriteTo(binaryWriter.BaseStream);
    }

    public void ReadMemoryStreamFromBinaryReader(BinaryReader binaryReader) {
        var num = binaryReader.ReadInt32();
        MemoryStream.SetLength(num);
        binaryReader.Read(MemoryStream.GetBuffer(), 0, num);
    }

    public bool Reading => !write;

    public bool Writing => write;

    public void ResetStream() {
        MemoryStream.Position = 0L;
    }

    public void WriteMode() {
        ResetStream();
        write = true;
    }

    public void ReadMode() {
        memoryStream.Position = 0L;
        write = false;
    }

    public void Serialize(ref float value) {
        value = Serialize(value);
    }

    public void Serialize(ref int value) {
        value = Serialize(value);
    }

    public void Serialize(ref bool value) {
        value = Serialize(value);
    }

    public void Serialize(ref string value) {
        value = Serialize(value);
    }

    public void Serialize(ref Vector2 value) {
        value = Serialize(value);
    }

    public void Serialize(ref Vector3 value) {
        value = Serialize(value);
    }

    public void Serialize(ref Quaternion value) {
        value = Serialize(value);
    }

    public void Serialize(ref Dictionary<int, int> value) {
        value = Serialize(value);
    }

    public float Serialize(float value) {
        if (write) {
            binaryWriter.Write(value);
            return value;
        }

        return binaryReader.ReadSingle();
    }

    public int Serialize(int value) {
        if (write) {
            binaryWriter.Write(value);
            return value;
        }

        return binaryReader.ReadInt32();
    }

    public bool Serialize(bool value) {
        if (write) {
            binaryWriter.Write(value);
            return value;
        }

        return binaryReader.ReadBoolean();
    }

    public string Serialize(string value) {
        if (write) {
            binaryWriter.Write(value);
            return value;
        }

        return binaryReader.ReadString();
    }

    public Vector2 Serialize(Vector2 value) {
        value.x = Serialize(value.x);
        value.y = Serialize(value.y);
        return value;
    }

    public Vector3 Serialize(Vector3 value) {
        value.x = Serialize(value.x);
        value.y = Serialize(value.y);
        value.z = Serialize(value.z);
        return value;
    }

    public Quaternion Serialize(Quaternion value) {
        value.x = Serialize(value.x);
        value.y = Serialize(value.y);
        value.z = Serialize(value.z);
        value.w = Serialize(value.w);
        return value;
    }

    public Dictionary<int, int> Serialize(Dictionary<int, int> value) {
        var pairs = "";
        if (write) {
            foreach (var key in value.Keys) {
                pairs += key + ":" + value[key] + ",";
            }

            pairs = pairs.TrimEnd(',');

            binaryWriter.Write(pairs);
            return value;
        }

        value.Clear();
        pairs = binaryReader.ReadString();
        foreach (var pair in pairs.Split(',')) {
            var kandv = pair.Split(':');
            value[int.Parse(kandv[0])] = int.Parse(kandv[1]);
        }

        return value;
    }

    public void SerializeVersion(ref int version) {
    }

    private MemoryStream memoryStream = new MemoryStream();

    private BinaryReader binaryReader;

    private BinaryWriter binaryWriter;

    private bool write;
}
