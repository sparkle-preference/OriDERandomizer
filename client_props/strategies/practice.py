"""Strategies for the practice slice: json documents, zip and folder entries, placement lines, seed files."""
import json
import unicodedata

from hypothesis import strategies as st

any_char = st.characters(exclude_categories=())
plain_char = st.characters(exclude_categories=("Cs",))


def cs_space(c):
    """.NET Framework's char.IsWhiteSpace, which Trim uses."""
    return c in "\t\n\x0b\x0c\r \x85\xa0" or unicodedata.category(c) in ("Zs", "Zl", "Zp")


def cs_trim(s):
    a, b = 0, len(s)
    while a < b and cs_space(s[a]):
        a += 1
    while b > a and cs_space(s[b - 1]):
        b -= 1
    return s[a:b]


# --- json ---

# json's special characters, which plain random text rarely holds
json_char = any_char | st.sampled_from(list('"\\/\b\f\n\r\t\x00\x1f\x7f') + [chr(0x2028), chr(0xD800), chr(0xDC00),
                                                                           chr(0xFEFF)])
json_text = st.text(json_char, max_size=8)
json_scalars = (st.none() | st.booleans() | st.integers(-2**53, 2**53)
                | st.floats(allow_nan=False, allow_infinity=False) | json_text)
whole_scalars = st.none() | st.booleans() | st.integers(-2**53 + 1, 2**53 - 1) | json_text


def json_values(scalars=json_scalars, max_leaves=12):
    keys = st.text(json_char, max_size=6)
    return st.recursive(scalars, lambda kids: st.lists(kids, max_size=4) | st.dictionaries(keys, kids, max_size=4),
                        max_leaves=max_leaves)


STYLES = {
    "default": dict(),
    "compact": dict(ensure_ascii=False, separators=(",", ":")),
    "indent": dict(ensure_ascii=False, indent=2),
    "tabs": dict(indent="\t"),
    "flat": dict(indent=0, separators=(" , ", " : ")),
}


@st.composite
def python_json(draw, values=json_values()):
    """(value, text): a value and how some Python writer spells it, padded with json whitespace."""
    v = draw(values)
    text = json.dumps(v, **STYLES[draw(st.sampled_from(sorted(STYLES)))])
    pad = st.text(st.sampled_from(" \t\r\n"), max_size=3)
    return v, draw(pad) + text + draw(pad)


@st.composite
def duplicate_key_objects(draw):
    """(pairs, text): an object written with keys that may repeat."""
    pairs = draw(st.lists(st.tuples(st.sampled_from(["a", "b", "c", "\xe9"]), json_scalars), max_size=6))
    return pairs, "{" + ",".join(json.dumps(k) + ":" + json.dumps(v) for k, v in pairs) + "}"


JSONISH = '{}[]",:0123456789.eE+-\\/ubfnrtxX truefalsenull\t\n\r\x01' + chr(0xFEFF)
# pieces of escapes and numbers that parsers disagree on
FRAGMENTS = ["\\u", "\\u-0", "\\u0x", "\\u+0", "\\uD800", "\\U0041", "\\x41", "\\", "0x", "1e", "-.", "01", ".5", "1e400"]


@st.composite
def mutated_json(draw):
    """Valid json with a few characters inserted, dropped or replaced; or json-alphabet noise."""
    branch = draw(st.integers(0, 4))
    if branch == 0:
        return "".join(draw(st.lists(st.sampled_from(JSONISH) | st.sampled_from(FRAGMENTS), max_size=20)))
    if branch == 1:
        return '"' + "".join(draw(st.lists(st.sampled_from(FRAGMENTS) | st.sampled_from("0aF-x\\\""), max_size=8))) + '"'
    _, text = draw(python_json(json_values(max_leaves=6)))
    for _ in range(draw(st.integers(1, 3))):
        at = draw(st.integers(0, len(text)))
        op = draw(st.sampled_from(["insert", "insert", "drop", "replace"]))
        c = draw(st.sampled_from(JSONISH) | any_char | st.sampled_from(FRAGMENTS))
        if op == "insert":
            text = text[:at] + c + text[at:]
        elif op == "drop":
            text = text[:at] + text[at + 1:]
        else:
            text = text[:at] + c + text[at + 1:]
    return text


# --- zip entries ---

zip_name = st.text(st.characters(exclude_categories=("Cs",), exclude_characters="\\\x00"), min_size=1, max_size=12) \
    .filter(lambda n: not n.endswith("/"))
zip_data = st.binary(max_size=300)


def zip_entries(min_size=0, max_size=6):
    return st.lists(st.tuples(zip_name, zip_data), min_size=min_size, max_size=max_size, unique_by=lambda e: e[0])


# --- folder entry names ---

INVALID = set('"<>|:*?\\/') | {chr(c) for c in range(32)}
DEVICES = {"CON", "PRN", "AUX", "NUL"} | {"COM%d" % n for n in range(1, 10)} | {"LPT%d" % n for n in range(1, 10)}
# Windows' reserved names as Microsoft documents them, superscript digits and 0 included
RESERVED_WINDOWS = DEVICES | {p + d for p in ("COM", "LPT") for d in "0\xb9\xb2\xb3"}


def client_part_ok(part):
    """FolderStore.Parts' rule for one part, for the alphabets drawn here."""
    return (part not in ("", ".", "..") and not (set(part) & INVALID) and not part.endswith((".", " "))
            and cs_trim(part.split(".")[0]).upper() not in DEVICES)


name_part = st.text(st.sampled_from("aAb\xe9\xc9 .-\xa0"), min_size=1, max_size=3).filter(client_part_ok) \
    | st.sampled_from(["x.bfrp", "Y.BFRP", "z.bfrp-tmp", "ghosts", "segment.json", "COM\xb9", "LPT0", "CONOUT$"])
entry_name = st.lists(name_part, min_size=1, max_size=3).map("/".join)


@st.composite
def entry_names(draw, min_size=1, max_size=6):
    """Distinct entry names, often with a case-swapped twin or an entry inside another one."""
    names = draw(st.lists(entry_name, min_size=min_size, max_size=max_size, unique=True))
    if names and draw(st.integers(0, 3)) == 0:
        names.append(draw(st.sampled_from(names)).swapcase())
    if names and draw(st.integers(0, 3)) == 0:
        names.append(draw(st.sampled_from(names)) + "/" + draw(name_part))
    return list(dict.fromkeys(names))


# any text at all, '/' separated, for what Parts refuses
loose_name = st.lists(st.text(plain_char, max_size=5) | st.sampled_from(["..", ".", "a.", "a ", "CON", "com1.txt", "LPT\xb2",
                                                                           "C:", "x\\y", "~1", "COM0", "nul ", "\x85",
                                                                           "a\xa0"]),
                      min_size=1, max_size=4).map("/".join)


# --- placement lines ---

def coord_of(locs):
    return st.sampled_from([k for k, _, _ in locs]) | st.integers(-2**31, 2**31 - 1)


corner = st.sampled_from(["0", "1", "-2.5", "100.25", "3.14159"]) | st.integers(-500, 500).map(str)
box_flags = st.lists(st.sampled_from(["kill", "solid", "item", "ritem", "once", "unsafe", "", "renderDepth=3"]),
                     min_size=1, max_size=2).map(",".join)
deleted_flags = st.sampled_from(["tombstone", "none", "Tombstone", "kill,none", " none "])


def bm(n):
    return st.sampled_from(["BM|%d", "BM|%d=1", "BM| %d ", "MU|SK/3/BM/%d", "RP|BM/%d=0/SH/x"]).map(lambda f: f % n)


@st.composite
def box_line(draw, numbers=st.integers(0, 6), live=None):
    """(line, named): a box line that parses, and the box number its payload names, if any."""
    deleted = draw(st.booleans()) if live is None else not live
    flags = draw(deleted_flags if deleted else box_flags)
    line = "BX|%s|%s" % (flags, ",".join(draw(st.lists(corner, min_size=4, max_size=4))))
    named = None
    kind = draw(st.integers(0, 4))
    if kind == 1:
        line += "|ff0000"
    elif kind >= 2:
        if kind == 4:
            named = draw(numbers)
            payload = draw(bm(named))
        else:
            payload = draw(st.sampled_from(["SK|3", "EX|15", "SH|hi there", "SH|a|b"]))
        line += "|%s|%s" % (draw(st.sampled_from(["", "00a2e8"])), payload)
    return line, (None if deleted else named)


# box lines Parse refuses: no number, kept as written
broken_box_line = st.sampled_from(["BX|kill|1,2", "BX|solid|a,b,c,d", "BX|item|0,0,1,1||SK|abc", "BX|", "BX|kill"])
# the same, with a deleting flag
broken_tombstone_line = st.sampled_from(["BX|none", "BX|tombstone|", "BX|none|1,2", "BX|tombstone|x,0,0,0"])


@st.composite
def placement_line(draw, locs):
    code, value = draw(st.sampled_from([("SK", "3"), ("EX", "15"), ("EN", "3"), ("MW", "x"), ("SH", "hello"),
                                        ("SK", "abc"), ("EX", "1.5"), ("HC", "1"), ("RB", "17"), ("MU", "SK/0/EX/5"),
                                        ("XX", "1"), ("BM", "2")]))
    line = "%d|%s|%s" % (draw(coord_of(locs)), code, value)
    return line + draw(st.sampled_from(["", "|Glades", "|"]))


@st.composite
def other_line(draw, locs):
    """A seed or container line that is not a box: a placement, comment, header, or noise."""
    return draw(placement_line(locs) | st.sampled_from(["// a note", "//", "Standard,Clues|seed", "OpenWorld|x|y", "junk",
                                                         "12|SK", "-3|EX|", "abc|SK|0|Glades", "BXkill|0,0,1,1"])
                | st.text(st.sampled_from("ab|1 /\t"), max_size=8))
