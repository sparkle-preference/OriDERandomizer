using System;
using System.Collections.Generic;
using System.Globalization;
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

    private Dictionary<int, int> randomizerItems = new();

    public int SetRandomizerItem(int code, int value) {
        randomizerItems[code] = value;
        if (code is >= RandomizerBoxes.FirstBitId and <= RandomizerBoxes.LastBitId) {
            RandomizerBoxes.MaskUpdated(code);
        }

        return value;
    }

    // {T}: physics ticks played, in the client's block so a death rolls it back
    public const int TickSlot = 11000;

    public static void CountTick() {
        if (Randomizer.Inventory is not null) {
            Randomizer.Inventory.IncRandomizerItem(TickSlot, 1);
        }
    }

    // BR|n=x,y puts box n's top-left corner at (x, y), rounded to hundredths; += and -= move it, bare BR|n puts it back
    public static void MoveBox(string text) {
        text ??= "";
        var key = "BR|" + text;
        var eq = text.IndexOf('=');
        var name = (eq < 0 ? text : text.Substring(0, eq)).Trim();
        var op = eq >= 0 && name.Length > 0 ? name[name.Length - 1] : '=';
        if (op == '+' || op == '-') {
            name = name.Substring(0, name.Length - 1).Trim();
        } else {
            op = '=';
        }

        if (!int.TryParse(name, out var n) || !RandomizerBoxes.Exists(n)) {
            Complain(key, key + ":this seed has no box " + name);
            return;
        }

        if (RandomizerBoxes.Deleted(n)) {
            return;
        }

        RandomizerBoxes.Offset(n, out var wasX, out var wasY);
        decimal x = 0, y = 0;
        if (eq >= 0) {
            var xy = text.Substring(eq + 1);
            var comma = xy.IndexOf(',');
            if (comma < 0) {
                Complain(key, key + ":a move is BR|n=x,y, BR|n+=x,y or BR|n-=x,y");
                return;
            }

            if (!Evaluate(xy.Substring(0, comma), out var dx, out var problem)
                || !Evaluate(xy.Substring(comma + 1), out var dy, out problem)) {
                Complain(key, key + ":" + problem);
                return;
            }

            if (!RandomizerBoxes.Corner(n, out var cornerX, out var cornerY)) {
                Complain(key, key + ":box " + n + " is too far out to move");
                return;
            }

            var sign = op == '-' ? -1 : 1;
            var hx = Math.Round(dx * 100, MidpointRounding.AwayFromZero);
            var hy = Math.Round(dy * 100, MidpointRounding.AwayFromZero);
            x = op == '=' ? hx - cornerX : wasX + sign * hx;
            y = op == '=' ? hy - cornerY : wasY + sign * hy;
        }

        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) {
            Complain(key, key + ":that's too far to move a box");
            return;
        }

        RandomizerBoxes.SetOffset(n, (int)x, (int)y);
    }

    public int GetRandomizerItem(int code) {
        return randomizerItems.TryGetValue(code, out var value) ? value : 0;
    }

    public int IncRandomizerItem(int code, int value) {
        return SetRandomizerItem(code, GetRandomizerItem(code) + value);
    }

    public void Clear() {
        randomizerItems.Clear();
    }

    // Survive death and reload: 4000-4499 stats, 4500+ warmth fragments, 7000-7099 bingo deaths,
    // 8000-8999 plando. Anything a save should forget goes below 4000.
    public static bool KeptOnDeath(int code) {
        return code >= 4000 && code < 10000;
    }

    // Which slot the in-memory preserved values belong to, stored as slot+1 so an absent
    // stamp claims nothing. Sits just past the preserved range so it cannot vouch for itself.
    private const int SlotStamp = 10000;

    // End of the internally-reserved range.
    private const int LastRandoSlot = 20000;

    // Seeds may not write the MW grant bitfields (a forged bit silently eats an item) nor
    // 10000-19999, the slot stamp, practice block and tick count the save reads back as truth.
    public static bool Writable(int id) {
        return id >= 0 && (id < SlotStamp || id >= LastRandoSlot)
            && (id < RandomizerMW.GrantedSlotsBase || id > RandomizerMW.GrantedSlotsLast);
    }

    public static int Read(int id) {
        if (id < 0) {
            Complain("slot " + id, "slot " + id + ": slots start at 0");
            return 0;
        }

        return Randomizer.Inventory.GetRandomizerItem(id);
    }

    public static Func<int> Now = () => Environment.TickCount;

    private static readonly Dictionary<string, int> LastComplaint = new();

    // a scripting error at most once per 5 s per key, so a box firing every frame can't flood the screen
    public static void Complain(string key, string message) {
        var now = Now();
        if (LastComplaint.TryGetValue(key, out var at) && unchecked(now - at) < 5000) {
            return;
        }

        if (LastComplaint.Count >= 1024) {
            LastComplaint.Clear();
        }

        LastComplaint[key] = now;
        Randomizer.LogError(message);
    }

    public static void ForgetComplaints() {
        LastComplaint.Clear();
    }

    // RI, BM and BR values: math on numbers, reads and sin()/cos() (* / % first, / dropping the remainder),
    // or comparisons, each (a OP b) and 1 or 0, joined by AND, OR and NOT (AND first). Only sin and cos nest.
    public static bool ParseValue(string text, out int value) {
        value = 0;
        if (!Evaluate(text, out var number, out _)) {
            return false;
        }

        value = ToSlot(number, out _);
        return true;
    }

    // false with the reason when text is not a value or its math can't be done
    public static bool Evaluate(string text, out decimal value, out string problem) {
        value = 0;
        var parsed = Parsed(text ?? "");
        problem = parsed.Problem;
        return problem == null && parsed.Eval(out value, out problem);
    }

    // a slot holds whole numbers, so a fraction rounds, halves away from zero; past 32 bits it wraps
    private static int ToSlot(decimal number, out bool rounded) {
        var whole = Math.Round(number, MidpointRounding.AwayFromZero);
        rounded = whole != number;
        return unchecked((int)(long)(whole - 4294967296m * decimal.Truncate(whole / 4294967296m)));
    }

    public static string Show(decimal number) {
        return number == 0 ? "0" : number.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // every step stays under this: past 2^63, so *= on slots still wraps, and low enough that decimal's
    // 28 digits keep / exact at eight places
    private const decimal Biggest = 10_000_000_000_000_000_000m;

    private const int Places = 8;

    private static readonly Dictionary<string, SlotValue> ParsedValues = new();

    private static SlotValue Parsed(string text) {
        if (!ParsedValues.TryGetValue(text, out var parsed)) {
            if (ParsedValues.Count >= 4096) {
                ParsedValues.Clear();
            }

            ParsedValues[text] = parsed = SlotValue.Parse(text);
        }

        return parsed;
    }

    // a * b rounded to eight places: the whole and fraction parts multiply exactly, and all four products share a sign
    private static decimal Times(decimal a, decimal b) {
        decimal ai = decimal.Truncate(a), af = a - ai, bi = decimal.Truncate(b), bf = b - bi;
        return ai * bi + ai * bf + af * bi + Math.Round(af * bf, Places, MidpointRounding.AwayFromZero);
    }

    // degrees in, eight places out, rounded by hand so no runtime's double-to-decimal conversion is involved
    private static decimal SinCos(decimal degrees, bool cosine) {
        var turn = degrees - 360 * decimal.Truncate(degrees / 360);
        var radians = (double)(long)(turn * 100_000_000m) / 1e8 * Math.PI / 180;
        var scaled = (cosine ? Math.Cos(radians) : Math.Sin(radians)) * 1e8;
        var whole = Math.Truncate(scaled);
        if (Math.Abs(scaled - whole) >= 0.5) {
            whole += Math.Sign(scaled);
        }

        return (long)whole / 100_000_000m;
    }

    // / drops the remainder and % is what it dropped; * keeps eight places, rounding the rest
    private static bool Arith(decimal a, char op, decimal b, out decimal result, out string problem) {
        result = 0;
        problem = null;
        if ((op == '/' || op == '%') && b == 0) {
            problem = "division by zero";
            return false;
        }

        try {
            switch (op) {
                case '+': result = a + b; break;
                case '-': result = a - b; break;
                case '*': result = Times(a, b); break;
                case '/': result = decimal.Truncate(a / b); break;
                default: result = a - b * decimal.Truncate(a / b); break;
            }
        } catch (OverflowException) {
            result = Biggest;
        }

        if (Math.Abs(result) >= Biggest) {
            problem = "a number got too big";
            return false;
        }

        return true;
    }

    // two-character operators first, so <= is not read as < followed by junk
    private static readonly string[] Comparators = { "==", "!=", "<=", ">=", "<", ">" };

    private static bool Is(string word, string keyword) {
        return string.Equals(word, keyword, StringComparison.OrdinalIgnoreCase);
    }

    // parsed whole before any slot is read, so a typo refuses whatever the slots hold
    private sealed class SlotValue {
        public string Problem;
        // a value with sin or cos in it is rounded into a slot without a warning
        public bool Trig;
        private Sum math;
        private readonly List<List<Clause>> anyOf = new();

        public static SlotValue Parse(string text) {
            var r = new Reader(text);
            var parsed = new SlotValue();
            try {
                if (r.Peek() == '(' || r.AtNot()) {
                    parsed.ParseClauses(r);
                } else {
                    parsed.math = Sum.Parse(r);
                    if (r.AtWord()) {
                        throw r.Fail("AND, OR and NOT join comparisons, each in its own ()");
                    }
                }

                if (r.Skip() < text.Length) {
                    throw r.Fail("unexpected text");
                }
            } catch (FormatException e) {
                parsed.Problem = e.Message;
            }

            parsed.Trig = r.Trig;
            return parsed;
        }

        private void ParseClauses(Reader r) {
            var all = new List<Clause>();
            anyOf.Add(all);
            while (true) {
                all.Add(Clause.Parse(r));
                var at = r.Skip();
                var word = r.Word();
                if (word == null) {
                    return;
                }

                if (Is(word, "OR")) {
                    all = new List<Clause>();
                    anyOf.Add(all);
                } else if (!Is(word, "AND")) {
                    throw r.Fail("expected AND or OR", at);
                }
            }
        }

        // short-circuits: a clause after a settled AND or OR is never read
        public bool Eval(out decimal value, out string problem) {
            value = 0;
            problem = null;
            if (math != null) {
                return math.Eval(out value, out problem);
            }

            foreach (var all in anyOf) {
                var holds = true;
                foreach (var clause in all) {
                    if (!clause.Eval(out holds, out problem)) {
                        return false;
                    }

                    if (!holds) {
                        break;
                    }
                }

                if (holds) {
                    value = 1;
                    return true;
                }
            }

            return true;
        }
    }

    // { NOT } ( math OP math )
    private sealed class Clause {
        private bool not;
        private Sum left, right;
        private string op;

        public static Clause Parse(Reader r) {
            var clause = new Clause();
            var at = r.Skip();
            for (var word = r.Word(); word != null; word = r.Word()) {
                if (!Is(word, "NOT")) {
                    throw r.Fail("expected NOT or (", at);
                }

                clause.not = !clause.not;
                at = r.Skip();
            }

            r.Expect('(');
            clause.left = Sum.Parse(r);
            clause.op = r.Comparator();
            clause.right = Sum.Parse(r);
            if (r.AtWord()) {
                throw r.Fail("each comparison gets its own parentheses");
            }

            r.Expect(')');
            return clause;
        }

        public bool Eval(out bool holds, out string problem) {
            holds = false;
            decimal a, b;
            if (!left.Eval(out a, out problem) || !right.Eval(out b, out problem)) {
                return false;
            }

            holds = op switch {
                "==" => a == b,
                "!=" => a != b,
                "<=" => a <= b,
                ">=" => a >= b,
                "<" => a < b,
                _ => a > b,
            } != not;
            return true;
        }
    }

    // atoms joined by + - * / %
    private sealed class Sum {
        private readonly List<Atom> atoms = new();
        private readonly List<char> ops = new();

        public static Sum Parse(Reader r) {
            var sum = new Sum();
            sum.atoms.Add(Atom.Parse(r));
            while (Operators.IndexOf(r.Peek()) >= 0) {
                sum.ops.Add(r.Text[r.At++]);
                sum.atoms.Add(Atom.Parse(r));
            }

            return sum;
        }

        // left to right, a * / % run folded into its term before the + or - that ends it
        public bool Eval(out decimal value, out string problem) {
            value = 0;
            decimal total = 0;
            if (!atoms[0].Eval(out var term, out problem)) {
                return false;
            }

            var pending = '+';
            for (var i = 0; i < ops.Count; i++) {
                if (!atoms[i + 1].Eval(out var next, out problem)) {
                    return false;
                }

                if (ops[i] == '+' || ops[i] == '-') {
                    if (!Arith(total, pending, term, out total, out problem)) {
                        return false;
                    }

                    pending = ops[i];
                    term = next;
                } else if (!Arith(term, ops[i], next, out term, out problem)) {
                    return false;
                }
            }

            return Arith(total, pending, term, out value, out problem);
        }
    }

    // [+|-] a number with up to eight decimals, {n}, {Bn}, {Bn.x}, {Bn.y}, {T}, {Pos.x}, {Pos.y}, sin(math) or cos(math)
    private sealed class Atom {
        // 'n' a number, 's' a slot, 'b' a box's on/off, 'x' or 'y' its corner, 't' the tick count,
        // 'o' Ori's position (id 0 x, 1 y), 'f' sin or cos of inner
        private char kind = 'n';
        private bool minus;
        private int id;
        private decimal number;
        private bool cosine;
        private Sum inner;

        // the sign is parsed with its digits, so -2147483648 stays in range
        public static Atom Parse(Reader r) {
            var atom = new Atom();
            var sign = r.Peek();
            if (sign == '+' || sign == '-') {
                r.At++;
            }

            if (r.AtWord()) {
                var at = r.At;
                var word = r.Word();
                if (!Is(word, "sin") && !Is(word, "cos")) {
                    throw r.Fail("expected a number, {slot}, sin() or cos()", at);
                }

                r.Expect('(');
                atom.kind = 'f';
                atom.cosine = Is(word, "cos");
                atom.inner = Sum.Parse(r);
                r.Expect(')');
                atom.minus = sign == '-';
                r.Trig = true;
                return atom;
            }

            var c = r.Peek();
            if (c == '{') {
                var close = r.Text.IndexOf('}', r.At);
                if (close < 0) {
                    throw r.Fail("a { without its }");
                }

                atom.ParseRead(r, r.Text.Substring(r.At + 1, close - r.At - 1).Trim());
                atom.minus = sign == '-';
                r.At = close + 1;
                return atom;
            }

            if (c == '(') {
                throw r.Fail("math takes no parentheses");
            }

            var from = r.At;
            var whole = Digits(r).TrimStart('0');
            if (r.At == from) {
                throw r.Fail("expected a number or {slot}");
            }

            if (whole.Length > 10 || (whole.Length == 10 && string.CompareOrdinal(whole, sign == '-' ? "2147483648" : "2147483647") > 0)) {
                throw r.Fail("too big for a slot", from);
            }

            atom.number = whole.Length == 0 ? 0 : decimal.Parse(whole, CultureInfo.InvariantCulture);
            if (r.At < r.Text.Length && r.Text[r.At] == '.') {
                r.At++;
                var places = Digits(r);
                if (places.Length is 0 or > Places) {
                    throw r.Fail("a number takes one to eight decimals", from);
                }

                atom.number += decimal.Parse("0." + places, CultureInfo.InvariantCulture);
            }

            if (sign == '-') {
                atom.number = -atom.number;
            }

            return atom;
        }

        private static string Digits(Reader r) {
            var from = r.At;
            while (r.At < r.Text.Length && r.Text[r.At] is >= '0' and <= '9') {
                r.At++;
            }

            return r.Text.Substring(from, r.At - from);
        }

        private void ParseRead(Reader r, string inner) {
            if (Is(inner, "T")) {
                kind = 't';
                return;
            }

            if (inner.Length == 5 && Is(inner.Substring(0, 4), "Pos.") && (inner[4] is 'x' or 'X' or 'y' or 'Y')) {
                kind = 'o';
                id = inner[4] is 'x' or 'X' ? 0 : 1;
                return;
            }

            if (inner.Length > 1 && (inner[0] == 'B' || inner[0] == 'b')) {
                var dot = inner.IndexOf('.');
                var part = dot < 0 ? "" : inner.Substring(dot + 1);
                if (int.TryParse(dot < 0 ? inner.Substring(1) : inner.Substring(1, dot - 1), NumberStyles.None, CultureInfo.InvariantCulture, out id)
                    && (dot < 0 || Is(part, "x") || Is(part, "y"))) {
                    kind = dot < 0 ? 'b' : char.ToLowerInvariant(part[0]);
                    return;
                }
            } else if (int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) {
                kind = 's';
                return;
            }

            throw r.Fail("expected {slot}, {Bn}, {Bn.x}, {Bn.y}, {T}, {Pos.x} or {Pos.y}");
        }

        public bool Eval(out decimal value, out string problem) {
            problem = null;
            if (kind == 'f') {
                if (!inner.Eval(out value, out problem)) {
                    return false;
                }

                value = SinCos(value, cosine);
            } else {
                value = kind switch {
                    'n' => number,
                    's' => Read(id),
                    't' => Read(TickSlot),
                    'o' => ReadOri(id),
                    _ => ReadBox(id, kind),
                };
            }

            value = minus ? -value : value;
            return true;
        }
    }

    public static Func<Vector2?> OriPosition = () => Characters.Sein ? (Vector2?)Characters.Sein.Position : null;

    // {Pos.x} and {Pos.y}: Ori's position, rounded to hundredths like a box's corner
    public static decimal ReadOri(int part) {
        var at = OriPosition();
        if (at == null) {
            Complain("{Pos}", "{Pos." + (part == 0 ? "x" : "y") + "}: Ori isn't in the world");
            return 0;
        }

        return Math.Round((decimal)(part == 0 ? at.Value.x : at.Value.y), 2, MidpointRounding.AwayFromZero);
    }

    // {Bn} is 1 while box n is on; {Bn.x} and {Bn.y} its top-left corner, moves included
    public static decimal ReadBox(int box, char part) {
        if (!RandomizerBoxes.Exists(box)) {
            Complain("{B" + box + "}", "{B" + box + "}: this seed has no box " + box);
            return 0;
        }

        if (part == 'b') {
            return RandomizerBoxes.IsOn(box) ? 1 : 0;
        }

        if (!RandomizerBoxes.Corner(box, out var x, out var y)) {
            Complain("{B" + box + "}", "{B" + box + "." + part + "}: box " + box + " is too far out to read");
            return 0;
        }

        RandomizerBoxes.Offset(box, out var dx, out var dy);
        return (part == 'x' ? x + dx : y + dy) / 100m;
    }

    private sealed class Reader {
        public readonly string Text;
        public int At;

        public Reader(string text) {
            Text = text;
        }

        public int Skip() {
            while (At < Text.Length && char.IsWhiteSpace(Text[At])) {
                At++;
            }

            return At;
        }

        // '\0' past the end
        public char Peek() {
            return Skip() < Text.Length ? Text[At] : '\0';
        }

        private static bool IsLetter(char c) {
            return c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
        }

        public bool Trig;

        public bool AtWord() {
            return IsLetter(Peek());
        }

        // NOT starts a comparison; any other word starts math
        public bool AtNot() {
            var at = At;
            var word = Word();
            At = at;
            return word != null && Is(word, "NOT");
        }

        // a run of letters, or null
        public string Word() {
            if (!AtWord()) {
                return null;
            }

            var from = At;
            while (At < Text.Length && IsLetter(Text[At])) {
                At++;
            }

            return Text.Substring(from, At - from);
        }

        public string Comparator() {
            Skip();
            foreach (var op in Comparators) {
                if (At + op.Length <= Text.Length && string.CompareOrdinal(Text, At, op, 0, op.Length) == 0) {
                    At += op.Length;
                    return op;
                }
            }

            throw Fail(AtWord() ? "each comparison gets its own parentheses" : "expected == != < > <= or >=");
        }

        public void Expect(char c) {
            if (Peek() != c) {
                throw Fail("expected " + c);
            }

            At++;
        }

        public FormatException Fail(string why) {
            return Fail(why, Skip());
        }

        public FormatException Fail(string why, int at) {
            return new FormatException(why + (at >= Text.Length ? " at the end" : " at \"" + Text.Substring(at) + "\""));
        }
    }

    // RI: slot=value, or slot+=value with any of + - * / %
    public static void Apply(string text) {
        var key = "RI|" + text;
        var eq = (text ?? "").IndexOf('=');
        if (eq < 1) {
            Complain(key, key + ": a slot write is n=v, n+=v, n-=v, n*=v, n/=v or n%=v");
            return;
        }

        var left = text.Substring(0, eq).Trim();
        var op = left.Length > 0 ? left[left.Length - 1] : '=';
        if (Operators.IndexOf(op) >= 0) {
            left = left.Substring(0, left.Length - 1).Trim();
        } else {
            op = '=';
        }

        int id;
        if (!int.TryParse(left, out id)) {
            Complain(key, key + ": a slot write is n=v, n+=v, n-=v, n*=v, n/=v or n%=v");
            return;
        }

        if (!Evaluate(text.Substring(eq + 1), out var value, out var problem)
            || (op != '=' && !Arith(Read(id), op, value, out value, out problem))) {
            Complain(key, key + ": " + problem);
            return;
        }

        var whole = ToSlot(value, out var rounded);
        if (rounded && !Parsed(text.Substring(eq + 1)).Trig) {
            Complain(key, key + ": a slot holds whole numbers, so " + Show(value) + " became " + whole);
        }

        if (!Writable(id)) {
            Complain(key, key + ": slot " + id + " is the client's to write, not a seed's");
            return;
        }

        Randomizer.Inventory.SetRandomizerItem(id, whole);
    }

    private const string Operators = "+-*/%";

    private static readonly Regex SlotRef = new Regex(@"\{(-?\d+|[Bb]\d+(?:\.[XxYy])?|[Tt]|[Pp][Oo][Ss]\.[XxYy])\}");

    // {n} in a message is whatever slot n holds, {Bn} and {Bn.x} what box n reads, {T} and {Pos.x} the same as in math
    public static string ResolveSlots(string text) {
        if (text == null || text.IndexOf('{') < 0) {
            return text;
        }

        return SlotRef.Replace(text, m => {
            var name = m.Groups[1].Value;
            if (name is "T" or "t") {
                return Read(TickSlot).ToString();
            }

            if (name.Length == 5 && name[0] is 'P' or 'p') {
                return Show(ReadOri(name[4] is 'x' or 'X' ? 0 : 1));
            }

            int id;
            if (name[0] == 'B' || name[0] == 'b') {
                var dot = name.IndexOf('.');
                if (!int.TryParse(dot < 0 ? name.Substring(1) : name.Substring(1, dot - 1), NumberStyles.None, CultureInfo.InvariantCulture, out id)) {
                    Randomizer.log("{" + name + "}: no such box, using 0");
                    return "0";
                }

                return Show(ReadBox(id, dot < 0 ? 'b' : char.ToLowerInvariant(name[dot + 1])));
            }

            if (!int.TryParse(name, out id)) {
                Randomizer.log("{" + name + "}: no such slot, using 0");
                return "0";
            }

            return Read(id).ToString();
        });
    }

    private static readonly Regex BoxRead = new Regex(@"\{\s*[Bb]([0-9]+)(?:\.[XxYy])?\s*\}");

    // the highest box a {Bn} read in text names, or -1
    public static int HighestBoxRead(string text) {
        var named = -1;
        foreach (Match m in BoxRead.Matches(text ?? "")) {
            if (int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) {
                named = Math.Max(named, n);
            }
        }

        return named;
    }

    public override void Serialize(Archive ar) {
        if (ar.Reading) {
            // A practice run owns its stat block until it ends: it survives every load,
            // slot swap and file copy the session makes, and nothing else is worth keeping.
            var slot = SaveSlotsManager.CurrentSlotIndex + 1;
            var preserve = PracticeController.Active
                ? randomizerItems.Where(item => item.Key >= PracticeController.FirstStat
                    && item.Key <= PracticeController.LastStat
                ).ToList()
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

            RandomizerBoxes.Serialize(randomizerItems.Remove(1_000_000_001) ? ar : null);
        } else {
            randomizerItems[SlotStamp] = SaveSlotsManager.CurrentSlotIndex + 1;
            ar.Serialize(randomizerItems.Count);
            foreach (var kvp in randomizerItems) {
                ar.Serialize(kvp.Key);
                ar.Serialize(kvp.Value);
            }
        }

        ar.Serialize(1_000_000_001);
        ar.Serialize(0);
        RandomizerBoxes.Serialize(ar);
    }
}
