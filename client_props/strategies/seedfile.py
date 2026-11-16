"""Strategies for seedfile_props: seedgen requests for flag lines, box models, enemy roll lines and modes."""
import math

from hypothesis import strategies as st

from enums import KeyMode, Variation, presets
from test.strategies.seedgen import enemy_opts

seed_names = st.text(alphabet="abcdefghijklmnopqrstuvwxyz0123456789", min_size=1, max_size=8)
SHARE_LABELS = ["Skills", "World Events", "Teleporters", "Upgrades", "Misc", "Saves"]
COOP_MODES = ["Co-op", "Multiworld", "Race", "SplitShards", "Bingo"]
SENSE_TOKENS = ["EV", "SK", "RB17", "RB28", "TPForlorn", "HC", "MS", "WT"]


@st.composite
def flag_json(draw):
    """A seedgen request reaching every variation, key mode and sync mode the server can write."""
    j = {"seed": draw(seed_names),
         "paths": [p.value for p in presets[draw(st.sampled_from(["Casual", "Standard", "Expert", "Master"]))]],
         "variations": draw(st.lists(st.sampled_from(sorted(v.value for v in Variation)), unique=True, max_size=10)),
         "keyMode": draw(st.sampled_from([k.value for k in KeyMode])),
         "players": draw(st.sampled_from([1, 1, 2, 3])), "tracking": draw(st.booleans())}
    if j["players"] > 1:
        j["coopGameMode"] = draw(st.sampled_from(COOP_MODES))
        j["syncShared"] = draw(st.lists(st.sampled_from(SHARE_LABELS), unique=True, max_size=3))
    if draw(st.integers(0, 3)) == 0:
        j["apMode"] = True
        j["apDeathLink"] = draw(st.booleans())
    if draw(st.booleans()):
        j["relicCount"] = draw(st.integers(1, 11))
    if draw(st.booleans()):
        j["fragCount"] = draw(st.integers(10, 60))
        j["fragReq"] = draw(st.integers(1, j["fragCount"]))
    if draw(st.integers(0, 3)) == 0:
        j["senseData"] = " ".join(draw(st.lists(st.sampled_from(SENSE_TOKENS), min_size=1, unique=True)))
    if draw(st.integers(0, 3)) == 0:
        j["pathDiff"] = draw(st.sampled_from(["Easy", "Hard"]))
    if draw(st.integers(0, 3)) == 0:
        j["fillAlg"] = "Classic"
    if "EnemyRando" in j["variations"] and draw(st.booleans()):
        j["enemyOpts"] = draw(enemy_opts())
    return j


BOX_FLAGS = ["kill", "once", "solid", "item", "ritem", "goal", "unsafe", "on=Tick", "on=Frame", "on=Enter",
             "on=Tick/30/60", "on=Tick/never", "damage=1", "damage=2.5/Lava/Player/normal", "renderDepth=-99",
             "parallaxDepth=10", "unlit"]
GIVES = ["", "", "SK|0", "EX|15", "RB|3", "SH|hello there", "SH|a|b", "MU|SK/0/EX/5", "BM|0", "BM|2=0", "TP|Grove",
         "WT|#Glades#", "NO|1"]
hex_digits = st.text(alphabet="0123456789abcdefABCDEF", min_size=2, max_size=2)


def flag_case(draw, flag):
    name, eq, value = flag.partition("=")
    return draw(st.sampled_from([name, name.upper(), name.capitalize()])) + eq + value


@st.composite
def colors(draw, hash_ok=True):
    kind = draw(st.sampled_from(["", "none", "0", "rgb", "rgba"]))
    if kind in ("rgb", "rgba"):
        text = "".join(draw(hex_digits) for _ in range(3 if kind == "rgb" else 4))
        return ("#" if hash_ok and draw(st.booleans()) else "") + text
    return kind


def page_number(x):
    """What the practice page sends for a typed or dragged corner: practice_editor.html num, JS Math.round."""
    v = x * 100
    r = math.floor(v)
    return (r + 1 if v - r >= 0.5 else r) / 100


corner = st.one_of(st.integers(-5000, 5000).map(float), st.floats(-5000, 5000, allow_nan=False, width=32),
                   st.integers(-500000, 500000).map(lambda n: n / 100))


@st.composite
def box_models(draw, hash_ok=True, deleted_ok=True, gives=GIVES):
    """A box as the plando builder holds one: {flags, box, color, give}."""
    flags = [flag_case(draw, f) for f in draw(st.lists(st.sampled_from(BOX_FLAGS), unique=True, max_size=4))]
    if deleted_ok and draw(st.integers(0, 9)) == 0:
        flags.append(draw(st.sampled_from(["tombstone", "none"])))
    return {"flags": ",".join(flags), "box": [draw(corner) for _ in range(4)], "color": draw(colors(hash_ok)),
            "give": draw(st.sampled_from(gives))}


SHAPE_WORDS = ["convex", "Convex", "CONVEX", "loop", "Loop", "LOOP", "previewLoop", "PREVIEWLOOP"]
NUMBERS = ["0", "1", "-2.5", "+3", ".5", "5.", " 4", "4 ", "007", "-0", "123.45", "-4000"]
NOT_NUMBERS = ["", "x", "1e3", "1.2.3", "0x10", "--1", "1 2", "+", "."]
hundredth = st.integers(-500000, 500000).map(lambda n: n / 100)


@st.composite
def shape_fields(draw, corners=True):
    """A box line's third field, near the grammar: corners, a convex or loops, now and then with a wrong token."""
    number = st.sampled_from(NUMBERS if draw(st.integers(0, 2)) else NUMBERS + NOT_NUMBERS)
    word = draw(st.sampled_from(["", ""] * corners + SHAPE_WORDS + ["hull", " convex", "loop "]))
    if not word:
        return ",".join(draw(st.lists(number, min_size=4 if draw(st.integers(0, 4)) else 3, max_size=4)))
    parts = [word]
    for k in range(draw(st.integers(1, 3))):
        if k:
            parts.append(draw(st.sampled_from(["loop", "Loop", "LOOP"])))
        points = draw(st.integers(3, 5)) if draw(st.integers(0, 3)) else draw(st.integers(0, 2))
        parts += draw(st.lists(number, min_size=2 * points, max_size=2 * points + draw(st.integers(0, 4)) // 4))
    return ",".join(parts)


@st.composite
def shapes(draw):
    """A convex or loop field the game reads, its points at hundredths."""
    points = st.lists(st.tuples(hundredth, hundredth), min_size=3, max_size=6)
    loop = lambda: ",".join("%r,%r" % p for p in draw(points))
    if draw(st.booleans()):
        return draw(st.sampled_from(["convex", "Convex"])) + "," + loop()
    return ",".join(draw(st.sampled_from(["loop", "LOOP"])) + "," + loop() for _ in range(draw(st.integers(1, 3))))


quarter = st.integers(-48, 48).map(lambda n: n / 4)


@st.composite
def drawn_shapes(draw):
    """A convex or loop field on quarter units, small enough to sample points of: (field, kind, loops)."""
    kind = draw(st.sampled_from(["convex", "loop"]))
    loops = draw(st.lists(st.lists(st.tuples(quarter, quarter), min_size=3, max_size=7), min_size=1,
                          max_size=1 if kind == "convex" else 3))
    return ",".join(kind + "," + ",".join("%r,%r" % p for p in l) for l in loops), kind, loops


# sixteenths between the quarter lines, so few of them land on an outline
spots = st.lists(st.tuples(st.integers(-100, 100), st.integers(-100, 100)).map(
    lambda p: ((2 * p[0] + 1) / 16, (2 * p[1] + 1) / 16)), min_size=1, max_size=12)


ROLL_ODDITIES = ["", "0", "-1", "-5", "+5", " 5", "5 ", "05", "x", "b", "b0", "b3", "b-3", "b+3", "b 3", "1.0", "5_0",
                 "99999999999", "b99999999999", "b2147483647", "b2147483646", chr(0x663), "b" + chr(0x663)]


@st.composite
def rolls_lines(draw, rolls, version, variants):
    """An //EnemyRolls= line near a rolled one: a few tokens swapped for odd ones, sometimes off in version or length."""
    tokens = ["" if r is None else str(r) for r in rolls]
    for _ in range(draw(st.integers(0, 3))):
        i = draw(st.integers(0, len(tokens) - 1))
        tokens[i] = draw(st.sampled_from(ROLL_ODDITIES + [str(variants - 1), str(variants), "b%d" % variants]))
    if draw(st.integers(0, 9)) == 0:
        tokens = tokens[:-1] if draw(st.booleans()) else tokens + [""]
    if draw(st.integers(0, 9)) == 0:
        version = draw(st.sampled_from(["", "0" + version, " " + version, version.upper()]))
    return "//EnemyRolls=" + version + ":" + ",".join(tokens)


MODE_ODDITIES = ["zone", "vanilla", "Zone", "VANILLA", "0.5-2", "1-4", "0.25-0.25", "4-4", "0.1-5", "2-1", "0.5--1",
                 "1e-1-2", "-1-2", "abc", "0.5-", "-2", "1-2-3", " 0.5-2", "0.5 - 2"]
