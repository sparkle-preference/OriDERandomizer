"""Client logic (protogen) against the server's tracker: reachability, logic flags, locations and keystone doors."""
import os
import re
import shutil
import unittest

from hypothesis import given, settings, strategies as st

from archipelago.convert import oride_module
from client_props.base import CLIENT, HERE, SERVER, examples
from client_props.cs_bridge import BuildError, CsError, slice_bridge
from client_props.strategies.logic import (EVENTS, PATHSETS, PRESETS, SKILLS, SPAWNS, TELEPORTERS, Held, areas_edits,
                                           helds, path_sets, preset_paths)
from enums import KeyMode, LogicPath, Variation, presets
from reachable import Map, PlayerState, initial_area_by_spawn
from seedbuilder import generator
from seedbuilder.generator import SPAWN_SPOTS, keysanity_map, warp_targets2
from seedbuilder.oriparse import get_areas, get_path_tags, hardest_pathset_from_tags
from util import get_preset_from_paths

AREAS = os.path.join(SERVER, "seedbuilder", "areas.ori")
WORK = os.path.join(os.environ.get("TEMP", HERE), "client_props", "work", "logic")
CS = os.path.join(HERE, "cs", "logic")
ID_BY_NAME = {name: key for key, name in PlayerState.name_from_id.items()}
NOTHING = Held(frozenset(), frozenset(), frozenset(), 0, 0, 0, 0, 0)
EVERYTHING = Held(frozenset(SKILLS), frozenset(EVENTS), frozenset(TELEPORTERS), 12, 15, 40, 11, 33)
# the tracker counts a pedestal as any reached area named *Map
PEDESTALS = frozenset(n for home in get_areas()["homes"].values() for n in home["conns"] if n.endswith("Map"))
FORLORN_DOOR = "ForlornKeyDoor"

B = None  # protogen, built from a scratch copy
G = None  # the release dll's RandomizerLocationManager and friends
SERVER_VIEW = None
TP_IDS = {}


def scratch_protogen():
    """A fresh copy of protogen/*.cs whose Inventory.cs reads the engine through cs/logic/Stubs.cs."""
    dest = os.path.join(WORK, "protogen")
    shutil.rmtree(dest, ignore_errors=True)
    os.makedirs(dest)
    src = os.path.join(CLIENT, "protogen")
    names = sorted(n for n in os.listdir(src) if n.endswith(".cs"))
    for name in names:
        shutil.copyfile(os.path.join(src, name), os.path.join(dest, name))
    path = os.path.join(dest, "Inventory.cs")
    with open(path, "rb") as f:
        text = f.read()
    nl = b"\r\n" if b"\r\n" in text else b"\n"
    anchor = nl.join([b"using Game;", b"using Sein.World;", b"using Events = Sein.World.Events;", b""])
    if text.count(anchor) != 1:
        raise BuildError("protogen/Inventory.cs: the engine usings moved; update scratch_protogen's anchor")
    with open(path, "wb") as f:
        f.write(text.replace(anchor, b"using ProtogenStubs;" + nl))
    return [os.path.join(dest, n) for n in names]


def teleport_table():
    """Randomizer.TeleportTable (server zone -> teleporter identifier), read from the client source."""
    with open(os.path.join(CLIENT, "randomizer", "Randomizer.cs"), encoding="utf-8-sig") as f:
        found = dict(re.findall(r'TeleportTable\["(\w+)"\]\s*=\s*"(\w+)"', f.read()))
    if len(found) != 10:
        raise BuildError("Randomizer.cs: expected ten TeleportTable entries, read %r" % found)
    return found


def areas_lines():
    with open(AREAS, "rb") as f:
        return f.read().decode("utf-8").split("\n")


def path_tokens(raw):
    """A path line's tokens as both parsers split them, or None for a marker, comment or blank line."""
    tokens = raw.split("--")[0].split()
    if not tokens or tokens[0].endswith(":"):
        return None
    return tokens


def server_dropped(tokens):
    """A path line the tracker files under no path set, so no mode admits it."""
    return hardest_pathset_from_tags(get_path_tags(tokens)) is None


def without_server_dropped_lines():
    kept = [raw for raw in areas_lines() if not (path_tokens(raw) and server_dropped(path_tokens(raw)))]
    path = os.path.join(WORK, "areas_server_view.ori")
    with open(path, "wb") as f:
        f.write("\n".join(kept).encode("utf-8"))
    return path


def setUpModule():
    global B, G, SERVER_VIEW
    os.makedirs(WORK, exist_ok=True)
    sources = scratch_protogen() + [os.path.join(CLIENT, "randomizer", "RandomizerLocationData.cs")]
    B = slice_bridge("logic", sources, extra=[os.path.join(CS, "Stubs.cs"), os.path.join(CS, "LogicGlue.cs")])
    G = slice_bridge("logic-game", [], extra=[os.path.join(CS, "GameGlue.cs")], game=True)
    SERVER_VIEW = without_server_dropped_lines()
    TP_IDS.update({"TP" + zone: ident for zone, ident in teleport_table().items()})


# --- both reaches ---

def client_reach(held, paths, closed=False, open_world=False, spawn="Glades", doors=(), areas=None):
    """The client's reachable names for held, with Map<i> spelled as the tracker's MS<i>."""
    names = B.call("LogicGlue.Reach", areas or AREAS, list(paths), sorted(held.skills), sorted(held.events),
                   sorted(TP_IDS[t] for t in held.tps), 3 + held.hc, held.ec, held.ks, held.ms, held.ac,
                   not closed, open_world, initial_area_by_spawn[spawn], list(doors))
    return {"MS" + n[3:] if re.match(r"Map\d$", n) else n for n in names}


def server_reach(held, paths, closed=False, open_world=False, spawn="Glades"):
    """The tracker's reachable names for held, read through PlayerState as seed pickups."""
    picks = [ID_BY_NAME[n] + (1,) for n in sorted(held.skills | held.events | held.tps)]
    picks += [(code, 1, n) for code, n in (("HC", held.hc), ("EC", held.ec), ("KS", held.ks), ("MS", held.ms),
                                            ("AC", held.ac)) if n]
    state = PlayerState([(code, str(pid), n, False) for code, pid, n in picks])
    modes = list(paths) + (["CLOSED_DUNGEON"] if closed else []) + (["OPEN_WORLD"] if open_world else [])
    reached = set(Map.get_reachable_areas(state, modes, spawn, need_reached_with=False))
    # the tracker's FronkeyFight extra names no areas.ori node
    reached.discard("SunkenGladesFirstEC")
    return reached


def progressive(names):
    return {n for n in names if re.match(r"MS\d$", n)}


@st.composite
def preset_worlds(draw, ks=st.just(0), ms=st.integers(0, 11), paths=None):
    """(held, paths, closed, open world, spawn): no dungeon teleporter or dungeon spawn behind closed dungeons."""
    held = draw(helds(ks=ks, ms=ms))
    chosen = draw(paths if paths is not None else st.sampled_from(PRESETS).map(preset_paths))
    closed, open_world = draw(st.booleans()), draw(st.booleans())
    spawns = [s for s in SPAWNS if not (closed and s in ("Ginso", "Horu"))]
    if closed:
        held = held._replace(tps=held.tps - {"TPGinso", "TPHoru"})
    return held, chosen, closed, open_world, draw(st.sampled_from(spawns))


# --- masks: input classes with an open disagreement ---

def holds_no_mapstones(held):
    return held.ms == 0


def holds_more_mapstones_than_pedestals_in_reach(held, tracker_reached):
    return held.ms > len(tracker_reached & PEDESTALS)


def holds_keystones(held):
    return held.ks > 0


def masked(held, client, tracker):
    """Both reaches with the open mapstone classes taken out."""
    if holds_no_mapstones(held):
        client, tracker = client - PEDESTALS, tracker - PEDESTALS
    if holds_more_mapstones_than_pedestals_in_reach(held, tracker):
        client, tracker = client - progressive(client), tracker - progressive(tracker)
    return client, tracker


LINE_SETS = None


def line_sets():
    """(client path sets, tracker's hardest path set) for every admissible areas.ori path line."""
    global LINE_SETS
    if LINE_SETS is None:
        bits = {name: 1 << i for i, name in enumerate(lp.value for lp in LogicPath)}
        found = set()
        for raw in areas_lines():
            tokens = path_tokens(raw)
            if tokens and not server_dropped(tokens):
                mask = B.call("OriParse.GetPathMaskFromLine", tokens)
                found.add((frozenset(n for n, b in bits.items() if mask & b), hardest_pathset_from_tags(get_path_tags(tokens))))
        LINE_SETS = found
    return LINE_SETS


def splits_a_line(paths):
    """A path set holding some line's hardest path set but not every set its tags name, or the reverse."""
    held = set(paths)
    return any((hardest in held) != (needs <= held) for needs, hardest in line_sets())


def enemy_rando_paths(paths):
    """The path sets the generator and the tracker walk under EnemyRando."""
    return [p for p in paths if not p.endswith("-lure")]


class Reach(unittest.TestCase):
    @settings(max_examples=examples(150))
    @given(preset_worlds())
    def test_client_and_tracker_reach_the_same_places(self, world):
        held, paths, closed, open_world, spawn = world
        client, tracker = masked(held, client_reach(*world), server_reach(*world))
        self.assertEqual((sorted(client - tracker), sorted(tracker - client)), ([], []), world)

    @settings(max_examples=examples(80))
    @given(preset_worlds(ks=st.integers(1, 40)))
    def test_with_keystones_the_client_never_reaches_less_than_the_tracker(self, world):
        held = world[0]
        client, tracker = masked(held, client_reach(*world), server_reach(*world))
        self.assertEqual(sorted(tracker - client), [], world)
        if not holds_keystones(held):
            self.assertEqual(sorted(client - tracker), [], world)

    @settings(max_examples=examples(80))
    @given(preset_worlds(paths=path_sets()))
    def test_custom_path_sets_reach_the_same_places(self, world):
        held, paths = world[:2]
        client, tracker = masked(held, client_reach(*world), server_reach(*world))
        self.assertEqual(sorted(client - tracker), [], world)
        if not splits_a_line(paths):
            self.assertEqual(sorted(tracker - client), [], world)

    @settings(max_examples=examples(40))
    @given(preset_worlds(ks=st.integers(0, 40)))
    def test_lines_the_tracker_drops_never_change_the_clients_reach(self, world):
        self.assertEqual(client_reach(*world), client_reach(*world, areas=SERVER_VIEW), world)

    def test_nothing_and_everything_agree(self):
        for name in PRESETS:
            for held in (NOTHING, EVERYTHING._replace(ks=0, ms=9)):
                self.assertEqual(client_reach(held, preset_paths(name)), server_reach(held, preset_paths(name)), name)

    @unittest.expectedFailure
    def test_a_pedestal_needs_a_mapstone(self):
        self.assertEqual(client_reach(NOTHING, preset_paths("Casual"), spawn="Grotto") & PEDESTALS, set())

    @unittest.expectedFailure
    def test_progressive_maps_in_logic_never_outnumber_reachable_pedestals(self):
        held = NOTHING._replace(skills=frozenset({"WallJump", "ChargeJump"}), ms=2)
        client = client_reach(held, preset_paths("Casual"), spawn="Blackroot")
        self.assertEqual(client & PEDESTALS, {"BlackrootMap"})
        self.assertEqual(progressive(client), {"MS1"})

    @unittest.expectedFailure
    def test_keys_spent_on_one_door_do_not_open_another(self):
        held = NOTHING._replace(skills=frozenset({"ChargeFlame", "ChargeJump"}), ks=6)
        paths = preset_paths("Casual")
        client, tracker = masked(held, client_reach(held, paths), server_reach(held, paths))
        self.assertEqual(sorted(client - tracker), [])


class Monotone(unittest.TestCase):
    @settings(max_examples=examples(50))
    @given(preset_worlds(ks=st.integers(0, 40)), helds(ks=st.integers(0, 40), ms=st.integers(0, 11)))
    def test_more_items_never_reach_less_on_the_client(self, world, extra):
        held, rest = world[0], world[1:]
        more = grown(held, extra, closed=rest[1])
        self.assertEqual(sorted(client_reach(held, *rest) - client_reach(more, *rest)), [], (world, more))

    @settings(max_examples=examples(50))
    @given(preset_worlds(ks=st.integers(0, 40)), helds(ks=st.integers(0, 40), ms=st.integers(0, 11)))
    def test_more_items_never_reach_less_on_the_tracker(self, world, extra):
        held, rest = world[0], world[1:]
        more = grown(held, extra, closed=rest[1])
        if holds_keystones(more):
            held, more = held._replace(ks=0), more._replace(ks=0)
        self.assertEqual(sorted(server_reach(held, *rest) - server_reach(more, *rest)), [], (world, more))

    @unittest.expectedFailure
    def test_a_teleporter_never_costs_the_tracker_a_door(self):
        held = NOTHING._replace(skills=frozenset(SKILLS), ks=14)
        paths = preset_paths("Casual")
        lost = server_reach(held, paths) - server_reach(held._replace(tps=frozenset({"TPForlorn"})), paths)
        self.assertEqual(sorted(lost), [])


def grown(held, extra, closed):
    """held with extra's items added and every count at the larger of the two."""
    tps = held.tps | extra.tps
    if closed:
        tps -= {"TPGinso", "TPHoru"}
    return Held(held.skills | extra.skills, held.events | extra.events, tps,
                *(max(a, b) for a, b in zip(held[3:], extra[3:])))


# --- logic flags ---

def flag_line(paths, variations, key_mode):
    """A seed's first line as SeedGenParams.flag_line writes it, minus the multiplayer and fill flags."""
    flags = [get_preset_from_paths(presets, paths), key_mode] + [v for v in variations if v not in FLAGLESS]
    return ",".join(flags) + "|1234"


FLAGLESS = {Variation.WARMTH_FRAGMENTS.value, Variation.WORLD_TOUR.value}


def client_paths(meta):
    G.set("Randomizer.SeedMeta", meta)
    found = G.call("RandomizerLocationManager.InitializePaths")
    return None if found is None else set(found)


def written_as_custom(meta):
    return meta.startswith("Custom")


def glitched_preset(meta):
    return meta.startswith("Glitched,")


def enemy_rando_seed(variations):
    return Variation.ENEMY_RANDO.value in variations


def tracker_paths(paths, variations):
    """The path sets the tracker walks for a seed (web/tracker.py)."""
    return set(enemy_rando_paths(paths) if enemy_rando_seed(variations) else paths)


def any_paths():
    return st.one_of(st.sampled_from(sorted(presets)).map(preset_paths), path_sets())


class Flags(unittest.TestCase):
    @settings(max_examples=examples(150))
    @given(any_paths(), st.lists(st.sampled_from([v.value for v in Variation]), unique=True, max_size=6),
           st.sampled_from([k.value for k in KeyMode]))
    def test_a_seeds_flag_line_reads_back_as_the_path_sets_the_tracker_walks(self, paths, variations, key_mode):
        meta = flag_line(paths, variations, key_mode)
        if glitched_preset(meta):
            return
        if written_as_custom(meta):
            meta = "c" + meta[1:]
        expected, got = tracker_paths(paths, variations), client_paths(meta)
        self.assertIsNotNone(got, meta)
        if enemy_rando_seed(variations):
            got = set(enemy_rando_paths(got))
        self.assertEqual(sorted(got), sorted(expected), meta)

    @settings(max_examples=examples(100))
    @given(path_sets())
    def test_path_masks_are_the_servers(self, paths):
        mask = int(get_preset_from_paths({}, paths)[len("Custom"):])
        self.assertEqual(B.call("OriParse.PathSetToPathMask", paths), mask)
        self.assertEqual(sorted(B.call("OriParse.PathMaskToPathSet", mask)), sorted(paths))
        self.assertEqual(sorted(G.call("Protogen.OriParse.PathMaskToPathSet", mask)), sorted(paths))

    def test_no_other_flag_the_server_writes_reads_as_logic(self):
        others = [v.value for v in Variation] + [k.value for k in KeyMode] + [
            "Frags/3/5", "WorldTour=8", "mode=Shared", "share=keys+skills", "prefer_path_difficulty=Easy", "pool=Hard",
            "LegacyFill", "DeathLink", "anti_bk_bias=0.5", "sense=a+b", "lps=Casual-core+Standard-core"]
        self.assertEqual([f for f in others if G.call("RandomizerLocationManager.PathsForFlag", f) is not None], [])

    @unittest.expectedFailure
    def test_the_glitched_preset_reads_back_as_its_path_sets(self):
        self.assertEqual(client_paths("Glitched,Clues|1"), set(preset_paths("Glitched")))

    @unittest.expectedFailure
    def test_a_custom_flag_as_the_server_writes_it_reads_back(self):
        paths = preset_paths("Casual") + ["gjump"]
        self.assertEqual(client_paths(flag_line(paths, [], "Clues")), set(paths))

    @unittest.expectedFailure
    def test_an_enemy_rando_seed_walks_no_lure_path(self):
        self.assertEqual(client_paths("Standard,Clues,EnemyRando|1"), tracker_paths(preset_paths("Standard"), ["EnemyRando"]))


# --- keystone doors ---

def doors():
    """[source, destination, keysanity id, keysanity cost] per RandomizerLocationData.KeystoneDoors entry."""
    return G.call("GameGlue.KeysanityDoors")


def the_forlorn_door(source):
    return source == FORLORN_DOOR


class Doors(unittest.TestCase):
    def test_every_keystone_door_is_a_keystone_conn_in_areas_ori(self):
        costs, pos = G.get("RandomizerLocationManager.DoorCosts"), G.get("RandomizerLocationManager.DoorWirePos")
        reqs = {}
        for s, d, req in B.call("LogicGlue.Conns", AREAS, preset_paths("Casual")):
            reqs.setdefault((s, d), []).append(req.split(", "))
        for source, dest, _, _ in doors():
            if the_forlorn_door(source):
                continue
            self.assertIn(["Free", "Keystone=%d" % costs[pos[source]]], reqs.get((source, dest), []), (source, dest))

    def test_keysanity_ids_and_costs_are_the_servers(self):
        costs, pos = G.get("RandomizerLocationManager.DoorCosts"), G.get("RandomizerLocationManager.DoorWirePos")
        homes = get_areas()["homes"]
        shared = oride_module("shared").KEYSTONE_DOORS
        rb_by_name = generator.SeedGenerator.keysanityOutput
        for source, dest, ks_id, ks_cost in doors():
            wire = pos[source]
            self.assertEqual((ks_id - 300, ks_cost), (wire, costs[wire]), source)
            self.assertEqual((shared[wire][0], shared[wire][2]), (source, ks_cost), source)
            far = shared[wire][1]
            if not the_forlorn_door(source):
                self.assertEqual(far, dest, source)
            sanity = [p for p in homes[source]["conns"][far]["paths"] if "Keysanity" in p]
            self.assertEqual(len(sanity), 1, source)
            names = keysanity_map[[t for t in sanity[0][1:] if t != "Keysanity"][0]]
            self.assertEqual((len(names), {rb_by_name[n] for n in names}), (ks_cost, {"RB%d" % ks_id}), source)

    @settings(max_examples=examples(60))
    @given(preset_worlds(), st.integers(0, 11))
    def test_an_opened_door_reaches_no_further_than_paying_for_it(self, world, door):
        held, rest = world[0], world[1:]
        source, _, _, cost = doors()[door]
        if the_forlorn_door(source):
            return
        opened = client_reach(held, *rest, doors=[door])
        paid = client_reach(held._replace(ks=cost), *rest)
        self.assertEqual(sorted(opened - paid - progressive(opened)), [], (world, source))

    @unittest.expectedFailure
    def test_opening_the_forlorn_door_reaches_no_further_than_paying_for_it(self):
        door = [d[0] for d in doors()].index(FORLORN_DOOR)
        opened = client_reach(NOTHING, preset_paths("Casual"), spawn="Forlorn", doors=[door])
        self.assertIn(FORLORN_DOOR, opened)
        self.assertEqual(sorted(opened - client_reach(NOTHING._replace(ks=4), preset_paths("Casual"), spawn="Forlorn")), [])


# --- locations ---

def server_keys():
    """name -> the generator's Location key for every areas.ori loc, leaving the generator's tables as found."""
    saved = dict(generator.all_locations), set(generator.repeatable_locs)
    try:
        return {name: generator.Location(int(loc["x"]), int(loc["y"]), name, loc["item"], int(loc["difficulty"]),
                                         loc["zone"], 1).get_key() for name, loc in get_areas()["locs"].items()}
    finally:
        generator.all_locations.clear()
        generator.all_locations.update(saved[0])
        generator.repeatable_locs.clear()
        generator.repeatable_locs.update(saved[1])


def client_keys(culture=""):
    G.culture(culture)
    try:
        return G.call("GameGlue.LocationKeys")
    finally:
        G.culture("")


def comma_decimal_culture(culture):
    return G.call("GameGlue.DecimalSeparator", culture) != "."


CULTURES = ["", "en-US", "en-GB", "ja-JP", "de-DE", "fr-FR", "tr-TR", "ru-RU", "pt-BR", "es-ES", "it-IT", "pl-PL",
            "sv-SE", "nl-NL", "zh-CN", "ar-SA", "hi-IN", "th-TH"]


class Locations(unittest.TestCase):
    def test_client_location_keys_are_the_servers(self):
        client = client_keys()
        self.assertEqual({n: k for n, k in server_keys().items() if client.get(n) != str(k)}, {})

    def test_progressive_map_keys_are_the_servers(self):
        client = client_keys()
        # SeedGenerator.next_ms_loc
        self.assertEqual([int(client["Map%d" % i]) for i in range(1, 10)], [20 + 4 * (1 + n) for n in range(9)])

    def test_no_two_client_locations_share_a_key(self):
        client = client_keys()
        self.assertEqual(len(set(client.values())), len(client))

    @settings(max_examples=examples(18))
    @given(st.sampled_from(CULTURES))
    def test_location_keys_hold_in_any_culture(self, culture):
        if culture and comma_decimal_culture(culture):
            return
        self.assertEqual(client_keys(culture), client_keys(), culture)

    @unittest.expectedFailure
    def test_location_keys_do_not_depend_on_the_culture(self):
        self.assertEqual(client_keys("de-DE")["FirstPickup"], client_keys()["FirstPickup"])


# --- parsing ---

PARSE_BASE = None


def parse_base():
    """areas.ori's first 1200 lines: every loc and the Glades homes, Origin among them."""
    global PARSE_BASE
    if PARSE_BASE is None:
        PARSE_BASE = areas_lines()[:1200]
    return PARSE_BASE


def a_path_line_before_the_first_conn(lines):
    """A path line before the file's first conn: the client files it against no destination."""
    seen_conn = False
    for raw in lines:
        tokens = raw.split("--")[0].split()
        if not tokens:
            continue
        if tokens[0] in ("conn:", "pickup:"):
            seen_conn = True
        elif not tokens[0].endswith(":") and not seen_conn:
            return True
    return False


class Parse(unittest.TestCase):
    def test_areas_ori_parses_with_no_dangling_conn(self):
        with open(AREAS, encoding="utf-8") as f:
            self.assertEqual(B.call("LogicGlue.Dangling", f.read(), preset_paths("Master")), [])

    def test_both_parsers_read_the_same_conns_from_areas_ori(self):
        client = {(s, d) for s, d, _ in B.call("LogicGlue.Conns", AREAS, list(PATHSETS)) if not re.match(r"Map\d$", d)}
        tracker = {(home, target) for home, data in get_areas()["homes"].items() for target, conn in data["conns"].items()
                   if not conn["paths"] or any(hardest_pathset_from_tags(p[0]) for p in conn["paths"])}
        self.assertEqual((sorted(client - tracker), sorted(tracker - client)), ([], []))

    @settings(max_examples=examples(60))
    @given(st.data())
    def test_a_mutated_areas_ori_parses_to_a_graph_or_throws(self, data):
        lines = data.draw(areas_edits(parse_base()))
        if a_path_line_before_the_first_conn(lines):
            return
        text = "\n".join(lines)
        try:
            dangling = B.call("LogicGlue.Dangling", text, preset_paths("Master"))
        except CsError:
            return
        self.assertEqual(dangling, [], text[:200])
        B.call("LogicGlue.ReachText", text, preset_paths("Master"))

    @unittest.expectedFailure
    def test_a_path_line_before_any_conn_is_no_conn(self):
        text = "home: SunkenGladesRunaway\n\tcasual Free\n"
        self.assertEqual(B.call("LogicGlue.Dangling", text, preset_paths("Casual")), [])


# --- names both sides must share ---

class Names(unittest.TestCase):
    def test_every_spawn_starts_where_the_tracker_starts(self):
        client = G.get("RandomizerLocationManager.stupidBullshit")
        nodes = set(B.call("LogicGlue.Nodes", AREAS, preset_paths("Casual")))
        for zone, (x, y) in SPAWN_SPOTS.items():
            start = client.get("%d,%d,force" % (x, y))
            self.assertEqual(start, initial_area_by_spawn[zone], zone)
            self.assertIn(start, nodes, zone)

    def test_every_warp_target_is_a_logic_node(self):
        nodes = set(B.call("LogicGlue.Nodes", AREAS, preset_paths("Casual")))
        targets = {w[4] for group in warp_targets2 for w in group if w[4]}
        self.assertEqual(sorted(targets - nodes), [])

    def test_every_held_item_unlocks_the_name_the_tracker_gives_it(self):
        always = {"Free", "Rekindle", "Lure"}
        for name in TELEPORTERS:
            held = B.call("LogicGlue.Held", [], [], [TP_IDS[name]], 3, 0, 0, 0, 0)
            self.assertEqual(set(held["Unlocks"]) - always, {name})
        for name in EVENTS:
            held = B.call("LogicGlue.Held", [], [name], [], 3, 0, 0, 0, 0)
            self.assertEqual(set(held["Unlocks"]) - always, {name})
        for (code, pid), name in PlayerState.name_from_id.items():
            if code == "SK":
                self.assertEqual(G.call("GameGlue.AbilityName", pid), name)
                held = B.call("LogicGlue.Held", [name], [], [], 3, 0, 0, 0, 0)
                self.assertIn(name, held["Unlocks"])
