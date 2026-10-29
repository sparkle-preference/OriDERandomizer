// The property bridge: one JSON request per stdin line, one JSON reply per stdout line.
//   {"call": "Type.Method", "args": [...], "on": handle?, "keep": bool?}  -> {"ok": value, "out": [...]?}
//   {"get": "Type.Member", "on": handle?}  {"set": "Type.Member", "value": v, "on": handle?}
//   "Type.new" constructs; "$culture" / "$clear" / "$ping" are built in.
// Failures reply {"err": type, "msg": message, "at": frames}. Doubles travel as {"$d64": hex bits},
// bytes as {"$b": base64}, kept objects as {"$h": id}, maps as {"$map": {...}} so no key reads as a tag.
// Replies are pure ASCII so lone surrogates survive.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

static class PropBridge {
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Dictionary<int, object> Handles = new Dictionary<int, object>();
    static int nextHandle = 1;
    static Type[] types;

    static int Main() {
        var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
        var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { NewLine = "\n" };
        // the protocol owns stdout; client code that prints lands on stderr
        Console.SetOut(Console.Error);
        types = typeof(PropBridge).Assembly.GetTypes();
        string line;
        while ((line = input.ReadLine()) != null) {
            object reply;
            try {
                reply = Handle((Dictionary<string, object>)Read(line));
            } catch (Exception e) {
                reply = Failure(e);
            }
            string text;
            try {
                text = Write(reply);
            } catch (Exception e) {
                text = Write(Failure(e));
            }
            output.WriteLine(text);
            output.Flush();
        }
        return 0;
    }

    static object Handle(Dictionary<string, object> req) {
        object on = req.ContainsKey("on") && req["on"] != null ? Handles[Convert.ToInt32(req["on"])] : null;
        if (req.ContainsKey("get")) {
            return Ok(GetMember((string)req["get"], on), req);
        }
        if (req.ContainsKey("set")) {
            SetMember((string)req["set"], on, req["value"]);
            return Ok(null, req);
        }
        string call = (string)req["call"];
        object[] args = req.ContainsKey("args") && req["args"] != null ? ((IEnumerable)req["args"]).Cast<object>().ToArray() : new object[0];
        switch (call) {
            case "$ping":
                return Ok("pong", req);
            case "$clear":
                Handles.Clear();
                return Ok(null, req);
            case "$culture":
                var culture = CultureInfo.GetCultureInfo((string)args[0]);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                return Ok(null, req);
        }
        List<object> outs;
        object ret = Invoke(call, on, args, out outs);
        var reply = Ok(ret, req);
        if (outs.Count > 0) {
            reply["out"] = outs.Select(o => ToJson(o, 0)).ToList();
        }
        return reply;
    }

    static Dictionary<string, object> Ok(object value, Dictionary<string, object> req) {
        if (req.ContainsKey("keep") && req["keep"] is bool && (bool)req["keep"] && value != null) {
            int id = nextHandle++;
            Handles[id] = value;
            return new Dictionary<string, object> { { "ok", new Dictionary<string, object> { { "$h", id }, { "type", value.GetType().FullName } } } };
        }
        return new Dictionary<string, object> { { "ok", ToJson(value, 0) } };
    }

    static Dictionary<string, object> Failure(Exception e) {
        while (e is TargetInvocationException && e.InnerException != null) {
            e = e.InnerException;
        }
        var frames = (e.StackTrace ?? "").Split('\n').Select(f => f.Trim()).Where(f => f.Length > 0).Take(6).ToList();
        var reply = new Dictionary<string, object> { { "err", e.GetType().FullName }, { "msg", e.Message }, { "at", frames } };
        if (e is TypeInitializationException && e.InnerException != null) {
            reply["inner"] = e.InnerException.GetType().FullName + ": " + e.InnerException.Message;
        }
        return reply;
    }

    static Type[] referenced;

    // the slice's own types first: a compiled source shadows the game dll's copy
    static Type FindType(string name) {
        var t = FindIn(types, name);
        if (t != null) {
            return t;
        }
        if (referenced == null) {
            var found = new List<Type>();
            foreach (var an in typeof(PropBridge).Assembly.GetReferencedAssemblies()) {
                try {
                    found.AddRange(Assembly.Load(an).GetTypes());
                } catch (ReflectionTypeLoadException e) {
                    found.AddRange(e.Types.Where(x => x != null));
                } catch (Exception) {
                    // an assembly that won't load has no types to offer
                }
            }
            referenced = found.ToArray();
        }
        t = FindIn(referenced, name) ?? Type.GetType(name);
        if (t == null) {
            throw new ArgumentException("bridge: no type " + name);
        }
        return t;
    }

    static Type FindIn(Type[] pool, string name) {
        foreach (var t in pool) {
            if (t.FullName == name || t.FullName.Replace('+', '.') == name) {
                return t;
            }
        }
        var hits = pool.Where(t => t.Name == name).ToList();
        if (hits.Count > 1) {
            throw new ArgumentException("bridge: ambiguous type " + name + " (" + string.Join(", ", hits.Select(h => h.FullName).ToArray()) + ")");
        }
        return hits.Count == 1 ? hits[0] : null;
    }

    static void Split(string path, object on, out Type type, out string member) {
        if (on != null) {
            type = on.GetType();
            member = path;
            return;
        }
        int dot = path.LastIndexOf('.');
        if (dot < 0) {
            throw new ArgumentException("bridge: expected Type.Member, got " + path);
        }
        type = FindType(path.Substring(0, dot));
        member = path.Substring(dot + 1);
    }

    static object GetMember(string path, object on) {
        Type type;
        string name;
        Split(path, on, out type, out name);
        for (var t = type; t != null; t = t.BaseType) {
            var f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (f != null) {
                return f.GetValue(f.IsStatic ? null : on);
            }
            var p = t.GetProperty(name, Any | BindingFlags.DeclaredOnly);
            if (p != null && p.GetIndexParameters().Length == 0) {
                return p.GetValue(p.GetGetMethod(true).IsStatic ? null : on, null);
            }
        }
        throw new MissingMemberException("bridge: no field or property " + path);
    }

    static void SetMember(string path, object on, object value) {
        Type type;
        string name;
        Split(path, on, out type, out name);
        for (var t = type; t != null; t = t.BaseType) {
            var f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (f != null) {
                f.SetValue(f.IsStatic ? null : on, FromJson(value, f.FieldType));
                return;
            }
            var p = t.GetProperty(name, Any | BindingFlags.DeclaredOnly);
            if (p != null && p.GetIndexParameters().Length == 0) {
                p.SetValue(p.GetSetMethod(true).IsStatic ? null : on, FromJson(value, p.PropertyType), null);
                return;
            }
        }
        throw new MissingMemberException("bridge: no field or property " + path);
    }

    static object Invoke(string path, object on, object[] args, out List<object> outs) {
        Type type;
        string name;
        Split(path, on, out type, out name);
        outs = new List<object>();
        var errors = new List<string>();
        if (name == "new") {
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                object[] full;
                string why;
                if (Bind(ctor.GetParameters(), args, out full, out why)) {
                    var made = ctor.Invoke(full);
                    CollectOuts(ctor.GetParameters(), full, outs);
                    return made;
                }
                errors.Add(why);
            }
            throw new MissingMethodException("bridge: no constructor of " + type.FullName + " takes these args (" + string.Join("; ", errors.ToArray()) + ")");
        }
        var flags = on == null ? BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy : Any;
        var candidates = type.GetMethods(flags).Where(m => m.Name == name && !m.IsGenericMethodDefinition).ToList();
        if (candidates.Count == 0) {
            throw new MissingMethodException("bridge: no method " + type.FullName + "." + name);
        }
        foreach (var m in candidates) {
            object[] full;
            string why;
            if (Bind(m.GetParameters(), args, out full, out why)) {
                var ret = m.Invoke(m.IsStatic ? null : on, full);
                CollectOuts(m.GetParameters(), full, outs);
                return ret;
            }
            errors.Add(why);
        }
        throw new MissingMethodException("bridge: no overload of " + type.FullName + "." + name + " takes these args (" + string.Join("; ", errors.ToArray()) + ")");
    }

    // out parameters take no arg; ref parameters take one and come back in "out" too
    static bool Bind(ParameterInfo[] ps, object[] args, out object[] full, out string why) {
        full = new object[ps.Length];
        int a = 0;
        for (int i = 0; i < ps.Length; i++) {
            var p = ps[i];
            if (p.IsOut && p.ParameterType.IsByRef) {
                continue;
            }
            if (a < args.Length) {
                try {
                    full[i] = FromJson(args[a++], p.ParameterType);
                } catch (Exception e) {
                    why = p.Name + ": " + e.Message;
                    return false;
                }
            } else if (p.IsOptional) {
                full[i] = p.DefaultValue;
            } else {
                why = "too few args";
                return false;
            }
        }
        why = a == args.Length ? null : "too many args";
        return a == args.Length;
    }

    static void CollectOuts(ParameterInfo[] ps, object[] full, List<object> outs) {
        for (int i = 0; i < ps.Length; i++) {
            if (ps[i].ParameterType.IsByRef) {
                outs.Add(full[i]);
            }
        }
    }

    static object FromJson(object v, Type t) {
        if (t.IsByRef) {
            t = t.GetElementType();
        }
        var d = v as Dictionary<string, object>;
        if (d != null && d.ContainsKey("$map")) {
            d = (Dictionary<string, object>)d["$map"];
            v = d;
        } else if (d != null && d.ContainsKey("$h")) {
            return Handles[Convert.ToInt32(d["$h"])];
        }
        if (d != null && d.ContainsKey("$d64")) {
            double x = BitConverter.Int64BitsToDouble(long.Parse((string)d["$d64"], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (t == typeof(float)) {
                return (float)x;
            }
            return t == typeof(object) ? (object)x : Convert.ChangeType(x, t, CultureInfo.InvariantCulture);
        }
        if (d != null && d.ContainsKey("$b")) {
            return Convert.FromBase64String((string)d["$b"]);
        }
        if (v == null) {
            if (t.IsValueType && Nullable.GetUnderlyingType(t) == null) {
                throw new ArgumentException("null for " + t.Name);
            }
            return null;
        }
        if (t == typeof(object)) {
            return v;
        }
        var under = Nullable.GetUnderlyingType(t);
        if (under != null) {
            t = under;
        }
        if (t == typeof(string)) {
            if (!(v is string)) {
                throw new ArgumentException("expected a string, got " + v.GetType().Name);
            }
            return v;
        }
        if (t.IsEnum) {
            return v is string ? Enum.Parse(t, (string)v) : Enum.ToObject(t, Convert.ToInt64(v, CultureInfo.InvariantCulture));
        }
        if (t == typeof(bool)) {
            if (!(v is bool)) {
                throw new ArgumentException("expected a bool");
            }
            return v;
        }
        if (t == typeof(char)) {
            var s = v as string;
            if (s == null || s.Length != 1) {
                throw new ArgumentException("expected a one-char string");
            }
            return s[0];
        }
        if (t.IsPrimitive || t == typeof(decimal)) {
            if (v is string || v is bool) {
                throw new ArgumentException("expected a number for " + t.Name);
            }
            return Convert.ChangeType(v, t, CultureInfo.InvariantCulture);
        }
        var list = v as IEnumerable;
        if (t.IsArray && list != null && !(v is string) && d == null) {
            var items = list.Cast<object>().ToList();
            var arr = Array.CreateInstance(t.GetElementType(), items.Count);
            for (int i = 0; i < items.Count; i++) {
                arr.SetValue(FromJson(items[i], t.GetElementType()), i);
            }
            return arr;
        }
        if (t.IsGenericType) {
            var def = t.GetGenericTypeDefinition();
            var gs = t.GetGenericArguments();
            if (d != null && (def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>))) {
                var made = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(gs));
                foreach (var kv in d) {
                    made.Add(FromJson(KeyFor(kv.Key, gs[0]), gs[0]), FromJson(kv.Value, gs[1]));
                }
                return made;
            }
            if (list != null && d == null && (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(IEnumerable<>) || def == typeof(ICollection<>) || def == typeof(HashSet<>))) {
                var concrete = def == typeof(HashSet<>) ? typeof(HashSet<>).MakeGenericType(gs) : typeof(List<>).MakeGenericType(gs);
                var made = Activator.CreateInstance(concrete);
                var add = concrete.GetMethod("Add");
                foreach (var item in list) {
                    add.Invoke(made, new[] { FromJson(item, gs[0]) });
                }
                return made;
            }
        }
        if (d != null) {
            object made = t.IsValueType || t.GetConstructor(Type.EmptyTypes) == null ? FormatterServices.GetUninitializedObject(t) : Activator.CreateInstance(t, true);
            foreach (var kv in d) {
                var f = t.GetField(kv.Key, Any & ~BindingFlags.Static);
                if (f != null) {
                    f.SetValue(made, FromJson(kv.Value, f.FieldType));
                    continue;
                }
                var p = t.GetProperty(kv.Key, Any & ~BindingFlags.Static);
                if (p == null || p.GetSetMethod(true) == null) {
                    throw new ArgumentException("no settable member " + kv.Key + " on " + t.Name);
                }
                p.SetValue(made, FromJson(kv.Value, p.PropertyType), null);
            }
            return made;
        }
        if (t.IsInstanceOfType(v)) {
            return v;
        }
        throw new ArgumentException("can't make a " + t.Name + " from " + v.GetType().Name);
    }

    static object KeyFor(string key, Type t) {
        if (t == typeof(string) || t.IsEnum) {
            return key;
        }
        return long.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    static object ToJson(object v, int depth) {
        if (v == null || v is string || v is bool) {
            return v;
        }
        if (v is char) {
            return v.ToString();
        }
        if (v is double || v is float) {
            double x = v is float ? (double)(float)v : (double)v;
            return new Dictionary<string, object> { { "$d64", BitConverter.DoubleToInt64Bits(x).ToString("x16", CultureInfo.InvariantCulture) } };
        }
        if (v is decimal) {
            return ((decimal)v).ToString(CultureInfo.InvariantCulture);
        }
        var t = v.GetType();
        if (t.IsEnum) {
            return v.ToString();
        }
        if (t.IsPrimitive) {
            return v;
        }
        var bytes = v as byte[];
        if (bytes != null) {
            return new Dictionary<string, object> { { "$b", Convert.ToBase64String(bytes) } };
        }
        if (depth > 6) {
            return new Dictionary<string, object> { { "$type", t.FullName }, { "str", SafeString(v) } };
        }
        var dict = v as IDictionary;
        if (dict != null) {
            var made = new Dictionary<string, object>();
            foreach (DictionaryEntry kv in dict) {
                made[Convert.ToString(kv.Key, CultureInfo.InvariantCulture)] = ToJson(kv.Value, depth + 1);
            }
            return new Dictionary<string, object> { { "$map", made } };
        }
        var list = v as IEnumerable;
        if (list != null) {
            return list.Cast<object>().Select(o => ToJson(o, depth + 1)).ToList();
        }
        var fields = new Dictionary<string, object> { { "$type", t.FullName } };
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            fields[f.Name] = ToJson(f.GetValue(v), depth + 1);
        }
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (p.GetIndexParameters().Length > 0 || fields.ContainsKey(p.Name)) {
                continue;
            }
            try {
                fields[p.Name] = ToJson(p.GetValue(v, null), depth + 1);
            } catch (Exception) {
                // a getter that throws reads as absent
            }
        }
        return fields;
    }

    static string SafeString(object v) {
        try {
            return v.ToString();
        } catch (Exception e) {
            return "<ToString threw " + e.GetType().Name + ">";
        }
    }

    // JavaScriptSerializer reads a lone surrogate as U+FFFD, so the bridge reads and writes JSON itself
    static object Read(string s) {
        int i = 0;
        var v = Read(s, ref i);
        Skip(s, ref i);
        if (i != s.Length) {
            throw new FormatException("bridge: trailing text at " + i);
        }
        return v;
    }

    static void Skip(string s, ref int i) {
        while (i < s.Length && char.IsWhiteSpace(s[i])) {
            i++;
        }
    }

    static object Read(string s, ref int i) {
        Skip(s, ref i);
        char c = s[i];
        if (c == '{') {
            var d = new Dictionary<string, object>();
            i++;
            Skip(s, ref i);
            if (s[i] == '}') {
                i++;
                return d;
            }
            while (true) {
                Skip(s, ref i);
                var key = (string)Read(s, ref i);
                Skip(s, ref i);
                i++;  // ':'
                d[key] = Read(s, ref i);
                Skip(s, ref i);
                if (s[i++] == '}') {
                    return d;
                }
            }
        }
        if (c == '[') {
            var list = new List<object>();
            i++;
            Skip(s, ref i);
            if (s[i] == ']') {
                i++;
                return list;
            }
            while (true) {
                list.Add(Read(s, ref i));
                Skip(s, ref i);
                if (s[i++] == ']') {
                    return list;
                }
            }
        }
        if (c == '"') {
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"') {
                if (s[i] != '\\') {
                    sb.Append(s[i++]);
                    continue;
                }
                char e = s[i + 1];
                i += 2;
                switch (e) {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }
        if (s.Substring(i).StartsWith("true")) {
            i += 4;
            return true;
        }
        if (s.Substring(i).StartsWith("false")) {
            i += 5;
            return false;
        }
        if (s.Substring(i).StartsWith("null")) {
            i += 4;
            return null;
        }
        int start = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) {
            i++;
        }
        string num = s.Substring(start, i - start);
        if (num.IndexOfAny(".eE".ToCharArray()) >= 0) {
            return double.Parse(num, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        long n;
        if (long.TryParse(num, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n)) {
            return n >= int.MinValue && n <= int.MaxValue ? (object)(int)n : n;
        }
        return decimal.Parse(num, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    static string Write(object v) {
        var sb = new StringBuilder();
        Write(v, sb);
        return sb.ToString();
    }

    static void Write(object v, StringBuilder sb) {
        if (v == null) {
            sb.Append("null");
        } else if (v is string) {
            sb.Append('"');
            foreach (char c in (string)v) {
                if (c == '"' || c == '\\') {
                    sb.Append('\\').Append(c);
                } else if (c >= 0x20 && c < 0x7f) {
                    sb.Append(c);
                } else {
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
            }
            sb.Append('"');
        } else if (v is bool) {
            sb.Append((bool)v ? "true" : "false");
        } else if (v is IDictionary) {
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry kv in (IDictionary)v) {
                if (!first) {
                    sb.Append(',');
                }
                first = false;
                Write(Convert.ToString(kv.Key, CultureInfo.InvariantCulture), sb);
                sb.Append(':');
                Write(kv.Value, sb);
            }
            sb.Append('}');
        } else if (v is IEnumerable) {
            sb.Append('[');
            bool first = true;
            foreach (var x in (IEnumerable)v) {
                if (!first) {
                    sb.Append(',');
                }
                first = false;
                Write(x, sb);
            }
            sb.Append(']');
        } else {
            sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
        }
    }
}
