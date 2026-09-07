using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Game;
using UnityEngine;

public class RandomizerInventory : SaveSerialize {
    public bool FinishedGinsoEscape {
        get => GetRandomizerItem(950) == 1;
        set => SetRandomizerItem(950, value ? 1 : 0);
    }

    public static RandomizerInventory Initialize() {
        var inventory = new GameObject("randomizerInventory").AddComponent<RandomizerInventory>();
        inventory.MoonGuid = new MoonGuid(new Guid("3daedf09-4080-405a-8b5b-bf35a4652fb1"));
        inventory.RegisterToSaveSceneManager(GameController.Instance.GetComponent<SaveSceneManager>());
        return inventory;
    }

    private Dictionary<int, int> randomizerItems = new Dictionary<int, int>();

    public int SetRandomizerItem(int code, int value) {
        randomizerItems[code] = value;
        return value;
    }

    public int GetRandomizerItem(int code) {
        if (randomizerItems.ContainsKey(code)) {
            return randomizerItems[code];
        }

        return 0;
    }

    public int IncRandomizerItem(int code, int value) {
        return SetRandomizerItem(code, GetRandomizerItem(code) + value);
    }

    public void Clear() {
        randomizerItems.Clear();
    }

    // Ids that survive a death or a reload. One range, so that "does this persist" is a
    // property of where an id lives rather than a list to remember: 4000-4599 stats, 4501 up
    // warmth fragments (one each, no fixed end), 7000-7099 bingo goals that record something
    // that HAPPENED rather than something you hold. Anything a save should forget goes below.
    public static bool KeptOnDeath(int code) {
        return code >= 4000 && code < 10000;
    }

    // Which slot the in-memory preserved values belong to, stored as slot+1 so an absent
    // stamp claims nothing. Sits just past the preserved range so it cannot vouch for itself.
    private const int SlotStamp = 10000;

    // End of the internally-reserved range.
    private const int LastRandoSlot = 20000;

    // A seed may not write the multiworld grant bitfields -- the tick redelivers items by
    // diffing them, so a forged bit loses an item silently -- nor the slot stamp and the
    // practice block above it, which the save machinery reads back as truth.
    public static bool Writable(int id) {
        return id >= 0 && (id < SlotStamp || id >= LastRandoSlot)
            && (id < RandomizerMW.GrantedSlotsBase || id > RandomizerMW.GrantedSlotsLast);
    }

    public static int Read(int id) {
        if (id < 0) {
            Randomizer.LogError("slot " + id + ": slots start at 0");
            return 0;
        }

        if (Characters.Sein == null) {
            Randomizer.LogError("slot " + id + ": read before the game was ready, using 0");
            return 0;
        }

        return Characters.Sein.Inventory.GetRandomizerItem(id);
    }

    public static void Write(int id, int value) {
        if (!Writable(id)) {
            Randomizer.LogError("slot " + id + " is the client's to write, not a seed's");
            return;
        }

        if (Characters.Sein == null) {
            Randomizer.LogError("slot " + id + ": written before the game was ready");
            return;
        }

        Characters.Sein.Inventory.SetRandomizerItem(id, value);
    }

    // a plain number, {n} for whatever slot n holds, or (a OP b): 1 when the comparison
    // holds and 0 when it does not, a and b each a number or {n}, OP one of == != < > <= >=
    public static bool Value(string text, out int value) {
        text = (text ?? "").Trim();
        if (text.Length > 2 && text[0] == '(' && text[text.Length - 1] == ')') {
            return Comparison(text.Substring(1, text.Length - 2), out value);
        }

        return Operand(text, out value);
    }

    // two-character operators first, so <= is not read as < followed by junk
    private static readonly string[] Comparators = { "==", "!=", "<=", ">=", "<", ">" };

    private static bool Comparison(string text, out int value) {
        value = 0;
        foreach (var op in Comparators) {
            var at = text.IndexOf(op);
            if (at < 0) {
                continue;
            }

            int left, right;
            if (!Operand(text.Substring(0, at), out left) || !Operand(text.Substring(at + op.Length), out right)) {
                return false;
            }

            var holds = op == "==" ? left == right : op == "!=" ? left != right : op == "<=" ? left <= right
                : op == ">=" ? left >= right : op == "<" ? left < right : left > right;
            value = holds ? 1 : 0;
            return true;
        }

        return false;
    }

    private static bool Operand(string text, out int value) {
        text = (text ?? "").Trim();
        if (text.Length > 2 && text[0] == '{' && text[text.Length - 1] == '}') {
            int id;
            if (int.TryParse(text.Substring(1, text.Length - 2).Trim(), out id)) {
                value = Read(id);
                return true;
            }
        }

        return int.TryParse(text, out value);
    }

    // RI: slot=value, slot+=value, slot-=value, the value being a number or {n}
    public static void Apply(string text) {
        var eq = (text ?? "").IndexOf('=');
        if (eq < 1) {
            Randomizer.LogError("RI|" + text + ": a slot write is n=v, n+=v or n-=v");
            return;
        }

        var left = text.Substring(0, eq).Trim();
        var op = left.Length > 0 ? left[left.Length - 1] : '=';
        if (op == '+' || op == '-') {
            left = left.Substring(0, left.Length - 1).Trim();
        } else {
            op = '=';
        }

        int id, value;
        if (!int.TryParse(left, out id) || !Value(text.Substring(eq + 1), out value)) {
            Randomizer.LogError("RI|" + text + ": a slot write is n=v, n+=v or n-=v");
            return;
        }

        Write(id, op == '+' ? Read(id) + value : op == '-' ? Read(id) - value : value);
    }

    private static readonly Regex SlotRef = new Regex(@"\{(-?\d+)\}");

    // {n} in a message is whatever slot n holds
    public static string ResolveSlots(string text) {
        if (text == null || text.IndexOf('{') < 0) {
            return text;
        }

        return SlotRef.Replace(text, m => Read(int.Parse(m.Groups[1].Value)).ToString());
    }

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            // A practice run owns its stat block until it ends: it survives every load,
            // slot swap and file copy the session makes, and nothing else is worth keeping.
            var slot = SaveSlotsManager.CurrentSlotIndex + 1;
            var preserve = PracticeController.Active
                ? randomizerItems.Where(item => item.Key >= PracticeController.FirstStat
                    && item.Key <= PracticeController.LastStat).ToList()
                : GetRandomizerItem(SlotStamp) == slot
                    ? randomizerItems.Where(item => KeptOnDeath(item.Key)).ToList()
                    : new List<KeyValuePair<int, int>>();

            randomizerItems.Clear();
            var count = ar.Serialize(0);
            for (var i = 0; i < count; i++) {
                randomizerItems[ar.Serialize(0)] = ar.Serialize(0);
            }

            foreach (var kvp in preserve) {
                randomizerItems[kvp.Key] = kvp.Value;
            }

            randomizerItems[SlotStamp] = slot;
        } else {
            randomizerItems[SlotStamp] = SaveSlotsManager.CurrentSlotIndex + 1;
            ar.Serialize(randomizerItems.Count);
            foreach (var kvp in randomizerItems) {
                ar.Serialize(kvp.Key);
                ar.Serialize(kvp.Value);
            }
        }
    }
}
