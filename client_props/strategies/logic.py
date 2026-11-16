"""Strategies for logic_props.py: held items as the server's tracker names them, path sets and areas.ori edits."""
from collections import namedtuple

from hypothesis import strategies as st

from enums import presets

SKILLS = ("Bash", "ChargeFlame", "WallJump", "Stomp", "DoubleJump", "ChargeJump", "Climb", "Glide", "Dash", "Grenade")
EVENTS = ("GinsoKey", "Water", "ForlornKey", "Wind", "HoruKey")
TELEPORTERS = ("TPGrove", "TPSwamp", "TPGrotto", "TPValley", "TPForlorn", "TPSorrow", "TPGinso", "TPHoru", "TPBlackroot",
               "TPGlades")
SPAWNS = ("Glades", "Grove", "Swamp", "Grotto", "Forlorn", "Valley", "Horu", "Ginso", "Sorrow", "Blackroot")
PATHSETS = ("casual-core", "casual-dboost", "standard-core", "standard-dboost", "standard-lure", "standard-abilities",
            "expert-core", "expert-dboost", "expert-lure", "expert-abilities", "dbash", "master-core", "master-dboost",
            "master-lure", "master-abilities", "gjump", "glitched", "timed-level", "insane")
PRESETS = ("Casual", "Standard", "Expert", "Master")

# hc counts pickups: both sides start a run with three cells
Held = namedtuple("Held", "skills events tps hc ec ks ms ac")


@st.composite
def helds(draw, ks=st.just(0), ms=st.integers(0, 7)):
    """A held-item set; each named group is drawn as a subset or its complement, so near-full sets are common."""
    def group(names):
        picked = draw(st.sets(st.sampled_from(names)))
        return frozenset(set(names) - picked if draw(st.booleans()) else picked)
    return Held(group(SKILLS), group(EVENTS), group(TELEPORTERS), draw(st.integers(0, 12)), draw(st.integers(0, 15)),
                draw(ks), draw(ms), draw(st.integers(0, 33)))


def preset_paths(name):
    return sorted(lp.value for lp in presets[name])


def path_sets():
    """Any path set the site can ask for: casual-core always, anything else on top."""
    return st.lists(st.sampled_from(PATHSETS[1:]), unique=True).map(lambda rest: sorted(["casual-core"] + rest))


@st.composite
def areas_edits(draw, lines):
    """areas.ori's lines with a few lines dropped, duplicated, swapped, token-mangled or added."""
    out = list(lines)
    junk = st.sampled_from(["home:", "conn:", "pickup:", "loc:", "casual", "expert", "Keystone=", "Health=x", "=", "--",
                            "Mapstone", "Free", "SunkenGladesRunaway", "\t", "Ability=0"])
    for _ in range(draw(st.integers(1, 6))):
        if not out:
            out.append(draw(st.text(min_size=1, max_size=5)))
            continue
        i = draw(st.integers(0, len(out) - 1))
        op = draw(st.sampled_from(["drop", "dup", "swap", "token", "add"]))
        if op == "drop":
            del out[i]
        elif op == "dup":
            out.insert(i, out[i])
        elif op == "swap":
            j = draw(st.integers(0, len(out) - 1))
            out[i], out[j] = out[j], out[i]
        elif op == "token":
            parts = out[i].split(" ")
            parts[draw(st.integers(0, len(parts) - 1))] = draw(junk)
            out[i] = " ".join(parts)
        else:
            out.insert(i, " ".join(draw(st.lists(junk, min_size=1, max_size=4))))
    return out
