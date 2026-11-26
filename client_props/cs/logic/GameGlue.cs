// Game-dll values the logic props compare against the server, as plain values.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

public static class GameGlue {
    public static string AbilityName(int id) {
        return ((AbilityType)id).ToString();
    }

    public static string DecimalSeparator(string culture) {
        return CultureInfo.GetCultureInfo(culture).NumberFormat.NumberDecimalSeparator;
    }

    // per KeystoneDoors entry: [source, destination, keysanity id, keysanity cost], read off a fresh RandomizerKeysanity
    public static List<List<object>> KeysanityDoors() {
        const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Instance;
        var keysanity = new RandomizerKeysanity(null);
        var ids = (Dictionary<MoonGuid, int>)typeof(RandomizerKeysanity).GetField("doorKeyMap", any).GetValue(keysanity);
        var cost = typeof(RandomizerKeysanity).GetMethod("countForDoor", any);
        var rows = new List<List<object>>();
        for (var i = 0; i < RandomizerLocationData.KeystoneDoors.Count; i++) {
            var door = new RandomizerLocationManager.KeystoneDoor(i, RandomizerLocationData.KeystoneDoors[i]);
            int id;
            var found = ids.TryGetValue(door.MoonGuid, out id);
            rows.Add(new List<object> { door.Source, door.Destination, found ? id : -1, found ? cost.Invoke(keysanity, new object[] { id }) : -1 });
        }
        return rows;
    }

    // RandomizerEntrances.Doors as [name, x, y, key] rows, in table order
    public static List<List<object>> EntranceDoors() {
        var rows = new List<List<object>>();
        foreach (var door in RandomizerEntrances.Doors) {
            rows.Add(new List<object> { door.Name, door.X, door.Y, door.Key });
        }
        return rows;
    }

    // name -> Key for every RandomizerLocationData.All line, read as Initialize reads it; a throw reads as "!" + its type
    public static Dictionary<string, string> LocationKeys() {
        var keys = new Dictionary<string, string>();
        var reader = new StringReader(RandomizerLocationData.All);
        for (var line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
            var name = line.Trim().Split()[0];
            try {
                keys[name] = new RandomizerLocationManager.Location(line.Trim()).Key.ToString(CultureInfo.InvariantCulture);
            } catch (Exception e) {
                keys[name] = "!" + e.GetType().FullName;
            }
        }
        return keys;
    }
}
