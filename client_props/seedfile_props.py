"""Seed files as the client reads them: the server's flag, meta and placement lines, the editors' boxes, enemy lines."""
import logging
import math
import os
import re
import socket
import struct
import tempfile
import unittest
from collections import defaultdict

from hypothesis import event, example, given, note, settings, strategies as st

from client_props.base import HERE, deep_only, examples
from client_props.cs_bridge import outcome, slice_bridge
from client_props.strategies import seedfile as SF
from enums import MultiplayerGameType, Variation
from seedbuilder import enemyopts, enemyroll
from test import js_bridge
from test.strategies import seedgen as S
from util import SEED_FORMAT, decompose_multi_value

# a private emulator for this suite; never the shared :8000/:8001 ones
EMULATOR = os.environ.get("CLIENT_PROPS_EMULATOR", "127.0.0.1:8032")
B = None
TMP = None
STRING_CODES = set()


def setUpModule():
    global B, TMP
    B = slice_bridge("seedfile", [], extra=[os.path.join(HERE, "cs", "seedfile", "SeedfileGlue.cs")], game=True)
    TMP = tempfile.mkdtemp(prefix="seedfile_props_")
    STRING_CODES.update(B.get("RandomizerAction.StringValPickupTypes"))


def js(fn, *args):
    return js_bridge.call("boxes.js", fn, *args)


def f32(x):
    return struct.unpack("<f", struct.pack("<f", x))[0]


def cs_int(text):
    """int.Parse's view of text: an int32 in invariant digits, whitespace and one sign allowed."""
    m = re.fullmatch(r"[ \t\n\v\f\r]*([+-]?[0-9]+)[ \t\n\v\f\r]*", text)
    return m is not None and -2**31 <= int(m.group(1)) < 2**31


def file_lines(text):
    """File.ReadAllLines: \\n, \\r\\n and a lone \\r all end a line."""
    lines = re.split(r"\r\n|\r|\n", text)
    return lines[:-1] if lines and lines[-1] == "" else lines


def load(text):
    """initialize()'s read of text as the seed file: ({threw, bad, logged, first, bingo}, path)."""
    path = os.path.join(TMP, "randomizer.bfr")
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    B.call("SeedfileGlue.Reset")
    return B.call("SeedfileGlue.Load", path), path


def parse_flags(header):
    """ParseFlags on a header line, as initialize() calls it; returns what it said about bingo."""
    B.call("SeedfileGlue.Reset")
    B.set("Randomizer.SeedMeta", header)
    parts = header.split("|")
    return B.call("Randomizer.ParseFlags", parts[1], parts[0].split(","))


_client = []


def datastore():
    if not _client:
        sock = socket.socket()
        sock.settimeout(2)
        host, port = EMULATOR.rsplit(":", 1)
        try:
            sock.connect((host, int(port)))
        except OSError:
            raise unittest.SkipTest("no datastore emulator at %s" % EMULATOR)
        finally:
            sock.close()
        import google.auth.credentials
        from google.cloud import ndb
        old = os.environ.get("DATASTORE_EMULATOR_HOST")
        os.environ["DATASTORE_EMULATOR_HOST"] = EMULATOR
        try:
            _client.append(ndb.Client(project="unit-test", credentials=google.auth.credentials.AnonymousCredentials()))
        finally:
            if old is None:
                del os.environ["DATASTORE_EMULATOR_HOST"]
            else:
                os.environ["DATASTORE_EMULATOR_HOST"] = old
    return _client[0].context()


def facts(params, p=1):
    """What a world's header should say, from the params rather than the line."""
    view = params.world_params(p)
    sync = params.sync
    return {"players": int(params.players or 1),
            "mode": sync.mode if sync.enabled else None, "shared": [s.value for s in sync.shared] if sync.enabled else [],
            "variations": [v.value for v in view.variations], "key_mode": view.key_mode.value,
            "relic_count": params.relic_count, "frag_req": params.frag_req, "frag_count": params.frag_count,
            "sense": params.sense, "ap_mode": bool(params.ap_mode), "death_link": bool(params.ap_death_link)}


def served(j, generate=True, boxes=None):
    """(facts, {world: seed text}) as the website serves request j; texts None when it doesn't generate."""
    from seedbuilder.seedparams import BoxLine, SeedGenParams
    old = logging.root.manager.disable
    logging.disable(logging.CRITICAL)
    try:
        with datastore():
            params = SeedGenParams.from_json(j, store=False)
            if params is None or (generate and not params.generate(store=False)):
                return None, None
            if boxes:
                params.boxes = [BoxLine(player=str(p), line=line) for p, line in boxes]
            texts = {p: params.get_seed(p, include_sync=False) for p in range(1, int(params.players or 1) + 1)}
            return {p: facts(params, p) for p in texts}, texts
    finally:
        logging.disable(old)


_pool = []


def web(seed, **kw):
    """A seedgen page request with the page's own item pool."""
    if not _pool:
        _pool.append(S.page_pool(None))
    name, pool = _pool[0]
    j = {"seed": seed, "paths": ["casual-core", "casual-dboost", "standard-core", "standard-dboost", "standard-lure",
                                 "standard-abilities"],
         "variations": [], "keyMode": "Clues", "players": 1, "tracking": True, "itemPool": pool, "selectedPool": name}
    j.update(kw)
    return j


BOX_MODELS = [{"flags": "kill", "box": [10, -20.5, 14, -18], "color": "", "give": ""},
              {"flags": "item", "box": [100, 5, 104.25, 9], "color": "40c0ff", "give": "SK|0"},
              {"flags": "tombstone", "box": [0, 0, 0, 0], "color": "", "give": ""},
              {"flags": "ritem,on=Tick/30/60", "box": [-3, -3, 3, 3], "color": "none", "give": "MU|EX/5/BM/0"}]
CORPUS_REQUESTS = {
    "solo": dict(variations=["ForceTrees", "EnemyRando", "WorldTour"], relicCount=5, enemyOpts={"hp": "0.5-2", "dmg": "vanilla"}),
    "keysanity": dict(keyMode="Shards", variations=["Keysanity", "OpenWorld"]),
    "entrance": dict(keyMode="Free", variations=["Entrance", "WarmthFrags"], fragCount=20, fragReq=12),
    "mw": dict(players=2, coopGameMode="Multiworld", syncShared=["Skills"], variations=["EnemyRando"]),
    "mw3": dict(players=3, coopGameMode="Multiworld", syncShared=["World Events", "Teleporters"], variations=["Keysanity"]),
    "coop": dict(players=2, coopGameMode="Co-op", syncShared=["Skills", "World Events"], variations=["EnemyRando"]),
    "ap": dict(apMode=True, variations=["EnemyRando"]),
}
_corpus = {}


def corpus():
    """{(name, world): (facts, text)} for a handful of website rolls at fixed seed strings, boxes on two."""
    if not _corpus:
        lines = [js("box_line", m) for m in BOX_MODELS]
        for name, kw in sorted(CORPUS_REQUESTS.items()):
            boxes = [(p, line) for p in (1, 2) for line in lines] if name in ("solo", "mw") else None
            for k in range(4):
                info, texts = served(web("props-%s%s" % (name, "~%d" % k if k else ""), **kw), boxes=boxes)
                if texts:
                    break
            else:
                raise AssertionError("corpus seed %s never generated" % name)
            for p, text in texts.items():
                _corpus[(name, p)] = (info[p], text)
    return _corpus


def header_of(j, p=1):
    """(facts, served text) for request j without rolling it: the header and meta lines only."""
    info, texts = served(j, generate=False)
    return (None, None) if texts is None else (info[p], texts[p])


DEFAULT_SENSE = sorted(["EV", "RB17", "RB19", "RB21", "RB28", "SK", "TPForlorn", "TPHoru", "TPGinso", "TPValley", "TPSorrow"])
# no frag counts: initialize() keeps the last seed's, and fragsEnabled guards them
FLAG_DEFAULTS = {
    "ZeroXP": False, "IgnoreEnemyExp": False, "Entrance": False, "ForceMaps": False,
    "ForceTrees": False, "OHKO": False, "OpenMode": True, "OpenWorld": False, "WorldTour": False,
    "RelicCountOverride": False, "RelicCount": 0, "fragsEnabled": False, "StompTriggers": False, "GoalModeFinish": False, "InLogicWarps": False, "TeleportersLockedByClues": False,
    "Keysanity": False, "EnhancedMode": False, "VanillaRespawns": False, "EnemyRando": False, "CluesMode": False,
    "Shards": False, "DeathLink": False, "ShareSaves": False, "BonusActive": True, "AltRDisabled": False,
    "AllowOrbWarps": False, "HotCold": False, "HotColdTypes": DEFAULT_SENSE,
}
# what each variation the server writes sets in the client
CLIENT_FLAGS = {
    "0XP": {"ZeroXP": True, "IgnoreEnemyExp": True},
    "Entrance": {"Entrance": True}, "ForceMaps": {"ForceMaps": True}, "ForceTrees": {"ForceTrees": True},
    "OHKO": {"OHKO": True}, "ClosedDungeons": {"OpenMode": False}, "OpenWorld": {"OpenWorld": True},
    "WorldTour": lambda f: {"WorldTour": True, "RelicCountOverride": True, "RelicCount": f["relic_count"]},
    "WarmthFrags": lambda f: {"fragsEnabled": True, "fragKeyFinish": f["frag_req"], "maxFrags": f["frag_count"]},
    "StompTriggers": {"StompTriggers": True}, "NoExtraExp": {"IgnoreEnemyExp": True},
    "GoalModeFinish": {"GoalModeFinish": True}, "InLogicWarps": {"InLogicWarps": True},
    "ClueLockedTPs": {"TeleportersLockedByClues": True}, "Keysanity": {"Keysanity": True},
    "Enhanced": {"EnhancedMode": True}, "VanillaRespawns": {"VanillaRespawns": True}, "EnemyRando": {"EnemyRando": True},
    "Bingo": {}, "Race": {},
}
# variations only the generator reads; the client must ignore their flags
GENERATOR_ONLY = {"Hard", "Starved", "BonusPickups", "DoubleSkills", "StrictMapstones", "TPStarved", "WallStarved",
                  "GrenadeStarved", "WarpsInsteadOfTPs", "WarpCount", "StartingHealth", "StartingEnergy",
                  "StartingSkills", "NoTPs", "Competitive", "BonusLite", "ZoneLockedTPs"}
# retired variations: no new seed carries one, and nothing reads an old seed's
RETIRED = {"NonProgressMapStones"}


def expected_flags(f):
    want = dict(FLAG_DEFAULTS)
    for v in f["variations"]:
        sets = CLIENT_FLAGS.get(v, {})
        want.update(sets(f) if callable(sets) else sets)
    want["CluesMode"] = f["key_mode"] == "Clues"
    want["Shards"] = f["key_mode"] == "Shards"
    synced = f["mode"] not in (None, MultiplayerGameType.SIMUSOLO)
    want["ShareSaves"] = synced and "Saves" in f["shared"]
    want["DeathLink"] = f["ap_mode"] and f["death_link"]
    if f["sense"]:
        want["HotColdTypes"] = sorted(set(f["sense"].replace(" ", "+").split("+")))
    return want


class FlagLine(unittest.TestCase):

    def test_every_variation_is_read_by_the_client_or_generator_only(self):
        names = {v.value for v in Variation} - RETIRED
        self.assertEqual(set(CLIENT_FLAGS) & GENERATOR_ONLY, set())
        self.assertEqual(names, set(CLIENT_FLAGS) | GENERATOR_ONLY)

    @settings(max_examples=examples(120))
    @given(SF.flag_json())
    def test_a_served_flag_line_sets_the_flags_it_names(self, j):
        note("SeedGenParams.from_json(%r)" % j)
        f, text = header_of(j)
        if text is None:
            event("from_json refused the request")
            return
        header = text.split("\n")[0]
        note(header)
        bingo = parse_flags(header)
        got = B.call("SeedfileGlue.Flags")
        want = expected_flags(f)
        self.assertEqual({k: got[k] for k in want}, want)
        self.assertEqual(got["SyncMode"] == 5, f["mode"] == MultiplayerGameType.MULTIWORLD)
        self.assertEqual(bingo, "Bingo" in f["variations"])
        self.assertEqual(got["SeedMeta"] != header, "Race" in f["variations"])


def meta_truth(f, text):
    """The statics a served seed's meta lines name, by the server's own reading of them."""
    lines = text.split("\n")
    rolls = [enemyroll.rolls_of(l) for l in lines if l.startswith(enemyroll.META_PREFIX)]
    tiers = [[int(t) for t in l[len("//KeyTiers="):].split("+")] for l in lines if l.startswith("//KeyTiers=")]
    opts = ([enemyopts.parse_meta(l) for l in lines if l.startswith(enemyopts.META_PREFIX)] or [{}])[-1]
    return {"SeedFormat": SEED_FORMAT, "PlayerCount": f["players"],
            "HpMode": opts.get("hp", "zone").lower(), "DmgMode": opts.get("dmg", "zone").lower(),
            "Rolls": table_of(rolls[-1]) if rolls else None, "KeyTiers": tiers[-1] if tiers else None}


def table_of(rolls):
    """The client's s_table for server rolls: -1 vanilla, -(N + 2) bonus N."""
    return [-1 if r is None else -2 - int(r[1:]) if isinstance(r, str) else r for r in rolls]


def meta_now():
    return {"SeedFormat": B.get("Randomizer.SeedFormat"), "PlayerCount": B.get("Randomizer.PlayerCount"),
            "HpMode": B.get("RandomizerEnemies.HpMode"), "DmgMode": B.get("RandomizerEnemies.DmgMode"),
            "Rolls": B.call("SeedfileGlue.Rolls"), "KeyTiers": B.get("RandomizerLocationManager.KeyTiers")}


class MetaLines(unittest.TestCase):

    @settings(max_examples=examples(60))
    @given(SF.flag_json())
    def test_each_served_meta_line_lands_in_its_static(self, j):
        note("SeedGenParams.from_json(%r)" % j)
        f, text = header_of(j)
        if text is None:
            return
        B.call("SeedfileGlue.Fresh")
        for line in text.split("\n")[1:]:
            if line:
                self.assertIsNone(B.call("SeedfileGlue.Line", line), line)
        self.assertEqual(meta_now(), meta_truth(f, text))

    @settings(max_examples=examples(60))
    @given(SF.flag_json())
    def test_served_meta_lines_survive_a_whole_load(self, j):
        note("SeedGenParams.from_json(%r)" % j)
        f, text = header_of(j)
        if text is None:
            return
        report, _ = load(text)
        self.assertIsNone(report["threw"])
        want, got = meta_truth(f, text), meta_now()
        self.assertEqual(got, want)

def value_of(code, value):
    return value if code in STRING_CODES else str(int(value))


def reads(text):
    """What each line of a served seed tells the client, by what the server meant by it."""
    lines = file_lines(text)
    flags = {x.lower() for x in lines[0].split("|")[0].split(",")}
    want = {"pickups": {}, "manifest": {}, "spawn": "", "relics": {}, "zones": ["MIA", "MIA", -1, -1], "clues": {},
            "keys": defaultdict(list), "doors": set(), "warps": {}, "boxes": []}

    def touch(coords, code, value, area, slot=-1):
        if code == "WT":
            want["relics"][value] = area
        elif code == "SK" and value in ("4", "51"):
            i = 1 if value == "4" else 0
            want["zones"][i], want["zones"][i + 2] = area, slot
        elif code == "EV" and value in ("0", "2", "4") and "clues" in flags:
            want["clues"][int(value) // 2] = area
        elif code == "RB" and "keysanity" in flags and cs_int(value) and 300 <= int(value) <= 311:
            want["keys"][value].append("%d@%s" % (coords, area))
        elif code == "TW":
            warp = value.split(",")
            if len(warp) > 3:
                want["warps"].setdefault(warp[0], warp[3])

    for line in lines[1:]:
        if not line.strip() or line.startswith("//"):
            continue
        if line.startswith("BX|"):
            want["boxes"].append(line)
            continue
        parts = line.split("|")
        coords, code, value, zone = int(parts[0]), parts[1], parts[2], parts[3]
        if code == "MW" and -257 <= coords <= -2:
            finder, holder, icode, iid = value.split(",", 3)
            who = holder or "P" + finder
            want["manifest"][str(-coords - 2)] = [int(finder), icode, iid, zone]
            touch(coords, icode, iid, who + " " + zone if zone else who, -coords - 2)
            continue
        for c, v in (decompose_multi_value(value) if code in ("MU", "RP") else [(code, value)]):
            touch(coords, c, v, zone)
        if coords == 2:
            want["spawn"] = code + value
        elif code == "EN":
            want["doors"].add(coords)
        else:
            want["pickups"][str(coords)] = code + "|" + value_of(code, value) + ("|repeatable" if code == "RP" else "")
    return want


REFUSED_HEADERS = ["Standard,WorldTour=many|refused", "Standard,Frags/a/b|refused", "Standard,Clues"]


def a_header_the_client_refuses(text):
    return text[:text.index("\n")] in REFUSED_HEADERS


def read_back_problems(text):
    """How the loaded client state differs from what the seed's lines meant."""
    want = reads(text)
    side = B.call("SeedfileGlue.Side")
    keys = {str(k) for k in B.call("SeedfileGlue.Keys")}
    out = []
    pickups = B.call("SeedfileGlue.Pickups")
    out += ["%s holds %r, the line says %r" % (k, pickups.get(k), v) for k, v in sorted(want["pickups"].items())
            if pickups.get(k) != v]
    out += ["%s isn't a client location" % k for k in sorted(want["pickups"]) if k not in keys]
    out += ["%s holds %r, no line put it there" % (k, v) for k, v in sorted(pickups.items()) if k not in want["pickups"]]
    if side["manifest"] != want["manifest"]:
        out.append("manifest: %r" % {k: (side["manifest"].get(k), want["manifest"].get(k))
                                     for k in set(side["manifest"]) | set(want["manifest"])
                                     if side["manifest"].get(k) != want["manifest"].get(k)})
    if B.get("Randomizer.SpawnWith") != want["spawn"]:
        out.append("spawn %r, the line says %r" % (B.get("Randomizer.SpawnWith"), want["spawn"]))
    relics = dict(p.split("=", 1) for p in side["relics"])
    if relics != want["relics"]:
        out.append("relic zones %r, the lines say %r" % (relics, want["relics"]))
    if side["zones"] != want["zones"]:
        out.append("grenade/stomp %r, the lines say %r" % (side["zones"], want["zones"]))
    if B.get("Randomizer.CluesMode"):
        clues = [side["clues"][side["revealOrder"][i] - 1] for i in range(3)]
        expect = [want["clues"].get(i, "Unknown") for i in range(3)]
        if clues != expect:
            out.append("clues %r, the lines say %r" % (clues, expect))
    elif want["clues"]:
        out.append("clue lines in a seed without clues")
    keys_now = {k: v for k, v in side["keysanity"].items() if v}
    if keys_now != dict(want["keys"]):
        out.append("keysanity clues %r, the lines say %r" % (keys_now, dict(want["keys"])))
    doors = {int(p.split("=", 1)[0]) for p in side["doors"]}
    if doors != want["doors"]:
        out.append("doors %r, the lines say %r" % (sorted(doors), sorted(want["doors"])))
    warps = dict(p.split("=", 1) for p in side["warps"])
    if warps != want["warps"]:
        out.append("warps %r, the lines say %r" % (warps, want["warps"]))
    boxes = B.call("SeedfileGlue.LoadedBoxLines")
    if boxes != want["boxes"]:
        out.append("boxes %r, the lines say %r" % (boxes, want["boxes"]))
    return out


class ServedSeeds(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.corpus = corpus()

    @settings(max_examples=examples(20))
    @given(st.data())
    def test_every_line_of_a_served_seed_reads_back_as_written(self, data):
        key = data.draw(st.sampled_from(sorted(self.corpus)))
        f, text = self.corpus[key]
        text = text.replace("\n", data.draw(st.sampled_from(["\n", "\r\n"])))
        report, _ = load(text)
        self.assertIsNone(report["threw"])
        self.assertEqual(report["bad"], [], report["first"])
        self.assertEqual(report["logged"], [], "lines that reached LogError")
        self.assertEqual(read_back_problems(text), [])

    @settings(max_examples=examples(12))
    @given(st.data())
    def test_a_served_seeds_meta_lines_survive_its_whole_load(self, data):
        f, text = self.corpus[data.draw(st.sampled_from(sorted(self.corpus)))]
        load(text)
        want, got = meta_truth(f, text), meta_now()
        self.assertEqual(got, want)

    @settings(max_examples=examples(20))
    @given(st.data())
    def test_a_seed_loaded_after_another_reads_back_as_if_alone(self, data):
        keys = sorted(self.corpus)
        first, then = data.draw(st.sampled_from(keys)), data.draw(st.sampled_from(keys))
        text = self.corpus[then][1]
        if data.draw(st.integers(0, 3)) == 0:
            text = data.draw(st.sampled_from(REFUSED_HEADERS)) + text[text.index("\n"):]
        note((first, then, text[:text.index("\n")]))
        load(self.corpus[first][1])
        report, _ = load(text)
        if a_header_the_client_refuses(text):
            self.assertIsNotNone(report["threw"])
            self.assertEqual(B.call("SeedfileGlue.Pickups"), {}, "the last seed's items are still placed")
            return
        self.assertIsNone(report["threw"])
        self.assertEqual(read_back_problems(text), [])

    @deep_only
    @settings(max_examples=examples(8))
    @given(S.web_json())
    def test_every_line_of_a_rolled_seed_reads_back_as_written(self, j):
        note("SeedGenParams.from_json(%r)" % j)
        info, texts = served(j)
        if texts is None:
            event("generation failed")
            return
        for p, text in texts.items():
            report, _ = load(text)
            self.assertIsNone(report["threw"], "world %s" % p)
            self.assertEqual(report["bad"], [], "world %s: %s" % (p, report["first"]))
            self.assertEqual(report["logged"], [], "world %s: lines that reached LogError" % p)
            self.assertEqual(read_back_problems(text), [], "world %s" % p)


ap_names = st.one_of(st.integers(1, 9).map(lambda n: "P%d" % n),
                       st.text(st.characters(codec="ascii", exclude_characters="\r\n"), min_size=1, max_size=12))


def a_room_name_with_a_separator(to, item):
    """A recipient holding ',' or '|', or an item name holding '|': the line's own separators."""
    return any(c in to for c in ",|") or "|" in item


class ApLines(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.corpus = corpus()

    @settings(max_examples=examples(40))
    @given(st.data())
    def test_a_scouted_reserved_line_reads_back_as_scouted(self, data):
        from types import SimpleNamespace
        from archipelago import annotate
        f, text = self.corpus[("ap", 1)]
        lines = file_lines(text)
        rows = [tuple(l.split("|", 3)) for l in lines[1:] if l and not l.startswith(("//", "BX|"))]
        shadow = str(f["players"] + 1)
        slots = sorted({annotate._reserved_slot(c, i, shadow)[0] for _, c, i, _ in rows if annotate._reserved_slot(c, i, shadow)})
        chosen = data.draw(st.lists(st.sampled_from(slots), unique=True, min_size=1, max_size=6))
        scouts = {s: SimpleNamespace(ap_item=None, item=data.draw(ap_names), to=data.draw(ap_names), ap_owner=None)
                  for s in chosen}
        promises = {s: data.draw(st.integers(0, 255)) for s in chosen if data.draw(st.booleans())}
        out = annotate.annotate(rows, f["players"], 1, {1: (scouts, None)}, lambda v: rows, promises)
        served_text = "\n".join(lines[:1] + [l for l in lines[1:] if l.startswith("//")]
                                + ["|".join(x for x in r if x is not None) for r in out]) + "\n"
        note([(s, scouts[s].to, scouts[s].item, promises.get(s)) for s in chosen])
        report, _ = load(served_text)
        self.assertEqual((report["threw"], report["bad"], report["logged"]), (None, [], []), report["first"])
        items, selves = B.call("SeedfileGlue.ApItems"), dict(p.split("=") for p in B.call("SeedfileGlue.Side")["apSelf"])
        for loc, code, value, zone in out:
            held = annotate._reserved_slot(code, value, shadow)
            if not held or held[0] not in scouts:
                continue
            scout = scouts[held[0]]
            if a_room_name_with_a_separator(scout.to, scout.item):
                event("masked: a room name with a separator")
                continue
            self.assertEqual(items.get(loc), [scout.to, scout.item], loc)
            self.assertEqual(selves.get(loc), str(promises[held[0]]) if held[0] in promises else None, loc)

    @unittest.expectedFailure
    def test_a_room_name_with_a_comma_reads_back(self):
        report, _ = load("Standard,Clues,mode=Multiworld|seed\n// SEED_FORMAT: 2\n// PLAYERS: 1\n"
                         "-3160308|MW|2,8,Bob, Jr,-1,AP,Sword|Glades\n")
        self.assertEqual(B.call("SeedfileGlue.ApItems").get("-3160308"), ["Bob, Jr", "Sword"])


def enemy_seed_key(header):
    parse_flags(header)
    return B.call("SeedfileGlue.EnemySeedKey")


class EnemySeedKey(unittest.TestCase):

    @settings(max_examples=examples(30))
    @given(SF.seed_names, st.integers(2, 4), st.sampled_from(["Co-op", "Race"]), st.booleans())
    def test_partners_on_one_seed_share_its_enemy_seed_key(self, seed, players, mode, race):
        j = {"seed": seed, "paths": ["casual-core"], "variations": ["EnemyRando"] + (["Race"] if race else []),
             "keyMode": "Clues", "players": players, "coopGameMode": mode, "tracking": True,
             "syncShared": ["Skills"] if mode == "Co-op" else []}
        texts = served(j, generate=False)[1]
        headers = [texts[p].split("\n")[0] for p in sorted(texts)]
        note(headers)
        self.assertEqual(len({enemy_seed_key(h) for h in headers}), 1)

    def test_multiworld_worlds_draw_apart(self):
        keys = {enemy_seed_key("Standard,Clues,EnemyRando,mode=Multiworld|seed/%d" % p) for p in (1, 2)}
        self.assertEqual(len(keys), 2)


def a_numeric_code_with_a_non_numeric_id(line):
    parts = line.split("|")
    return len(parts) >= 4 and not line.startswith(("//", "BX|")) and parts[1] not in STRING_CODES and not cs_int(parts[2])


JUNK_IDS = ["x", "", "1.5", "99999999999", "0x1", chr(0x663), " "]
# codes whose lines touch more than their own location
TELLING_CODES = {"EV", "SK", "WT", "TW", "RB", "EN", "MW", "MU", "RP"}


class RejectedLines(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.corpus = corpus()

    @settings(max_examples=examples(60))
    @given(st.data())
    def test_a_rejected_line_changes_nothing(self, data):
        f, text = self.corpus[data.draw(st.sampled_from(sorted(self.corpus)))]
        body = [l for l in file_lines(text)[1:] if l and not l.startswith("//")]
        telling = [l for l in body if l.split("|")[1] in TELLING_CODES or l.startswith("BX|")]
        parts = data.draw(st.sampled_from(telling if telling and data.draw(st.integers(0, 3)) else body)).split("|")
        how = data.draw(st.sampled_from(["id", "id", "id", "zone", "coords", "code", "corners"]))
        if how == "id" and len(parts) > 2:
            parts[2] = data.draw(st.sampled_from(JUNK_IDS))
        elif how == "zone":
            parts = parts[:3]
        elif how == "coords":
            parts[0] = data.draw(st.sampled_from(["", "abc", "1.5"]))
        elif how == "code" and len(parts) > 1:
            parts[1] = data.draw(st.sampled_from(["ZZ", "", "sk"]))
        elif how == "corners":
            parts = ["BX", "kill", data.draw(st.sampled_from(["1,2,3", "a,b,c,d", "1e3,0,1,1"]))]
        line = "|".join(parts)
        note(line)
        head = [l for l in file_lines(text) if not l or l.startswith("//")]
        load("\n".join(file_lines(text)[:1] + head) + "\n" if data.draw(st.booleans()) else text)
        before = (B.call("SeedfileGlue.Side"), B.call("SeedfileGlue.Pickups"))
        err = B.call("SeedfileGlue.Line", line)
        if err is None:
            event("accepted")
            return
        if err.startswith("logged: "):
            # the game logs and goes on; the bridge can't run LogError, so the line's outcome is unknown here
            event("reached LogError")
            return
        if a_numeric_code_with_a_non_numeric_id(line):
            event("masked: a numeric code with a non-numeric id")
            return
        self.assertEqual((B.call("SeedfileGlue.Side"), B.call("SeedfileGlue.Pickups")), before)

    @unittest.expectedFailure
    def test_a_line_with_a_bad_id_leaves_clues_and_sense_alone(self):
        report, _ = load("Standard,Clues|seed\n")
        key = B.call("SeedfileGlue.Keys")[0]
        before = B.call("SeedfileGlue.Side")
        self.assertIsNotNone(B.call("SeedfileGlue.Line", "%d|EV|x|Glades" % key))
        after = B.call("SeedfileGlue.Side")
        self.assertEqual((after["clues"], after["hotColdItems"]), (before["clues"], before["hotColdItems"]))


HEX = re.compile(r"^#?([0-9a-f]{6})([0-9a-f]{2})?$", re.I)


def box_expectation(model):
    """What the client should make of an editor box: the model, corners at the hundredths box_line keeps."""
    names = [f.strip() for f in model["flags"].split(",") if f.strip()]
    lower = [n.split("=", 1)[0].lower() for n in names]
    want = {"deleted": js("is_box_gone", model)}
    if want["deleted"]:
        return want
    x1, y1, x2, y2 = [SF.page_number(x) for x in model["box"]]
    want["corners"] = [min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)]
    m = HEX.match(model["color"].strip())
    if js("box_hidden", model):
        want["color"] = None
    elif m:
        rgb = bytes.fromhex(m.group(1))
        alpha = int(m.group(2), 16) if m.group(2) else (140 if "solid" in lower else 64)
        want["color"] = [rgb[0], rgb[1], rgb[2], alpha]
    want.update(once="once" in lower or "item" in lower, solid="solid" in lower, goal="goal" in lower,
                unsafe="unsafe" in lower)
    triggers = [n.split("=", 1)[1].split("/")[0].lower() for n in names if n.lower().startswith("on=")]
    want["trigger"] = "Frame" if triggers and triggers[-1] == "frame" else "Tick"
    give = model["give"]
    if "|" in give:
        code, value = give.split("|", 1)
        want["item"] = code + "|" + value_of(code, value)
    else:
        want["item"] = "RB|3" if "kill" in lower else None
    return want


def a_give_the_client_cannot_read(model):
    code, bar, value = model["give"].partition("|")
    return bool(bar) and code not in STRING_CODES and not cs_int(value)


def a_lone_carriage_return(text):
    return re.search(r"\r(?!\n)", text) is not None


def cs_float(text):
    """float.TryParse with NumberStyles.Number on a plain decimal: whitespace around it and one leading sign."""
    return re.fullmatch(r"[ \t\n\v\f\r]*[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)[ \t\n\v\f\r]*", text) is not None


SHAPE_TYPES = {"convex": "ConvexHull", "loop": "Polygon", "orientedloop": "OrientedPolygon", "previewloop": "PreviewPolygon"}


def shape_model(field):
    """RandomizerBox.ParseShape on a line's third field: (ShapeType, loops of (x, y), corners), or None where it throws."""
    parts = field.split(",")
    kind = parts[0].lower()
    if kind not in SHAPE_TYPES:
        if len(parts) != 4 or not all(cs_float(p) for p in parts):
            return None
        x1, y1, x2, y2 = [float(p) for p in parts]
        return "Rectangle", None, [min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)]
    loops, k = [[]], 1
    while k < len(parts):
        if kind != "convex" and parts[k].lower() == "loop":
            loops.append([])
            k += 1
        elif k == len(parts) - 1:
            break
        elif not (cs_float(parts[k]) and cs_float(parts[k + 1])):
            return None
        else:
            loops[-1].append((float(parts[k]), float(parts[k + 1])))
            k += 2
    if kind != "previewloop" and any(len(l) < 3 for l in loops):
        return None
    points = [p for l in loops for p in l]
    xs, ys = [p[0] for p in points] or [0.0], [p[1] for p in points] or [0.0]
    return SHAPE_TYPES[kind], loops, [min(xs), min(ys), max(xs), max(ys)]


def near(a, b):
    return math.isclose(a, f32(b), rel_tol=1e-6, abs_tol=1e-3)


def turn(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def hull_model(points):
    """The convex hull's corners, counterclockwise, by Andrew's monotone chain."""
    points = sorted(set(points))

    def half(seq):
        out = []
        for p in seq:
            while len(out) > 1 and turn(out[-2], out[-1], p) <= 0:
                out.pop()
            out.append(p)
        return out[:-1]
    return points if len(points) < 3 else half(points) + half(points[::-1])


def area(ring):
    """Signed: positive for a counterclockwise ring."""
    return sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(ring, ring[1:] + ring[:1])) / 2


class Boxes(unittest.TestCase):

    @settings(max_examples=examples(150))
    @given(SF.box_models(gives=SF.GIVES + ["SK|x", "EX|1.5"]))
    def test_a_box_line_the_editor_writes_reads_back_in_the_client(self, model):
        line = js("box_line", model)
        note(line)
        got = outcome(B.call, "SeedfileGlue.Box", line)
        if a_give_the_client_cannot_read(model):
            event("masked: a give the client cannot read")
            return
        self.assertEqual(got[0], "ok", got)
        box, want = got[1], box_expectation(model)
        self.assertEqual(box["deleted"], want["deleted"])
        if want["deleted"]:
            return
        for a, b in zip(box["corners"], want["corners"]):
            self.assertTrue(math.isclose(a, f32(b), rel_tol=1e-6, abs_tol=1e-3), (box["corners"], want["corners"]))
        for k in ("once", "solid", "goal", "unsafe", "trigger", "item"):
            self.assertEqual(box[k], want[k], k)
        if "color" in want:
            self.assertEqual(box["color"], want["color"])

    @unittest.expectedFailure
    def test_every_box_line_the_editor_writes_gets_a_number(self):
        line = js("box_line", js("parse_box_line", "BX|item|0,0,1,1||SK|x"))
        report, _ = load("Standard|seed\n" + line + "\nBX|kill|2,2,3,3\n")
        self.assertEqual(B.call("SeedfileGlue.LoadedBoxLines"), [line, "BX|kill|2,2,3,3"])

    @settings(max_examples=examples(100))
    @given(SF.box_models(hash_ok=False))
    def test_both_editors_write_a_page_box_the_same_line(self, model):
        model = dict(model, box=[SF.page_number(x) for x in model["box"]])
        line = js("box_line", model)
        self.assertEqual(B.call("SeedfileGlue.BoxFromJson", model["flags"], model["box"], model["color"], model["give"]), line)

    @settings(max_examples=examples(300))
    @given(SF.shape_fields())
    def test_a_shape_reads_the_same_in_the_game_the_plando_builder_and_the_model(self, field):
        # a color after it: the builder trims a pasted line's end
        line = "BX|solid|" + field + "|ff0000"
        want, got, site = shape_model(field), outcome(B.call, "SeedfileGlue.Box", line), js("parse_box_line", line)
        self.assertEqual(got[0] == "ok", want is not None, got)
        self.assertEqual(site is not None, want is not None, site)
        if want is None:
            return
        kind, loops, corners = want
        box = got[1]
        self.assertEqual(box["shape"], kind)
        for a, b in zip(box["corners"], corners):
            self.assertTrue(near(a, b), (box["corners"], corners))
        if loops is None:
            x1, y1, x2, y2 = site["box"]
            self.assertEqual([min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2)], corners)
            self.assertEqual(site["shape"], "")
            self.assertIsNone(box["points"])
            return
        self.assertEqual(site["box"], corners)
        self.assertEqual(site["shape"], field)
        self.assertEqual(js("box_line", site), line)
        self.assertEqual([len(l) for l in box["points"]], [len(l) for l in loops])
        for mine, theirs in zip(loops, box["points"]):
            for p, q in zip(mine, theirs):
                self.assertTrue(near(q[0], p[0]) and near(q[1], p[1]), (p, q))
        self.assertEqual(js("parse_shape", field)["loops"], [[list(p) for p in l] for l in loops])

    @settings(max_examples=examples(150))
    @given(SF.shapes(), st.floats(-2000, 2000), st.floats(-2000, 2000))
    def test_a_shaped_box_the_editor_moves_reads_back_where_it_was_put(self, shape, dx, dy):
        was = js("parse_box_line", "BX|solid|" + shape)
        moved = dict(was, **js("move_box", was, dx, dy))
        line = js("box_line", moved)
        note(line)
        box = B.call("SeedfileGlue.Box", line)
        mx, my = SF.page_number(dx), SF.page_number(dy)
        for a, b, d in zip(box["corners"], was["box"], (mx, my, mx, my)):
            self.assertTrue(near(a, b + d), (box["corners"], was["box"], dx, dy))
        loops = shape_model(shape)[1]
        self.assertEqual([len(l) for l in box["points"]], [len(l) for l in loops])
        for mine, theirs in zip(loops, box["points"]):
            for p, q in zip(mine, theirs):
                self.assertTrue(near(q[0], p[0] + mx) and near(q[1], p[1] + my), (p, q, dx, dy))

    @settings(max_examples=examples(150))
    @given(SF.shapes(), st.lists(st.tuples(st.sampled_from(["set", "insert", "remove", "tidy", "add", "convex", "loop", ""]),
                                           st.integers(0, 50), SF.hundredth, SF.hundredth), min_size=1, max_size=6))
    def test_a_shape_the_editor_reshapes_reads_in_the_game_as_it_does_on_the_site(self, shape, edits):
        box = js("parse_box_line", "BX|solid|" + shape)
        for op, at, x, y in edits:
            points = js("box_points", box)
            p = points[at % len(points)] if points else None
            if op in ("convex", "loop", ""):
                patch = js("reshape_box", box, op)
            elif not p:
                continue
            elif op == "set":
                patch = js("set_point", box, p["loop"], p["k"], x, y)
            elif op == "insert":
                side = js("nearest_side", box, x, y)
                patch = js("insert_point", box, side["loop"], side["k"], side["x"], side["y"])
            elif op == "remove":
                patch = js("remove_point", box, p["loop"], p["k"]) or {}
            else:
                patch = js("tidy_shape" if op == "tidy" else "add_loop", box)
            box = dict(box, **patch)
        line = js("box_line", box)
        note(line)
        game, read = B.call("SeedfileGlue.Box", line), js("parse_shape", box["shape"])
        self.assertEqual(game["shape"], SHAPE_TYPES[read["kind"]] if read else "Rectangle")
        for a, b in zip(game["corners"], box["box"]):
            self.assertTrue(near(a, b), (game["corners"], box["box"]))
        self.assertEqual([len(l) for l in game["points"] or []], [len(l) for l in read["loops"]] if read else [])
        for mine, theirs in zip(read["loops"] if read else [], game["points"] or []):
            for p, q in zip(mine, theirs):
                self.assertTrue(near(q[0], p[0]) and near(q[1], p[1]), (p, q))

    @settings(max_examples=examples(100))
    @given(SF.box_models(hash_ok=False), SF.shapes())
    def test_both_editors_write_a_shaped_page_box_the_same_line(self, model, shape):
        model = dict(js("parse_box_line", "BX|solid|" + shape), flags=model["flags"], color=model["color"], give=model["give"])
        line = js("box_line", model)
        self.assertEqual(B.call("SeedfileGlue.ShapedBoxFromJson", model["flags"], model["box"], shape, model["color"],
                                model["give"]), line)

    @settings(max_examples=examples(200))
    @given(st.lists(st.tuples(st.integers(-40, 40), st.integers(-40, 40)), min_size=3, max_size=12))
    @example([(0, 0), (0, -4), (1, 0), (0, -3), (-1, -4), (-2, 1)])
    def test_a_convex_box_covers_the_hull_of_its_points(self, points):
        want = hull_model(points)
        if len(want) < 3:
            event("masked: points on one line")
            return
        got = [tuple(p) for p in B.call("SeedfileGlue.Hull", [float(x) for x, _ in points], [float(y) for _, y in points])]
        note(got)
        self.assertLessEqual(set(got), set(points))
        self.assertLessEqual(set(want), set(got))
        self.assertAlmostEqual(area(got), area(want), places=3)

    @settings(max_examples=examples(80))
    @given(st.data())
    def test_box_numbers_agree_across_readers(self, data):
        items = data.draw(st.lists(st.one_of(
            SF.box_models().map(lambda m: js("box_line", m)),
            SF.shape_fields(corners=False).map(lambda f: "BX|kill|" + f + "|ff0000"),
            st.sampled_from(["-280256|EC|1|Glades", "2|MU|EC/1|Glades", "// PLAYERS: 2", "", "   ", "BX|kill|1,2,3",
                             "BX|kill|a,b,c,d", "BX", "bx|kill|0,0,1,1", "BXkill|0,0,1,1"])), max_size=10))
        endings = data.draw(st.lists(st.sampled_from(["\n", "\r\n", "\r"]), min_size=len(items) + 1,
                                     max_size=len(items) + 1))
        text = "Standard|seed" + "".join(e + l for e, l in zip(endings, items + [""]))
        note(repr(text))
        load(text)
        game = B.call("SeedfileGlue.LoadedBoxLines")
        pasted = [js("box_line", b) for b in js("parse_box_paste", text)["boxes"]]
        self.assertEqual(pasted, game)
        if a_lone_carriage_return(text):
            event("masked: a lone carriage return")
            return
        self.assertEqual(B.call("SeedfileGlue.EditorBoxLines", os.path.join(TMP, "randomizer.bfr")), game)

    @unittest.expectedFailure
    def test_the_box_editor_numbers_boxes_as_the_game_does(self):
        report, path = load("Standard|seed\nBX|kill|0,0,1,1\rBX|kill|2,2,3,3\n")
        self.assertEqual(B.call("SeedfileGlue.EditorBoxLines", path), B.call("SeedfileGlue.LoadedBoxLines"))


def even_odd(ring, x, y):
    """Whether the ring holds the point, by crossings; only sound off the ring itself."""
    hit = False
    for (x1, y1), (x2, y2) in zip(ring, ring[1:] + ring[:1]):
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit


def winding(ring, x, y):
    """The ring's turns around the point, by signed crossings; only sound off the ring itself."""
    turns = 0
    for (x1, y1), (x2, y2) in zip(ring, ring[1:] + ring[:1]):
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            turns += 1 if y2 > y1 else -1
    return turns


def holds_model(kind, rings, p):
    """An orientedLoop's loops fill together by nonzero winding; any other kind's rings each even-odd."""
    if kind == "orientedloop":
        return sum(winding(r, *p) for r in rings) != 0
    return any(even_odd(r, *p) for r in rings)


def reach(p, a, b):
    """The distance from p to the segment ab."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    t = max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / (dx * dx + dy * dy))) if dx or dy else 0
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)


def sides(rings):
    return [(a, b) for r in rings for a, b in zip(r, r[1:] + r[:1])]


def clear_of(rings, p):
    return all(reach(p, a, b) > 0.05 for a, b in sides(rings))


def in_triangles(triangles, p):
    def holds(t):
        turns = [turn(t[k], t[(k + 1) % 3], p) for k in range(3)]
        return abs(area(t)) > 1e-9 and (min(turns) >= -1e-9 or max(turns) <= 1e-9)
    return any(holds(t) for t in triangles)


def geometry(field):
    """SeedfileGlue.Geometry for a solid box of this shape, its paths as tuples and its triangles in one list;
    None where the game draws the rectangle around it instead."""
    g = B.call("SeedfileGlue.Geometry", "BX|solid|" + field)
    if g is None:
        event("masked: the game drew the rectangle")
        return None
    for key in ("outline", "edge", "fill"):
        g[key] = [[tuple(p) for p in path] for path in g[key]]
    g["mesh"] = g["edge"] + g["fill"]
    return g


def covered(g):
    return sum(area(path) for path in g["outline"])


def rim(g):
    return sum(math.dist(a, b) for a, b in sides(g["outline"]))


class BoxGeometry(unittest.TestCase):
    @settings(max_examples=examples(150))
    @given(SF.drawn_shapes(), SF.spots)
    def test_a_shaped_box_fills_what_the_plando_builder_says_it_holds(self, shape, spots):
        field, kind, loops = shape
        rings = [hull_model(loops[0])] if kind == "convex" else loops
        g, site = geometry(field), js("parse_box_line", "BX|solid|" + field)
        if g is None:
            return
        for p in spots:
            if not clear_of(rings, p):
                continue
            want = holds_model(kind, rings, p)
            event("a spot %s the box" % ("in" if want else "out of"))
            self.assertEqual(in_triangles(g["mesh"], p), want, p)
            self.assertEqual(js("box_holds", site, *p), want, p)

    @settings(max_examples=examples(150))
    @given(SF.drawn_shapes())
    def test_a_shaped_box_s_triangles_tile_its_outline(self, shape):
        g = geometry(shape[0])
        if g is None:
            return
        self.assertAlmostEqual(sum(abs(area(t)) for t in g["mesh"]), covered(g), delta=1e-3 * (1 + rim(g)))
        self.assertEqual([tuple(v) for v in g["vertices"]], [p + (0.0,) for t in g["mesh"] for p in t])
        self.assertEqual(g["triangles"], list(range(len(g["vertices"]))))
        self.assertEqual(g["edgeColored"], 3 * len(g["edge"]))

    @settings(max_examples=examples(150))
    @given(SF.drawn_shapes())
    def test_the_fill_keeps_an_edge_s_width_off_the_outline(self, shape):
        g = geometry(shape[0]) or {"fill": []}
        for p in {p for t in g["fill"] for p in t}:
            self.assertGreater(min(reach(p, a, b) for a, b in sides(g["outline"])), 0.24, p)

    # an outline runs counterclockwise around the box and clockwise around a hole, so a wall's front is on its right
    @settings(max_examples=examples(150))
    @given(SF.drawn_shapes())
    def test_the_collider_s_walls_stand_on_the_outline_facing_out(self, shape):
        g = geometry(shape[0])
        if g is None:
            return
        corners = [p for path in g["outline"] for p in path]
        self.assertEqual([tuple(w) for w in g["walls"]], [p + (z,) for p in corners for z in (50.0, -50.0)])
        want, at = [], 0
        for path in g["outline"]:
            for k in range(len(path)):
                a, b = at + 2 * k, at + 2 * ((k + 1) % len(path))
                want += [a, a + 1, b, a + 1, b + 1, b]
            at += 2 * len(path)
        self.assertEqual(g["wallTriangles"], want)

    @settings(max_examples=examples(150))
    @given(SF.drawn_shapes(), SF.spots)
    def test_a_shape_the_in_game_editor_rewrites_covers_what_it_did(self, shape, spots):
        g = geometry(shape[0])
        if g is None:
            return
        if not g["outline"]:
            event("masked: a shape with no area")
            return
        if any(area(path) < 0 for path in g["outline"]):
            event("a hole")
        again = geometry(g["rewrite"])
        self.assertIsNotNone(again, g["rewrite"])
        self.assertAlmostEqual(covered(again), covered(g), delta=0.01 * (1 + rim(g)))
        for p in spots:
            if clear_of(g["outline"], p):
                self.assertEqual(in_triangles(again["mesh"], p), in_triangles(g["mesh"], p), p)

    def test_a_hole_survives_the_in_game_editor_rewriting_the_shape(self):
        g = geometry("loop,0,0,10,0,10,10,0,10,0,0,3,3,3,7,7,7,7,3,3,3")
        self.assertEqual(covered(geometry(g["rewrite"])), covered(g))

    @unittest.expectedFailure
    def test_a_shape_with_no_area_survives_the_in_game_editor_rewriting_it(self):
        g = geometry("convex,0,0,1,1,2,2")
        self.assertEqual(outcome(B.call, "SeedfileGlue.Box", "BX|solid|" + g["rewrite"])[0], "ok")

    # one triangle turns against part of the bow, so their overlap cancels to empty
    @unittest.expectedFailure
    def test_an_oriented_loop_that_cancels_part_of_another_still_draws(self):
        self.assertIsNotNone(geometry("orientedLoop,-4,4,0,-8,0,-4,loop,0,0,0,4,-4,0,-4,4"))

    @unittest.expectedFailure
    def test_a_thin_loop_s_triangles_cover_its_outline(self):
        g = geometry("loop,0,0,0,0.25,-0.25,3.5,loop,0,0.5,-0.25,3.5,0,-0.25,-0.5,6.75")
        self.assertAlmostEqual(sum(abs(area(t)) for t in g["mesh"]), covered(g), places=3)


TABLE = []


def enemy_table():
    if not TABLE:
        t = enemyroll.table()
        TABLE.append((t["version"], len(t["variants"])))
    return TABLE[0]


def client_rolls(line):
    B.call("SeedfileGlue.Reset")
    err = B.call("SeedfileGlue.Line", line)
    return None if err else B.call("SeedfileGlue.Rolls")


def a_bonus_number_outside_the_clients_range(rolls):
    """bN whose -(N + 2) the client can't hold in an int, or whose digits int.Parse doesn't read."""
    return any(isinstance(r, str) and not (cs_int(r[1:]) and int(r[1:]) <= 2**31 - 2) for r in rolls)


def a_roll_only_the_client_reads(line):
    """Signs and spaces int.Parse takes and rolls_of doesn't, and a version or body padded with spaces."""
    head, _, body = line[len(enemyroll.META_PREFIX):].partition(":")
    tokens = body.split(",")
    return (head != head.strip() or body != body.strip()
            or any(t.startswith("-") or (t.startswith("b") and not re.fullmatch(r"b[0-9]+", t) and cs_int(t[1:]))
                   for t in tokens))


class EnemyLines(unittest.TestCase):

    @settings(max_examples=examples(40))
    @given(S.seed_strings, st.integers(1, 4), S.enemy_opts())
    def test_a_rolled_table_loads_whole(self, seed, world, opts):
        rolls = enemyroll.roll(seed, world, opts)
        self.assertEqual(client_rolls(enemyroll.meta_line(rolls)), table_of(rolls))

    @settings(max_examples=examples(150))
    @given(st.data())
    def test_a_rolls_line_the_server_keeps_loads_as_kept(self, data):
        version, variants = enemy_table()
        rolls = enemyroll.roll(data.draw(S.seed_strings), 1, None)
        line = data.draw(SF.rolls_lines(rolls, version, variants))
        kept = enemyroll.rolls_of(line)
        if kept is None:
            event("rolls_of refused it")
            return
        if a_bonus_number_outside_the_clients_range(kept):
            event("masked: a bonus number outside the client's range")
            return
        self.assertEqual(client_rolls(enemyroll.meta_line(kept)), table_of(kept))

    @unittest.expectedFailure
    def test_a_bonus_roll_the_server_keeps_loads(self):
        version, variants = enemy_table()
        rolls = [None] * len(enemyroll.table()["placements"])
        rolls[0] = "b99999999999"
        self.assertEqual(enemyroll.rolls_of(enemyroll.meta_line(rolls)), rolls)
        self.assertEqual(client_rolls(enemyroll.meta_line(rolls)), table_of(rolls))

    @settings(max_examples=examples(150))
    @given(st.data())
    def test_the_client_refuses_a_rolls_line_rolls_of_refuses(self, data):
        version, variants = enemy_table()
        line = data.draw(SF.rolls_lines(enemyroll.roll(data.draw(S.seed_strings), 1, None), version, variants))
        note(line)
        if enemyroll.rolls_of(line) is not None:
            return
        if a_roll_only_the_client_reads(line):
            event("masked: a roll only the client reads")
            return
        self.assertIsNone(client_rolls(line))

    @unittest.expectedFailure
    def test_the_client_refuses_a_negative_roll(self):
        version, _ = enemy_table()
        line = enemyroll.meta_line([None] * len(enemyroll.table()["placements"])).replace(":,", ":-5,", 1)
        self.assertIsNone(enemyroll.rolls_of(line))
        self.assertIsNone(client_rolls(line))

    @settings(max_examples=examples(60))
    @given(S.enemy_opts(), S.seed_strings)
    def test_enemy_options_read_back_as_written(self, opts, seed):
        line = enemyopts.meta_line(opts)
        note(line)
        B.call("SeedfileGlue.Reset")
        B.set("Randomizer.SeedMeta", "Standard|" + seed)
        if line:
            self.assertIsNone(B.call("SeedfileGlue.Line", line))
        want = enemyopts.normalize(opts) or {}
        for key, static in (("hp", "HpMode"), ("dmg", "DmgMode")):
            mode = want.get(key, "zone")
            self.assertEqual(B.get("RandomizerEnemies." + static), mode)
            self.assertScales(mode, scale(mode))

    def assertScales(self, mode, factor):
        """ModeFactor at zone strength 2: 2 for zone, 1 for vanilla, within 2*lo..2*hi for a range."""
        m = enemyopts.RANGE.match(mode)
        if m:
            lo, hi = float(m.group(1)), float(m.group(2))
            self.assertTrue(2 * lo - 1e-5 <= factor <= 2 * hi + 1e-5, (mode, factor))
        else:
            self.assertEqual(factor, 1.0 if mode == "vanilla" else 2.0, mode)

    @settings(max_examples=examples(80))
    @given(st.one_of(st.sampled_from(SF.MODE_ODDITIES), st.tuples(st.floats(0.25, 4), st.floats(0.25, 4)).map(
        lambda t: "%g-%g" % t)))
    def test_a_mode_scales_as_enemyopts_reads_it(self, mode):
        B.call("SeedfileGlue.Reset")
        self.assertIsNone(B.call("SeedfileGlue.Line", "//Enemies=hp=" + mode))
        if a_mode_enemyopts_drops(mode):
            event("masked: a mode enemyopts drops")
            return
        self.assertScales(mode.strip(), scale(B.get("RandomizerEnemies.HpMode")))

    @unittest.expectedFailure
    def test_a_mode_enemyopts_drops_scales_like_zone(self):
        B.call("SeedfileGlue.Reset")
        B.call("SeedfileGlue.Line", "//Enemies=hp=0.5--1")
        self.assertEqual(scale(B.get("RandomizerEnemies.HpMode")), 2.0)


def scale(mode):
    return B.call("RandomizerEnemies.ModeFactor", mode, 2.0, "props")


def a_mode_enemyopts_drops(mode):
    """A value the server's normalize would drop: anything mode_ok refuses."""
    return not enemyopts.mode_ok(mode.strip())
