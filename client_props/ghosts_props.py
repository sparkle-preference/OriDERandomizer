"""Ghosts: the motion packet and .ghost file codecs, the clip table, the signal parsers, and the
practice page's loopback server."""
import json
import math
import os
import re
import shutil
import socket
import struct
import threading
import time
import unittest
import urllib.parse
import uuid

from hypothesis import assume, example, given, settings, strategies as st

from client_props.base import HERE, client_file, examples
from client_props.cs_bridge import CsError, outcome, slice_bridge
from client_props.strategies import ghosts as gs

NAN = float("nan")
CS = os.path.join(HERE, "cs", "ghosts")
WORK = os.path.join(os.environ.get("TEMP", HERE), "client_props", "work", "ghosts")
SOURCES = ["randomizer/RandomizerGhostPacket.cs", "randomizer/RandomizerGhostAnimations.cs",
           "randomizer/RandomizerGhostSignal.cs", "randomizer/RandomizerGhostSource.cs", "randomizer/JsonValue.cs"]
STUBS = [os.path.join(CS, f) for f in ("Engine.cs", "Fakes.cs", "Glue.cs")]

B = None


def setUpModule():
    global B
    B = slice_bridge("ghosts", SOURCES, extra=STUBS)


def table_from_source():
    text = client_file("randomizer/RandomizerGhostAnimations.cs").decode("utf-8-sig")
    block = text[text.index("Names = {"):]
    return re.findall(r'^\s*"([^"]*)",?\s*$', block[:block.index("};")], re.M)


NAMES = table_from_source()


def f32(v):
    """v as C# casts it to a float: nearest, and infinity past the range."""
    try:
        return struct.unpack("<f", struct.pack("<f", v))[0]
    except OverflowError:
        return math.copysign(math.inf, v)


def same(a, b, eps=0.0):
    if isinstance(a, float) or isinstance(b, float):
        if math.isnan(a) or math.isnan(b):
            return math.isnan(a) and math.isnan(b)
        return abs(a - b) <= eps
    return a == b


def same_list(case, got, want):
    case.assertEqual(len(got), len(want))
    for g, w in zip(got, want):
        case.assertEqual(sorted(g), sorted(w))
        case.assertTrue(all(same(g[k], w[k]) for k in g), (g, w))


def circular(a, b):
    return abs((a - b + 180.0) % 360.0 - 180.0)


def turn_between(p, q):
    """Degrees of rotation taking quaternion p to q."""
    w = p["w"] * q["w"] + p["x"] * q["x"] + p["y"] * q["y"] + p["z"] * q["z"]
    x = p["w"] * q["x"] - p["x"] * q["w"] - p["y"] * q["z"] + p["z"] * q["y"]
    y = p["w"] * q["y"] - p["y"] * q["w"] - p["z"] * q["x"] + p["x"] * q["z"]
    z = p["w"] * q["z"] - p["z"] * q["w"] - p["x"] * q["y"] + p["y"] * q["x"]
    return 2 * math.degrees(math.atan2(math.sqrt(x * x + y * y + z * z), abs(w)))


def finite(*xs):
    return all(math.isfinite(x) for x in xs)


# --- models -------------------------------------------------------------------------------------

def euler(ey, ez):
    """Unity's Euler(0, ey, ez), in doubles."""
    hy, hz = math.radians(ey) / 2, math.radians(ez) / 2
    return {"x": math.sin(hy) * math.sin(hz), "y": math.sin(hy) * math.cos(hz),
            "z": math.cos(hy) * math.sin(hz), "w": math.cos(hy) * math.cos(hz)}


def model_decode(b):
    """The wire format as documented in RandomizerGhostPacket.cs, read from exactly these bytes."""
    n = len(b)
    if n < 22 or n > 48 or b[0] != 1:
        return None
    t, x, y = struct.unpack_from("<fff", b, 4)
    if not finite(t, x, y):
        return None
    clip, clip_time, flags, roll, flags2 = struct.unpack_from("<HBBBB", b, 16)
    anim = NAMES[clip] if clip < len(NAMES) else ""
    q = euler(180.0 if flags & 1 else 0.0, roll * 45 / 32)
    out = {"player": b[1], "seq": b[2] | b[3] << 8, "t": t, "x": x, "y": y, "z": 0.0,
           "rx": q["x"], "ry": q["y"], "rz": q["z"], "rw": q["w"],
           "anim": anim, "animTime": f32(clip_time / 255.0) if anim else 0.0,
           "charge": 2 if flags & 4 else (1 if flags & 2 else 0),
           "triple": bool(flags & 64), "died": bool(flags & 128), "menu": bool(flags2 & 2), "title": bool(flags2 & 4),
           "bash": NAN, "bx": NAN, "by": NAN, "ax": NAN, "ay": NAN, "wall": NAN, "sx": NAN, "sy": NAN}
    at = 22
    # an optional field cut short is skipped, and the next one is read from the same place
    if flags & 8 and at + 9 <= n:
        bx, by = struct.unpack_from("<ff", b, at + 1)
        if not finite(bx, by):
            return None
        out.update(bash=b[at] * 45 / 32, bx=bx, by=by)
        at += 9
    if flags & 16 and at + 8 <= n:
        ax, ay = struct.unpack_from("<ff", b, at)
        if not finite(ax, ay):
            return None
        out.update(ax=ax, ay=ay)
        at += 8
    if flags & 32 and at < n:
        out["wall"] = b[at] * 45 / 32
        at += 1
    if flags2 & 1 and at + 8 <= n:
        sx, sy = struct.unpack_from("<ff", b, at)
        if not finite(sx, sy):
            return None
        out.update(sx=sx, sy=sy)
    return out


ROTATION = ("rx", "ry", "rz", "rw")


def assert_decoded(case, got, want):
    if want is None:
        case.assertIsNone(got)
        return
    case.assertIsNotNone(got)
    case.assertEqual(sorted(got), sorted(want))
    for key, value in want.items():
        case.assertTrue(same(got[key], value, 1e-6 if key in ROTATION else 0.0), (key, got[key], value))


def model_unpack(data):
    if len(data) < 9 or data[0] != 1:
        return []
    out, at = [], 9
    while at < len(data):
        n = data[at]
        at += 1
        if n == 0 or n > 48 or at + n > len(data):
            break
        decoded = model_decode(data[at:at + n])
        at += n
        if decoded is not None:
            out.append(decoded)
    return out


def units(s):
    raw = s.encode("utf-16-le", "surrogatepass")
    return struct.unpack("<%dH" % (len(raw) // 2), raw)


def fnv1a32(names):
    h = 2166136261
    for name in names:
        for unit in units(name):
            h = ((h ^ unit) * 16777619) & 0xFFFFFFFF
        h = ((h ^ 10) * 16777619) & 0xFFFFFFFF
    return h


NET_SPACE = " \t\n\v\f\r"


def net_int(s):
    """.NET's int.TryParse(s) under en-US: spaces, one sign, ASCII digits, spaces, then only NULs."""
    i, n = 0, len(s)
    while i < n and s[i] in NET_SPACE:
        i += 1
    negative = i < n and s[i] == "-"
    if i < n and s[i] in "+-":
        i += 1
    start = i
    while i < n and "0" <= s[i] <= "9":
        i += 1
    if i == start:
        return False, 0
    value = -int(s[start:i]) if negative else int(s[start:i])
    while i < n and s[i] in NET_SPACE:
        i += 1
    if s[i:].strip("\x00") or not -2 ** 31 <= value < 2 ** 31:
        return False, 0
    return True, value


def model_roster(body):
    sep = body.find(":")
    if sep < 0:
        return None
    ok, host = net_int(body[:sep])
    if not ok:
        return None
    present = []
    for part in body[sep + 1:].split(","):
        ok, pid = net_int(part)
        if part and ok:
            present.append(pid)
    return host, present


def model_escape(s):
    """RFC 3986 percent-encoding of the UTF-8 bytes, unreserved characters kept."""
    return urllib.parse.quote(s, safe="-._~")


def non_ascii_credentials(*texts):
    """A TURN username or credential with a character outside ASCII."""
    return any(isinstance(t, str) and not t.isascii() for t in texts)


def model_relay(doc, url_before):
    """RelayUrl after OnIce(json.dumps(doc)): the url with both credentials folded in, or as it was."""
    if not isinstance(doc, dict):
        return url_before, None
    urls, user, secret = doc.get("urls"), doc.get("username"), doc.get("credential")
    if not (isinstance(urls, list) and urls and isinstance(urls[0], str) and isinstance(user, str)
            and isinstance(secret, str)) or ":" not in urls[0]:
        return url_before, None
    url = urls[0]
    i = url.index(":")
    ttl = doc.get("ttl")
    number = isinstance(ttl, (int, float)) and not isinstance(ttl, bool)
    expires = f32(10.0 + f32(ttl / 2)) if number and ttl > 0 else 3.4028234663852886e38
    return url[:i + 1] + model_escape(user) + ":" + model_escape(secret) + "@" + url[i + 1:], expires


def tile_model(path):
    """(z, x, y) for a /tiles/ path Tile serves or redirects, None for its 404."""
    parts = path[len("/tiles/"):].split("/")
    if len(parts) != 3 or not parts[2].endswith(".png"):
        return None
    got = [net_int(p) for p in (parts[0], parts[1], parts[2][:-4])]
    if not all(ok for ok, _ in got):
        return None
    z, x, y = (v for _, v in got)
    return (z, x, y) if 0 <= z <= 7 and x >= 0 and y >= 0 else None


def model_allowed(method, headers, body_length, port):
    ours = ("127.0.0.1:%d" % port, "localhost:%d" % port)
    host = headers.get("host")
    if host is None or host.lower() not in ours:
        return False
    if method == "GET":
        return True
    origin = headers.get("origin")
    if origin is not None and not (origin[:7].lower() == "http://" and origin[7:].lower() in ours):
        return False
    kind = headers.get("content-type")
    return body_length == 0 or kind is not None and kind.lower().startswith("application/json")


def model_read(data):
    """(method, path, headers, body) as ReadRequest returns them once the client has closed, or None."""
    end = data.find(b"\r\n\r\n")
    if end < 0:
        return None
    header = "".join(chr(c) if c < 128 else "?" for c in data[:end])
    lines = [line for line in header.split("\r\n") if line]
    if not lines:
        return None
    request = lines[0].split(" ")
    if len(request) < 2:
        return None
    headers = {}
    for line in lines[1:]:
        colon = line.find(":")
        if colon > 0:
            headers[line[:colon].strip(NET_SPACE).lower()] = line[colon + 1:].strip(NET_SPACE)
    length = net_int(headers["content-length"])[1] if "content-length" in headers else 0
    if length < 0 or length > 4 * 1024 * 1024:
        return None
    body = data[end + 4:end + 4 + length]
    if len(body) < length:
        return None
    return request[0].upper(), request[1], headers, body


JOBS = {("GET", "/api/segment"), ("PUT", "/api/segment"), ("PUT", "/api/placements"), ("POST", "/api/export"),
        ("GET", "/api/catalog"), ("GET", "/api/saves"), ("POST", "/api/create"), ("POST", "/api/save"),
        ("GET", "/api/ghosts"), ("POST", "/api/ghost/pin")}
JOB_STATUSES = {200, 400, 409, 503}


def model_route(method, path):
    """(statuses the route can answer, whether it queues a job) for an allowed request to an untiled server."""
    path = path.split("?", 1)[0]
    if method == "GET" and path in ("/", "/leaflet.js", "/leaflet.css"):
        return {200, 500}, False
    if method == "GET" and path.startswith("/tiles/"):
        return ({302} if tile_model(path) else {404}), False
    if method == "GET" and path in ("/api/tiles", "/api/pane"):
        return {200}, False
    if (method, path) in JOBS or method == "DELETE" and path.startswith(("/api/ghost/", "/api/variant/")):
        return JOB_STATUSES, True
    return {404}, False


# --- packets ------------------------------------------------------------------------------------

def to_sample(s):
    nan2 = {"x": NAN, "y": NAN}
    rotation = euler(180.0 if s["left"] else 0.0, s["roll"])
    return {
        "Time": s["t"], "Position": {"x": s["x"], "y": s["y"], "z": 0.0}, "Rotation": rotation,
        "Animation": s["anim"], "AnimationTime": s["animTime"], "Charge": s["charge"],
        "BashAngle": s["bash"][0] if s["bash"] else NAN,
        "BashTarget": {"x": s["bash"][1][0], "y": s["bash"][1][1]} if s["bash"] else nan2,
        "GrenadeAim": {"x": s["aim"][0], "y": s["aim"][1]} if s["aim"] else nan2,
        "WallAim": NAN if s["wall"] is None else s["wall"],
        "Triple": s["triple"], "Died": s["died"],
        "SoulLink": {"x": s["soul"][0], "y": s["soul"][1]} if s["soul"] else nan2,
        "InMenu": s["menu"], "OnTitle": s["title"],
    }


STEP = 360.0 / 256.0


def decoded_rotation(d):
    return {"x": d["rx"], "y": d["ry"], "z": d["rz"], "w": d["rw"]}


def left_facing_past_120(s):
    """Facing left with the sprite rolled more than a third of a turn either way."""
    return s["left"] and abs((s["roll"] + 180.0) % 360.0 - 180.0) >= 119.5


# every optional field present: bash 22-30, aim 31-38, wall 39, soul 40-47
FULL = struct.pack("<BBHfffHBBBB", 1, 2, 3, 1.0, 2.0, 3.0, 5, 128, 0x38, 64, 1) + bytes([32]) + \
    struct.pack("<ffff", 4.0, 5.0, 6.0, 7.0) + bytes([16]) + struct.pack("<ff", 8.0, 9.0)


class Packets(unittest.TestCase):
    @settings(max_examples=examples(600))
    @given(gs.packets(NAMES), st.binary(max_size=16))
    @example(FULL[:30], b"")
    @example(FULL[:30], b"\x00")
    @example(FULL[:38], b"\x00")
    @example(FULL[:47], b"\x00")
    def test_decode_agrees_with_the_wire_model_and_reads_only_its_length(self, packet, tail):
        got = outcome(B.call, "GhostGlue.Decode", packet + tail, len(packet))
        self.assertEqual(got[0], "ok", got)
        assert_decoded(self, got[1], model_decode(packet))

    @settings(max_examples=examples(300))
    @given(gs.samples(NAMES))
    def test_an_encoded_sample_decodes_to_itself_within_one_step(self, s):
        assume(not left_facing_past_120(s))
        packet = B.call("GhostGlue.Encode", to_sample(s), s["player"], s["seq"])
        self.assertEqual(len(packet), 22 + 9 * bool(s["bash"]) + 8 * bool(s["aim"]) + (s["wall"] is not None)
                         + 8 * bool(s["soul"]))
        d = B.call("GhostGlue.Decode", packet, len(packet))
        self.assertIsNotNone(d)
        for key in ("player", "seq", "t", "x", "y", "triple", "died", "menu", "title"):
            self.assertEqual(d[key], s[key], key)
        nan2 = (NAN, NAN)
        self.assertTrue(same(d["ax"], (s["aim"] or nan2)[0]) and same(d["ay"], (s["aim"] or nan2)[1]))
        self.assertTrue(same(d["sx"], (s["soul"] or nan2)[0]) and same(d["sy"], (s["soul"] or nan2)[1]))
        known = s["anim"] in NAMES
        self.assertEqual(d["anim"], s["anim"] if known else "")
        self.assertLessEqual(abs(d["animTime"] - (min(s["animTime"], 1.0) if known else 0.0)), 0.5 / 255 + 1e-6)
        self.assertEqual(d["charge"], s["charge"] if s["charge"] in (1, 2) else 0)
        self.assertLessEqual(turn_between(to_sample(s)["Rotation"], decoded_rotation(d)), STEP + 1e-3)
        if s["bash"]:
            self.assertLessEqual(circular(d["bash"], s["bash"][0]), STEP + 1e-3)
            self.assertEqual((d["bx"], d["by"]), s["bash"][1])
        else:
            self.assertTrue(math.isnan(d["bash"]) and math.isnan(d["bx"]))
        if s["wall"] is None:
            self.assertTrue(math.isnan(d["wall"]))
        else:
            self.assertLessEqual(circular(d["wall"], s["wall"]), STEP + 1e-3)

    @settings(max_examples=examples(100))
    @given(gs.samples(NAMES), st.sampled_from(["t", "x", "y", "bx", "by", "ax", "ay", "sx", "sy"]),
           st.sampled_from([NAN, math.inf, -math.inf]))
    def test_a_non_finite_float_in_a_sent_field_refuses_the_whole_packet(self, s, field, bad):
        s = dict(s, bash=s["bash"] or (0.0, (1.0, 2.0)), aim=s["aim"] or (3.0, 4.0), soul=s["soul"] or (5.0, 6.0))
        if field in ("t", "x", "y"):
            s[field] = bad
        elif field[0] == "b":
            s["bash"] = (s["bash"][0], (bad, s["bash"][1][1]) if field == "bx" else (s["bash"][1][0], bad))
        else:
            key = "aim" if field[0] == "a" else "soul"
            s[key] = (bad, s[key][1]) if field[1] == "x" else (s[key][0], bad)
        packet = B.call("GhostGlue.Encode", to_sample(s), 1, 0)
        decoded = B.call("GhostGlue.Decode", packet, len(packet))
        # a NaN x is how a sample says it has no aim or soul link
        if field in ("ax", "sx") and math.isnan(bad):
            self.assertTrue(math.isnan(decoded[field]) and math.isnan(decoded[field[0] + "y"]))
        else:
            self.assertIsNone(decoded)

    @settings(max_examples=examples(200))
    @given(gs.packets(NAMES))
    def test_unclip_blanks_the_clip_and_nothing_else(self, packet):
        got = B.call("GhostGlue.Unclipped", packet)
        self.assertEqual(got, packet[:16] + b"\xff\xff\x00" + packet[19:] if len(packet) > 18 else packet)
        want = model_decode(packet)
        if want is not None:
            assert_decoded(self, B.call("GhostGlue.Decode", got, len(got)), dict(want, anim="", animTime=0.0))

    @unittest.expectedFailure
    def test_a_left_facing_sprite_rolled_past_a_third_of_a_turn_decodes_to_its_own_rotation(self):
        s = {"t": 1.0, "x": 0.0, "y": 0.0, "left": True, "roll": 180.0, "anim": "run", "animTime": 0.0, "charge": 0,
             "bash": None, "aim": None, "wall": None, "triple": False, "died": False, "soul": None, "menu": False,
             "title": False}
        packet = B.call("GhostGlue.Encode", to_sample(s), 1, 0)
        decoded = decoded_rotation(B.call("GhostGlue.Decode", packet, len(packet)))
        self.assertLessEqual(turn_between(to_sample(s)["Rotation"], decoded), STEP)


# --- .ghost files -------------------------------------------------------------------------------

def strip(d):
    return {k: v for k, v in d.items() if k not in ("player", "seq")}


class Files(unittest.TestCase):
    @settings(max_examples=examples(80))
    @given(st.lists(gs.samples(NAMES), max_size=24))
    def test_pack_then_unpack_keeps_every_sample_as_the_wire_carries_it(self, takes):
        data = B.call("RandomizerGhostPacket.Pack", [to_sample(s) for s in takes])
        self.assertEqual(data[0], 1)
        self.assertEqual(struct.unpack_from("<I", data, 1)[0], len(takes))
        self.assertTrue(same(struct.unpack_from("<f", data, 5)[0], takes[-1]["t"] if takes else 0.0))
        got = B.call("GhostGlue.Unpack", data)
        self.assertEqual(len(got), len(takes))
        at = 9
        for i, (s, d) in enumerate(zip(takes, got)):
            wire = B.call("GhostGlue.Encode", to_sample(s), 0, i)
            self.assertEqual(data[at:at + 1 + len(wire)], bytes([len(wire)]) + wire)
            at += 1 + len(wire)
            assert_decoded(self, d, strip(model_decode(wire)))
        self.assertEqual(at, len(data))

    @settings(max_examples=examples(80))
    @given(st.lists(gs.samples(NAMES), min_size=1, max_size=12), st.data())
    def test_a_file_cut_anywhere_keeps_the_whole_records_before_the_cut(self, takes, data):
        packed = B.call("RandomizerGhostPacket.Pack", [to_sample(s) for s in takes])
        cut = data.draw(st.integers(0, len(packed)))
        full = B.call("GhostGlue.Unpack", packed)
        part = B.call("GhostGlue.Unpack", packed[:cut])
        whole, at = 0, 9
        while cut >= 9 and at < cut and at + 1 + packed[at] <= cut:
            at += 1 + packed[at]
            whole += 1
        same_list(self, part, full[:whole])

    @settings(max_examples=examples(300))
    @given(gs.ghost_files(NAMES))
    def test_unpack_agrees_with_the_record_model_on_any_bytes(self, data):
        got = outcome(B.call, "GhostGlue.Unpack", data)
        self.assertEqual(got[0], "ok", got)
        want = model_unpack(data)
        self.assertEqual(len(got[1]), len(want))
        for g, w in zip(got[1], want):
            assert_decoded(self, g, strip(w))

    def test_unpack_of_nothing_is_nothing(self):
        self.assertEqual(B.call("GhostGlue.Unpack", None), [])
        self.assertEqual(B.call("GhostGlue.Unpack", b""), [])


# --- the clip table -----------------------------------------------------------------------------

class Animations(unittest.TestCase):
    def test_the_table_is_the_sweep_file_strictly_sorted(self):
        names = B.get("RandomizerGhostAnimations.Names")
        with open(os.path.join(HERE, "ghost-animations.txt"), encoding="utf-8") as f:
            swept = [line.rstrip("\r\n") for line in f if line.strip()]
        self.assertEqual(names, NAMES)
        self.assertEqual(names, swept)
        keys = [units(n) for n in names]
        self.assertEqual(keys, sorted(set(keys)))

    def test_hash_is_fnv1a_over_the_table_and_rides_as_eight_hex_digits(self):
        self.assertEqual(B.get("RandomizerGhostAnimations.Hash"), fnv1a32(NAMES))
        self.assertEqual(B.get("RandomizerGhostSignal.Clips"), format(fnv1a32(NAMES), "08X"))

    @settings(max_examples=examples(300))
    @given(st.one_of(st.sampled_from(NAMES), st.text(max_size=20), st.sampled_from([n.upper() for n in NAMES]),
                     st.sampled_from([n + " " for n in NAMES]), st.none()))
    def test_index_of_finds_exactly_the_table_and_name_of_inverts_it(self, name):
        index = B.call("RandomizerGhostAnimations.IndexOf", name)
        self.assertEqual(index, NAMES.index(name) if name in NAMES else 0xFFFF)
        self.assertEqual(B.call("RandomizerGhostAnimations.NameOf", index), name if name in NAMES else None)

    @settings(max_examples=examples(200))
    @given(st.one_of(st.integers(-2 ** 31, 2 ** 31 - 1), st.integers(-2, len(NAMES) + 2)))
    def test_name_of_any_index(self, i):
        self.assertEqual(B.call("RandomizerGhostAnimations.NameOf", i), NAMES[i] if 0 <= i < len(NAMES) else None)


# --- the signal parsers -------------------------------------------------------------------------

OURS_CLIPS = format(fnv1a32(NAMES), "08X")


def b64(text):
    import base64
    return base64.b64encode(text.encode("utf-8")).decode("ascii")


def links_of(state):
    return sorted((l["pid"], l["offering"]) for l in state["links"])


@st.composite
def signal_setups(draw):
    """Who we are, the roster we had, the offers taken from it, and whose packets have come in since."""
    me = draw(st.integers(0, 4))
    pids = sorted(draw(st.sets(st.integers(1, 5), max_size=5)) | ({me} if me else set()))
    host = draw(st.sampled_from(pids or [1]))
    offers = draw(st.lists(st.sampled_from(pids), max_size=2)) if pids else []
    heard = draw(st.lists(st.sampled_from(pids), max_size=3)) if pids else []
    return me, "%d:%s" % (host, ",".join(map(str, pids))), offers, heard


def prepare(setup):
    me, roster, offers, heard = setup
    B.call("SignalGlue.Reset", me)
    B.call("RandomizerGhostSignal.OnRoster", roster)
    for pid in offers:
        B.call("RandomizerGhostSignal.OnDescription", "%d:offer|%s|%s" % (pid, b64("v=0"), OURS_CLIPS))
    for pid in heard:
        B.call("SignalGlue.Hear", pid, pid)
    return me, B.call("SignalGlue.State")


def roster_after(pre, me, host, present):
    """Link (pid, offering) pairs after a roster lands: leavers drop, a guest keeps only the host, a host offers."""
    links = [(l["pid"], l["offering"]) for l in pre["links"] if l["pid"] in present]
    if pre["joined"] and me > 0:
        if host != me:
            links = [l for l in links if l[0] == host]
        else:
            for pid in present:
                if pid != me and pid not in [l[0] for l in links]:
                    links.append((pid, True))
    return sorted(links)


class Signal(unittest.TestCase):
    @settings(max_examples=examples(500))
    @given(gs.any_ints)
    def test_the_int_parse_model_is_dotnets(self, s):
        ok, value = net_int(s)
        self.assertEqual(B.call("System.Int32.TryParse", s), (ok, [value]))

    @settings(max_examples=examples(300))
    @given(signal_setups(), gs.roster_bodies())
    def test_a_roster_applies_whole_as_parsed_or_not_at_all(self, setup, body):
        me, pre = prepare(setup)
        got = outcome(B.call, "RandomizerGhostSignal.OnRoster", body)
        self.assertEqual(got[0], "ok", got)
        post = B.call("SignalGlue.State")
        parsed = model_roster(body)
        if parsed is None:
            self.assertEqual(post, pre)
            return
        host, present = parsed
        self.assertEqual((post["host"], post["roster"]), (host, present))
        self.assertEqual(links_of(post), roster_after(pre, me, host, present))
        self.assertEqual(post["ghosts"], [g for g in pre["ghosts"] if g in present])
        kept = {l["handle"] for l in post["links"]}
        self.assertTrue(all(l["handle"] in post["released"] for l in pre["links"] if l["handle"] not in kept))

    @settings(max_examples=examples(100))
    @given(gs.server_rosters(), st.integers(1, 12))
    def test_the_servers_roster_frame_gives_the_client_its_host_and_players(self, roster, me):
        joined, relayed = roster
        import ws
        saved = set(ws._ghosts), set(ws._ghost_relayed)
        try:
            ws._ghosts.clear()
            ws._ghosts.update((70, p) for p in joined)
            ws._ghost_relayed.clear()
            ws._ghost_relayed.update((70, p) for p in relayed)
            host, pids = ws._ghost_roster(70)
        finally:
            ws._ghosts.clear()
            ws._ghosts.update(saved[0])
            ws._ghost_relayed.clear()
            ws._ghost_relayed.update(saved[1])
        frame = "ghosts:%s:%s" % (host, ",".join(str(p) for p in pids))
        B.call("SignalGlue.Reset", me)
        B.call("RandomizerGhostSignal.OnRoster", frame[frame.index(":") + 1:])
        state = B.call("SignalGlue.State")
        self.assertEqual((state["host"], state["roster"]), (host, sorted(joined)))
        want = [(p, True) for p in sorted(joined) if p != me] if host == me else []
        self.assertEqual(links_of(state), want)

    @settings(max_examples=examples(300))
    @given(signal_setups(), st.one_of(
        st.text(max_size=40),
        st.builds(lambda f, t, rest: f + ":" + t + "|" + rest, gs.any_ints,
                  st.sampled_from(["offer", "answer", "pranswer", ""]), st.text(max_size=30))))
    def test_any_description_text_is_taken_or_dropped_whole(self, setup, body):
        me, pre = prepare(setup)
        got = outcome(B.call, "RandomizerGhostSignal.OnDescription", body)
        self.assertEqual(got[0], "ok", got)
        post = B.call("SignalGlue.State")
        pids = [l["pid"] for l in post["links"]]
        self.assertEqual(len(pids), len(set(pids)))
        before = {l["handle"]: l for l in pre["links"]}
        changed = [l for l in post["links"] if l["handle"] not in before or l["remote"] != before[l["handle"]]["remote"]]
        self.assertLessEqual(len(changed), 1)
        if changed:
            ok, sender = net_int(body[:body.index(":")])
            self.assertTrue(ok and changed[0]["pid"] == sender)
            self.assertEqual(changed[0]["remote"][-1].split("\n", 1)[0], body[body.index(":") + 1:].split("|", 1)[0])
        else:
            self.assertEqual(links_of(post), links_of(pre))

    @settings(max_examples=examples(200))
    @given(signal_setups(), st.integers(1, 5), st.sampled_from(["offer", "answer"]), gs.sdps,
           st.one_of(st.just(OURS_CLIPS), st.just(None), st.sampled_from(["DEADBEEF", "", OURS_CLIPS.lower()])))
    def test_a_well_formed_description_reaches_the_link_for_its_sender(self, setup, sender, kind, sdp, clips):
        me, pre = prepare(setup)
        body = "%d:%s|%s" % (sender, kind, b64(sdp)) + ("" if clips is None else "|" + clips)
        B.call("RandomizerGhostSignal.OnDescription", body)
        post = B.call("SignalGlue.State")
        had = [l for l in pre["links"] if l["pid"] == sender]
        mine = [l for l in post["links"] if l["pid"] == sender]
        if not pre["joined"] or kind == "answer" and not had:
            self.assertEqual(post["links"], pre["links"])
            return
        self.assertEqual(len(mine), 1)
        link = mine[0]
        self.assertEqual(link["remote"][-1], kind + "\n" + sdp)
        self.assertEqual(link["otherClips"], clips != OURS_CLIPS)
        if kind == "offer":
            self.assertFalse(link["offering"])
            self.assertTrue(all(l["handle"] in post["released"] for l in had))

    @settings(max_examples=examples(100))
    @given(gs.sdps, gs.sdps)
    def test_an_offer_and_its_answer_cross_the_server_verbatim(self, offer, answer):
        import ws
        sent = []
        real = ws._send_to, set(ws._ghosts)
        ws._send_to = lambda gpid, frame: sent.append((gpid, frame)) or True
        ws._ghosts.update({(71, 1), (71, 2)})
        try:
            # the host offers, the server relays, the guest applies it and answers
            B.call("SignalGlue.Reset", 1)
            B.set("NativeWebSocket.LocalDescription", offer)
            B.call("RandomizerGhostSignal.OnRoster", "1:1,2")
            B.call("SignalGlue.Frame", 1 / 30)
            out = [t for t in B.call("SignalGlue.State")["texts"] if t.startswith("ghost:")]
            self.assertEqual(len(out), 1)
            self.assertEqual(ws.handle_frame(71, 1, out[0]), (None, False))
            (to, frame), = sent
            self.assertEqual(to, (71, 2))
            B.call("SignalGlue.Reset", 2)
            B.set("NativeWebSocket.LocalDescription", answer)
            B.call("RandomizerGhostSignal.OnRoster", "1:1,2")
            B.call("RandomizerGhostSignal.OnDescription", frame[frame.index(":") + 1:])
            guest = B.call("SignalGlue.State")
            self.assertEqual([(l["pid"], l["remote"], l["otherClips"]) for l in guest["links"]],
                             [(1, ["offer\n" + offer], False)])
            B.call("SignalGlue.Frame", 1 / 30)
            back = [t for t in B.call("SignalGlue.State")["texts"] if t.startswith("ghost:")]
            self.assertEqual(len(back), 1)
            sent.clear()
            self.assertEqual(ws.handle_frame(71, 2, back[0]), (None, False))
            (to, frame), = sent
            self.assertEqual(to, (71, 1))
            B.call("SignalGlue.Reset", 1)
            B.call("RandomizerGhostSignal.OnRoster", "1:1,2")
            B.call("RandomizerGhostSignal.OnDescription", frame[frame.index(":") + 1:])
            host = B.call("SignalGlue.State")
            self.assertEqual([(l["pid"], l["remote"]) for l in host["links"]], [(2, ["answer\n" + answer])])
        finally:
            ws._send_to = real[0]
            ws._ghosts.clear()
            ws._ghosts.update(real[1])

    @settings(max_examples=examples(300))
    @given(gs.ice_docs(), st.booleans())
    def test_a_relay_config_folds_in_whole_or_not_at_all(self, text, failing):
        B.call("SignalGlue.Reset", 1)
        B.call("RandomizerGhostSignal.OnRoster", "1:1,2")
        if failing:
            B.call("SignalGlue.Frame", 16.0)
        doc = json.loads(text)
        assume(not (isinstance(doc, dict) and non_ascii_credentials(doc.get("username"), doc.get("credential"))))
        pre = B.call("SignalGlue.State")
        got = outcome(B.call, "RandomizerGhostSignal.OnIce", text)
        self.assertEqual(got[0], "ok", got)
        post = B.call("SignalGlue.State")
        url, expires = model_relay(doc, pre["relayUrl"])
        self.assertEqual(post["relayUrl"], url)
        if expires is None:
            self.assertEqual(post["relayExpires"], pre["relayExpires"])
            self.assertEqual(links_of(post), links_of(pre))
            return
        self.assertTrue(same(post["relayExpires"], expires))
        if failing:
            relayed = [l for l in post["links"] if l["pid"] == 2]
            self.assertEqual(len(relayed), 1)
            self.assertTrue(relayed[0]["relayed"] and relayed[0]["ice"].endswith("," + url))

    @settings(max_examples=examples(200))
    @given(st.text(max_size=60))
    def test_any_relay_text_is_survived(self, text):
        B.call("SignalGlue.Reset", 1)
        got = outcome(B.call, "RandomizerGhostSignal.OnIce", text)
        self.assertEqual(got[0], "ok", got)

    @settings(max_examples=examples(300))
    @given(st.one_of(st.text(st.characters(max_codepoint=127), max_size=30), st.text(max_size=30)))
    def test_escape_is_the_percent_encoding_a_relay_decodes_back(self, s):
        assume(not non_ascii_credentials(s))
        got = B.call("RandomizerGhostSignal.Escape", s)
        self.assertEqual(got, model_escape(s))
        self.assertEqual(urllib.parse.unquote(got), s)

    @unittest.expectedFailure
    def test_a_non_ascii_relay_credential_percent_decodes_to_itself(self):
        self.assertEqual(urllib.parse.unquote(B.call("RandomizerGhostSignal.Escape", "é")), "é")


# --- the practice page's loopback server --------------------------------------------------------

S = None


def server():
    global S
    if S is None:
        S = slice_bridge("ghosts_server", ["randomizer/PracticeServer.cs"], extra=[os.path.join(CS, "ServerGlue.cs")],
                         game=True)
    return S


def fresh_dir(name):
    path = os.path.join(WORK, "%s-%s" % (name, uuid.uuid4().hex[:8]))
    os.makedirs(path)
    return path


def exchange(port, data, close=True, timeout=10.0):
    """Sends data (then half-closes) and reads until the server closes: (reply, seconds, closed by the server)."""
    start = time.monotonic()
    with socket.create_connection(("127.0.0.1", port), timeout=timeout) as c:
        try:
            c.sendall(data)
            if close:
                c.shutdown(socket.SHUT_WR)
        except OSError:
            pass
        out = b""
        try:
            while True:
                chunk = c.recv(65536)
                if not chunk:
                    break
                out += chunk
        except ConnectionResetError:
            pass
        except socket.timeout:
            return out, time.monotonic() - start, False
    return out, time.monotonic() - start, True


def parse_reply(raw):
    head, _, body = raw.partition(b"\r\n\r\n")
    lines = head.decode("ascii").split("\r\n")
    return int(lines[0].split(" ")[1]), dict(line.split(": ", 1) for line in lines[1:]), body


class ServerCase(unittest.TestCase):
    """A live listener on a free loopback port, with no practice session running."""

    @classmethod
    def setUpClass(cls):
        server()
        cls.dir = fresh_dir(cls.__name__.lower())
        S.call("ServerGlue.SetFolder", os.path.join(cls.dir, "practice"))
        S.call("ServerGlue.EndSession")
        cls.port = S.call("ServerGlue.Start")

    @classmethod
    def tearDownClass(cls):
        S.call("ServerGlue.Stop")
        shutil.rmtree(cls.dir, ignore_errors=True)


class Gate(ServerCase):
    @settings(max_examples=examples(500))
    @given(st.one_of(st.sampled_from(gs.METHODS), gs.ascii_text), gs.gate_headers(), st.sampled_from([0, 1, 100]))
    def test_allowed_is_our_host_then_a_read_or_a_same_origin_json_write(self, method, headers, n):
        headers = {k: gs.fill(v, self.port) for k, v in headers.items()}
        self.assertEqual(S.call("PracticeServer.Allowed", method, headers, n),
                         model_allowed(method, headers, n, self.port))


class Wire(ServerCase):
    @settings(max_examples=examples(250))
    @given(gs.requests())
    def test_any_request_ends_closed_with_the_status_its_route_gives(self, req):
        data = gs.render(req, self.port)
        before = S.call("ServerGlue.Jobs")
        raw, _, closed = exchange(self.port, data)
        self.assertTrue(closed)
        jobs = S.call("ServerGlue.Jobs") - before
        parsed = model_read(data)
        if parsed is None:
            self.assertEqual((raw, jobs), (b"", 0))
            return
        method, path, headers, body = parsed
        status, reply, content = parse_reply(raw)
        self.assertEqual((int(reply["Content-Length"]), reply["Connection"]), (len(content), "close"))
        if not model_allowed(method, headers, len(body), self.port):
            self.assertEqual((status, jobs), (403, 0))
        elif set(path) <= gs.PLAIN:
            statuses, job = model_route(method, path)
            self.assertIn(status, statuses)
            self.assertEqual(jobs, int(job))
        else:
            self.assertIn(status, {200, 302, 400, 404, 409, 500, 503})
            self.assertLessEqual(jobs, 0 if status in (302, 404) else 1)

    def test_an_unfinished_request_is_dropped_at_the_read_timeout(self):
        host = "Host: 127.0.0.1:%d\r\n" % self.port
        partial = [("GET / HTTP/1.1\r\n" + host).encode(),
                   ("PUT /api/segment HTTP/1.1\r\n" + host + "Content-Length: 10\r\n\r\n{").encode()]
        results = []
        threads = [threading.Thread(target=lambda d=d: results.append(exchange(self.port, d, close=False, timeout=15)))
                   for d in partial]
        for t in threads:
            t.start()
        for t in threads:
            t.join()
        for raw, took, closed in results:
            self.assertTrue(closed and raw == b"" and 3 < took < 9, (raw, took, closed))

    def test_an_oversize_header_or_body_is_dropped_without_waiting(self):
        host = "Host: 127.0.0.1:%d\r\n" % self.port
        for data in [b"GET / HTTP/1.1\r\nX: " + b"a" * 70000,
                     ("PUT /api/segment HTTP/1.1\r\n" + host + "Content-Length: 4194305\r\n\r\n").encode()]:
            raw, took, closed = exchange(self.port, data, close=False)
            self.assertTrue(closed and raw == b"" and took < 3, (raw[:80], took, closed))


TILE_HOST = "https://ori-tracker.firebaseapp.com/images/ori-map/"
PLANTED = {(0, 0, 0), (1, 2, 3), (7, 0, 1), (3, 10, 5)}


class Tiles(ServerCase):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        practice = os.path.join(cls.dir, "practice")
        for z, x, y in PLANTED:
            os.makedirs(os.path.join(practice, "map-tiles", str(z), str(x)), exist_ok=True)
            with open(os.path.join(practice, "map-tiles", str(z), str(x), "%d.png" % y), "wb") as f:
                f.write(b"tile %d %d %d" % (z, x, y))
        for spot in ("secret.png", os.path.join("map-tiles", "secret.png"), os.path.join("map-tiles", "0", "secret.png"),
                     os.path.join("..", "secret.png")):
            with open(os.path.join(practice, spot), "wb") as f:
                f.write(b"secret")

    @settings(max_examples=examples(300))
    @given(gs.tile_paths(sorted(PLANTED)))
    @example("/tiles/1/2/3.png")
    @example("/tiles/+1/ 2/3\x00.png")
    def test_a_tile_path_serves_a_cached_tile_or_redirects_to_the_site_and_nothing_else(self, path):
        content, (kind, status, location) = S.call("PracticeServer.Tile", path)
        self.assertNotEqual(content, b"secret")
        want = tile_model(path)
        if want is None:
            self.assertEqual((status, kind, content), (404, "application/json; charset=utf-8", b'{"error":"no such tile"}'))
        elif want in PLANTED:
            self.assertEqual((status, content), (200, b"tile %d %d %d" % want))
        else:
            self.assertEqual((status, location, content), (302, TILE_HOST + "%d/%d/%d.png" % want, b""))


def snapshot(root):
    files = {}
    for folder, _, names in os.walk(root):
        for name in names:
            with open(os.path.join(folder, name), "rb") as f:
                files[os.path.relpath(os.path.join(folder, name), root)] = f.read()
    return files


def given_bfrp(form):
    """A session running a .bfrp as it was given, not yet extracted to a folder."""
    return form == "bfrp"


class RefusedWrites(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        server()
        cls.dir = fresh_dir("refused")

    @classmethod
    def tearDownClass(cls):
        S.call("ServerGlue.EndSession")
        shutil.rmtree(cls.dir, ignore_errors=True)

    def refuse(self, form, handler, body):
        """(folder before, folder after, session path before and after), or None when the write was taken."""
        practice = os.path.join(self.dir, uuid.uuid4().hex[:8], "practice")
        os.makedirs(practice)
        S.call("ServerGlue.Session", S.call("ServerGlue.MakeSegment",
                                            os.path.join(practice, "Given.bfrp" if form == "bfrp" else "Given")))
        before, path = snapshot(practice), S.call("ServerGlue.SessionPath")
        try:
            reply = S.call("PracticeServer." + handler, *((body,) if handler == "DeleteVariant" else (body, False)))
            if handler == "DeleteVariant" or not reply[1][0]:
                return None
        except CsError:
            pass
        return before, snapshot(practice), path, S.call("ServerGlue.SessionPath")

    @settings(max_examples=examples(40))
    @given(st.sampled_from(["folder", "bfrp"]), gs.refusals())
    def test_a_refused_page_write_leaves_the_practice_folder_as_it_was(self, form, refusal):
        assume(not given_bfrp(form))
        seen = self.refuse(form, *refusal)
        assume(seen is not None)
        before, after, path, now = seen
        self.assertEqual(sorted(after), sorted(before))
        self.assertTrue(all(after[k] == before[k] for k in before))
        self.assertEqual(now, path)

    @unittest.expectedFailure
    def test_a_refused_page_save_leaves_a_given_bfrp_as_it_was(self):
        before, after, path, now = self.refuse("bfrp", "WriteSegment", '{"rev":"stale"}')
        self.assertEqual(sorted(after), sorted(before))
