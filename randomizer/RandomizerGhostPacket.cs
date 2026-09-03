using System.Collections.Generic;
using System.IO;
using UnityEngine;

using Sample = RandomizerGhost.Sample;

// The motion packet, versioned from the first byte. Little-endian, written by hand rather than
// with BitConverter so the layout is the same wherever it runs.
//
//   u8   version        u16  clipId        u8   flags2      optional, per flags:
//   u8   playerId       u8   clipTime                         u8  bashAngle
//   u16  seq            u8   flags                            f32 bashTargetX, bashTargetY
//   f32  t              u8   roll                             f32 aimX, aimY
//   f32  x, y                                                 u8  wallAngle
//                                                             f32 soulX, soulY   (flags2)
//
// 22 bytes for ordinary movement, 48 with every optional field present. Position stays float32:
// the world spans roughly 9,700 x 12,700 units, too wide for i16 at a useful precision.
public static class RandomizerGhostPacket {
    public const byte Version = 1;

    public const int MaxSize = 48;

    private const int FaceLeft = 1 << 0;
    private const int Charging = 1 << 1;
    private const int Charged = 1 << 2;
    private const int Bashing = 1 << 3;
    private const int Aiming = 1 << 4;
    private const int WallAiming = 1 << 5;
    private const int Triple = 1 << 6;
    private const int Dead = 1 << 7;

    private const int SoulLinked = 1 << 0;

    // Ori's sprite mirrors by turning 180 degrees about Y rather than by a negative scale --
    // Transform.lossyScale cannot report a mirror, which is why the recording never shows one.
    private static readonly Quaternion Flip = Quaternion.Euler(0f, 180f, 0f);

    public static int Encode(byte[] into, Sample sample, byte playerId, ushort seq) {
        var faceLeft = Mathf.Abs(sample.Rotation.y) > 0.5f;
        var flags = 0;
        if (faceLeft) { flags |= FaceLeft; }
        if (sample.Charge == 1) { flags |= Charging; }
        if (sample.Charge == 2) { flags |= Charged; }
        if (!float.IsNaN(sample.BashAngle)) { flags |= Bashing; }
        if (!float.IsNaN(sample.GrenadeAim.x)) { flags |= Aiming; }
        if (!float.IsNaN(sample.WallAim)) { flags |= WallAiming; }
        if (sample.Triple) { flags |= Triple; }
        if (sample.Died) { flags |= Dead; }

        var flags2 = 0;
        if (!float.IsNaN(sample.SoulLink.x)) { flags2 |= SoulLinked; }

        var at = 0;
        into[at++] = Version;
        into[at++] = playerId;
        at = U16(into, at, seq);
        at = F32(into, at, sample.Time);
        at = F32(into, at, sample.Position.x);
        at = F32(into, at, sample.Position.y);
        at = U16(into, at, (ushort)RandomizerGhostAnimations.IndexOf(sample.Animation));
        into[at++] = Fraction(sample.AnimationTime, RandomizerGhost.Duration(sample.Animation));
        into[at++] = (byte)flags;
        into[at++] = Degrees(Roll(sample.Rotation, faceLeft));
        into[at++] = (byte)flags2;

        if ((flags & Bashing) != 0) {
            into[at++] = Degrees(sample.BashAngle);
            // the arrow belongs on the thing being bashed, not on Ori; full floats, since it
            // rides only on bash packets
            at = F32(into, at, sample.BashTarget.x);
            at = F32(into, at, sample.BashTarget.y);
        }

        if ((flags & Aiming) != 0) {
            at = F32(into, at, sample.GrenadeAim.x);
            at = F32(into, at, sample.GrenadeAim.y);
        }

        if ((flags & WallAiming) != 0) {
            into[at++] = Degrees(sample.WallAim);
        }

        if ((flags2 & SoulLinked) != 0) {
            at = F32(into, at, sample.SoulLink.x);
            at = F32(into, at, sample.SoulLink.y);
        }

        return at;
    }

    // Returns false for a packet from a version we do not speak. Everything else is either
    // present or flagged absent, so there is nothing else to reject.
    public static bool Decode(byte[] from, int length, out Sample sample, out byte playerId, out ushort seq) {
        sample = new Sample();
        playerId = 0;
        seq = 0;
        if (length < 22 || from[0] != Version) {
            return false;
        }

        var at = 1;
        playerId = from[at++];
        seq = (ushort)(from[at] | (from[at + 1] << 8));
        at += 2;
        sample.Time = Float(from, ref at);
        var x = Float(from, ref at);
        var y = Float(from, ref at);
        sample.Position = new Vector3(x, y, 0f);
        var clip = (ushort)(from[at] | (from[at + 1] << 8));
        at += 2;
        var clipTime = from[at++];
        var flags = from[at++];
        var roll = from[at++] * 360f / 256f;
        var flags2 = from[at++];

        sample.Animation = RandomizerGhostAnimations.NameOf(clip) ?? "";
        sample.AnimationTime = clipTime / 255f * RandomizerGhost.Duration(sample.Animation);
        sample.Rotation = Quaternion.Euler(0f, (flags & FaceLeft) != 0 ? 180f : 0f, roll);
        // scale is not sent: it is Ori's own, the same for every player, and a mirror lives in
        // the rotation rather than in a sign here
        sample.Scale = RandomizerGhost.GhostScale();
        sample.Charge = (flags & Charged) != 0 ? 2 : ((flags & Charging) != 0 ? 1 : 0);
        sample.Triple = (flags & Triple) != 0;
        sample.Died = (flags & Dead) != 0;

        sample.BashAngle = float.NaN;
        sample.BashTarget = new Vector2(float.NaN, float.NaN);
        if ((flags & Bashing) != 0 && at + 9 <= length) {
            sample.BashAngle = from[at++] * 360f / 256f;
            var bx = Float(from, ref at);
            var by = Float(from, ref at);
            sample.BashTarget = new Vector2(bx, by);
        }
        if ((flags & Aiming) != 0 && at + 8 <= length) {
            var ax = Float(from, ref at);
            var ay = Float(from, ref at);
            sample.GrenadeAim = new Vector2(ax, ay);
        } else {
            sample.GrenadeAim = new Vector2(float.NaN, float.NaN);
        }

        sample.WallAim = (flags & WallAiming) != 0 && at < length ? from[at++] * 360f / 256f : float.NaN;

        sample.SoulLink = new Vector2(float.NaN, float.NaN);
        if ((flags2 & SoulLinked) != 0 && at + 8 <= length) {
            var sx = Float(from, ref at);
            var sy = Float(from, ref at);
            sample.SoulLink = new Vector2(sx, sy);
        }

        return true;
    }

    // A .ghost file: u8 version, u32 sample count, f32 seconds, then every packet behind a u8
    // length. The packets are the wire format, so a file ghost costs the same precision.
    public const byte FileVersion = 1;

    public static byte[] Pack(List<Sample> samples) {
        var buffer = new byte[MaxSize];
        var head = new byte[9];
        head[0] = FileVersion;
        U32(head, 1, (uint)samples.Count);
        F32(head, 5, RandomizerGhost.Length(samples));
        using (var stream = new MemoryStream()) {
            stream.Write(head, 0, head.Length);
            for (var i = 0; i < samples.Count; i++) {
                var length = Encode(buffer, samples[i], 0, (ushort)i);
                stream.WriteByte((byte)length);
                stream.Write(buffer, 0, length);
            }

            return stream.ToArray();
        }
    }

    // Empty for anything unreadable; a truncated file keeps the samples before the cut.
    public static List<Sample> Unpack(byte[] data) {
        var samples = new List<Sample>();
        if (data == null || data.Length < 9 || data[0] != FileVersion) {
            return samples;
        }

        var packet = new byte[MaxSize];
        var at = 9;
        while (at < data.Length) {
            int length = data[at++];
            if (length == 0 || length > MaxSize || at + length > data.Length) {
                break;
            }

            System.Array.Copy(data, at, packet, 0, length);
            at += length;
            Sample sample;
            byte who;
            ushort seq;
            if (Decode(packet, length, out sample, out who, out seq)) {
                samples.Add(sample);
            }
        }

        return samples;
    }

    // The mirror is a rotation, so it has to come back out before the roll underneath it means
    // anything. Euler decomposition would do it too, but it is free to pick a different triple.
    private static float Roll(Quaternion rotation, bool faceLeft) {
        var flat = faceLeft ? Quaternion.Inverse(Flip) * rotation : rotation;
        return Mathf.Repeat(2f * Mathf.Atan2(flat.z, flat.w) * Mathf.Rad2Deg, 360f);
    }

    private static byte Degrees(float angle) {
        return (byte)Mathf.Clamp(Mathf.Round(Mathf.Repeat(angle, 360f) * 256f / 360f), 0f, 255f);
    }

    private static byte Fraction(float value, float span) {
        return span <= 0.0001f ? (byte)0 : (byte)Mathf.Clamp(Mathf.Round(value / span * 255f), 0f, 255f);
    }

    private static int U16(byte[] into, int at, ushort value) {
        into[at] = (byte)(value & 0xFF);
        into[at + 1] = (byte)(value >> 8);
        return at + 2;
    }

    private static int U32(byte[] into, int at, uint value) {
        into[at] = (byte)(value & 0xFF);
        into[at + 1] = (byte)((value >> 8) & 0xFF);
        into[at + 2] = (byte)((value >> 16) & 0xFF);
        into[at + 3] = (byte)((value >> 24) & 0xFF);
        return at + 4;
    }

    private static int F32(byte[] into, int at, float value) {
        var bits = System.BitConverter.ToUInt32(System.BitConverter.GetBytes(value), 0);
        into[at] = (byte)(bits & 0xFF);
        into[at + 1] = (byte)((bits >> 8) & 0xFF);
        into[at + 2] = (byte)((bits >> 16) & 0xFF);
        into[at + 3] = (byte)((bits >> 24) & 0xFF);
        return at + 4;
    }

    private static float Float(byte[] from, ref int at) {
        var bits = (uint)(from[at] | (from[at + 1] << 8) | (from[at + 2] << 16) | (from[at + 3] << 24));
        at += 4;
        return System.BitConverter.ToSingle(System.BitConverter.GetBytes(bits), 0);
    }
}
