"""Server-side state for the netcode slice: players as the server stores them, rendered with the server's own code."""
import contextlib
from unittest import mock

import google.auth.credentials
from google.cloud import ndb
from hypothesis import strategies as st

from ap_models import wire_safe_name
from cache import Cache
from enums import ShareType
from models import Player, stacks
from pickups import Event, Skill, Teleporter, Upgrade
from seedbuilder.generator import warp_targets2

WARPS = [w for group in warp_targets2 for w in group]
SHARED_RB = sorted(i for i in Upgrade.names if Upgrade(i).share_type != ShareType.NOT_SHARED)
TREES = range(900, 910)
RELICS = range(911, 922)
BONUS_SKILLS = range(100, 130)


def warp_id(warp):
    """The TW id the seed generator writes for a warp target."""
    name, x, y, _area, node, _cost = warp
    return "Warp to %s,%s,%s,%s" % (name, x, y, node)


_client = None


@contextlib.contextmanager
def ndb_context():
    """An in-memory ndb context: entities build, nothing reaches a datastore."""
    global _client
    if _client is None:
        _client = ndb.Client(project="unit-test", credentials=google.auth.credentials.AnonymousCredentials())
    with _client.context():
        yield


def names_field(names):
    """Tick field 7 as mw_names_field builds it, from these players' raw names."""
    players = []
    for pid, raw in sorted(names.items()):
        p = Player(id="7.%s" % pid, nickname=raw or None)
        players.append(p)
    return ";".join("%s.%s" % (p.pid(), p.wire_name()) for p in players)


def render(state, mw):
    """Player.output for this state, with the names field the game's roster gives and no cache writes."""
    fields = dict(state["fields"])
    p = Player(id="7.%s" % state["pid"], **fields)
    names = names_field(state["names"]) if mw else None
    with mock.patch.object(Cache, "set_output", lambda *a: None), \
            mock.patch.object(Cache, "get_names", lambda *a: names), \
            mock.patch.object(Cache, "set_names", lambda *a: None):
        return p.output(include_slots=mw)


def bits_of(table):
    return st.sets(st.sampled_from(sorted(table.values()))).map(sum)


def bonus_counts(ids):
    return st.dictionaries(st.sampled_from(ids).map(str), st.integers(1, 4), max_size=8)


@st.composite
def bonuses(draw):
    """A bonuses dict as give_pickup leaves it: singletons at 1, stacks under their cap, warps at 1."""
    out = {}
    for key, cnt in draw(bonus_counts(SHARED_RB)).items():
        up = Upgrade(int(key))
        out[key] = min(cnt, up.max or cnt) if stacks(up) else 1
    for warp in draw(st.lists(st.sampled_from(WARPS), max_size=3, unique=True)):
        out[warp_id(warp)] = 1
    return out


wire_text = st.text(st.characters(exclude_categories=("Cs",)), max_size=16).map(wire_safe_name)


@st.composite
def signals(draw):
    """Signals in the shapes the server's producers write: every free text goes through a wire-safe filter."""
    kinds = st.one_of(
        wire_text.map(lambda t: "msg:@%s@" % t),
        st.tuples(st.sampled_from(["1st", "2nd", "13th"]), st.sampled_from(["00:12:03.5", "1:02:44"]))
        .map(lambda t: "win:$Finished in %s place at %s!" % t),
        st.just("stop"),
        st.tuples(st.integers(1, 99), wire_text).map(lambda t: "dl:%s;%s" % t),
        st.dictionaries(st.integers(0, 255), st.one_of(st.just(""), st.just("P2"), wire_text), min_size=1, max_size=3)
        .map(lambda d: "apfrom:" + ";".join("%s=%s" % kv for kv in sorted(d.items()))),
    )
    return draw(st.lists(kinds, max_size=4, unique=True))


slot_words = st.lists(st.one_of(st.just(0), st.integers(0, 2**32 - 1),
                                st.sets(st.integers(0, 31), max_size=4).map(lambda bs: sum(1 << b for b in bs))),
                      min_size=8, max_size=8)

extra_item = st.one_of(
    st.tuples(st.sampled_from(["HC", "EC", "AC", "KS", "MS"]), st.just("1")),
    st.tuples(st.just("EX"), st.integers(1, 300).map(str)),
    st.tuples(st.just("SK"), st.sampled_from(sorted(Skill.names)).map(str)),
    st.tuples(st.just("RB"), st.sampled_from(SHARED_RB).map(str)),
    st.tuples(st.just("TW"), st.sampled_from(WARPS).map(warp_id)),
    st.tuples(st.just("SH"), st.text(st.sampled_from("ab%,;|2C3B5 "), min_size=1, max_size=10)),
)


@st.composite
def player_states(draw, mw):
    """A Player as the server holds it after some shared finds; mw adds what only a multiworld body carries."""
    fields = {
        "skills": draw(bits_of(Skill.bits)),
        "events": draw(bits_of(Event.bits)),
        "teleporters": draw(bits_of(Teleporter.bits)),
        "bonuses": draw(bonuses()),
        "hints": draw(st.dictionaries(st.integers(-10**7, 10**7).map(str), st.integers(1, 6), max_size=3)),
        "signals": draw(signals()),
    }
    state = {"pid": draw(st.integers(1, 6)), "names": {}}
    if mw:
        fields["slot_bflds"] = draw(slot_words)
        fields["released"] = draw(st.booleans())
        fields["ap_hints"] = draw(st.dictionaries(st.integers(0, 255).map(str), wire_text, max_size=3))
        fields["extra_items"] = [[i, c, v] for i, (c, v) in enumerate(draw(st.lists(extra_item, max_size=4)))]
        state["names"] = draw(st.dictionaries(st.integers(1, 12), st.text(max_size=24), min_size=1, max_size=4))
    state["fields"] = fields
    return state
