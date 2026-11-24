// Targets for bridge_props.py: the bridge's own conversions, failures and handles.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

static class SelfTest {
    public static string Echo(string s) { return s; }
    public static double Twice(double x) { return x * 2; }
    public static float Single(float x) { return x; }
    public static long Wide(long x) { return x; }
    public static byte[] Bytes(byte[] b) { return b; }
    public static List<string> Rev(List<string> xs) { xs.Reverse(); return xs; }
    public static Dictionary<string, int> Counts(string[] xs) { return xs.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()); }
    public static Dictionary<string, string> Same(Dictionary<string, string> d) { return d; }
    public static bool Parse(string s, out int value) { return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value); }
    public static void Bump(ref int x) { x++; }
    public static string Format(double x) { return x.ToString(); }
    public static void Boom(string why) { throw new InvalidOperationException(why); }
    public static int Deep(int n) { return n <= 0 ? 0 : 1 + Deep(n - 1); }
    public static void Spin() { while (true) { } }
    static int hidden = 7;
    static int Secret() { return hidden; }
}

class Counter {
    public int Count;
    public Counter(int start) { Count = start; }
    public int Add(int n) { Count += n; return Count; }
}
