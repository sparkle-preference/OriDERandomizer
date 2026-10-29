"""Netcode: the client reads what the server writes (tick bodies, frames, goals lines) as the server meant it,
and its found queue keeps every find until the server has answered for it."""
import collections
import json
import logging
import os
import re
import unittest
from urllib.parse import unquote

from hypothesis import given, settings, strategies as st

from client_props.base import HERE, client_file, examples
from client_props.cs_bridge import CsError, slice_bridge
from client_props.strategies import netcode as S

import util
from pickups import Event, Skill, Teleporter, Upgrade

SOURCES = [
    "randomizer/RandomizerSyncManager.cs", "randomizer/RandomizerMW.cs", "randomizer/RandomizerDeathLink.cs",
    "randomizer/RandomizerTrackedDataManager.cs", "bingo/BingoController.cs", "bingo/BingoEnemyKinds.cs",
    "randomizer/NativeWebSocket.cs", "randomizer/RandomizerUpdater.cs",
]
CS = os.path.join(HERE, "cs", "netcode")

B = None
CTX = None
TELEPORT_TABLE = None
VERSION = None


def setUpModule():
    global B, CTX, TELEPORT_TABLE, VERSION
    B = slice_bridge("netcode", SOURCES, extra=[os.path.join(CS, "Stubs.cs"), os.path.join(CS, "Glue.cs")], game=True)
    text = client_file("randomizer/Randomizer.cs").decode("utf-8-sig")
    TELEPORT_TABLE = dict(re.findall(r'TeleportTable\["(\w+)"\] = "(\w+)";', text))
    VERSION = re.search(r'public static string VERSION = "([^"]+)";', text).group(1)
    CTX = S.ndb_context()
    CTX.__enter__()
    B.call("NetGlue.Reset")


def tearDownModule():
    CTX.__exit__(None, None, None)


def fresh(sync_mode=1, ws=True, http=True):
    B.call("NetGlue.Fresh", VERSION, sync_mode, ws, http, TELEPORT_TABLE)


def client_skills():
    """{skill id: bit} for the client's SkillInfoLines."""
    return {int(k): v for k, v in B.call("NetGlue.Lines")["skills"].items()}


def client_events():
    return {int(k): v for k, v in B.call("NetGlue.Lines")["events"].items()}


def client_teleporters():
    return B.call("NetGlue.Lines")["tps"]


# --- tick bodies ---

SF = 15  # Spirit Flame: every world's spawn grants it, so no client line reads its bit


@st.composite
def local_state(draw, skills, mw):
    """What the receiving client already holds."""
    out = {
        "skills": draw(st.sets(st.sampled_from(sorted(skills)), max_size=3)),
        "events": draw(st.sets(st.integers(0, 4), max_size=2)),
        "tps": draw(st.sets(st.sampled_from(sorted(Teleporter.bits)), max_size=3)),
        "counts": draw(st.dictionaries(st.sampled_from(S.SHARED_RB), st.integers(1, 4), max_size=4)),
        "words": [0] * 8, "manifest": {},
    }
    if mw:
        out["words"] = draw(S.slot_words)
        out["manifest"] = draw(st.dictionaries(st.integers(0, 255), st.sampled_from([("HC", "1"), ("EX", "15"), ("KS", "1")]), max_size=40))
    return out


def apply_local(local):
    for sk in local["skills"]:
        B.call("NetGlue.Hold", "SK", sk)
    for ev in local["events"]:
        B.call("NetGlue.Hold", "EV", ev)
    for tp in local["tps"]:
        B.call("NetGlue.HoldTeleporter", tp)
    B.set("RandomizerBonus.Counts", {str(k): v for k, v in local["counts"].items()})
    for i, word in enumerate(local["words"]):
        B.call("NetGlue.SetItem", 940 + i, word - 2**32 if word >= 2**31 else word)
    for slot, (code, id) in local["manifest"].items():
        B.call("NetGlue.AddManifest", slot, code, id)


def tick_grants(state, local, skills, events):
    """The coords-0 grants a tick of this state owes the client: skills, events, teleporters, warps."""
    f = state["fields"]
    owed = ["SK|%s" % sk for sk, bit in Skill.bits.items() if f["skills"] & bit and sk in skills and sk not in local["skills"]]
    owed += ["EV|%s" % ev for ev, bit in Event.bits.items() if f["events"] & bit and ev in events and ev not in local["events"]]
    owed += ["TP|%s" % tp for tp, bit in Teleporter.bits.items() if f["teleporters"] & bit and tp not in local["tps"]]
    for key in f["bonuses"]:
        if key.startswith("Warp to"):
            owed.append("TW|" + ",".join(key.split(",")[:3]))
    return owed


class TickAgreement(unittest.TestCase):
    """A teammate's tick body, rendered by Player.output, read by ProcessTickResponse until it settles."""

    @settings(max_examples=examples(60), deadline=None)
    @given(st.data())
    def test_a_tick_grants_what_the_server_holds_and_no_more(self, data):
        mw = data.draw(st.booleans(), "mw")
        state = data.draw(S.player_states(mw), "state")
        fresh(sync_mode=5 if mw else 1)
        skills = client_skills()
        local = data.draw(local_state(skills, mw), "local")
        apply_local(local)
        body = S.render(state, mw)
        B.call("RandomizerSyncManager.ProcessTickResponse", body)
        first = B.call("NetGlue.State")
        ticks = B.call("NetGlue.Settle", body, 20)
        self.assertGreater(ticks, 0, "the client never settles on one body")
        got = B.call("NetGlue.State")
        self.assertEqual(got["errors"], [e for e in got["errors"] if e.startswith("MW: server reports slot")])
        f = state["fields"]

        # signals: each confirmed by its exact text on the tick that brought it
        self.assertEqual(first["sent"], ["conf:" + s for s in f["signals"]])

        # coords-0 grants: the tick's own, then field 10's items in order
        owed = tick_grants(state, local, skills, client_events())
        extras = ["%s|%s" % (c, v) for _, c, v in f.get("extra_items") or []] if mw else []
        zero = [g[:-2] for g in got["given"] if g.endswith("@0")]
        self.assertEqual(collections.Counter(zero), collections.Counter(owed + extras))

        # upgrades: counted ones converge on the server's count; trees, relics and bonus skills land once
        counts = {int(k): v for k, v in got["counts"].items()}
        inv = {int(k): v for k, v in got["inv"].items()}
        for key, cnt in f["bonuses"].items():
            if key.startswith("Warp to"):
                continue
            id = int(key)
            if id in S.TREES:
                self.assertTrue(inv.get(1001, 0) >> (id - 899) & 1, key)
            elif id in S.RELICS:
                self.assertTrue(inv.get(1002, 0) >> (id - 911) & 1, key)
            elif id in S.BONUS_SKILLS:
                self.assertGreaterEqual(counts.get(id, 0), 1, key)
            else:
                self.assertEqual(counts.get(id, 0), cnt, key)
        for id, cnt in local["counts"].items():
            if str(id) not in f["bonuses"]:
                self.assertEqual(counts.get(id, 0), cnt, "an upgrade the server does not list is left alone")
        self.assertEqual(sorted(got["trees"]), sorted(int(k) - 899 for k in f["bonuses"] if k.isdigit() and int(k) in S.TREES))

        if not mw:
            self.assertFalse([g for g in got["given"] if not g.endswith("@0")])
            return
        # field 6: exactly the server's slots this save lacks, once each, where the seed has a manifest line
        server = [int(w) for w in f["slot_bflds"]]
        fresh_slots = {w * 32 + b for w in range(8) for b in range(32)
                       if (server[w] & ~local["words"][w]) >> b & 1}
        granted = collections.Counter(-int(g.rsplit("@", 1)[1]) - 2 for g in got["given"] if not g.endswith("@0"))
        self.assertEqual(granted, collections.Counter(fresh_slots & set(local["manifest"])))
        for w in range(8):
            want = local["words"][w] | sum(1 << b for b in range(32) if (w * 32 + b) in granted)
            self.assertEqual(inv.get(940 + w, 0) & 0xFFFFFFFF, want)
        # fields 7-10
        self.assertEqual({int(k): v for k, v in got["names"].items()},
                         {int(p.split(".", 1)[0]): p.split(".", 1)[1] for p in S.names_field(state["names"]).split(";")})
        self.assertEqual(got["hints"], {k: v for k, v in f["ap_hints"].items() if v})
        self.assertEqual(got["released"], f["released"])
        self.assertEqual(inv.get(948, 0), len(f["extra_items"]))


# --- the found queue ---

FOUND_WS = re.compile(r"^found:(\d+)\|zone=[^|]*\|(-?\d+)\|")
FOUND_HTTP = re.compile(r"^GET \S+/found/(-?\d+)/")
TICK_HTTP = re.compile(r"^POST \S+/tick/")
# what found_pickup can answer; ws carries only these, http adds proxies' and the sidecar's own failures
WS_STATUSES = [200, 406, 410, 412, 404]
HTTP_STATUSES = WS_STATUSES + [201, 204, 500, 502, 503, 0, -2]
FINDS = [("RB", "17"), ("RB", "6"), ("HC", "1"), ("SK", "0"), ("EX", "15")]


def outcome_of(status):
    """The one status table both lanes follow."""
    if 200 <= status < 300 or status in (406, 410):
        return "drop"
    return "refuse" if status == 412 else "retry"


_find = st.tuples(st.just("find"), st.sampled_from(FINDS))
_ack = st.tuples(st.just("ack"), st.sampled_from(WS_STATUSES), st.integers(0, 4).map(lambda n: n == 0))
_finish = st.tuples(st.just("finish"), st.sampled_from(HTTP_STATUSES), st.integers(0, 4).map(lambda n: n > 0))
_wait = st.tuples(st.just("wait"), st.sampled_from([0.02, 1.0, 5.5, 16.0]))
queue_actions = st.lists(st.one_of(
    _find, _find, _ack, _ack, _ack, _finish, _finish, _finish, _wait, _wait,
    st.tuples(st.just("ws"), st.integers(0, 3).map(lambda n: n > 0)),
    st.tuples(st.just("http"), st.integers(0, 3).map(lambda n: n > 0)),
    st.tuples(st.just("tickreply"), st.sampled_from([200, 200, 412, 503])),
    st.tuples(st.just("frame"), st.sampled_from(["nohttp:", "err:found", "err:tick:412"])),
    st.tuples(st.just("reload"), st.none()),
), min_size=10, max_size=40)


class FoundQueue(unittest.TestCase):
    """Finds against a fake socket and sidecar: each stays queued or in flight until a response for its own send drops it."""

    def run_queue(self, ws, http, actions):
        fresh(ws=ws, http=http)
        self.alive, self.kind, self.requests, self.sent_ever = collections.Counter(), {}, {}, set()
        self.look = B.call("NetGlue.Look")
        coords = 1000
        for action, arg, *rest in actions:
            refused_before = self.look["refused"]
            dt, delivered = 0.0, None
            if action == "find":
                coords += 1
                B.call("NetGlue.Enqueue", arg[0], arg[1], coords)
                self.alive[coords] += 1
                self.kind[coords] = arg
            elif action == "ws":
                B.set("FakeNet.State", 1 if arg else 3)
            elif action == "http":
                B.call("FakeNet.SetHttp", arg)
            elif action == "ack":
                if B.get("FakeNet.State") != 1:
                    continue
                current = self.look["token"]
                stale = rest[0] or not current
                token = (current or 1) + 1000 if stale else current
                B.call("FakeNet.Push", "foundack:%s|%s" % (token, arg))
                delivered = None if stale else (("ws", token), arg)
            elif action == "finish":
                pending = [h for h in B.call("FakeNet.Pending") if ("http", h) in self.requests]
                current = self.look["handle"]
                if rest[0] and current in pending:
                    h = current
                else:
                    others = [h for h in pending if h != current]
                    if not others:
                        continue
                    h = others[0]
                B.call("FakeNet.Finish", h, arg, None)
                # a refusal requeues without dropping the handle, and its answer then finds nothing in flight
                delivered = (("http", h), arg) if h == current and self.look["sending"] else None
            elif action == "tickreply":
                th = self.look["tickHandle"]
                if not th:
                    continue
                B.call("FakeNet.Finish", th, arg, "0,0,0,," if arg == 200 else None)
            elif action == "frame":
                # pushed only while open, so no frame waits in front of a later ack
                if B.get("FakeNet.State") != 1:
                    continue
                B.call("FakeNet.Push", arg)
            elif action == "wait":
                dt = arg
            elif action == "reload":
                B.call("RandomizerSyncManager.Initialize")
                self.look = B.call("NetGlue.Look")
                refused_before = False
            self.step(dt, delivered, refused_before)
        self.drain()

    def step(self, dt, delivered, refused_before):
        calls_before = len(self.look["calls"])
        was_sending = self.look["sending"]
        self.look = look = B.call("NetGlue.Step", dt)
        self.assertFalse([e for e in look["errors"] if e.startswith("RSM.Update")])
        held = collections.Counter(int(p.rsplit("@", 1)[1]) for p in look["queue"])
        if look["sending"]:
            held[int(look["sending"].rsplit("@", 1)[1])] += 1
        self.assertFalse(held - self.alive, "a find is held once, and only while unanswered")
        vanished = self.alive - held
        refused_now = look["refused"] and not refused_before
        if delivered:
            key, status = delivered
            c = self.requests[key]
            verdict = outcome_of(status)
            if verdict == "refuse":
                self.assertTrue(look["refused"], "412 refuses the game on either lane")
            # a refusal in the same frame requeues first, and the answer then finds nothing in flight
            if verdict == "drop" and not refused_now:
                self.assertEqual(vanished, collections.Counter({c: 1}), "2xx, 406 and 410 drop the find on either lane")
            revoke = [-int(self.kind[c][1])] if vanished and status == 410 and self.kind[c][0] == "RB" else []
            self.assertEqual(look["calls"][calls_before:], revoke, "410 takes back exactly an RB")
            if verdict != "drop":
                self.assertFalse(vanished, "only 2xx, 406 and 410 drop a find")
        else:
            self.assertFalse(vanished, "only a response to the current send drops a find")
        self.alive = held
        sends = []
        for frame in look["sent"]:
            m = FOUND_WS.match(frame)
            if m:
                self.requests[("ws", int(m.group(1)))] = int(m.group(2))
                sends.append(int(m.group(2)))
            elif frame.startswith("tick:"):
                self.assertFalse(refused_before, "a refused game sends no ticks")
        for h, text in look["begun"]:
            m = FOUND_HTTP.match(text)
            if m:
                self.requests[("http", h)] = int(m.group(1))
                sends.append(int(m.group(1)))
            elif TICK_HTTP.match(text):
                self.assertFalse(refused_before, "a refused game sends no ticks")
        self.assertLessEqual(len(set(sends)), 1, "one find in flight at a time")
        for c in sends:
            self.assertFalse(refused_before, "a refused game sends no finds")
            self.assertGreater(held[c], 0, "a find is never sent again once it is answered")
            self.assertIn(was_sending, (None, "%s|%s@%s" % (self.kind[c][0], self.kind[c][1], c)), "one find in flight at a time")
            self.sent_ever.add(c)
        # whatever the client would act on belongs to the find in flight
        for key in (("ws", look["token"]), ("http", look["handle"])):
            if key[1] and look["sending"]:
                self.assertEqual(self.requests[key], int(look["sending"].rsplit("@", 1)[1]))

    def drain(self):
        """A healthy server from here on answers 200 to every send: everything left goes out and clears."""
        B.set("FakeNet.State", 1)
        B.call("FakeNet.SetHttp", True)
        for _ in range(400):
            if not self.alive:
                break
            refused_before, delivered = self.look["refused"], None
            if self.look["refused"] or (self.look["unsupported"] and self.look["nohttp"]):
                B.call("RandomizerSyncManager.Initialize")
                self.look = B.call("NetGlue.Look")
                refused_before = False
            elif self.look["token"] and self.look["sending"]:
                B.call("FakeNet.Push", "foundack:%s|200" % self.look["token"])
                delivered = (("ws", self.look["token"]), 200)
            elif self.look["handle"] and self.look["sending"]:
                B.call("FakeNet.Finish", self.look["handle"], 200, None)
                delivered = (("http", self.look["handle"]), 200)
            self.step(0.0 if delivered else 0.5, delivered, refused_before)
        self.assertFalse(self.alive, "a healthy server drains the queue")
        self.assertTrue(set(self.kind) <= self.sent_ever, "every find reached the server at least once")

    @settings(max_examples=examples(150), deadline=None)
    @given(st.booleans(), st.booleans(), queue_actions)
    def test_a_find_is_held_until_a_response_to_its_own_send_drops_it(self, ws, http, actions):
        self.run_queue(ws, http, actions)


message_text = st.text(st.characters(exclude_categories=("Cs",), exclude_characters="/\r\n"), min_size=1, max_size=12)
reported = st.one_of(
    st.tuples(st.sampled_from(["SK", "EV", "RB", "HC", "EX"]), st.integers(0, 60).map(str)),
    st.tuples(st.just("TP"), st.sampled_from(sorted(Teleporter.bits))),
    st.tuples(st.just("TW"), st.sampled_from(S.WARPS).map(S.warp_id)),
    st.tuples(st.just("SH"), message_text),
    st.lists(st.one_of(st.tuples(st.just("SK"), st.integers(0, 51).map(str)), st.tuples(st.just("SH"), message_text)),
             min_size=2, max_size=3).map(lambda ps: ("MU", "/".join("%s/%s" % p for p in ps))),
)


def cleaned(id):
    """Pickup.CleanedId: no '#', nothing from the first backslash on."""
    return id.replace("#", "").split("\\")[0]


def url_syntax(id):
    """An id holding what a url path reads as syntax: '?', '%', or a segment ending in a dot."""
    return "?" in id or "%" in id or any(seg.endswith(".") for seg in id.split("/"))


def http_find(url):
    """(coords, kind, id, zone) as the found route reads the request: <coords>/<kind>/<path:id>, zone from the query."""
    from urllib.parse import parse_qs, urlsplit
    parts = urlsplit(url)
    coords, kind, id = unquote(parts.path.split("/found/", 1)[1]).split("/", 2)
    return int(coords), kind, id, parse_qs(parts.query).get("zone", [None])[0]


class FoundWire(unittest.TestCase):
    """One find as the ws body and the http request carry it, read back the way the server's handlers read each."""

    def sent_find(self, ws, kind, id):
        fresh(ws=ws)
        B.call("NetGlue.Enqueue", kind, id, 4242)
        look = B.call("NetGlue.Step", 0.0)
        if ws:
            from ws import _qs
            token, qs, coords, kind, id = [f for f in look["sent"] if f.startswith("found:")][0][len("found:"):].split("|", 4)
            return int(coords), kind, id, _qs(qs).get("zone")
        return http_find([b[1] for b in look["begun"] if FOUND_HTTP.match(b[1])][0].split(" ", 1)[1])

    @settings(max_examples=examples(80), deadline=None)
    @given(reported)
    def test_both_lanes_report_the_same_find(self, pickup):
        kind, id = pickup
        if url_syntax(cleaned(id)):
            return  # test_an_http_find_keeps_a_question_mark_in_its_message
        want = (4242, kind, cleaned(id), "Glades")
        self.assertEqual(self.sent_find(True, kind, id), want)
        self.assertEqual(self.sent_find(False, kind, id), want)

    @unittest.expectedFailure
    def test_an_http_find_keeps_a_question_mark_in_its_message(self):
        self.assertEqual(self.sent_find(False, "MU", "SH/Why?/SK/0"), (4242, "MU", "SH/Why?/SK/0", "Glades"))


class DataAgreement(unittest.TestCase):
    """Tables the client and server each keep for the same wire bits."""

    def setUp(self):
        fresh()

    def test_every_client_skill_line_reads_the_bit_the_server_sets(self):
        for sk, bit in client_skills().items():
            self.assertEqual(1 << bit, Skill.bits[sk], sk)
        self.assertEqual(set(Skill.bits) - set(client_skills()), {SF})

    def test_every_tracked_event_and_teleporter_reads_the_server_bit(self):
        events = client_events()
        self.assertEqual({ev: 1 << bit for ev, bit in events.items()}, {ev: Event.bits[ev] for ev in events})
        # 5 is each world's own finale; 6 and 7 are never shared, so no teammate is owed them
        self.assertEqual(set(Event.bits) - set(events), {5, 6, 7})
        self.assertEqual(Event.untracked, {6, 7})
        self.assertEqual({tp: 1 << bit for tp, bit in client_teleporters().items()}, Teleporter.bits)

    def test_every_warp_the_generator_places_is_one_the_client_can_grant(self):
        warps = B.get("RandomizerSyncManager.WarpDatas")
        for warp in S.WARPS:
            name, x, y, _area, node, _cost = warp
            self.assertIn("%s_%s" % (x, y), warps, name)
            self.assertEqual(warps["%s_%s" % (x, y)]["name"], "Warp to " + name)
            self.assertEqual(warps["%s_%s" % (x, y)]["area"], node)

    def test_tree_and_relic_ids_name_the_same_tree_and_zone(self):
        trees = B.get("RandomizerTrackedDataManager.Trees")
        zones = B.get("RandomizerTrackedDataManager.Zones")
        for id in S.TREES:
            self.assertEqual(trees[str(id - 899)] + " Tree", Upgrade.names[id])
        for id in S.RELICS:
            self.assertEqual(zones[str(id - 911)] + " Relic", Upgrade.names[id])


class SeenLayout(unittest.TestCase):
    """The seen and held words: the client's coord index is the server's coords_in_order index."""

    def test_every_coord_sits_at_the_servers_index(self):
        fresh()
        cmap = {int(k): v for k, v in B.get("RandomizerTrackedDataManager.CoordsMap").items()}
        self.assertEqual(cmap, {c: i for i, c in enumerate(util.coords_in_order)})

    @settings(max_examples=examples(80), deadline=None)
    @given(st.sets(st.sampled_from(util.coords_in_order)), st.sets(st.sampled_from(util.coords_in_order)))
    def test_the_tick_form_carries_seen_and_held_coords_as_the_server_reads_them(self, seen, have):
        from ws import _qs
        fresh()
        index = {int(k): v for k, v in B.get("RandomizerTrackedDataManager.CoordsMap").items()}
        seen_base = B.get("RandomizerSyncManager.SeenBase")
        # Randomizer.SeenCoord/HaveCoord: word base + idx/32, bit idx%32
        for base, coords in ((seen_base, seen), (930, have)):
            words = [0] * 8
            for c in coords:
                words[index[c] // 32] |= 1 << (index[c] % 32)
            for i, w in enumerate(words):
                B.call("NetGlue.SetItem", base + i, w - 2**32 if w >= 2**31 else w)
        form = _qs(B.call("RandomizerSyncManager.TickPayload"))
        # netcode._tick_slow's read of the form
        got_seen = [int(form.get("seen_%s" % i, 0)) for i in range(8)]
        got_have = [int(form.get("have_%s" % i, 0)) for i in range(8)]
        self.assertEqual(set(util.bfields_to_coords(got_seen)), seen)
        self.assertEqual(set(util.bfields_to_coords(got_have)), have)
        self.assertEqual(form["version"], VERSION)
        self.assertNotIn("aph", form)
        self.assertNotIn("dl", form)


# --- totality ---

FRAME_KINDS = ["tick", "foundack", "bingoack", "goals", "completeack", "nohttp", "err", "ghosts", "ghost", "ice", "zz", ""]
junk = st.text(st.sampled_from("abyz-+._ #!|:;,=%x"), max_size=8)
frame_body = st.one_of(junk, st.lists(st.one_of(junk, st.integers(-2**40, 2**40).map(str)), max_size=12).map(",".join))


def malformed_bitfield(body):
    """Field 0, 1 or 2 is not an int32: the server always writes str(int) there."""
    parts = body.split(",")
    for p in parts[:3]:
        try:
            if not -2**31 <= int(p) < 2**31 or p != p.strip():
                return True
        except ValueError:
            return True
    return False


def upgrade_junk(token):
    """A field 3 token the client can't read as id x count or a warp."""
    head, _, rest = token.partition("x")
    return "_" not in head and not (re.fullmatch(r"[+-]?\d+", head) and re.fullmatch(r"[+-]?\d+", rest.split("x")[0]))


class Totality(unittest.TestCase):

    @settings(max_examples=examples(150), deadline=None)
    @given(st.sampled_from(FRAME_KINDS), st.sampled_from([":", ""]), frame_body)
    def test_no_frame_escapes_process_frame(self, kind, sep, body):
        fresh(sync_mode=5)
        B.call("RandomizerSyncManager.ProcessFrame", kind + sep + body)

    @settings(max_examples=examples(60), deadline=None)
    @given(S.player_states(False), st.lists(st.text(st.sampled_from("abyz-+._ #!|:=%x0"), max_size=8).filter(upgrade_junk),
                                            min_size=1, max_size=3))
    def test_an_unreadable_upgrade_token_costs_only_itself(self, state, junks):
        body = S.render(state, False)
        fresh()
        B.call("RandomizerSyncManager.ProcessTickResponse", body)
        clean = B.call("NetGlue.State")
        parts = body.split(",")
        parts[3] = ";".join([t for t in parts[3].split(";") if t] + junks)
        fresh()
        B.call("RandomizerSyncManager.ProcessTickResponse", ",".join(parts))
        dirty = B.call("NetGlue.State")
        for key in ("given", "calls", "sent", "trees", "relics"):
            self.assertEqual(dirty[key], clean[key], key)

    @settings(max_examples=examples(60), deadline=None)
    @given(S.player_states(False), st.integers(0, 2), st.one_of(st.integers(-2**31, 2**31 - 1).map(str), junk))
    def test_the_fields_after_a_bitfield_apply_whatever_it_holds(self, state, at, value):
        signals = state["fields"]["signals"] = state["fields"]["signals"] or ["stop"]
        parts = S.render(state, False).split(",")
        parts[at] = value
        body = ",".join(parts)
        if malformed_bitfield(body):
            return  # test_a_malformed_bitfield_still_lets_the_signals_through
        fresh()
        B.call("RandomizerSyncManager.ProcessTickResponse", body)
        self.assertEqual(B.get("FakeNet.Sent"), ["conf:" + s for s in signals])

    @unittest.expectedFailure
    def test_a_malformed_bitfield_still_lets_the_signals_through(self):
        fresh()
        try:
            B.call("RandomizerSyncManager.ProcessTickResponse", "x,0,0,,,msg:@hi@")
        except CsError:
            pass
        self.assertEqual(B.get("FakeNet.Sent"), ["conf:msg:@hi@"])


# --- multiworld fields ---

class MultiworldFields(unittest.TestCase):

    @settings(max_examples=examples(100), deadline=None)
    @given(st.dictionaries(st.integers(1, 300), st.text(max_size=30), min_size=1, max_size=8))
    def test_the_names_field_reads_back_as_each_players_wire_name(self, names):
        fresh(sync_mode=5)
        field = S.names_field(names)
        B.call("RandomizerMW.OnNamesField", field)
        want = {str(pid): S.Player(id="7.%s" % pid, nickname=raw or None).wire_name() for pid, raw in names.items()}
        self.assertEqual(B.get("RandomizerMW.PlayerNames"), want)

    @settings(max_examples=examples(200), deadline=None)
    @given(st.text(st.one_of(st.sampled_from("%,;|2C3B5c"), st.characters(exclude_categories=("Cs",))), max_size=24))
    def test_unescape_inverts_the_servers_wire_escape(self, text):
        from models import _wire_escape
        self.assertEqual(B.call("RandomizerMW.UnescapeTickItem", _wire_escape(text)), text)

    @settings(max_examples=examples(100), deadline=None)
    @given(st.text(st.characters(exclude_categories=("Cs",), exclude_characters="|"), min_size=1, max_size=4),
           st.text(st.characters(exclude_categories=("Cs",)), min_size=1, max_size=20))
    def test_an_extra_item_parses_back_to_its_code_and_id(self, code, id):
        from models import _wire_escape
        entry = B.call("RandomizerMW.ParseExtraItem", _wire_escape("%s|%s" % (code, id)))
        self.assertEqual((entry["Code"], entry["Id"], entry["Sender"]), (code, id, "Archipelago"))

    @settings(max_examples=examples(40), deadline=None)
    @given(st.lists(st.lists(S.extra_item, max_size=3), min_size=1, max_size=5))
    def test_appended_extra_items_are_each_granted_once_in_order(self, batches):
        fresh(sync_mode=5)
        items, seen = [], []
        for batch in batches:
            items += batch
            fields = {"skills": 0, "events": 0, "teleporters": 0, "bonuses": {}, "hints": {}, "signals": [],
                      "extra_items": [[i, c, v] for i, (c, v) in enumerate(items)]}
            B.call("RandomizerSyncManager.ProcessTickResponse", S.render({"pid": 2, "names": {2: "x"}, "fields": fields}, True))
            applied = B.call("NetGlue.State")["inv"].get("948", 0)
            self.assertGreaterEqual(applied, len(seen), "the applied counter never goes back")
            seen = items[:applied]
        given = [g[:-2] for g in B.call("NetGlue.State")["given"]]
        self.assertEqual(given, ["%s|%s" % kv for kv in items])


def ends_in_a_dot(text):
    """A signal ending in '.': the bridge's .NET Uri trims that dot and the game's Mono keeps it (ingame_props checks Mono)."""
    return text.endswith(".")


def http_confirms(signals):
    fresh(ws=False)
    B.call("RandomizerSyncManager.ProcessTickResponse", "0,0,0,,," + "|".join(signals))
    urls = [b.split(" ", 1)[1] for b in B.get("FakeNet.Begun")]
    prefix = B.get("RandomizerSyncManager.RootUrl") + "/callback/"
    # the route is <path:signal>, decoded as the server's wsgi layer does
    return [unquote(u[len(prefix):]) if u.startswith(prefix) else u for u in urls]


class SignalConfirms(unittest.TestCase):

    @settings(max_examples=examples(60), deadline=None)
    @given(S.signals().filter(bool))
    def test_an_http_confirm_names_the_signal_exactly(self, signals):
        signals = [s for s in signals if not ends_in_a_dot(s)]
        self.assertEqual(http_confirms(signals), signals)


# --- bingo ---

class BingoWire(unittest.TestCase):
    """The goals line the server deals and the progress json the client posts back."""

    @classmethod
    def setUpClass(cls):
        cls._floor = logging.root.manager.disable
        logging.disable(logging.CRITICAL)

    @classmethod
    def tearDownClass(cls):
        logging.disable(cls._floor)

    def boot(self):
        fresh()
        B.call("BingoController.Init", None)

    @staticmethod
    def dealt(seed, opts):
        from test.strategies.bingo import roll
        cards = roll(seed, opts)
        for i, card in enumerate(cards):
            card.square = i
        return cards

    @settings(max_examples=examples(40), deadline=None)
    @given(st.data())
    def test_the_goals_line_arrives_as_the_boards_goals(self, data):
        from models import BingoGameData
        from test.strategies.bingo import board_options, board_seeds
        cards = self.dealt(data.draw(board_seeds), data.draw(board_options()))
        self.boot()
        B.call("RandomizerSyncManager.ProcessFrame", "goals:" + BingoGameData(board=cards).goals_line(1).rstrip("\n"))
        singles, targets, multis = set(), {}, collections.defaultdict(set)
        for card in cards:
            if card.subgoals:
                multis[card.name] |= {sg["name"] for sg in card.subgoals}
            elif card.goal_method == "count":
                multis[card.name].add("COUNT")
            else:
                singles.add(card.name)
                if card.target:
                    targets[card.name] = card.target
        got = B.call("NetGlue.ActiveGoals")
        self.assertEqual(set(got["singles"]) - {""}, singles)
        self.assertEqual({k: set(v) for k, v in got["multis"].items()}, dict(multis))
        for name, target in targets.items():
            self.assertEqual(got["targets"].get(name), target, name)

    @settings(max_examples=examples(40), deadline=None)
    @given(st.data())
    def test_every_dealt_card_reads_its_progress_from_the_clients_json(self, data):
        from models import BingoCardProgress
        from test.strategies.bingo import board_options, board_seeds
        cards = self.dealt(data.draw(board_seeds), data.draw(board_options()))
        self.boot()
        items = B.call("NetGlue.BingoItems")
        ids = set(items["bool"].values()) | set(items["int"].values()) | {i for m in items["multi"].values() for i, _ in m.values()}
        for id in sorted(ids):
            B.call("NetGlue.SetItem", id, data.draw(st.one_of(st.integers(0, 3), st.integers(-2**31, 2**31 - 1)), "item %s" % id))
        report = json.loads(B.call("BingoController.GetJson"))
        player = S.Player(id="7.2", bingo_prog=[BingoCardProgress(square=i) for i in range(25)])
        for card in cards:
            if card.meta:
                continue
            self.assertIn(card.name, report)
            entry = report[card.name]
            if card.goal_type == "multi":
                values = entry["value"]
                self.assertEqual(entry["total"], sum(1 for v in values.values() if v["value"]))
                for sg in card.subgoals or []:
                    self.assertIn(sg["name"], items["multi"][card.name], "%s has no client subgoal %s" % (card.name, sg["name"]))
            card.update(entry, player, [], player.key)


# --- small codecs ---

def utf16(s):
    return s.encode("utf-16-le")


def pair_straddles_a_chunk_edge(s):
    """A surrogate pair split by EscapeLong's 16000-unit chunking."""
    units = utf16(s)
    return any(0xD800 <= int.from_bytes(units[2 * i - 2:2 * i], "little") < 0xDC00 for i in range(16000, len(units) // 2, 16000))


class Codecs(unittest.TestCase):

    @settings(max_examples=examples(60), deadline=None)
    @given(st.sampled_from([0, 15990, 31990]), st.integers(0, 20), st.text(st.characters(exclude_categories=("Cs",)), max_size=12))
    def test_escape_long_round_trips_through_the_servers_form_parse(self, base, pad, tail):
        from ws import _qs
        s = "a" * (base + pad) + tail
        if pair_straddles_a_chunk_edge(s):
            return  # test_escape_long_keeps_a_pair_split_by_its_chunking
        esc = B.call("RandomizerSyncManager.EscapeLong", s)
        self.assertEqual(_qs("seed=" + esc)["seed"], s)
        if len(utf16(s)) <= 32000:
            self.assertEqual(esc, B.call("System.Uri.EscapeDataString", s))

    @unittest.expectedFailure
    def test_escape_long_keeps_a_pair_split_by_its_chunking(self):
        from ws import _qs
        s = "a" * 15999 + "\U0001F600"
        try:
            esc = B.call("RandomizerSyncManager.EscapeLong", s)
        except CsError:
            esc = None
        self.assertEqual(_qs("seed=%s" % esc)["seed"], s)

    @settings(max_examples=examples(60), deadline=None)
    @given(st.integers(1, 10**9), st.integers(1, 300), st.text(st.characters(exclude_categories=("Cs",)), max_size=40),
           st.lists(st.text(st.characters(exclude_categories=("Cs",), exclude_characters="\r\n"), max_size=20), max_size=2))
    def test_the_seed_upload_names_its_game(self, gid, pid, rest, more):
        from ws import _qs
        fresh()
        B.set("RandomizerSyncManager.SeedHead", ["Sync%s.%s,%s" % (gid, pid, rest)] + more)
        B.call("RandomizerSyncManager.UploadSeed")
        frames = [f for f in B.get("FakeNet.Sent") if f.startswith("seed:")]
        self.assertEqual(len(frames), 1)
        form = _qs(frames[0][len("seed:"):])
        self.assertEqual(util.seed_sync_id(form["seed"]), "%s.%s" % (gid, pid))
        self.assertEqual(form["version"], VERSION)

    @settings(max_examples=examples(150), deadline=None)
    @given(st.lists(st.integers(0, 10**6), min_size=3, max_size=3), st.lists(st.integers(0, 10**6), min_size=3, max_size=3))
    def test_the_updater_takes_only_a_newer_release(self, a, b):
        newer = B.call("RandomizerUpdater.IsNewer", ".".join(map(str, a)), ".".join(map(str, b)))
        self.assertEqual(newer, util.version_at_least(".".join(map(str, a)), b) and a != b)


class DeathLink(unittest.TestCase):

    @settings(max_examples=examples(60), deadline=None)
    @given(st.integers(0, 40), st.lists(st.one_of(st.none(), st.integers(0, 50)), max_size=12))
    def test_a_replayed_death_link_token_kills_once(self, saved, ticks):
        fresh()
        B.set("RandomizerDeathLink.Enabled", True)
        B.call("NetGlue.SetItem", 4092, saved)
        high = saved
        for token in ticks:
            body = "0,0,0,," + ("" if token is None else ",dl:%s;Someone" % token)
            B.call("RandomizerSyncManager.ProcessTickResponse", body)
            new = token is not None and token > high
            self.assertEqual(B.get("RandomizerDeathLink.killPending"), new, token)
            high = max(high, token or 0)
            B.set("RandomizerDeathLink.killPending", False)
        self.assertEqual(B.call("NetGlue.State")["inv"].get("4092", 0), high)

    @settings(max_examples=examples(60), deadline=None)
    @given(st.integers(0, 2**31 - 1), st.integers(0, 2**31 - 1))
    def test_the_death_counters_reach_the_server_as_written(self, deaths, linked):
        from archipelago.ap_bridge import _parse_deaths
        fresh()
        B.set("RandomizerDeathLink.Enabled", True)
        B.call("NetGlue.SetItem", 4090, deaths)
        B.call("NetGlue.SetItem", 4091, linked)
        self.assertEqual(_parse_deaths(B.call("RandomizerDeathLink.Field")), (deaths, linked))
