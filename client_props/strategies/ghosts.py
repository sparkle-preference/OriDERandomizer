"""Strategies for the ghosts slice: wire packets, samples, signal frames and loopback requests."""
import json
import string
import struct

from hypothesis import strategies as st

finite32 = st.floats(width=32, allow_nan=False, allow_infinity=False)
angles = st.floats(-720, 720, width=32)
pairs = st.tuples(finite32, finite32)

# a NaN with a payload, both infinities, and a signaling NaN, as raw little-endian bits
POISON = [struct.pack("<I", b) for b in (0x7FC00000, 0xFFC00001, 0x7F800000, 0xFF800000, 0x7F800001)]


def samples(names):
    """What a sender can capture: finite fields, NaN only where an optional field is absent."""
    def shaped(full):
        maybe = (lambda s: s) if full else (lambda s: st.one_of(st.none(), s))
        return st.fixed_dictionaries({
            "t": finite32, "x": finite32, "y": finite32,
            "left": st.booleans(), "roll": st.floats(-360, 360, width=32),
            "anim": st.one_of(st.sampled_from(names), st.sampled_from(names), st.just(""), st.text(max_size=12)),
            "animTime": st.floats(0, 1.5, width=32),
            "charge": st.sampled_from([0, 1, 2, 3, -1]),
            "bash": maybe(st.tuples(angles, pairs)),
            "aim": maybe(pairs),
            "wall": maybe(angles),
            "triple": st.booleans(), "died": st.booleans(),
            "soul": maybe(pairs),
            "menu": st.booleans(), "title": st.booleans(),
            "menuKind": st.sampled_from([0, 1, 2]), "skills": st.integers(0, 2**32 - 1),
            "player": st.integers(0, 255), "seq": st.integers(0, 65535),
        })
    return st.one_of(shaped(False), shaped(False), shaped(True))


@st.composite
def float_bytes(draw):
    if draw(st.integers(0, 9)) == 0:
        return draw(st.sampled_from(POISON))
    return struct.pack("<f", draw(finite32))


@st.composite
def packets(draw, names):
    """Bytes near the wire format: header, flags and the fields they announce, then maybe a cut or a tail."""
    if draw(st.integers(0, 4)) == 0:
        head = draw(st.sampled_from([b"\x01", b"\x01", b"\x00", b"\x02", b""]))
        return (head + draw(st.binary(max_size=64)))[:64]
    flags = draw(st.integers(0, 255))
    flags2 = draw(st.integers(0, 255))
    out = struct.pack("<BBH", draw(st.sampled_from([1, 1, 1, 1, 0, 2])), draw(st.integers(0, 255)),
                      draw(st.integers(0, 65535)))
    out += draw(float_bytes()) + draw(float_bytes()) + draw(float_bytes())
    clip = draw(st.one_of(st.integers(0, len(names) - 1), st.just(0xFFFF), st.integers(0, 65535)))
    out += struct.pack("<HBBBB", clip, draw(st.integers(0, 255)), flags, draw(st.integers(0, 255)), flags2)
    ends = [len(out)]
    if flags & 8:
        out += bytes([draw(st.integers(0, 255))]) + draw(float_bytes()) + draw(float_bytes())
        ends.append(len(out))
    if flags & 16:
        out += draw(float_bytes()) + draw(float_bytes())
        ends.append(len(out))
    if flags & 32:
        out += bytes([draw(st.integers(0, 255))])
        ends.append(len(out))
    if flags2 & 1:
        out += draw(float_bytes()) + draw(float_bytes())
        ends.append(len(out))
    shape = draw(st.sampled_from(["whole", "whole", "cut", "edge", "edge", "tail"]))
    if shape == "cut":
        out = out[:draw(st.integers(0, len(out)))]
    elif shape == "edge":
        # a byte either side of where a field ends
        out = out[:draw(st.sampled_from(ends)) + draw(st.sampled_from([-1, 1]))]
    elif shape == "tail":
        out += draw(st.binary(min_size=1, max_size=20))
    return out[:64]


@st.composite
def ghost_files(draw, names):
    """A .ghost file's bytes: header, then length-prefixed records that may lie about their length."""
    head = bytes([draw(st.sampled_from([1, 1, 1, 0, 2]))]) + draw(st.binary(min_size=8, max_size=8))
    records = b""
    for _ in range(draw(st.integers(0, 8))):
        packet = draw(packets(names))
        n = draw(st.one_of(st.just(len(packet)), st.just(len(packet)), st.integers(0, 255)))
        records += bytes([n]) + packet
    cut = draw(st.one_of(st.none(), st.integers(0, len(head) + len(records))))
    data = head + records
    return data if cut is None else data[:cut]


@st.composite
def int_texts(draw):
    """Integers as the server writes them, and decorated as .NET's int.TryParse might still read them."""
    n = draw(st.one_of(st.integers(-3, 30), st.integers(-2 ** 31, 2 ** 31 - 1),
                       st.sampled_from([2 ** 31, -2 ** 31 - 1, 10 ** 12, 0])))
    pre = draw(st.sampled_from(["", "", "", " ", "\t", "+", "0", " +", "+ ", "\x00", "\x0b", "-"]))
    post = draw(st.sampled_from(["", "", "", " ", "\x00", "\x00\x00", " \x00", "\x00 ", "x", ".0", "\r", "\x1c"]))
    return pre + str(n) + post


net_text = st.text(st.characters(codec="utf-8"), max_size=12)
any_ints = st.one_of(int_texts(), net_text)


@st.composite
def roster_bodies(draw):
    """The body of a ghosts: frame, as the server writes it or bent."""
    if draw(st.integers(0, 5)) == 0:
        return draw(st.text(max_size=30))
    host = draw(any_ints) if draw(st.booleans()) else str(draw(st.integers(0, 6)))
    pids = draw(st.lists(st.one_of(st.integers(0, 6).map(str), any_ints, st.just("")), max_size=6))
    sep = draw(st.sampled_from([":", ":", ":", "", "::"]))
    return host + sep + ",".join(pids)


def server_rosters():
    """Joined players of one game and the ones nobody could reach directly."""
    return st.sets(st.integers(1, 12), min_size=1, max_size=8).flatmap(
        lambda pids: st.tuples(st.just(pids), st.sets(st.sampled_from(sorted(pids)))))


sdps = st.one_of(
    st.text(st.characters(exclude_categories=("Cs",)), max_size=120),
    st.lists(st.sampled_from(["v=0", "o=- 1 2 IN IP4 127.0.0.1", "a=candidate:1 1 UDP 1 192.0.2.1 9 typ host",
                              "a=fingerprint:sha-256 AB:CD", "a=ice-pwd:x|y", "m=application 9 UDP/DTLS/SCTP"]),
             max_size=6).map("\r\n".join))


@st.composite
def ice_docs(draw):
    """An ice: body as JSON: the relay's shape, with fields missing, mistyped or odd."""
    url = st.one_of(st.sampled_from(["turn:192.0.2.1:3478?transport=udp", "turns:relay.example:5349", "stun:x",
                                     "noscheme", ":", ""]), st.text(max_size=20))
    text = st.one_of(st.sampled_from(["1735689600:g7", "9:g7", "", "x/y+z=="]), st.text(max_size=16))
    odd = st.one_of(st.none(), st.booleans(), st.integers(-10, 10), st.text(max_size=5))
    doc = draw(st.fixed_dictionaries({}, optional={
        "urls": st.one_of(st.lists(st.one_of(url, odd), max_size=3), url, odd),
        "username": st.one_of(text, odd),
        "credential": st.one_of(text, odd),
        "ttl": st.one_of(st.integers(-10, 10 ** 7), st.floats(-1e9, 1e40, allow_nan=False), odd),
        "other": odd,
    }))
    if draw(st.integers(0, 9)) == 0:
        doc = draw(st.sampled_from([[doc], "x", 3, None]))
    return json.dumps(doc, ensure_ascii=draw(st.booleans()))


# --- loopback requests: placeholders are filled in with the live port when sent ---

OURS = "@ours"
HOSTS = [OURS, "@localhost", "@LOCALHOST", "127.0.0.1", "@other", "evil.example:@port", "@ours.", "[::1]:@port", ""]
ORIGINS = ["http://" + OURS, "HTTP://" + OURS, "https://" + OURS, "null", "http://@localhost", "http://" + OURS + "/",
           "http://evil.example", "", "http://@other"]
TYPES = ["application/json", "application/json; charset=utf-8", "APPLICATION/JSON", "text/plain", "application/jsonx",
         "multipart/form-data", "", "application/x-www-form-urlencoded"]
ROUTES = ["/", "/leaflet.js", "/leaflet.css", "/api/tiles", "/api/segment", "/api/placements", "/api/export",
          "/api/pane?id=tab&new=1", "/api/catalog", "/api/saves", "/api/create", "/api/save", "/api/ghosts",
          "/api/ghost/pin", "/api/ghost/recent", "/api/ghost/%2e%2e", "/api/variant/v", "/api/variant/%zz",
          "/tiles/0/0/0.png", "/tiles/7/1/2.png", "/tiles/8/0/0.png", "/tiles/1/2", "/tiles/+1/ 2/3.png",
          "/tiles/../../x.png", "/nothing", "/api/segment?x=1", "", "/API/SEGMENT", "*"]
METHODS = ["GET", "GET", "POST", "PUT", "DELETE", "get", "Put", "HEAD", "OPTIONS", "PATCH", ""]
PAGE = [("GET", "/"), ("GET", "/leaflet.css"), ("GET", "/api/tiles"), ("GET", "/api/segment"), ("PUT", "/api/segment"),
        ("PUT", "/api/placements"), ("POST", "/api/export"), ("GET", "/api/pane?id=tab&see=1"), ("GET", "/api/catalog"),
        ("GET", "/api/saves"), ("POST", "/api/create"), ("POST", "/api/save"), ("GET", "/api/ghosts"),
        ("POST", "/api/ghost/pin"), ("DELETE", "/api/ghost/recent"), ("DELETE", "/api/variant/v"),
        ("GET", "/tiles/0/0/0.png"), ("GET", "/tiles/9/0/0.png")]

ascii_text = st.text(st.characters(min_codepoint=32, max_codepoint=126), max_size=16)


@st.composite
def gate_headers(draw):
    """Request headers as ReadRequest leaves them (lowercased names, trimmed values), for Allowed."""
    headers = {}
    for name, pool in (("host", HOSTS), ("origin", ORIGINS), ("content-type", TYPES)):
        if draw(st.integers(0, 3)):
            headers[name] = draw(st.one_of(st.sampled_from(pool), st.sampled_from(pool), ascii_text))
    return headers


@st.composite
def page_requests(draw):
    """What the page itself sends: our Host, our Origin on writes, json bodies with their length."""
    method, path = draw(st.one_of(st.sampled_from(PAGE), st.sampled_from(PAGE),
                                  st.tuples(st.sampled_from(METHODS[:5]), st.sampled_from(ROUTES))))
    body = draw(st.one_of(st.just(b""), st.just(b'{"rev":"x","segment":{}}'), st.binary(max_size=20)))
    lines = ["Host: " + draw(st.sampled_from([OURS, "@localhost"]))]
    if method != "GET" and draw(st.booleans()):
        lines.append("Origin: http://" + OURS)
    if body or draw(st.booleans()):
        lines.append("Content-Type: application/json")
    lines.append("Content-Length: %d" % len(body))
    return {"head": [method + " " + path + " HTTP/1.1"] + lines, "eol": "\r\n", "body": body}


@st.composite
def requests(draw):
    """One request's parts, the page's own or bent; render() turns them into bytes for a port."""
    shape = draw(st.integers(0, 9))
    if shape == 0:
        return {"raw": draw(st.binary(max_size=200))}
    if shape < 5:
        return draw(page_requests())
    method = draw(st.one_of(st.sampled_from(METHODS), ascii_text.filter(lambda s: " " not in s)))
    path = draw(st.one_of(st.sampled_from(ROUTES), st.sampled_from(ROUTES), ascii_text.map(lambda s: "/" + s)))
    body = draw(st.one_of(st.just(b""), st.binary(max_size=40), st.just(b'{"rev":"x","segment":{}}')))
    lines = []
    for name, pool in (("Host", HOSTS), ("Origin", ORIGINS), ("Content-Type", TYPES)):
        for _ in range(draw(st.sampled_from([0, 1, 1, 1, 2]))):
            spell = draw(st.sampled_from([name, name.lower(), name.upper(), " " + name]))
            lines.append(spell + ":" + draw(st.sampled_from(["", " ", "  ", "\t"])) + draw(st.sampled_from(pool)))
    length = draw(st.sampled_from(["right", "right", "right", "none", "short", "long", "junk"]))
    if length == "right":
        lines.append("Content-Length: %d" % len(body))
    elif length == "short" and body:
        lines.append("Content-Length: %d" % (len(body) - 1))
    elif length == "long":
        lines.append("Content-Length: %d" % draw(st.sampled_from([len(body) + 1, -1, 4 * 1024 * 1024 + 1, 2 ** 31])))
    elif length == "junk":
        lines.append("Content-Length: " + draw(st.sampled_from(["x", "+%d" % len(body), " %d\x00" % len(body), "1e2", ""])))
    for _ in range(draw(st.integers(0, 2))):
        lines.append(draw(st.sampled_from([":nameless", "no colon", "X-Junk: a:b", " ", "Host"])))
    order = draw(st.permutations(range(len(lines))))
    version = draw(st.sampled_from([" HTTP/1.1", " HTTP/1.1", "", " HTTP/1.1 extra"]))
    eol = draw(st.sampled_from(["\r\n", "\r\n", "\r\n", "\r\n", "\n"]))
    return {"head": [method + " " + path + version] + [lines[i] for i in order], "eol": eol, "body": body}


def fill(s, port):
    return (s.replace(OURS, "127.0.0.1:%d" % port).replace("@localhost", "localhost:%d" % port)
            .replace("@LOCALHOST", "LOCALHOST:%d" % port).replace("@other", "127.0.0.1:%d" % (port ^ 1))
            .replace("@port", str(port)))


def render(req, port):
    if "raw" in req:
        return req["raw"]
    eol = req["eol"]
    return (eol.join(fill(l, port) for l in req["head"]) + eol + eol).encode("ascii") + req["body"]


TILE_PARTS = ["..", ".", "", "%2e%2e", "secret", "C:", "\\..\\secret", "0x1", "1.5", "１"]


@st.composite
def tile_paths(draw, planted):
    """Paths under /tiles/: plain or decorated z/x/y.png, or segments that try to climb out."""
    shape = draw(st.sampled_from(["planted", "plain", "decorated", "decorated", "climb"]))
    if shape == "planted":
        return "/tiles/%d/%d/%d.png" % draw(st.sampled_from(planted))
    if shape == "plain":
        return "/tiles/%d/%d/%d.png" % draw(st.tuples(st.integers(-1, 8), st.integers(-1, 12), st.integers(-1, 12)))
    if shape == "decorated":
        return "/tiles/" + "/".join(draw(st.lists(int_texts(), min_size=3, max_size=3))) + ".png"
    part = st.one_of(int_texts(), st.sampled_from(TILE_PARTS), st.integers(0, 12).map(str))
    parts = draw(st.lists(part, min_size=1, max_size=4))
    end = draw(st.sampled_from([".png", ".png", ".png", ".PNG", "", ".png.png", "png", ".png/"]))
    return "/tiles/" + "/".join(parts) + end


@st.composite
def refusals(draw):
    """A page write the handler refuses: bad json, a stale revision, a bad id, an unknown variant or pickup."""
    return draw(st.one_of(
        st.sampled_from([
            ("WriteSegment", "not json"),
            ("WriteSegment", '{"rev":"stale"}'),
            ("WriteSegment", '{"segment":3}'),
            ("WriteSegment", '{"segment":{"shuffle":[{"give":["XX|nope"]}]}}'),
            ("WriteSegment", '{"segment":{},"variants":{"../x":{}}}'),
            ("WritePlacements", '{"rev":"stale","text":""}'),
            ("WritePlacements", '{"variant":"nope","text":""}'),
            ("DeleteVariant", "nope"),
            ("DeleteVariant", "v"),
        ]),
        st.tuples(st.just("WriteSegment"), st.text(max_size=30)),
        st.tuples(st.just("DeleteVariant"), st.text(max_size=8)),
    ))


PLAIN = set(string.ascii_letters + string.digits + "/.?=&%+*_~ ")
