using System;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

namespace RandoExts {
    public static class RandomizerExtensions {
        public static T Next<T>(this T src) where T : Enum {
            var Arr = (T[])Enum.GetValues(src.GetType());
            var j = Array.IndexOf(Arr, src) + 1;
            return Arr.Length == j ? Arr[0] : Arr[j];
        }

        public static string Desc<T>(this T value) where T : Enum {
            var field = typeof(T).GetField(value.ToString(), BindingFlags.Static | BindingFlags.Public);
            var attrs = (DescriptionAttribute[])field?.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attrs is [var desc, ..] ? desc.Description : value.ToString();
        }
        public static bool IsNullOrWhitespace(this string value) => string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        public static bool IsWhitespace(this char value) => value.ToString().Trim().Length == 0;

        public static bool TryParseEnum<T>(this string stringValue, bool ignoreCase, out T parsedValue)
            where T : Enum {
            try {
                parsedValue = (T)Enum.Parse(typeof(T), stringValue, ignoreCase);
                return true;
            } catch (Exception) {
                parsedValue = default;
                return false;
            }
        }

        extension(Vector2) {
            public static Vector2 operator /(Vector2 a, Vector2 b) =>
                new(a.x / b.x, a.y / b.y);

            public static Vector2 operator *(Vector2 a, Vector2 b) =>
                new(a.x * b.x, a.y * b.y);
        }
    }
}
