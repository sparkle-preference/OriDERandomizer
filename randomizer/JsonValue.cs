using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// A json tree, parsed and serialized by hand: this runtime has no json
// library, and the practice container's segment.json is edited by people,
// so errors carry positions. Numbers live as doubles, which hold every
// location coordinate and duration this project uses exactly.
public class JsonValue {
    private Dictionary<string, JsonValue> obj;

    private List<string> objOrder;

    private List<JsonValue> arr;

    private string str;

    private double num;

    private bool flag;

    private int kind;   // 0 null, 1 object, 2 array, 3 string, 4 number, 5 bool

    public bool IsNull { get { return kind == 0; } }

    public bool IsObject { get { return kind == 1; } }

    public bool IsArray { get { return kind == 2; } }

    public bool IsString { get { return kind == 3; } }

    public bool IsNumber { get { return kind == 4; } }

    public bool IsBool { get { return kind == 5; } }

    public string Str { get { return str; } }

    public double Num { get { return num; } }

    public bool Flag { get { return flag; } }

    public static JsonValue NewObject() {
        var v = new JsonValue();
        v.kind = 1;
        v.obj = new Dictionary<string, JsonValue>();
        v.objOrder = new List<string>();
        return v;
    }

    public static JsonValue NewArray() {
        var v = new JsonValue();
        v.kind = 2;
        v.arr = new List<JsonValue>();
        return v;
    }

    public static JsonValue Of(string s) {
        var v = new JsonValue();
        v.kind = 3;
        v.str = s;
        return v;
    }

    public static JsonValue Of(double n) {
        var v = new JsonValue();
        v.kind = 4;
        v.num = n;
        return v;
    }

    public static JsonValue Of(bool b) {
        var v = new JsonValue();
        v.kind = 5;
        v.flag = b;
        return v;
    }

    public static JsonValue Null() {
        return new JsonValue();
    }

    // object access: missing keys read as null-kind so callers can chain
    public JsonValue this[string key] {
        get {
            JsonValue v;
            return kind == 1 && obj.TryGetValue(key, out v) ? v : Null();
        }
    }

    public void Set(string key, JsonValue value) {
        if (kind != 1) {
            throw new InvalidOperationException("Set on a non-object json value");
        }

        if (!obj.ContainsKey(key)) {
            objOrder.Add(key);
        }

        obj[key] = value;
    }

    public List<string> Keys {
        get { return kind == 1 ? new List<string>(objOrder) : new List<string>(); }
    }

    public JsonValue this[int index] {
        get { return kind == 2 && index >= 0 && index < arr.Count ? arr[index] : Null(); }
    }

    public void Add(JsonValue value) {
        if (kind != 2) {
            throw new InvalidOperationException("Add on a non-array json value");
        }

        arr.Add(value);
    }

    public int Count {
        get { return kind == 2 ? arr.Count : kind == 1 ? obj.Count : 0; }
    }

    // --- parsing ---

    public static JsonValue Parse(string text) {
        var at = 0;
        var v = ParseValue(text, ref at);
        SkipSpace(text, ref at);
        if (at != text.Length) {
            throw Bad(text, at, "trailing content after the document");
        }

        return v;
    }

    private static JsonValue ParseValue(string t, ref int at) {
        SkipSpace(t, ref at);
        if (at >= t.Length) {
            throw Bad(t, at, "value expected");
        }

        var c = t[at];
        if (c == '{') {
            return ParseObject(t, ref at);
        }

        if (c == '[') {
            return ParseArray(t, ref at);
        }

        if (c == '"') {
            return Of(ParseString(t, ref at));
        }

        if (c == '-' || (c >= '0' && c <= '9')) {
            return ParseNumber(t, ref at);
        }

        if (Word(t, at, "true")) {
            at += 4;
            return Of(true);
        }

        if (Word(t, at, "false")) {
            at += 5;
            return Of(false);
        }

        if (Word(t, at, "null")) {
            at += 4;
            return Null();
        }

        throw Bad(t, at, "unrecognized value");
    }

    private static JsonValue ParseObject(string t, ref int at) {
        var v = NewObject();
        at++;
        SkipSpace(t, ref at);
        if (at < t.Length && t[at] == '}') {
            at++;
            return v;
        }

        while (true) {
            SkipSpace(t, ref at);
            if (at >= t.Length || t[at] != '"') {
                throw Bad(t, at, "object key expected");
            }

            var key = ParseString(t, ref at);
            SkipSpace(t, ref at);
            if (at >= t.Length || t[at] != ':') {
                throw Bad(t, at, "':' expected after key");
            }

            at++;
            v.Set(key, ParseValue(t, ref at));
            SkipSpace(t, ref at);
            if (at >= t.Length) {
                throw Bad(t, at, "unterminated object");
            }

            if (t[at] == ',') {
                at++;
                continue;
            }

            if (t[at] == '}') {
                at++;
                return v;
            }

            throw Bad(t, at, "',' or '}' expected");
        }
    }

    private static JsonValue ParseArray(string t, ref int at) {
        var v = NewArray();
        at++;
        SkipSpace(t, ref at);
        if (at < t.Length && t[at] == ']') {
            at++;
            return v;
        }

        while (true) {
            v.Add(ParseValue(t, ref at));
            SkipSpace(t, ref at);
            if (at >= t.Length) {
                throw Bad(t, at, "unterminated array");
            }

            if (t[at] == ',') {
                at++;
                continue;
            }

            if (t[at] == ']') {
                at++;
                return v;
            }

            throw Bad(t, at, "',' or ']' expected");
        }
    }

    private static string ParseString(string t, ref int at) {
        at++;
        var sb = new StringBuilder();
        while (true) {
            if (at >= t.Length) {
                throw Bad(t, at, "unterminated string");
            }

            var c = t[at++];
            if (c == '"') {
                return sb.ToString();
            }

            if (c != '\\') {
                sb.Append(c);
                continue;
            }

            if (at >= t.Length) {
                throw Bad(t, at, "unterminated escape");
            }

            var e = t[at++];
            switch (e) {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (at + 4 > t.Length) {
                        throw Bad(t, at, "truncated \\u escape");
                    }

                    sb.Append((char)Convert.ToInt32(t.Substring(at, 4), 16));
                    at += 4;
                    break;
                default:
                    throw Bad(t, at - 1, "unknown escape '\\" + e + "'");
            }
        }
    }

    private static JsonValue ParseNumber(string t, ref int at) {
        var start = at;
        if (t[at] == '-') {
            at++;
        }

        while (at < t.Length && ((t[at] >= '0' && t[at] <= '9') || t[at] == '.'
                || t[at] == 'e' || t[at] == 'E' || t[at] == '+' || t[at] == '-')) {
            at++;
        }

        double n;
        if (!double.TryParse(t.Substring(start, at - start), NumberStyles.Float,
                CultureInfo.InvariantCulture, out n)) {
            throw Bad(t, start, "malformed number");
        }

        return Of(n);
    }

    private static bool Word(string t, int at, string word) {
        return at + word.Length <= t.Length && t.Substring(at, word.Length) == word;
    }

    private static void SkipSpace(string t, ref int at) {
        while (at < t.Length && (t[at] == ' ' || t[at] == '\t' || t[at] == '\n' || t[at] == '\r')) {
            at++;
        }
    }

    private static Exception Bad(string t, int at, string why) {
        var line = 1;
        var col = 1;
        for (var i = 0; i < at && i < t.Length; i++) {
            if (t[i] == '\n') {
                line++;
                col = 1;
            } else {
                col++;
            }
        }

        return new FormatException("json line " + line + " col " + col + ": " + why);
    }

    // --- serializing ---

    public string Serialize(bool pretty) {
        var sb = new StringBuilder();
        Write(sb, pretty ? 0 : -1);
        return sb.ToString();
    }

    private void Write(StringBuilder sb, int indent) {
        switch (kind) {
            case 0:
                sb.Append("null");
                return;
            case 3:
                WriteString(sb, str);
                return;
            case 4:
                // whole values print whole: coordinates must not grow ".0" or E-notation
                if (num == Math.Floor(num) && Math.Abs(num) < 9007199254740992.0) {
                    sb.Append(((long)num).ToString(CultureInfo.InvariantCulture));
                } else {
                    sb.Append(num.ToString("R", CultureInfo.InvariantCulture));
                }

                return;
            case 5:
                sb.Append(flag ? "true" : "false");
                return;
        }

        var open = kind == 1 ? '{' : '[';
        var close = kind == 1 ? '}' : ']';
        var count = kind == 1 ? objOrder.Count : arr.Count;
        if (count == 0) {
            sb.Append(open);
            sb.Append(close);
            return;
        }

        sb.Append(open);
        for (var i = 0; i < count; i++) {
            if (i > 0) {
                sb.Append(',');
            }

            if (indent >= 0) {
                sb.Append('\n');
                sb.Append(' ', (indent + 1) * 2);
            }

            if (kind == 1) {
                WriteString(sb, objOrder[i]);
                sb.Append(indent >= 0 ? ": " : ":");
                obj[objOrder[i]].Write(sb, indent >= 0 ? indent + 1 : -1);
            } else {
                arr[i].Write(sb, indent >= 0 ? indent + 1 : -1);
            }
        }

        if (indent >= 0) {
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        sb.Append(close);
    }

    private static void WriteString(StringBuilder sb, string s) {
        sb.Append('"');
        for (var i = 0; i < s.Length; i++) {
            var c = s[i];
            switch (c) {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4"));
                    } else {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }
}
