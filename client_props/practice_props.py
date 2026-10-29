"""Practice containers: segment.json, the .bfrp zip and its folder form, the practice folder, defaults, seed boxes."""
import contextlib
import csv
import io
import json
import math
import os
import re
import shutil
import struct
import tempfile
import unittest
import warnings
import zipfile
import zlib

from hypothesis import assume, given, settings, strategies as st

from client_props.base import HERE, examples
from client_props.cs_bridge import BridgeDied, CsError, outcome, slice_bridge
from client_props.strategies import practice as S

SOURCES = ["randomizer/" + f for f in ("JsonValue.cs", "ZipStore.cs", "FolderStore.cs", "PracticeFolder.cs",
                                        "PracticeDefaults.cs", "BfrpFile.cs", "PracticeSegment.cs", "SeedBoxes.cs",
                                        "RandomizerBox.cs", "RandomizerAction.cs")]
EXTRA = [os.path.join(HERE, "cs", "practice", f) for f in ("Stubs.cs", "Glue.cs")]
WORK = os.path.join(os.environ.get("TEMP", HERE), "client_props", "work", "practice")
os.makedirs(WORK, exist_ok=True)
# .NET's GetFullPath expands 8.3 names like WORSTM~1, so paths are compared in long form
WORK = os.path.realpath(WORK)
TOMBSTONE = "BX|tombstone|0,0,0,0"

B = None
LOCS = []
NEVER_GIVEN = set()
GOOD_BFRP = b""


def setUpModule():
    global B, LOCS, NEVER_GIVEN, GOOD_BFRP
    B = slice_bridge("practice", SOURCES, extra=EXTRA, game=True)
    LOCS = B.call("PracticeGlue.Locations")
    NEVER_GIVEN = {k for k, kind, special in LOCS if kind == "ProgressiveMap" or (kind == "Skill" and special == 0)}
    with scratch() as d:
        path = os.path.join(d, "good.bfrp")
        B.call("PracticeGlue.ZipWrite", path, ["segment.json", "save.sav"], [b'{"name":"x"}', b"\x01\x02"])
        with open(path, "rb") as f:
            GOOD_BFRP = f.read()


@contextlib.contextmanager
def scratch():
    os.makedirs(WORK, exist_ok=True)
    d = tempfile.mkdtemp(dir=WORK)
    try:
        yield d
    finally:
        shutil.rmtree(d, ignore_errors=True)


def tree_of(d):
    """Every file under d, by '/' path, with its bytes."""
    out = {}
    for base, _, files in os.walk(d):
        for f in files:
            p = os.path.join(base, f)
            with open(p, "rb") as fh:
                out[os.path.relpath(p, d).replace("\\", "/")] = fh.read()
    return out


def write_tree(d, entries):
    for name, data in entries:
        p = os.path.join(d, *name.split("/"))
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "wb") as f:
            f.write(data)


# --- json ---

def _double(bits):
    return struct.unpack("<d", struct.pack("<Q", int(bits, 16)))[0]


def utf16(s):
    """s as a UTF-16 string holds it: two adjacent lone surrogates are one character there."""
    return s.encode("utf-16-le", "surrogatepass").decode("utf-16-le", "surrogatepass")


def _from_canon(v):
    if isinstance(v, dict):
        if "n" in v:
            return ("n", _double(v["n"]))
        return ("o", tuple((utf16(k), _from_canon(x)) for k, x in v["o"]))
    if isinstance(v, list):
        return ("a", tuple(_from_canon(x) for x in v))
    if v is None:
        return ("z",)
    if isinstance(v, bool):
        return ("b", v)
    return ("s", utf16(v))


def client_tree(text):
    return _from_canon(json.loads(B.call("PracticeGlue.Canon", text)))


def shape(v, nonfinite_null=False):
    """A Python json value in the form client_tree gives."""
    if v is None:
        return ("z",)
    if isinstance(v, bool):
        return ("b", v)
    if isinstance(v, (int, float)):
        return ("z",) if nonfinite_null and not math.isfinite(v) else ("n", float(v))
    if isinstance(v, str):
        return ("s", utf16(v))
    if isinstance(v, list):
        return ("a", tuple(shape(x, nonfinite_null) for x in v))
    return ("o", tuple((utf16(k), shape(x, nonfinite_null)) for k, x in v.items()))


def near(a, b):
    """Same tree, numbers within 4 ULP: .NET Framework's double parse and "R" format are not exact."""
    if a[0] != b[0]:
        return False
    if a[0] == "n":
        x, y = a[1], b[1]
        return x == y or abs(x - y) <= 4 * math.ulp(max(abs(x), abs(y)))
    if a[0] == "a":
        return len(a[1]) == len(b[1]) and all(near(x, y) for x, y in zip(a[1], b[1]))
    if a[0] == "o":
        return len(a[1]) == len(b[1]) and all(k == j and near(x, y) for (k, x), (j, y) in zip(a[1], b[1]))
    return a == b


def _reject_constant(name):
    raise ValueError(name)


def python_strict(text):
    """Python's strict json reading of text inside the shared subset, or None."""
    try:
        v = json.loads(text, parse_constant=_reject_constant)
    except ValueError:
        return None

    def shared(x):
        if isinstance(x, bool) or x is None or isinstance(x, str):
            return True
        if isinstance(x, int):
            return abs(x) <= 2**53
        if isinstance(x, float):
            return math.isfinite(x)
        if isinstance(x, list):
            return all(shared(y) for y in x)
        return all(shared(y) for y in x.values())
    return (v,) if shared(v) else None


def minus_after_u_escape(text):
    """Text holding a \\u escape whose four characters start with a minus."""
    return "\\u-" in text


class Json(unittest.TestCase):
    @settings(max_examples=examples(400))
    @given(S.mutated_json())
    def test_parse_refuses_only_with_format_exception_and_agrees_with_python(self, text):
        r = outcome(B.call, "PracticeGlue.Canon", text)
        if not minus_after_u_escape(text):
            self.assertTrue(r[0] == "ok" or r[1] == "System.FormatException", r)
        py = python_strict(text)
        if py is not None:
            self.assertEqual(r[0], "ok", "python reads %r, the client refuses: %r" % (text, r))
            self.assertTrue(near(_from_canon(json.loads(r[1])), shape(py[0])), text)

    @settings(max_examples=examples(300))
    @given(S.python_json())
    def test_what_python_writes_parses_to_the_same_tree(self, vt):
        _, text = vt
        self.assertTrue(near(client_tree(text), shape(json.loads(text))), text)

    @settings(max_examples=examples(100))
    @given(S.duplicate_key_objects())
    def test_a_repeated_key_keeps_its_first_place_and_last_value(self, pt):
        _, text = pt
        self.assertTrue(near(client_tree(text), shape(json.loads(text))), text)

    @settings(max_examples=examples(200))
    @given(S.python_json(S.json_values(S.whole_scalars)))
    def test_serialize_writes_what_python_dumps_writes(self, vt):
        _, text = vt
        py = json.loads(text)
        self.assertEqual(B.call("PracticeGlue.Ser", text, False),
                         utf16(json.dumps(py, ensure_ascii=False, separators=(",", ":"))))
        self.assertEqual(B.call("PracticeGlue.Ser", text, True), utf16(json.dumps(py, ensure_ascii=False, indent=2)))

    @settings(max_examples=examples(200))
    @given(S.python_json(), st.booleans())
    def test_python_reads_back_what_serialize_writes(self, vt, pretty):
        _, text = vt
        out = B.call("PracticeGlue.Ser", text, pretty)
        self.assertTrue(near(shape(json.loads(out)), client_tree(text)), out)

    @settings(max_examples=examples(200))
    @given(S.json_values(S.json_scalars | st.floats()), st.booleans())
    def test_serialize_then_parse_is_the_same_tree(self, v, pretty):
        out = B.call("PracticeGlue.SerPlain", [v], pretty)
        self.assertTrue(near(client_tree(out), shape(v, nonfinite_null=True)), out)

    def test_nesting_64_deep_parses_and_65_is_a_format_error(self):
        for opener, closer in (("[", "]"), ('{"k":', "}")):
            self.assertEqual(outcome(B.call, "PracticeGlue.Canon", opener * 64 + "1" + closer * 64)[0], "ok")
            for depth in (65, 100000):
                self.assertEqual(outcome(B.call, "PracticeGlue.Canon", opener * depth + "1" + closer * depth),
                                 ("err", "System.FormatException"), depth)

    def test_what_parse_accepts_beyond_strict_json(self):
        """None of these change what a document means; Python refuses each."""
        lenient = [("01", 1.0), ("-01", -1.0), ("1.", 1.0), ("-.5", -0.5), ("1.e2", 100.0),
                   ('"\\u0x41"', "A"), ('"\\u0X41"', "A"), ('"\\u+041"', "A"), ('"a\x01b"', "a\x01b"),
                   ("\N{ZERO WIDTH NO-BREAK SPACE}[]", [])]
        for text, want in lenient:
            self.assertIsNone(python_strict(text), text)
            self.assertTrue(near(client_tree(text), shape(want)), text)
        # Python reads these; the client refuses them, which only turns a non-finite number into an error
        for text in ("1e400", "-1e400", "NaN", "Infinity"):
            self.assertEqual(outcome(B.call, "PracticeGlue.Canon", text), ("err", "System.FormatException"), text)

    def test_serialize_spells_numbers_like_this(self):
        """Whole values under 2^53 print whole; the rest use "R", whose exponents differ from Python's repr."""
        cases = [(3.0, "3"), (-0.0, "0"), (0.5, "0.5"), (1e-07, "1E-07"), (1e16, "1E+16"),
                 (2.0**53, "9007199254740992"), (2.0**60, "1.152921504606847E+18"), (float("nan"), "null"),
                 (float("-inf"), "null")]
        for x, want in cases:
            self.assertEqual(B.call("PracticeGlue.SerPlain", [x], False), want, x)

    @unittest.expectedFailure
    def test_parse_refuses_a_bad_escape_with_a_format_exception(self):
        self.assertEqual(outcome(B.call, "PracticeGlue.Canon", '"\\u-041"'), ("err", "System.FormatException"))


# --- zip ---

def py_zip(path, entries, comments=None, extras=None, archive_comment=b""):
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        with zipfile.ZipFile(path, "w", zipfile.ZIP_STORED) as z:
            for i, (name, data) in enumerate(entries):
                info = zipfile.ZipInfo(name, date_time=(2020, 1, 2, 3, 4, 6))
                info.compress_type = zipfile.ZIP_STORED
                if comments:
                    info.comment = comments[i]
                if extras:
                    info.extra = extras[i]
                z.writestr(info, data)
            z.comment = archive_comment


def first_place_last_bytes(entries):
    d = {}
    for name, data in entries:
        d[name] = data
    return [[k, v] for k, v in d.items()]


extra_block = st.builds(lambda hid, data: struct.pack("<HH", hid, len(data)) + data,
                        st.sampled_from([0xCAFE, 0x5455, 0x000A]), st.binary(max_size=12))


@st.composite
def python_zip_args(draw):
    pool = draw(st.lists(S.zip_name, min_size=1, max_size=4, unique=True))
    entries = draw(st.lists(st.tuples(st.sampled_from(pool) | S.zip_name, S.zip_data), max_size=6))
    n = len(entries)
    comments = draw(st.none() | st.lists(st.binary(max_size=10), min_size=n, max_size=n))
    extras = draw(st.none() | st.lists(extra_block, min_size=n, max_size=n))
    archive_comment = draw(st.binary(max_size=40).filter(lambda c: b"PK\x05\x06" not in c))
    return entries, comments, extras, archive_comment


@st.composite
def damaged_zips(draw):
    """Bytes that are not a zip, or a stored zip with bytes overwritten, cut or spliced."""
    if draw(st.integers(0, 4)) == 0:
        return draw(st.binary(max_size=300))
    entries = draw(S.zip_entries(min_size=1, max_size=3))
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_STORED) as z:
        for name, data in entries:
            z.writestr(name, data)
    raw = bytearray(buf.getvalue())
    for _ in range(draw(st.integers(1, 4))):
        op = draw(st.sampled_from(["byte", "word", "cut", "splice"]))
        at = draw(st.integers(0, len(raw) - 1))
        if op == "byte":
            raw[at] = draw(st.integers(0, 255))
        elif op == "word":
            raw[at:at + 4] = draw(st.sampled_from([b"\xff\xff\xff\xff", b"\xff\xff\xff\x7f", b"\x00\x00\x00\x80",
                                                   b"\xfe\xff\xff\xff", b"\x00\x00\x00\x00"]))
        elif op == "cut":
            raw = raw[:at] if draw(st.booleans()) else raw[at:]
            if not raw:
                raw = bytearray(b"PK")
        else:
            raw[at:at] = draw(st.binary(min_size=1, max_size=30))
    return bytes(raw)


class Zip(unittest.TestCase):
    @settings(max_examples=examples(150))
    @given(python_zip_args())
    def test_reads_python_stored_zips_by_name_order_and_bytes(self, args):
        entries, comments, extras, archive_comment = args
        with scratch() as d:
            path = os.path.join(d, "x.bfrp")
            py_zip(path, entries, comments, extras, archive_comment)
            got = B.call("PracticeGlue.ZipRead", path)
        self.assertEqual(got, first_place_last_bytes(entries))

    @settings(max_examples=examples(150))
    @given(S.zip_entries())
    def test_write_then_read_is_the_same_store_and_python_agrees(self, entries):
        names, datas = [n for n, _ in entries], [b for _, b in entries]
        with scratch() as d:
            path = os.path.join(d, "x.bfrp")
            B.call("PracticeGlue.ZipWrite", path, names, datas)
            self.assertEqual(B.call("PracticeGlue.ZipRead", path), [list(e) for e in entries])
            with zipfile.ZipFile(path) as z:
                self.assertIsNone(z.testzip())
                infos = z.infolist()
                self.assertEqual([i.filename for i in infos], names)
                self.assertTrue(all(i.compress_type == zipfile.ZIP_STORED and i.flag_bits & 0x800 for i in infos))
                self.assertEqual([z.read(i) for i in infos], datas)
            self.assertFalse(os.path.exists(path + ".tmp"))

    @settings(max_examples=examples(100))
    @given(st.lists(st.tuples(S.zip_name, st.binary(min_size=1, max_size=100)), min_size=1, max_size=4,
                    unique_by=lambda e: e[0]), st.data())
    def test_a_changed_data_byte_is_refused_whole(self, entries, data):
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w", zipfile.ZIP_STORED) as z:
            for name, b in entries:
                z.writestr(name, b)
            infos = z.infolist()
        raw = bytearray(buf.getvalue())
        info = data.draw(st.sampled_from(infos))
        at = info.header_offset + 30 + len(info.filename.encode("utf-8")) + len(info.extra) \
            + data.draw(st.integers(0, info.file_size - 1))
        raw[at] ^= data.draw(st.integers(1, 255))
        with scratch() as d:
            path = os.path.join(d, "x.bfrp")
            with open(path, "wb") as f:
                f.write(raw)
            self.assertEqual(outcome(B.call, "PracticeGlue.ZipRead", path), ("err", "System.IO.IOException"))

    @settings(max_examples=examples(200))
    @given(st.binary(max_size=2000))
    def test_crc32_is_zlib_crc32(self, data):
        self.assertEqual(B.call("ZipStore.Crc32", data), zlib.crc32(data))

    @settings(max_examples=examples(300))
    @given(damaged_zips())
    def test_any_bytes_read_whole_or_throw_never_hang_or_exhaust_memory(self, raw):
        with scratch() as d:
            path = os.path.join(d, "x.bfrp")
            with open(path, "wb") as f:
                f.write(raw)
            try:
                r = outcome(B.call, "PracticeGlue.ZipRead", path, timeout=10)
            except BridgeDied as e:
                self.fail("the bridge died reading %r: %s" % (raw[:80], e))
        self.assertNotEqual(r, ("err", "System.OutOfMemoryException"), raw[:80])


# --- folder store and practice folder ---

def reserved_name_the_client_allows(name):
    """A part Windows documents as reserved (COM0, LPT0, superscript digits) that the client's list lacks."""
    return any(p.split(".")[0].rstrip(" ").upper() in S.RESERVED_WINDOWS - S.DEVICES for p in name.split("/"))


def part_ending_in_nbsp_or_nel(names):
    """A part ending in U+00A0 or U+0085, which .NET Framework trims from the end of a path."""
    return any(p.endswith(("\xa0", "\x85")) for n in names for p in n.split("/"))


def names_differing_only_in_case(names):
    """Two entries, or the folders they sit in, that only case tells apart."""
    paths = {"/".join(n.split("/")[:i + 1]) for n in names for i in range(len(n.split("/")))}
    return len({p.upper() for p in paths}) < len(paths)


def a_name_that_is_also_a_folder(names):
    """An entry named like a folder another entry sits in."""
    upper = [n.upper() for n in names]
    return any(m.startswith(n + "/") for n in upper for m in upper)


def kept_by_layout(name):
    """Read skips leftover temp files and, at the top, the .bfrp a folder was extracted from."""
    return not name.upper().endswith(".BFRP-TMP") and not ("/" not in name and name.upper().endswith(".BFRP"))


def ordinal_path_order(names):
    """Read's order: full paths compared ordinally, '\\' separated (code points, as these names are BMP)."""
    return sorted(names, key=lambda n: n.replace("/", "\\"))


def windows_reserved(part):
    return part.split(".")[0].rstrip(" ").upper() in S.RESERVED_WINDOWS


def segment_number(name):
    """N in "New Segment N" (any case, a " (k)" tail ignored), else 0: what NextName counts past."""
    bracket = name.find(" (")
    stem = name[:bracket] if bracket > 0 else name
    n = cs_int(S.cs_trim(stem[len("New Segment "):])) if stem.lower().startswith("new segment ") else None
    return 0 if n is None else n


def a_name_numbered_the_largest_int(names):
    """A "New Segment 2147483647", past which an int can't count."""
    return any(segment_number(n) == 2**31 - 1 for n in names)


EXPORT_NAMES = ["segment.json", "save.sav", "runs.csv", "placements.bfr", "ghosts/pinned.ghost", "ghosts/best.ghost",
                "ghosts/old/x.ghost", "ghosts/notes.txt", "variants/v/segment.json", "variants/v/runs.csv",
                "variants/v/ghosts/pinned.ghost", "variants/v/ghosts/last.ghost", "variants/runs.csv", "runs.csv.bak"]


def exported(entry):
    """BfrpFile.Exported's rule: no run history, and of the ghosts only the pinned one, at the root or in a variant."""
    own = entry
    if own.startswith("variants/"):
        slash = own.find("/", len("variants/"))
        own = own if slash < 0 else own[slash + 1:]
    ghost = own.startswith("ghosts/") and own.endswith(".ghost") and "/" not in own[len("ghosts/"):]
    return own != "runs.csv" and (not ghost or own == "ghosts/pinned.ghost")


class Folder(unittest.TestCase):
    ROOT = os.path.join(WORK, "root")

    @settings(max_examples=examples(400))
    @given(S.loose_name | S.entry_name)
    def test_an_accepted_name_resolves_strictly_inside_the_root(self, name):
        full = B.call("PracticeGlue.Inside", self.ROOT, name)
        if full is None:
            return
        if not part_ending_in_nbsp_or_nel([name]):
            self.assertTrue(full.startswith(self.ROOT + "\\"), full)
            self.assertEqual(full[len(self.ROOT) + 1:].split("\\"), name.split("/"))
        if not reserved_name_the_client_allows(name):
            self.assertFalse(any(windows_reserved(p) for p in name.split("/")), name)

    @settings(max_examples=examples(120))
    @given(S.entry_names(), st.data())
    def test_write_then_read_gives_the_same_entries(self, names, data):
        if names_differing_only_in_case(names) or a_name_that_is_also_a_folder(names) \
                or part_ending_in_nbsp_or_nel(names):
            return
        datas = [data.draw(S.zip_data) for _ in names]
        with scratch() as d:
            root = os.path.join(d, "seg")
            B.call("PracticeGlue.FolderWrite", root, names, datas)
            got = B.call("PracticeGlue.FolderRead", root)
            self.assertEqual(sorted(tree_of(root)), sorted(names))
        want = dict(zip(names, datas))
        self.assertEqual(got, [[n, want[n]] for n in ordinal_path_order([n for n in names if kept_by_layout(n)])])

    @settings(max_examples=examples(80))
    @given(S.entry_names(min_size=0, max_size=4), S.loose_name, st.data())
    def test_a_refused_set_leaves_the_store_unchanged(self, names, bad, data):
        if B.call("PracticeGlue.Inside", self.ROOT, bad) is not None or names_differing_only_in_case(names) \
                or a_name_that_is_also_a_folder(names) or part_ending_in_nbsp_or_nel(names):
            return
        store = B.new("FolderStore")
        datas = [data.draw(S.zip_data) for _ in names]
        for n, b in zip(names, datas):
            store.call("Set", n, b)
        with self.assertRaises(CsError) as cm:
            store.call("Set", bad, b"x")
        self.assertEqual(cm.exception.type, "System.IO.IOException")
        self.assertEqual(store.get("Names"), names)
        with scratch() as d:
            store.call("Write", os.path.join(d, "seg"))
            self.assertEqual(sorted(tree_of(os.path.join(d, "seg")).items()), sorted(zip(names, datas)))

    @settings(max_examples=examples(300))
    @given(st.text(S.any_char, max_size=10) | st.sampled_from(["CON", "con.txt", "COM\xb9", "LPT0", "export", " a ", "a.b",
                                                                "...", "\N{IDEOGRAPHIC SPACE}", "nul\N{IDEOGRAPHIC SPACE}"]))
    def test_safe_name_is_a_usable_fixed_point_and_agrees_with_the_server_check(self, name):
        safe = B.call("PracticeFolder.SafeName", name)
        self.assertTrue(safe and "." not in safe, safe)
        self.assertEqual(B.call("PracticeFolder.SafeName", safe), safe)
        self.assertIsNotNone(B.call("PracticeGlue.Inside", self.ROOT, safe), safe)
        # practice_defaults_test's check on a shipped name: the client takes a default iff SafeName leaves it alone
        server = bool(name) and name == name.strip() and "." not in name and not (S.INVALID & set(name)) \
            and name.upper() not in S.DEVICES
        self.assertEqual(server, safe == name, name)
        if not reserved_name_the_client_allows(safe):
            self.assertFalse(windows_reserved(safe), safe)

    @settings(max_examples=examples(60))
    @given(st.lists(st.tuples(S.entry_name, S.zip_data), min_size=1, max_size=6, unique_by=lambda e: e[0].upper()))
    def test_unpack_of_pack_is_byte_identical(self, entries):
        names = [n for n, _ in entries]
        # a folder on disk can't hold such a pair, so it is no input here
        assume(not a_name_that_is_also_a_folder(names))
        if part_ending_in_nbsp_or_nel(names):
            return
        with scratch() as d:
            src, dst = os.path.join(d, "src"), os.path.join(d, "dst")
            write_tree(src, entries)
            B.call("PracticeFolder.Pack", src, os.path.join(d, "p.bfrp"))
            B.call("PracticeFolder.Unpack", os.path.join(d, "p.bfrp"), dst)
            want = {n: b for n, b in tree_of(src).items() if kept_by_layout(n)}
            self.assertEqual(tree_of(dst), want)
            self.assertFalse(os.path.exists(dst + ".bfrp-tmp"))

    @settings(max_examples=examples(60))
    @given(S.entry_names(max_size=5), st.sampled_from(["", "dir", "file"]), st.data())
    def test_extract_moves_both_or_neither(self, names, existing, data):
        entries = [(n, data.draw(S.zip_data)) for n in names]
        with scratch() as d:
            path = os.path.join(d, "seg.bfrp")
            py_zip(path, entries)
            with open(path, "rb") as f:
                raw = f.read()
            if existing == "dir":
                os.makedirs(os.path.join(d, "seg"))
            elif existing == "file":
                open(os.path.join(d, "seg"), "wb").close()
            before = set(os.listdir(d))
            r = outcome(B.call, "PracticeFolder.Extract", path)
            after = set(os.listdir(d))
            if r[0] == "err":
                self.assertTrue(os.path.exists(path), r)
                self.assertEqual({n for n in after - before if not n.endswith(".bfrp-tmp")}, set())
                return
            folder = r[1]
            self.assertFalse(os.path.exists(path))
            self.assertEqual(after - before, {os.path.basename(folder)})
            self.assertRegex(os.path.basename(folder), r"^seg( \(\d+\))?$")
            got = tree_of(folder)
            self.assertEqual(got.pop("seg.bfrp"), raw)
            if not names_differing_only_in_case(names) and not part_ending_in_nbsp_or_nel(names):
                self.assertEqual(got, dict(first_place_last_bytes(entries)))

    @settings(max_examples=examples(50))
    @given(st.sampled_from(["seg", "Seg", "export", "a b"]),
           st.lists(st.tuples(st.sampled_from(["", " (2)", " (3)", " (5)", ".bfrp", " (2).bfrp"]),
                              st.sampled_from(["dir", "file"]), st.sampled_from(["seg", "SEG", "a b"])), max_size=5))
    def test_free_path_is_the_first_name_not_taken(self, name, existing):
        with scratch() as d:
            for suffix, kind, stem in existing:
                p = os.path.join(d, stem + suffix)
                if os.path.exists(p):
                    continue
                if kind == "dir":
                    os.makedirs(p)
                else:
                    open(p, "wb").close()
            got = B.call("PracticeFolder.FreePath", d, name)
            candidates = [os.path.join(d, name)] + [os.path.join(d, "%s (%d)" % (name, k)) for k in range(2, 12)]

            def taken(p):
                return os.path.basename(p).lower() in ("export", "map-tiles") or os.path.exists(p) \
                    or os.path.exists(p + ".bfrp")
            self.assertEqual(got, next(c for c in candidates if not taken(c)))
            self.assertFalse(B.call("PracticeFolder.Taken", got))

    @settings(max_examples=examples(40))
    @given(st.lists(st.tuples(st.sampled_from(EXPORT_NAMES), S.zip_data), min_size=1, max_size=8, unique_by=lambda e: e[0]),
           st.data())
    def test_export_carries_what_exported_keeps_and_skips_an_export_that_matches(self, entries, data):
        with scratch() as d:
            folder, to = os.path.join(d, "seg"), os.path.join(d, "export", "seg.bfrp")
            write_tree(folder, entries)
            self.assertTrue(B.call("PracticeFolder.Export", folder, to, data.draw(st.booleans())))
            want = [[n, b] for n, b in B.call("PracticeGlue.FolderRead", folder) if exported(n)]
            self.assertEqual(B.call("PracticeGlue.ZipRead", to), want)
            with open(to, "rb") as f:
                first = f.read()
            self.assertFalse(B.call("PracticeFolder.Export", folder, to, True))
            with open(to, "rb") as f:
                self.assertEqual(f.read(), first)
            name, old = data.draw(st.sampled_from(entries))
            write_tree(folder, [(name, old + b"!")])
            self.assertEqual(B.call("PracticeFolder.Export", folder, to, True), exported(name))

    @settings(max_examples=examples(150))
    @given(st.lists(st.builds("{}{}{}{}".format, st.sampled_from(["New Segment ", "new segment ", "NEW SEGMENT  ", "New Seg ",
                                                                  "x"]),
                              st.sampled_from(["", "+", "-", " "]),
                              st.integers(0, 2**31 + 5) | st.sampled_from([2**31 - 1, 2**31 - 2]),
                              st.sampled_from(["", " (2)", " ", "x"])) | st.text(max_size=6), max_size=5))
    def test_next_name_numbers_past_every_new_segment_name(self, names):
        if a_name_numbered_the_largest_int(names):
            return
        top = max([0] + [segment_number(n) for n in names])
        self.assertEqual(B.call("PracticeFolder.NextName", names), "New Segment %d" % (top + 1))

    @unittest.expectedFailure
    def test_next_name_after_the_largest_int_stays_positive(self):
        self.assertFalse(B.call("PracticeFolder.NextName", ["New Segment 2147483647"]).startswith("New Segment -"))

    @unittest.expectedFailure
    def test_every_name_windows_reserves_is_refused(self):
        self.assertIsNone(B.call("PracticeGlue.Inside", self.ROOT, "COM\xb9"))

    @unittest.expectedFailure
    def test_entries_that_differ_only_in_case_both_survive(self):
        with scratch() as d:
            B.call("PracticeGlue.FolderWrite", os.path.join(d, "seg"), ["variants/V/segment.json", "variants/v/segment.json"],
                   [b"1", b"2"])
            self.assertEqual(len(B.call("PracticeGlue.FolderRead", os.path.join(d, "seg"))), 2)

    @unittest.expectedFailure
    def test_a_name_ending_in_a_no_break_space_stays_its_own_file_in_the_folder(self):
        full = B.call("PracticeGlue.Inside", self.ROOT, "..\xa0")
        self.assertTrue(full is None or full.startswith(self.ROOT + "\\"), full)
        with scratch() as d:
            B.call("PracticeGlue.FolderWrite", os.path.join(d, "seg"), ["a\xa0"], [b"1"])
            self.assertEqual(B.call("PracticeGlue.FolderRead", os.path.join(d, "seg")), [["a\xa0", b"1"]])

    @unittest.expectedFailure
    def test_an_entry_and_a_folder_of_its_name_both_survive(self):
        with scratch() as d:
            self.assertEqual(outcome(B.call, "PracticeGlue.FolderWrite", os.path.join(d, "seg"), ["x", "x/y"], [b"1", b"2"]),
                             ("ok", None))
            self.assertEqual(len(B.call("PracticeGlue.FolderRead", os.path.join(d, "seg"))), 2)


# --- defaults ---

offered_name = st.sampled_from(["Glades warmup", "glades WARMUP", "Bridge Skip", " Bridge Skip ", "a.b", "con", "export",
                                "Map-Tiles", "x/y", "Valley (any%)", "Swamp", "", "  ", "\xc9lan", "\xe9lan", "seg\t",
                                "New\N{IDEOGRAPHIC SPACE}"])
behavior = st.sampled_from(["ok", "ok", "ok", "garbage", "missing", "404", "throw", "half"])


class Defaults(unittest.TestCase):
    @settings(max_examples=examples(80))
    @given(st.lists(offered_name, max_size=7), st.dictionaries(offered_name.map(str.strip), behavior),
           st.lists(offered_name.map(str.strip), max_size=3), st.lists(st.tuples(offered_name.map(str.strip),
                                                                                 st.sampled_from(["", ".bfrp"]),
                                                                                 st.sampled_from(["dir", "file"])),
                                                                       max_size=2))
    def test_sync_adds_inside_the_folder_records_what_it_added_and_runs_once(self, offered, how, ledger, existing):
        with scratch() as d:
            folder = os.path.join(d, "practice")
            os.makedirs(folder)
            with open(os.path.join(d, "beside.txt"), "wb") as f:
                f.write(b"keep")
            ledger_path = os.path.join(folder, ".defaults")
            if ledger:
                with open(ledger_path, "w", encoding="utf-8", newline="") as f:
                    f.write("".join(n + "\n" for n in ledger))
            for stem, suffix, kind in existing:
                p = os.path.join(folder, stem + suffix)
                if stem and not os.path.exists(p) and stem == stem.strip() and not set(stem) & S.INVALID:
                    if kind == "dir":
                        os.makedirs(p)
                    else:
                        open(p, "wb").close()
            outside = set(os.listdir(d))
            before = tree_of(folder)
            listing = {f.lower() for f in os.listdir(folder)}
            added, calls = B.call("PracticeGlue.Sync", folder, offered, how, GOOD_BFRP)

            # the names Sync should add, in order: new to the ledger, SafeName-stable, not taken, downloaded whole
            had, want = {S.cs_trim(n).lower() for n in ledger}, []
            for line in offered:
                name = S.cs_trim(line)
                if not name or name.lower() in had or B.call("PracticeFolder.SafeName", name) != name \
                        or name.lower() in ("export", "map-tiles") or name.lower() in listing \
                        or (name + ".bfrp").lower() in listing or how.get(name, "ok") != "ok":
                    continue
                want.append(name)
                had.add(name.lower())
                listing.add((name + ".bfrp").lower())
            self.assertEqual(added, want)

            self.assertEqual(set(os.listdir(d)), outside)
            with open(os.path.join(d, "beside.txt"), "rb") as f:
                self.assertEqual(f.read(), b"keep")
            after = tree_of(folder)
            self.assertEqual([n for n in after if ".bfrp-tmp" in n.lower()], [])
            with open(ledger_path, encoding="utf-8") if os.path.exists(ledger_path) else io.StringIO() as f:
                self.assertEqual(f.read(), "".join(n + "\n" for n in ledger + added))
            self.assertEqual(set(after) - set(before) - {".defaults"}, {n + ".bfrp" for n in added})
            for n in added:
                self.assertEqual(after[n + ".bfrp"], GOOD_BFRP)
            for n in set(before) - {".defaults"}:
                self.assertEqual(after.get(n), before[n], n)
            failed = [n for n in calls if n not in added]

            again, calls2 = B.call("PracticeGlue.Sync", folder, offered, how, GOOD_BFRP)
            self.assertEqual(again, [])
            self.assertLessEqual(set(calls2), set(failed))
        # what reaches the download at all: trimmed, SafeName-stable, not in the ledger
        ledgered = {n.strip().lower() for n in ledger}
        for n in calls:
            self.assertEqual(B.call("PracticeFolder.SafeName", n), n)
            self.assertNotIn(n.lower(), ledgered)


# --- bfrp ---

def modern(line):
    fields = line.split("|")
    if line.startswith("BX|") and len(fields) > 1 and any(f.strip().lower() in ("none", "tombstone")
                                                          for f in fields[1].split(",")):
        return TOMBSTONE
    return line


def unreadable_deleted_box_line(entries):
    """A placements file holding a box line Parse refuses whose flags still say tombstone or none."""
    for name, data in entries:
        if name.endswith("placements.bfr"):
            for line in data.decode("utf-8", "replace").split("\n"):
                line = S.cs_trim(line)
                if modern(line) == TOMBSTONE and line != TOMBSTONE and not B.call("PracticeGlue.Parses", line):
                    return True
    return False


@st.composite
def containers(draw):
    """(entries, named): a container's files, and every box number a BM| in them names."""
    named = set()
    # one container in six holds a broken tombstone line, which the mask then skips whole
    broken_tombstones = draw(st.integers(0, 5)) == 0

    def lines(most):
        out = []
        for _ in range(draw(st.integers(0, most))):
            kind = draw(st.integers(0, 9))
            if kind <= 5:
                line, n = draw(S.box_line())
                if n is not None:
                    named.add(n)
            elif kind == 6:
                line = draw(S.broken_box_line)
            elif kind == 7 and broken_tombstones:
                line = draw(S.broken_tombstone_line)
            elif kind == 7:
                line = draw(S.box_line(live=False))[0]
            else:
                line = draw(S.other_line(LOCS))
                parts = line.split("|")
                if len(parts) >= 3 and parts[1] == "BM" and parts[2].strip().isdigit():
                    named.add(int(parts[2]))
            out.append(line)
        return out

    def segment():
        seg = {"name": draw(st.sampled_from(["a", "\xdc"])), "version": 1}
        if draw(st.booleans()):
            n = draw(st.integers(0, 6))
            named.add(n)
            seg["inventory"] = ["SK|3", "BM|%d" % n]
        if draw(st.booleans()):
            n = draw(st.integers(0, 6))
            named.add(n)
            seg["shuffle"] = [{"give": ["BM|%d=1" % n, "SK|0"], "among": [k for k, _, _ in LOCS[:3]]}]
        return json.dumps(seg, indent=draw(st.sampled_from([None, 2]))).encode("utf-8")

    def text(ls):
        eol = draw(st.sampled_from(["\n", "\r\n"]))
        return (eol.join(ls) + (eol if draw(st.booleans()) else "")).encode("utf-8")

    entries = [("segment.json", segment()), ("save.sav", draw(st.binary(min_size=1, max_size=20)))]
    root = lines(8)
    if root:
        entries.append(("placements.bfr", text(root)))
    if draw(st.booleans()):
        entries.append(("runs.csv", b"date,pickups,ms\n2026-01-02 03:04:05,3,12345\n"))
    for vid in draw(st.lists(st.sampled_from(["v1", "Hard", "b-2"]), max_size=2, unique=True)):
        entries.append(("variants/%s/segment.json" % vid, segment()))
        own = lines(5)
        if own:
            entries.append(("variants/%s/placements.bfr" % vid, text(own)))
    if draw(st.booleans()):
        entries.append(("ghosts/pinned.ghost", b"\x01ghost"))
    return entries, named


def make_container(d, kind, entries):
    if kind == "zip":
        path = os.path.join(d, "seg.bfrp")
        B.call("PracticeGlue.ZipWrite", path, [n for n, _ in entries], [b for _, b in entries])
    else:
        path = os.path.join(d, "seg")
        write_tree(path, entries)
    return path


def read_container(path):
    return B.call("PracticeGlue.ZipRead" if path.endswith(".bfrp") else "PracticeGlue.FolderRead", path)


def numbered(boxes, variant):
    """The boxes an attempt of this variant numbers: the shared ones, then the variant's own."""
    by = dict((v, [tuple(b) for b in bs]) for v, bs in boxes)
    return by[""] + (by.get(variant, []) if variant else [])


def csv_runs(text):
    """runs.csv read the way RunsFor documents: three unquoted comma fields, a whole count and a whole ms."""
    def whole(s, bits):
        m = re.fullmatch(r"[ \t\n\x0b\x0c\r]*([+-]?[0-9]+)[ \t\n\x0b\x0c\r]*", s)
        return int(m.group(1)) if m and -2**(bits - 1) <= int(m.group(1)) < 2**(bits - 1) else None
    rows = []
    for line in text.split("\n"):
        line = S.cs_trim(line)
        fields = next(csv.reader([line], quoting=csv.QUOTE_NONE)) if line else [""]
        if len(fields) == 3 and whole(fields[1], 32) is not None and whole(fields[2], 64) is not None:
            rows.append([fields[0], whole(fields[1], 32), whole(fields[2], 64)])
    return rows


csv_field = st.text(st.sampled_from("0123456789 -+:ab\t"), max_size=6) | st.integers(-2**64, 2**64).map(str)
csv_rows = st.lists(st.lists(csv_field, min_size=1, max_size=4).map(",".join), max_size=6)


def line_break_in_page_box(box):
    """A page box whose flags, color or give holds a line feed."""
    return any("\n" in (box.get(k) or "") for k in ("flags", "color", "give"))


page_text = st.text(st.sampled_from("ab |\n\r\t#"), max_size=6)
page_box = st.fixed_dictionaries({
    "flags": st.sampled_from(["kill", "ritem", "solid,once", "item"]) | page_text,
    "box": st.lists(st.sampled_from([0, 1.5, -20.25, 300]), min_size=4, max_size=4),
}, optional={"color": st.sampled_from(["", "ff0000", "#00a2e8"]) | page_text,
             "give": st.sampled_from(["", "SK|3", "SH|hello", "MU|SK/3/EX/15"]) | page_text.map(lambda t: "SH|" + t)})


class Bfrp(unittest.TestCase):
    @settings(max_examples=examples(80))
    @given(containers(), st.sampled_from(["zip", "folder"]))
    def test_save_is_idempotent_and_keeps_every_live_or_named_box_at_its_number(self, cn, kind):
        entries, named = cn
        if unreadable_deleted_box_line(entries):
            return
        with scratch() as d:
            path = make_container(d, kind, entries)
            seg_before = client_tree(dict(entries)["segment.json"].decode("utf-8"))
            boxes_before = B.call("PracticeGlue.Boxes", path)
            B.call("PracticeGlue.Save", path)
            saved = read_container(path)
            boxes_after = B.call("PracticeGlue.Boxes", path)
            B.call("PracticeGlue.Save", path)
            self.assertEqual(read_container(path), saved)
            self.assertTrue(near(client_tree(dict(saved)["segment.json"].decode("utf-8")), seg_before))
        for variant in [v for v, _ in boxes_before]:
            before, after = numbered(boxes_before, variant), numbered(boxes_after, variant)
            self.assertEqual(after, [(modern(l), dead) for l, dead in before][:len(after)], variant)
            keep = max([i for i, (_, dead) in enumerate(before) if not dead or i in named], default=-1) + 1
            self.assertGreaterEqual(len(after), min(keep, len(before)), variant)

    @settings(max_examples=examples(200))
    @given(st.lists(st.text(st.sampled_from("BX|ab ,1\t\r\xa0\N{IDEOGRAPHIC SPACE}"), max_size=8), max_size=6))
    def test_placement_lines_read_back_trimmed_and_without_blanks(self, lines):
        got = B.call("PracticeGlue.PlacementRoundTrip", lines)
        want = [S.cs_trim(l) for l in lines if S.cs_trim(l)]
        self.assertEqual(got, want)

    @settings(max_examples=examples(200))
    @given(page_box)
    def test_a_page_box_reads_back_as_the_one_line_it_wrote(self, box):
        r = outcome(B.call, "PracticeGlue.PageBox", json.dumps(box))
        if r[0] == "err" or line_break_in_page_box(box):
            return
        line, placements, read = r[1]
        self.assertEqual(placements, [S.cs_trim(line)])
        self.assertEqual(read, placements)

    @settings(max_examples=examples(200))
    @given(csv_rows, st.sampled_from(["\n", "\r\n"]), st.booleans())
    def test_runs_are_the_csv_rows_with_three_whole_fields(self, rows, eol, header):
        text = ("date,pickups,ms" + eol if header else "") + eol.join(rows)
        self.assertEqual(B.call("PracticeGlue.Runs", text.encode("utf-8")), csv_runs(text))

    @settings(max_examples=examples(100))
    @given(st.none() | csv_rows.map(lambda r: "\n".join(r)), st.integers(-2**31, 2**31 - 1),
           st.integers(-2**63, 2**63 - 1))
    def test_an_appended_run_reads_back_after_the_rows_already_there(self, text, pickups, ms):
        raw = None if text is None else text.encode("utf-8")
        out = B.call("PracticeGlue.AppendRun", raw, "2026-10-29 12:34:56", pickups, ms)
        old = [] if text is None else csv_runs(text)
        self.assertEqual(B.call("PracticeGlue.Runs", out), old + [["2026-10-29 12:34:56", pickups, ms]])

    @unittest.expectedFailure
    def test_save_never_renumbers_boxes(self):
        with scratch() as d:
            path = make_container(d, "zip", [("segment.json", b"{}"), ("save.sav", b"x"),
                                              ("placements.bfr", b"BX|none\nBX|kill|0,0,1,1\n")])
            before = numbered(B.call("PracticeGlue.Boxes", path), "")
            B.call("PracticeGlue.Save", path)
            self.assertEqual(numbered(B.call("PracticeGlue.Boxes", path), ""), before)

    @unittest.expectedFailure
    def test_a_page_box_with_a_line_feed_reads_back_as_one_line(self):
        _, placements, read = B.call("PracticeGlue.PageBox", '{"flags":"ritem","box":[0,0,1,1],"give":"SH|a\\nb"}')
        self.assertEqual(len(placements), 1)


# --- segment ---

any_json = S.json_values(max_leaves=6)
segment_json = st.fixed_dictionaries({}, optional={
    "end": st.fixed_dictionaries({}, optional={
        "items": st.lists(st.sampled_from(["SK|0", "SK|15", "SK|99", "EV|3", "EV|8", "XX|1", "SK|", "|3", "SK|abc"])
                          | any_json, max_size=4) | any_json,
        "locations": st.lists(st.sampled_from([-840248, 0, 1.5, 1e300, -1e300]) | any_json, max_size=4) | any_json,
        "count": any_json | st.sampled_from([3, -1, 1e300]),
    }) | any_json,
    "qtm_enabled": any_json,
}) | any_json


@st.composite
def placement_text(draw):
    """(raw lines, text): pasted lines of every sort, blank and padded ones too."""
    pad = st.sampled_from(["", " ", "\t", "\N{IDEOGRAPHIC SPACE}"])
    pick = draw(st.lists(st.one_of(S.box_line().map(lambda b: b[0]), S.broken_box_line, S.other_line(LOCS),
                                   st.just(""), st.just("BX|goal|0,0,1,1")), max_size=8))
    raw = [draw(pad) + p + draw(pad) for p in pick]
    return raw, draw(st.sampled_from(["\n", "\r\n"])).join(raw)


def live_goal_box_in_a_variant(lines):
    """A variant's own box line that parses as a live goal box."""
    def goal(line):
        return len(line.split("|")) > 2 and any(f.strip().lower() == "goal" for f in line.split("|")[1].split(","))
    return any(l.startswith("BX|") and modern(l) != TOMBSTONE and goal(l) and B.call("PracticeGlue.Parses", l) for l in lines)


def parsed_boxes(lines):
    """The (line, deleted) boxes a placements file holds, as PlacementLines trims them."""
    lines = [S.cs_trim(l) for l in lines]
    return [(l, modern(l) == TOMBSTONE) for l in lines if l.startswith("BX|") and B.call("PracticeGlue.Parses", l)]


goal_lines = st.sampled_from(["BX|goal|0,0,1,1", "BX|goal,solid|1,1,2,2", "BX|goal,none|3,3,4,4"])
attempt_lines = st.lists(st.one_of(S.box_line().map(lambda b: b[0]), goal_lines, S.broken_box_line), max_size=5)
variant_json = st.fixed_dictionaries({}, optional={
    "inventory": st.lists(st.sampled_from(["SK|3", "EX|15", "SK|abc", "HC|x", "MU|SK/3/EX/1", "XX|1", "|3", "SK"])
                          | S.json_scalars, max_size=4) | S.json_scalars}) | S.json_values(max_leaves=4)


class Segment(unittest.TestCase):
    @settings(max_examples=examples(100))
    @given(attempt_lines, variant_json, attempt_lines)
    def test_an_attempt_numbers_boxes_as_the_container_does_and_never_throws(self, shared, vjson, own):
        r = outcome(B.call, "PracticeGlue.Attempt", shared, json.dumps(vjson), own)
        self.assertEqual(r[0], "ok", r)
        is_variant = isinstance(vjson, dict)
        if not (is_variant and live_goal_box_in_a_variant(own)):
            want = parsed_boxes(shared) + (parsed_boxes(own) if is_variant else [])
            self.assertEqual([tuple(b) for b in r[1]["boxes"]], want)
        inventory = vjson.get("inventory") if is_variant else None
        codes = [x for x in inventory if isinstance(x, str)] if isinstance(inventory, list) else []
        self.assertEqual(r[1]["items"], [t for t in (B.call("PracticeGlue.PickupText", x) for x in codes) if t is not None])
        self.assertEqual(r[1]["goal"], any(not dead and "goal" in l.split("|")[1] for l, dead in parsed_boxes(shared)))

    @unittest.expectedFailure
    def test_a_variant_goal_box_keeps_later_boxes_at_their_numbers(self):
        r = B.call("PracticeGlue.Attempt", [], "{}", ["BX|goal|0,0,1,1", "BX|kill|2,2,3,3"])
        self.assertEqual([b[0] for b in r["boxes"]][1:], ["BX|kill|2,2,3,3"])

    @settings(max_examples=examples(300))
    @given(segment_json)
    def test_parse_never_throws_and_keeps_only_what_met_can_check(self, seg):
        r = outcome(B.call, "PracticeGlue.Segment", json.dumps(seg))
        self.assertEqual(r[0], "ok", r)
        end = seg.get("end") if isinstance(seg, dict) else None
        items = end.get("items") if isinstance(end, dict) else None
        offered = [x for x in items if isinstance(x, str)] if isinstance(items, list) else []
        self.assertEqual(r[1]["items"], [x for x in offered if holdable(x)])

    @settings(max_examples=examples(200))
    @given(placement_text(), st.booleans())
    def test_read_placements_splits_lines_into_kept_skipped_and_one_header(self, rt, variant):
        raw, text = rt
        kept, skipped = B.call("PracticeGlue.ReadPlacements", text, variant)
        rows = [(i + 1, S.cs_trim(r)) for i, r in enumerate(text.split("\n")) if S.cs_trim(r)]
        k = s = headers = 0
        for at, (number, line) in enumerate(rows):
            if k < len(kept) and kept[k] == line:
                k += 1
            elif s < len(skipped) and skipped[s].startswith("line %d (" % number) \
                    and skipped[s].endswith("): " + line):
                s += 1
            else:
                self.assertEqual(at, 0, "%r dropped silently" % line)
                headers += 1
        self.assertEqual((k, s), (len(kept), len(skipped)))
        self.assertLessEqual(headers, 1)
        self.assertEqual(B.call("PracticeGlue.ReadPlacements", "\n".join(kept), variant), [kept, []])

    @settings(max_examples=examples(150))
    @given(st.lists(st.sampled_from(["SK|3", "SK|0", "EX|15", "SH|hi", "SK|abc", "EX|1.5", "MU|SK/3/EX/1", "XX|1",
                                     "BM|2"]) | st.integers() | st.none(), max_size=6),
           st.lists(st.integers(-5, 5) | st.sampled_from([1.5, -0.5, 2.0, "12", None, True, "loc", "loc", "never"]),
                    max_size=8),
           st.data())
    def test_shuffle_gives_land_on_distinct_spots_none_of_them_never_given(self, give, among, data):
        keys = sorted(k for k, _, _ in LOCS)
        # "loc" and "never" stand for a real location key and a never-given one
        among = [data.draw(st.sampled_from(sorted(NEVER_GIVEN))) if a == "never"
                 else data.draw(st.sampled_from(keys)) if a == "loc" else a for a in among]
        seg = {"shuffle": [{"give": give, "among": among}]}
        table = {int(k): v for k, v in B.call("PracticeGlue.Resolve", json.dumps(seg), []).items()}
        spots = []
        for a in among:
            number = isinstance(a, (int, float)) and not isinstance(a, bool)
            if number and int(a) not in spots and int(a) not in NEVER_GIVEN:
                spots.append(int(a))
        n = min(len(give), len(spots))
        want = [B.call("PracticeGlue.PickupText", g) for g in give[:n] if isinstance(g, str)]
        want = [w for w in want if w is not None]
        self.assertTrue(set(table) <= set(spots), (table, spots))
        self.assertEqual(sorted(table.values()), sorted(want))


def cs_int(s):
    """int.TryParse with NumberStyles.Integer, or None."""
    m = re.fullmatch(r"[ \t\n\x0b\x0c\r]*([+-]?[0-9]+)[ \t\n\x0b\x0c\r]*", s)
    return int(m.group(1)) if m and -2**31 <= int(m.group(1)) < 2**31 else None


ABILITY_IDS = {0, 2, 3, 4, 5, 8, 12, 14, 15, 50, 51}


def holdable(item):
    """An end item Met can answer: a skill the player can hold, or one of the eight world events."""
    bar = item.find("|")
    n = cs_int(item[bar + 1:]) if bar >= 1 else None
    return n is not None and (item[:bar] == "SK" and n in ABILITY_IDS or item[:bar] == "EV" and 0 <= n <= 7)


# --- seed boxes ---

@st.composite
def seed_files(draw, mixed=False):
    """(lines, eols, trailing): a seed file's lines, flag line first, and its line endings."""
    body = draw(st.lists(st.one_of(S.box_line().map(lambda b: b[0]), S.broken_box_line, S.broken_tombstone_line,
                                   S.other_line(LOCS), st.just("")), max_size=8))
    lines = ["Standard,Clues,Sync1234.1|seed"] + body
    if mixed:
        eols = draw(st.lists(st.sampled_from(["\n", "\r\n"]), min_size=len(lines), max_size=len(lines)))
    else:
        eols = [draw(st.sampled_from(["\n", "\r\n"]))] * len(lines)
    return lines, eols, draw(st.booleans())


def seed_text(lines, eols, trailing):
    text = "".join(l + e for l, e in zip(lines[:-1], eols))
    return text + lines[-1] + (eols[-1] if trailing else "")


@st.composite
def box_edits(draw, boxes):
    out = []
    for line in boxes:
        op = draw(st.sampled_from(["keep", "keep", "replace", "delete"]))
        out.append(line if op == "keep" else draw(S.box_line(live=True))[0] if op == "replace"
                   else draw(st.sampled_from(["BX|none|0,0,1,1", "BX|tombstone|2,2,3,3", TOMBSTONE])))
    if out and draw(st.booleans()):
        out = out[:draw(st.integers(0, len(out)))]
    return out + [draw(S.box_line())[0] for _ in range(draw(st.integers(0, 2)))]


def expected_seed(text, new, parses):
    """SeedBoxes.Save as documented: box slots rewritten in place, undone ones dropped, new ones last; CRLF if any."""
    eol = "\r\n" if "\r\n" in text else "\n"
    lines = text.replace("\r\n", "\n").split("\n")
    if text.endswith("\n"):
        lines.pop()
    at = [i for i in range(1, len(lines)) if lines[i].startswith("BX|") and parses(lines[i])]
    for i in range(min(len(at), len(new))):
        lines[at[i]] = modern(new[i])
    for i in reversed(range(len(new), len(at))):
        del lines[at[i]]
    lines += [modern(b) for b in new[len(at):]]
    return eol.join(lines) + (eol if text.endswith("\n") else "")


class Seed(unittest.TestCase):
    def edit(self, seed, data):
        text = seed_text(*seed)
        with scratch() as d:
            path = os.path.join(d, "randomizer.dat")
            with open(path, "wb") as f:
                f.write(text.encode("utf-8"))
            boxes = B.call("PracticeGlue.SeedBoxLines", path)
            new = data.draw(box_edits(boxes))
            B.call("PracticeGlue.SeedEdit", path, new)
            with open(path, "rb") as f:
                out = f.read().decode("utf-8")
        return out, expected_seed(text, new, lambda l: B.call("PracticeGlue.Parses", l))

    @settings(max_examples=examples(80))
    @given(seed_files(), st.data())
    def test_save_keeps_every_other_line_and_its_ending_byte_for_byte(self, seed, data):
        out, want = self.edit(seed, data)
        self.assertEqual(out, want)

    @settings(max_examples=examples(40))
    @given(seed_files(mixed=True), st.data())
    def test_save_of_mixed_endings_keeps_every_line_and_writes_crlf(self, seed, data):
        out, want = self.edit(seed, data)
        self.assertEqual(out, want)
