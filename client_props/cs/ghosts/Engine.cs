// Engine stand-ins for the ghost codec and signal layer. RandomizerGhost.Sample is a copy of the real one: keep them in step.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine {
    public struct Vector2 {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public struct Vector3 {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Quaternion {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        // Unity's order: Z, then X, then Y
        public static Quaternion Euler(float ex, float ey, float ez) {
            Func<float, float> s = a => (float)Math.Sin(a * Math.PI / 360.0);
            Func<float, float> c = a => (float)Math.Cos(a * Math.PI / 360.0);
            var qx = new Quaternion(s(ex), 0f, 0f, c(ex));
            var qy = new Quaternion(0f, s(ey), 0f, c(ey));
            var qz = new Quaternion(0f, 0f, s(ez), c(ez));
            return qy * qx * qz;
        }

        public static Quaternion Inverse(Quaternion q) { return new Quaternion(-q.x, -q.y, -q.z, q.w); }

        public static Quaternion operator *(Quaternion l, Quaternion r) {
            return new Quaternion(
                l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y,
                l.w * r.y + l.y * r.w + l.z * r.x - l.x * r.z,
                l.w * r.z + l.z * r.w + l.x * r.y - l.y * r.x,
                l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z);
        }
    }

    public static class Mathf {
        public const float Rad2Deg = 57.29578f;
        public static float Abs(float f) { return Math.Abs(f); }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Round(float f) { return (float)Math.Round(f); }
        public static float Atan2(float y, float x) { return (float)Math.Atan2(y, x); }
        public static float Clamp(float v, float min, float max) { return v < min ? min : (v > max ? max : v); }
        public static float Repeat(float t, float length) { return Clamp(t - (float)Math.Floor(t / length) * length, 0f, length); }
    }

    public static class Time {
        public static float time;
        public static float realtimeSinceStartup;
    }

    public class Transform {
    }
}

public static class RandomizerGhost {
    public struct Sample {
        public float Time;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public string Animation;
        public float AnimationTime;
        public int Charge;
        public float BashAngle;
        public Vector2 BashTarget;
        public Vector2 GrenadeAim;
        public float WallAim;
        public bool Triple;
        public bool Died;
        public Vector2 SoulLink;
        public bool InMenu;
        public bool OnTitle;
        public int Menu;
        public uint Skills;
    }

    internal const float InterpolationDelay = 0.12f;

    public const int MenuPause = 0;
    public const int MenuMap = 1;
    public const int MenuTree = 2;

    internal static readonly Color Tint = new Color(0.55f, 0.8f, 1f, 0.35f);

    // every named clip lasts one second; no name, no clip
    internal static float Duration(string name) { return string.IsNullOrEmpty(name) ? 0f : 1f; }

    internal static Vector3 GhostScale() { return new Vector3(3.4f, 3.4f, 1f); }

    internal static float Length(List<Sample> samples) { return samples.Count == 0 ? 0f : samples[samples.Count - 1].Time; }

    public static bool OriHere = true;
    public static readonly List<IGhostSource> Shown = new List<IGhostSource>();

    internal static Transform Sprite() { return OriHere ? new Transform() : null; }

    public static bool AddLive(IGhostSource source) {
        if (!OriHere) {
            return false;
        }

        Shown.Add(source);
        return true;
    }

    public static bool Showing(IGhostSource source) { return Shown.Contains(source); }

    public static void Remove(IGhostSource source) { Shown.Remove(source); }

    public static void ClearEchoes() { }

    public static bool SampleLive(out Sample sample) {
        sample = new Sample {
            Time = UnityEngine.Time.time, Rotation = new Quaternion(0f, 0f, 0f, 1f), Animation = "run",
            BashAngle = float.NaN, BashTarget = new Vector2(float.NaN, float.NaN),
            GrenadeAim = new Vector2(float.NaN, float.NaN), WallAim = float.NaN, SoulLink = new Vector2(float.NaN, float.NaN)
        };
        return true;
    }
}
