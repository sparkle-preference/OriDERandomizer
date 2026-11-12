// Plain-value entry points over RandomizerAction and RandomizerInventory for pickups_props.py.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

static class PickupsGlue {
    static readonly FieldInfo Items =
        typeof(RandomizerInventory).GetField("randomizerItems", BindingFlags.NonPublic | BindingFlags.Instance);

    // a MonoBehaviour can't be constructed offline, so the inventory skips its constructor
    public static void Reset(Dictionary<int, int> items) {
        var inventory = (RandomizerInventory)FormatterServices.GetUninitializedObject(typeof(RandomizerInventory));
        Items.SetValue(inventory, new Dictionary<int, int>(items));
        Randomizer.Inventory = inventory;
        Randomizer.Errors.Clear();
        Randomizer.Logs.Clear();
        RandomizerBoxes.Masks.Clear();
        RandomizerBoxes.Moves.Clear();
        Boxes(new long[0], new long[0], new bool[0], new bool[0]);
        RandomizerInventory.ForgetComplaints();
        Clock = 0;
        RandomizerInventory.Now = () => Clock;
    }

    // the milliseconds the error cap sees; tests move it by hand
    public static int Clock;

    public static void Tick(int ms) { Clock += ms; }

    public static void Boxes(long[] xs, long[] ys, bool[] gone, bool[] off) {
        RandomizerBoxes.CornerX = xs;
        RandomizerBoxes.CornerY = ys;
        RandomizerBoxes.Gone = gone;
        RandomizerBoxes.Off = off;
    }

    // [ok, the number as written, the problem]
    public static string[] Evaluate(string text) {
        decimal value;
        string problem;
        var ok = RandomizerInventory.Evaluate(text, out value, out problem);
        return new[] { ok ? "ok" : "refused", value.ToString(System.Globalization.CultureInfo.InvariantCulture), problem };
    }

    public static Dictionary<int, int> Snapshot() {
        return new Dictionary<int, int>((Dictionary<int, int>)Items.GetValue(Randomizer.Inventory));
    }

    public static string[] Construct(string code, string value) {
        var action = new RandomizerAction(code, value);
        return new[] { action.Action, action.ValAsStr() };
    }

    public static List<string[]> Decompose(string code, string value) {
        return new RandomizerAction(code, value).Decompose().Select(a => new[] { a.Action, a.ValAsStr() }).ToList();
    }

    public static string[] AsMulti(string[] codes, string[] values, bool repeatable) {
        var actions = codes.Select((c, i) => new RandomizerAction(c, values[i])).ToList();
        var multi = RandomizerAction.AsMulti(actions, repeatable);
        return new[] { multi.Action, (string)multi.Value };
    }

    // pieces as the grant reads them, then packed back as WireSafe packs them
    public static string[] Repack(string code, string value) {
        var multi = RandomizerAction.AsMulti(new RandomizerAction(code, value).Decompose(), code == "RP");
        return new[] { multi.Action, (string)multi.Value };
    }

    public static List<string> NameProblems(string[] lines) {
        var problems = new List<string>();
        PickupNameRules.Parse(lines, problems);
        return problems;
    }

    public static string Message(string code, string id, bool cues) {
        RandomizerSettings.Accessibility.TextCues.Value = cues;
        try {
            return RandomizerItems.Message(code, id);
        } finally {
            RandomizerSettings.Accessibility.TextCues.Value = false;
        }
    }
}
