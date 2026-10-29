"""The real game reads every seed the website serves: no bad lines, every placement where the server put it.

Needs ORI_HARNESS=1, a harness build running at the title screen on the test seed (ori.ps1 launch), its oridbg.py
(ORI_HARNESS_TOOLS), and the private emulator the suite uses for get_seed (DATASTORE_EMULATOR_HOST, default
127.0.0.1:8032)."""
import logging
import os
import sys
import time
import unittest
import uuid

from hypothesis import given, note, settings

from client_props.base import HERE, REPO, examples
from enums import presets
from test.strategies import seedgen as S

HARNESS = os.environ.get("ORI_HARNESS") == "1"
GAME = os.environ.get("ORI_GAME_DIR", r"E:\SteamLibrary\steamapps\common\Ori DE")
WORK = os.path.join(HERE, "work", "ingame")
LOG = os.path.join(GAME, "randomizer.log")
COMPLAINTS = ("Couldn't parse", "Error parsing", "rror", "xception")

TOOLS = os.environ.get("ORI_HARNESS_TOOLS", os.path.join(os.path.dirname(REPO), "prior_notes", "tools", "ori_harness"))
sys.path.insert(0, TOOLS)
_ndb = None


def dbg(line, timeout=30.0):
    import oridbg
    reply = oridbg.send(line, timeout)
    first, _, rest = reply.partition("\n")
    if first != "ok":
        raise AssertionError("harness %r: %s" % (line, reply[:400]))
    return rest.strip()


def log_since(marker):
    size = os.path.getsize(LOG)
    with open(LOG, "rb") as f:
        f.seek(max(0, size - 4 * 1024 * 1024))
        text = f.read().decode("utf-8", "replace")
    at = text.rfind(marker)
    return text[at + len(marker):] if at >= 0 else None


def unquote(reply):
    return reply[1:-1] if len(reply) >= 2 and reply[0] == reply[-1] == '"' else reply


def load(path, expect_fail=False):
    """Loads a seed file the way Alt+L does; returns the log lines the load wrote."""
    # the harness's set keeps quote marks, so the path goes bare (and so may hold no spaces)
    assert " " not in path, path
    tag = uuid.uuid4().hex[:10]
    dbg("log start %s" % tag)
    dbg("set Randomizer.SeedFilePath %s" % path)
    dbg("call Randomizer.initialize()", timeout=60)
    # a load that throws or can't find the file puts the default path back
    if not expect_fail:
        assert unquote(dbg("eval Randomizer.SeedFilePath")) == path, "the game didn't load %s" % path
    dbg("log end %s" % tag)
    for _ in range(100):
        body = log_since("harness: start %s" % tag)
        if body is not None and ("harness: end %s" % tag) in body:
            return [l for l in body.split("harness: end %s" % tag)[0].splitlines() if l.strip()]
        time.sleep(0.1)
    raise AssertionError("the log never showed the load's end marker")


def table(keys):
    """{key: pickup text, "" when empty} for the keys the client has a location for."""
    import oridbg
    out = {}
    for k in keys:
        reply = oridbg.send("eval RandomizerLocationManager.LocationsByKey.get_Item(%d).Pickup.ToString()" % k)
        first, _, rest = reply.partition("\n")
        if first == "ok":
            out[k] = unquote(rest.strip())
        elif "NullReference" in reply:
            out[k] = ""
        elif "KeyNotFound" not in reply:
            raise AssertionError("harness read of %d: %s" % (k, reply[:300]))
    return out


def placements(lines):
    """{coords: "code|id"} for the seed's map locations: not the spawn, the manifest, meta or box lines."""
    out = {}
    for line in lines[1:]:
        if not line.strip() or line.startswith("//") or line.startswith("BX|"):
            continue
        p = line.split("|")
        coords = int(p[0])
        if p[1] == "EN" or coords == 2 or -257 <= coords <= -2:
            continue
        out[coords] = p[1] + "|" + p[2]
    return out


def served(j):
    """{world: seed text} as the website's download serves it, or None when the roll fails."""
    global _ndb
    import google.auth.credentials
    from google.cloud import ndb
    from seedbuilder.seedparams import SeedGenParams
    os.environ.setdefault("DATASTORE_EMULATOR_HOST", "127.0.0.1:8032")
    if _ndb is None:
        _ndb = ndb.Client(project="unit-test", credentials=google.auth.credentials.AnonymousCredentials())
    old = logging.root.manager.disable
    logging.disable(logging.CRITICAL)
    try:
        with _ndb.context():
            params = SeedGenParams.from_json(j, store=False)
            if not params.generate(store=False):
                return None
            return {p: params.get_seed(p, include_sync=False) for p in range(1, params.players + 1)}
    finally:
        logging.disable(old)


def write(name, text):
    os.makedirs(WORK, exist_ok=True)
    path = os.path.join(WORK, name)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return path


_pool = []


def request(**kw):
    """A page request with the page's default pool: from_json without an itemPool never generates."""
    if not _pool:
        _pool.append(S.page_pool(None))
    name, pool = _pool[0]
    j = {"seed": "client-props", "paths": [p.value for p in presets["Standard"]], "variations": [], "keyMode": "Clues",
         "players": 1, "tracking": True, "itemPool": pool, "selectedPool": name}
    j.update(kw)
    return j


@unittest.skipUnless(HARNESS, "in-game slice: ORI_HARNESS=1 with a harness build running")
class ServedSeeds(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if dbg("eval Randomizer.Sync").lower() != "false":
            raise unittest.SkipTest("the running game has a sync seed loaded; not loading seeds over it")

    @classmethod
    def tearDownClass(cls):
        dbg("set Randomizer.SeedFilePath randomizer.bfr")
        dbg("call Randomizer.initialize()", timeout=60)

    def load_served(self, j, world=1):
        worlds = served(j)
        self.assertIsNotNone(worlds, "the request should generate")
        lines = load(write("served_%d.bfr" % world, worlds[world]))
        return worlds[world], lines

    def check_world(self, text, logged):
        self.assertEqual([l for l in logged if any(c in l for c in COMPLAINTS)], [], "the load complained")
        want = placements(text.splitlines())
        got = table(want)
        self.assertEqual({c: v for c, v in want.items() if c not in got}, {}, "seed coords the client has no location for")
        self.assertEqual({c: (got[c], v) for c, v in want.items() if c in got and got[c] != v}, {},
                         "locations holding something other than what the seed says")

    @settings(max_examples=examples(12))
    @given(S.web_json())
    def test_every_served_world_loads_whole(self, j):
        note("SeedGenParams.from_json(%r)" % j)
        worlds = served(j)
        if worlds is None:
            return
        for p, text in worlds.items():
            self.check_world(text, load(write("served_%d.bfr" % p, text)))

    def test_flags_the_server_writes_reach_the_client(self):
        text, logged = self.load_served(request(variations=["OHKO", "ForceTrees", "0XP"]))
        self.check_world(text, logged)
        for flag in ("OHKO", "ForceTrees", "ZeroXP"):
            self.assertEqual(dbg("eval Randomizer.%s" % flag).lower(), "true", flag)

    @unittest.expectedFailure
    def test_non_progressive_map_stones_reach_the_client(self):
        self.load_served(request(variations=["NonProgressMapStones"]))
        self.assertEqual(dbg("eval Randomizer.ProgressiveMapStones").lower(), "false")

    def test_glitched_seeds_get_logic(self):
        self.load_served(request(paths=[p.value for p in presets["Glitched"]]))
        self.assertIsNotNone(wait_for_paths(), "the client found no logic paths for the seed")

    def test_custom_logic_seeds_get_logic(self):
        text, _ = self.load_served(request(paths=["casual-core", "casual-dboost", "standard-core", "dbash"]))
        self.assertTrue(any(f.startswith("Custom") for f in text.split("|")[0].split(",")), text.splitlines()[0])
        self.assertIsNotNone(wait_for_paths(), "the client found no logic paths for the seed")

    @unittest.expectedFailure
    def test_a_seed_that_fails_at_its_header_keeps_nothing_of_the_last_one(self):
        text, _ = self.load_served(request())
        first, rest = text.split("\n", 1)
        load(write("split_shards.bfr", first.replace("|", ",mode=SplitShards|", 1) + "\n" + rest), expect_fail=True)
        held = {k: v for k, v in table(placements(text.splitlines())).items() if v}
        self.assertEqual(held, {}, "locations still hold the last seed's items")

    def test_ap_key_tiers_survive_the_load(self):
        text, _ = self.load_served(request(apMode=True))
        self.assertIn("//KeyTiers=", text)
        self.assertNotEqual(dbg("eval RandomizerLocationManager.KeyTiers").lower(), "null")


@unittest.skipUnless(HARNESS, "in-game slice: ORI_HARNESS=1 with a harness build running")
class Runtime(unittest.TestCase):
    """What Mono does where the offline bridge (.NET 4.8) can't speak for it."""

    def test_the_game_takes_the_os_culture(self):
        # so every current-culture parse in the client reads the player's regional format
        import subprocess
        os_name = subprocess.run(["powershell", "-NoProfile", "-Command", "(Get-Culture).Name"],
                                 capture_output=True, text=True).stdout.strip()
        self.assertEqual(unquote(dbg("eval System.Globalization.CultureInfo.CurrentCulture.Name")), os_name)

    def test_mono_reads_a_decimal_point_as_a_group_separator_under_de_de(self):
        de = dbg('call System.Globalization.CultureInfo.GetCultureInfo("de-DE")').split()[-1]
        before = dbg("eval System.Threading.Thread.CurrentThread.CurrentCulture").split()[-1]
        try:
            dbg("call System.Threading.Thread.CurrentThread.set_CurrentCulture(%s)" % de)
            self.assertEqual(dbg('eval System.Single.Parse("92.0")'), "920")
        finally:
            dbg("call System.Threading.Thread.CurrentThread.set_CurrentCulture(%s)" % before)

    def test_mono_keeps_a_trailing_dot_in_a_request_url(self):
        url = unquote(dbg('call RandomizerSyncManager.RequestUrl("http://127.0.0.1/callback/dl%3A1%3BJr.")'))
        self.assertTrue(url.endswith("Jr."), url)

    def test_mono_sends_a_question_mark_in_a_find_id_raw(self):
        # so the server reads everything after it as the query: the http lane's id can't carry one
        url = unquote(dbg('call RandomizerSyncManager.RequestUrl("http://127.0.0.1/found/1/MU/SH/Why?/SK/0?zone=Glades")'))
        self.assertEqual(url, "http://127.0.0.1/found/1/MU/SH/Why?/SK/0?zone=Glades")


def wait_for_paths():
    """The logic paths the last load settled on, or None; InitializeLogic records them once areas.ori is in hand."""
    assert dbg("eval RandomizerLocationManager.HaveDownloadedAreas") == "true"
    paths = dbg("eval RandomizerLocationManager.s_lastLogicPaths")
    return None if paths.lower() == "null" else paths
