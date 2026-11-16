"""Strategies for settings_props.py: settings-file values, bind text, Color.txt, stat inventories, versions."""
import math

from hypothesis import strategies as st


def _chars(exclude=""):
    return st.characters(exclude_categories=("Cs",), exclude_characters=exclude)


def cased(word):
    """The word with each letter's case flipped at random."""
    return st.lists(st.booleans(), min_size=len(word), max_size=len(word)).map(
        lambda flips: "".join(c.swapcase() if f else c for c, f in zip(word, flips)))


pad = st.sampled_from(["", " ", "  ", "\t"])
newline = st.sampled_from(["\n", "\r\n"])

# --- settings values, by setting kind ---

# enum words a migration rewrites before the parse sees them
MIGRATED = {"free", "skilled", "all"}


def float_forms(x):
    """Texts that parse back to exactly the float32 x: shortest repr, 9 digits, a comma decimal."""
    texts = [repr(x), "%.9g" % x]
    return st.sampled_from(texts + [t.replace(".", ",") for t in texts if "." in t])


def floats_between(lo, hi):
    lo = -1e9 if math.isinf(lo) else lo
    hi = 1e9 if math.isinf(hi) else hi
    return st.floats(lo, hi, allow_nan=False, allow_infinity=False, width=32)


def float_value(desc):
    return floats_between(desc["min"], desc["max"]).flatmap(float_forms)


def float_junk(desc):
    out = [st.sampled_from(["", "abc", "1.2.3", "NaN", "--1", "1,000.5", "0x10"])]
    if not math.isinf(desc["min"]):
        out.append(st.floats(-1e4, desc["min"], exclude_max=True, width=32).flatmap(float_forms))
    if not math.isinf(desc["max"]):
        out.append(st.floats(desc["max"], 1e4, exclude_min=True, width=32).flatmap(float_forms))
    return st.one_of(*out)


color_part = st.one_of(st.integers(0, 1020).map(str),
                       st.floats(0, 1020, allow_nan=False, width=32).map(lambda x: "%.2f" % x))


def color_value(desc=None, part=color_part):
    return st.tuples(st.lists(part, min_size=4, max_size=5), st.sampled_from([",", ", ", " , "])).map(
        lambda t: t[1].join(t[0]))


color_junk = st.sampled_from(["", "1,2,3", "a,b,c,d", "1;2;3;4", "255 255 255 255", "1,,2,3"])
# components a color can hold that are not plain numbers
odd_color_part = st.sampled_from(["Infinity", "-Infinity", "NaN", "-5", "-0.4", "1e30", "+7"])
any_color_part = st.one_of(color_part, odd_color_part)

label = st.text(st.sampled_from("abcdefghijklmnopqrstuvwxyz0123456789"), min_size=1, max_size=8)
hostname = st.lists(label, min_size=1, max_size=3).map(".".join)
port = st.integers(1, 65535).map(str)
host_value = st.tuples(hostname, st.one_of(st.none(), port)).map(lambda t: t[0] if t[1] is None else t[0] + ":" + t[1])
host_junk = st.sampled_from(["", "a b", "http://x.test", "x.test:y", "a:1:2", "x.test:", "[::1]"])
# port texts int.TryParse takes besides a plain port
odd_port = st.sampled_from(["+80", "-1", " 80", "0", "99999", "65536", "080"])

string_value = st.text(_chars("\r\n"), max_size=16).filter(lambda s: "//" not in s)


def bool_value(desc=None):
    return st.sampled_from(["true", "false"]).flatmap(cased)


bool_junk = st.sampled_from(["", "yes", "1", "0", "on", "truee", "Tru"])


def enum_value(desc):
    words = list(desc["names"]) + (["true", "false"] if desc["kind"] == "DirtyWaterSetting" else [])
    return st.sampled_from(words).flatmap(cased)


def enum_junk(desc):
    names = {n.lower() for n in desc["names"]} | MIGRATED | {"true", "false"}
    return st.sampled_from(["", "Nope", "Hold Toggle", "Maybe", "_", "In Logic"]).filter(lambda w: w.lower() not in names)


# a name, a refused word, a number or a list of names: what Enum.Parse may be handed
def enum_text(desc):
    names = st.sampled_from(desc["names"]).flatmap(cased)
    return st.one_of(names, enum_junk(desc), st.integers(-3, 9).map(str),
                     st.lists(names, min_size=2, max_size=3).map(", ".join))


def value_for(desc):
    kind = desc["kind"]
    if kind == "BoolSetting":
        return bool_value()
    if kind == "FloatSetting":
        return float_value(desc)
    if kind == "ColorSetting":
        return color_value()
    if kind == "HostSetting":
        return host_value
    if kind == "StringSetting":
        return string_value
    return enum_value(desc)


def junk_for(desc):
    """Text the setting's parse refuses, or None for a setting that takes any text."""
    kind = desc["kind"]
    if kind == "BoolSetting":
        return bool_junk
    if kind == "FloatSetting":
        return float_junk(desc)
    if kind == "ColorSetting":
        return color_junk
    if kind == "HostSetting":
        return host_junk
    if kind == "StringSetting":
        return None
    return enum_junk(desc)


noise_line = st.one_of(
    st.text(_chars("\r\n"), max_size=20).map(lambda t: "// " + t),
    st.just(""),
    st.sampled_from(["Unknown Setting: 5", "no colon here", ": orphan value", "Invert Swim True"]),
)


# the old word each of these settings still reads, as the parse spells it
LEGACY = {"Grenade Jump Mode": "free", "Hints": "skilled", "Default Map Filter": "all"}


@st.composite
def settings_file(draw, descs, junk=True):
    """A settings file: some settings, any order, values good, bad or legacy, comments and stray lines between."""
    lines = []
    for desc in descs:
        if not draw(st.booleans()):
            continue
        bad = junk_for(desc)
        value = draw(bad if junk and bad is not None and draw(st.integers(0, 4)) == 0 else value_for(desc))
        if desc["name"] in LEGACY and draw(st.booleans()):
            value = draw(cased(LEGACY[desc["name"]]))
        tail = draw(st.sampled_from(["", " // edited", "//"]))
        lines.append(draw(pad) + desc["name"] + draw(pad) + ":" + draw(pad) + value + draw(pad) + tail)
    lines += draw(st.lists(noise_line, max_size=4))
    lines = draw(st.permutations(lines))
    return draw(newline).join(lines)


# --- binds ---

UNSIDED = ["Alt", "Shift", "Control", "Command", "Windows"]
SIDED = ["Left" + m for m in UNSIDED] + ["Right" + m for m in UNSIDED] + ["LeftApple", "RightApple"]


def bind_token(core, keys, pads):
    """One chord element as a player might spell it: core names exact, everything else in any case."""
    return st.one_of(
        st.sampled_from(core),
        st.sampled_from(pads).flatmap(cased).map(lambda p: "_" + p),
        st.sampled_from(keys).flatmap(cased),
        st.sampled_from(UNSIDED + SIDED).flatmap(cased),
    )


def bind_text(token, max_binds=4, max_chord=3):
    chord = st.lists(st.tuples(pad, token, pad).map("".join), min_size=1, max_size=max_chord).map("+".join)
    return st.lists(chord, min_size=1, max_size=max_binds).map(", ".join)


bind_junk = st.sampled_from(["Alt+5", "nokey", "_99", "_Q", "jump", "Alt+T+", "Ctrl+T", "-1"])
# text with the bind file's own separators in odd places
messy_bind_text = st.text(st.sampled_from("AltT+,_ a5Unbound"), max_size=16)

modifier_side = st.sampled_from(UNSIDED).flatmap(lambda m: st.sampled_from(["Left" + m, "Right" + m, m]))
plain_key = st.sampled_from(["T", "K", "Alpha1", "Mouse1", "PageUp", "Jump", "_A"])


@st.composite
def sided_bind_set(draw):
    """Chords sharing a shape, with modifiers spelled on either side or none, so Collapse has merges to find."""
    shape = draw(st.lists(st.sampled_from(["mod", "key"]), min_size=1, max_size=3))
    binds = []
    for _ in range(draw(st.integers(1, 5))):
        binds.append("+".join(draw(modifier_side) if part == "mod" else draw(plain_key) for part in shape))
    extra = draw(st.lists(st.lists(st.one_of(modifier_side, plain_key), min_size=1, max_size=3).map("+".join), max_size=2))
    return ", ".join(binds + extra)


# --- Color.txt ---

def color_line(part, parts=4):
    return st.lists(part, min_size=parts, max_size=parts).map(",".join)


def gradient_line(part):
    return st.tuples(color_line(part), st.integers(-2, 30)).map(lambda t: "%s,%d" % t)


@st.composite
def color_txt(draw, part=st.integers(0, 511).map(str)):
    """A single color (3 or 4 parts) or a customrotation file of colors and gradients."""
    nl = draw(newline)
    if draw(st.booleans()):
        return draw(color_line(part, draw(st.sampled_from([3, 4])))) + draw(st.sampled_from(["", nl]))
    header = draw(cased("customrotation"))
    body = draw(st.lists(st.one_of(color_line(part), gradient_line(part)), max_size=6))
    return nl.join([header] + body) + draw(st.sampled_from(["", nl]))


decimal_part = st.floats(0, 511, allow_nan=False, width=32).map(lambda x: "%.1f" % x)


# --- stats ---

def packed(zones):
    return st.tuples(st.integers(0, (1 << 18) - 1), st.sampled_from(zones)).map(lambda t: t[0] + (t[1] << 18))


@st.composite
def stat_inventory(draw, ids, zones):
    """Values for some of the stats manager's ids: counts, times, and packed time+zone stamps."""
    held = {}
    for role, id_ in ids:
        if not draw(st.booleans()):
            continue
        if role == "stamp":
            held[id_] = draw(st.one_of(st.just(0), packed(zones)))
        elif role == "time":
            held[id_] = draw(st.integers(0, 400000))
        else:
            held[id_] = draw(st.integers(0, 300))
    return held


# --- versions ---

dll_version = st.one_of(
    st.tuples(st.sampled_from([4, 5]), st.sampled_from([0, 8, 9, 10]), st.integers(0, 30)),
    st.tuples(st.integers(0, 99), st.integers(0, 99), st.integers(0, 99)),
).map(lambda t: "%d.%d.%d" % t)
