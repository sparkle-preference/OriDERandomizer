"""Strategies for the pickups slice: what the server emits, cross-world lines, RI slot expressions and inventories."""
from hypothesis import strategies as st

from pickups import Upgrade
from seedbuilder.relics import relics
from test.strategies.wire import CODES, line_text, raw_multi, real_pickup, slashy_text
from util import compose_multi_value

INT_MIN, INT_MAX = -2 ** 31, 2 ** 31 - 1

# integer ids out to the int edges, plus the spellings int.Parse treats specially
int_text = st.one_of(
    st.integers(-300, 5000).map(str),
    st.integers(INT_MIN, INT_MAX).map(str),
    st.sampled_from(["-2147483647", "2147483647", "2147483648", "-2147483649", "-0", "+5", " 5", "5 ", "007", "1e3"]),
)
any_id = st.one_of(int_text, slashy_text, line_text, raw_multi, st.text(max_size=12))
any_code = st.one_of(st.sampled_from(CODES + ["AP", "NB", ""]), st.text(st.sampled_from("ABHKMPRSWX|/ "), max_size=3))

relic = st.sampled_from([(n, f) for zone in relics.values() for n, f in zone]).map(
    lambda nf: ("WT", "#%s#\\n%s" % nf))
removal = st.one_of(
    st.tuples(st.sampled_from(["HC", "EC", "AC", "KS", "MS"]), st.just("-1")),
    st.tuples(st.just("RB"), st.sampled_from(sorted(i for i in Upgrade.names if i > 0)).map(lambda i: "-%d" % i)),
    st.tuples(st.just("EX"), st.integers(1, 500).map(lambda n: "-%d" % n)),
)

# AP item names come from the room: commas are common, bars and line breaks can't reach a seed line
ap_item = st.tuples(st.just("AP"), st.one_of(st.sampled_from(["Hush, Hush", "AP Item #3", "Key, Big, Red", ""]), line_text))
recipient = st.one_of(st.just(""), st.integers(1, 9).map("P{}".format), st.text(st.sampled_from("Ab1 _.'-"), max_size=6))


@st.composite
def mw_line(draw, players=None):
    """(players, (code, id), value) for a cross-world line as the server writes it: plain, or an AP reserved one."""
    players = draw(st.integers(1, 4)) if players is None else players
    code, id = draw(st.one_of(real_pickup, removal, ap_item))
    slot = draw(st.integers(0, 255))
    if draw(st.booleans()):
        owner = draw(st.integers(1, players))
        return players, (code, id), "%d,%d,%s,%s" % (owner, slot, code, id)
    shadow = players + draw(st.integers(1, players))
    own = draw(st.one_of(st.just(-1), st.integers(0, 255)))
    if draw(st.integers(0, 3)) == 0:
        # unscouted, as conversion writes it
        code, id, who, own = "AP", "AP Item #%d" % (slot + 1), "", -1
    else:
        who = draw(recipient)
    return players, (code, id), "%d,%d,%s,%d,%s,%s" % (shadow, slot, who, own, code, id)


# RI and BM as plando authors write them
ri_text = st.one_of(
    st.builds("{}={}".format, st.integers(8000, 8010), st.integers(-5, 50)),
    st.builds("{}{}={}".format, st.integers(8000, 8010), st.sampled_from("+-*/%"), st.integers(-5, 50)),
    st.builds("{}=({{{}}}>={})".format, st.integers(8000, 8010), st.integers(8000, 8010), st.integers(0, 9)),
    st.builds("{}+={{{}}}".format, st.integers(20000, 20010), st.integers(8000, 8010)),
    st.builds("{}={{{}}}*2 - 1".format, st.integers(20000, 20010), st.integers(8000, 8010)),
    st.builds("{}={{{}}}/2".format, st.integers(20000, 20010), st.integers(8000, 8010)),
)
bm_text = st.one_of(st.integers(0, 40).map(str),
                    st.builds("{}={}".format, st.integers(0, 40), st.sampled_from(
                        ["0", "1", "{8000}", "(5=={8000})", "({8000}>=3) AND NOT ({8001}==0)", "({1}/2 > 0) or (0 > 1)"])))

plain_emitted = st.one_of(
    real_pickup, removal, relic, st.just(("NO", "1")),
    st.tuples(st.just("RI"), ri_text), st.tuples(st.just("BM"), bm_text),
    mw_line().map(lambda m: ("MW", m[2])),
)


@st.composite
def emitted(draw):
    """(code, id, pieces) for a pickup the server can write into a seed, multipickups included."""
    if draw(st.integers(0, 2)):
        code, id = draw(plain_emitted)
        return code, id, [(code, id)]
    ps = draw(st.lists(plain_emitted, min_size=1, max_size=5))
    return draw(st.sampled_from(["MU", "RP"])), compose_multi_value(ps), ps


# slot ids at every range edge Writable draws, plus room for overflow
EDGE_SLOTS = [0, 1, 5, 939, 940, 947, 948, 949, 1699, 1700, 1999, 2000, 3999, 4000, 8000, 8001, 9999,
              10000, 19999, 20000, 20001, INT_MAX, -1, -5, INT_MIN]
EDGE_VALUES = [0, 1, -1, 2, 7, -7, 100, 65536, INT_MAX, INT_MIN, INT_MAX - 1, INT_MIN + 1]
HELD = [0, 5, 940, 948, 1700, 8000, 8001, 10000, 11000, 20000]

slot_id = st.one_of(st.sampled_from(EDGE_SLOTS), st.sampled_from(HELD), st.integers(-3, 25000), st.integers(INT_MIN, INT_MAX))
number = st.one_of(st.sampled_from(EDGE_VALUES), st.integers(-50, 50), st.integers(INT_MIN, INT_MAX),
                   st.integers(-2 ** 33, 2 ** 33))
inventory = st.dictionaries(st.sampled_from(HELD), st.one_of(st.sampled_from(EDGE_VALUES), st.integers(-9, 9)), max_size=6)
pad = st.sampled_from(["", "", "", " ", "  ", "\t", "\N{NO-BREAK SPACE}"])
# characters that should break a slot expression without crashing it
noise = st.sampled_from("x(){}=<>!+-*/%, 0\N{SOFT HYPHEN}\N{ARABIC-INDIC DIGIT ZERO}\N{FULLWIDTH DIGIT FIVE}")


@st.composite
def literal(draw, n=number):
    v = draw(n)
    sign = "-" if v < 0 else draw(st.sampled_from(["", "", "+"]))
    return draw(pad) + sign + "0" * draw(st.integers(0, 2) | st.just(0)) + str(abs(v)) + draw(pad)


@st.composite
def operand(draw):
    if draw(st.booleans()):
        return draw(literal())
    return draw(pad) + "{" + draw(pad) + draw(literal(slot_id)).strip() + draw(pad) + "}" + draw(pad)


@st.composite
def _mangle(draw, text):
    """One in four gets a stray character, or loses one."""
    roll = draw(st.integers(0, 7))
    if roll == 0 or not text:
        at = draw(st.integers(0, len(text)))
        return text[:at] + draw(noise) + text[at:]
    if roll == 1:
        at = draw(st.integers(0, len(text) - 1))
        return text[:at] + text[at + 1:]
    return text


@st.composite
def legacy_value_expr(draw):
    """The right of an RI or BM = before math: a number, {n}, or (a OP b)."""
    if draw(st.integers(0, 2)):
        text = draw(operand())
    else:
        op = draw(st.sampled_from(["==", "!=", "<=", ">=", "<", ">"]))
        text = draw(pad) + "(" + draw(operand()) + op + draw(operand()) + ")" + draw(pad)
    return draw(_mangle(text))


@st.composite
def decimal_literal(draw):
    """A number with one to eight decimals, now and then none or nine."""
    places = draw(st.sampled_from([1, 2, 8, 1, 2, 3, 5, 7, 8, 0, 9]))
    return draw(literal(st.integers(-99999, 99999))).rstrip() + "." + "".join(
        draw(st.sampled_from("0123456789")) for _ in range(places)) + draw(pad)


# the client's RandomizerInventory.Actions, in order
ACTIONS = ["Jump", "SpiritFlame", "Bash", "SoulFlame", "ChargeJump", "Glide", "Grab", "Dash", "Grenade", "Left", "Right",
           "Up", "Down", "Climb"]


@st.composite
def box_read(draw):
    """{Bn}, {Bn.x}, {Bn.y}, {Bn.xi}, {Bn.yi} or {Bn.r}, any case, some on boxes the fake seed lacks."""
    n = draw(st.one_of(st.integers(0, 7), st.sampled_from(["-1", "+2", "4500", "99999999999"])))
    part = draw(st.sampled_from(["", "", ".x", ".y", ".X", ".Y", ".z", ".", ".xi", ".yI", ".XI", ".r", ".R", ".ri", ".i", ".xj"]))
    return draw(pad) + "{" + draw(pad) + draw(st.sampled_from("Bb")) + str(n) + part + draw(pad) + "}" + draw(pad)


@st.composite
def special_read(draw):
    """{T}, {Pos.x}, {Pos.y} or an action like {[Jump]}, any case on every letter, now and then misspelled."""
    action = st.sampled_from(ACTIONS + ["Jumps", "Spirit Flame", "B", "Ba", "Bash1", "Dash.x", ""]).flatmap(
        lambda a: word_case.map(lambda c: c(a)))
    name = draw(st.one_of(
        st.sampled_from(["T", "t", "Pos.x", "Pos.y", "pos.X", "POS.y", "pOs.Y", "Pos.z", "Pos", "T1", "Pos. x"]),
        action.flatmap(lambda a: st.sampled_from(["[%s]", "[%s]", "[ %s ]", "%s", "[%s", "%s]", "[[%s]]"]).map(lambda f: f % a))))
    return draw(pad) + "{" + draw(pad) + name + draw(pad) + "}" + draw(pad)


word_case = st.sampled_from([str.upper, str.upper, str.lower, str.title, lambda w: w.title().swapcase()])
# angles: exact values, a turn or more out, a hair off one, reads and more trig
ANGLES = ["0", "30", "-30", "45", "90", "180", "270", "390", "-720.5", "0.00000001", "359.99999999", "{T}*6",
          "cos(0)*90", "sin({8000})"]
# typeable numbers in units of 1e-8, tiny to the int edge
eight_places = st.one_of(st.integers(-10 ** 4, 10 ** 4), st.integers(-10 ** 9, 10 ** 9),
                         st.integers(-INT_MAX * 10 ** 8, INT_MAX * 10 ** 8))
# typeable angles in units of 1e-8 degrees, often a hair off a multiple of 15
degrees = st.one_of(st.integers(-720 * 10 ** 8, 720 * 10 ** 8), st.integers(-INT_MAX * 10 ** 8, INT_MAX * 10 ** 8),
                    st.builds(lambda k, d: 15 * k * 10 ** 8 + d, st.integers(-48, 48), st.sampled_from([0, 0, 1, -1])))


@st.composite
def trig(draw, depth):
    """sin(math) or cos(math), any case, the math maybe holding more of them."""
    name = draw(word_case)(draw(st.sampled_from(["sin", "cos"])))
    inner = draw(st.sampled_from(ANGLES)) if draw(st.booleans()) else draw(math_expr(depth - 1))
    return draw(pad) + name + draw(st.sampled_from(["", "", " "])) + "(" + inner + ")" + draw(pad)


@st.composite
def math_expr(draw, depth=1):
    """Numbers (some with decimals), reads, sin and cos, some negated, joined by + - * / %; divisors are often 0 or -1."""
    def atom():
        if depth > 0 and draw(st.integers(0, 4)) == 0:
            text = draw(trig(depth))
        else:
            text = draw(st.one_of(operand(), operand(), decimal_literal(), box_read(), special_read()))
        if draw(st.integers(0, 3)):
            return text
        return draw(pad) + draw(st.sampled_from("-+")) + text.lstrip()
    text = atom()
    for _ in range(draw(st.integers(0, 3))):
        op = draw(st.sampled_from("+-*/%"))
        text += op + (draw(st.sampled_from([" 0", "-1", "{8000}"])) if draw(st.integers(0, 3)) == 0 else atom())
    return text


@st.composite
def condition(draw):
    """(a OP b) comparisons, a and b math, each maybe NOT, joined by AND and OR."""
    def clause():
        nots = "".join(draw(word_case)("not") + draw(st.sampled_from([" ", "  ", ""])) for _ in range(draw(st.integers(0, 2))))
        op = draw(st.sampled_from(["==", "!=", "<=", ">=", "<", ">"]))
        return draw(pad) + nots + "(" + draw(math_expr()) + op + draw(math_expr()) + ")" + draw(pad)
    text = clause()
    for _ in range(draw(st.integers(0, 3))):
        text += draw(st.sampled_from([" ", ""])) + draw(word_case)(draw(st.sampled_from(["and", "or"]))) + " " + clause()
    return text


@st.composite
def value_expr(draw):
    """The right of an RI or BM =: math on numbers, reads, sin and cos, or comparisons joined by NOT, AND and OR."""
    text = draw(st.one_of(legacy_value_expr(), math_expr(), condition()))
    if draw(st.integers(0, 5)) == 0:
        at = draw(st.integers(0, len(text)))
        text = text[:at] + draw(st.sampled_from([" AND ", " or ", "NOT ", "(", ")", "x", "{8000} AND ", "sin(", "cos(0)",
                                                 "tan(1)"])) + text[at:]
    return draw(_mangle(text))


@st.composite
def br_stmt(draw):
    """A BR value: n, or n=x,y,r with = += or -=, values left out or empty, mostly on the fake seed's boxes."""
    n = draw(st.one_of(st.integers(0, 7).map(str), st.sampled_from(["-1", " 3 ", "x", "4500"])))
    if draw(st.integers(0, 5)) == 0:
        return draw(_mangle(n))
    op = draw(st.sampled_from(["", "", "+", "-"]))
    value = st.one_of(value_expr(), value_expr(), st.sampled_from(["", " ", "90", "-90", "360", "720.004", "359.995"]))
    xy = ",".join(draw(st.lists(value, min_size=1, max_size=4)))
    return draw(_mangle(n + draw(pad) + op + "=" + xy))


# a fake seed's boxes: middles in hundredths, deleted, off
boxes = st.lists(st.tuples(
    st.one_of(st.integers(-500000, 500000), st.sampled_from([0, 999999999, -999999999, 10 ** 9])),
    st.one_of(st.integers(-500000, 500000), st.sampled_from([0, 999999999, -10 ** 9])),
    st.sampled_from([False, False, False, True]), st.booleans()), max_size=6)
# moved boxes' offsets in hundredths; moving a box back removes its entry, so none sits at (0, 0)
offset = st.one_of(st.integers(-10 ** 6, 10 ** 6), st.sampled_from([INT_MAX, INT_MIN]))
box_offsets = st.dictionaries(st.integers(0, 7), st.tuples(offset, offset).filter(lambda xy: xy != (0, 0)), max_size=4)
# turned boxes' turns in hundredths of a degree; an upright box has no entry
box_rotations = st.dictionaries(st.integers(0, 7), st.one_of(st.integers(1, 35999), st.sampled_from([1, 9000, 35999])),
                                max_size=4)
# which of the fake seed's boxes are goal or unsafe boxes
fixed_boxes = st.lists(st.booleans(), max_size=8)
# Ori's position in hundredths, or None for no Ori; seven digits at most, which C#'s float-to-decimal keeps
hundredths = st.one_of(st.sampled_from([0, 1, -1, 5, -5, 50, -50, 9999999, -9999999]), st.integers(-9999999, 9999999))
ori_at = st.one_of(st.none(), st.tuples(hundredths, hundredths))


@st.composite
def ri_stmt(draw):
    """An RI value: slot=v or slot OP= v, half of them on a slot the inventory may hold."""
    slot = draw(literal(st.one_of(st.sampled_from(HELD), slot_id)))
    op = draw(st.sampled_from(["", "", "+", "-", "*", "/", "%"]))
    return draw(_mangle(slot + draw(pad) + op + "=" + draw(value_expr())))


COUNTED = {"RB", "KS", "MS", "HC", "EC"}
RULE_CODES = ["RB", "KS", "MS", "EX", "SK", "RI"]
# each reported once as a problem
BROKEN_RULES = ["no colon", "SK|0:", "X: no bar", "EX|9-3: backwards"]


@st.composite
def name_key(draw):
    """(text, key) for one PickupNames.txt key: CODE|ID, CODE|LO-HI or CODE|*, maybe with =N or =LO-HI."""
    code = draw(st.sampled_from(RULE_CODES))
    kind = draw(st.sampled_from(["id", "id", "range", "any"]))
    key = {"code": code, "kind": kind}
    if kind == "id":
        key["id"] = draw(st.sampled_from(["0", "5", "-3", "17", "8000=5" if code == "RI" else "12"]))
        body = key["id"]
    elif kind == "range":
        lo = draw(st.integers(-5, 20))
        key["lo"], key["hi"] = lo, lo + draw(st.integers(0, 10))
        body = "%d-%d" % (key["lo"], key["hi"])
    else:
        body = "*"
    if code in COUNTED and draw(st.booleans()):
        lo = draw(st.integers(0, 6))
        key["count"] = (lo, lo + draw(st.sampled_from([0, 0, 1, 3])))
        body += "=%d" % lo if key["count"][0] == key["count"][1] else "=%d-%d" % key["count"]
    spell = draw(st.sampled_from([code, code.lower()]))
    return draw(st.sampled_from(["", " "])) + spell + "|" + body, key


@st.composite
def name_rules(draw):
    """(lines, keys in file order): rule n's only name is "rN"; comments and broken lines in between."""
    lines, keys = [], []
    for n in range(draw(st.integers(1, 6))):
        if draw(st.integers(0, 3)) == 0:
            lines.append(draw(st.sampled_from(["// SK|0: a comment", ""] + BROKEN_RULES)))
        rule = draw(st.lists(name_key(), min_size=1, max_size=3))
        lines.append(", ".join(t for t, _ in rule) + ": r%d" % n)
        keys += [dict(k, names=["r%d" % n]) for _, k in rule]
    return lines, keys


name_query = st.tuples(st.sampled_from(RULE_CODES), st.sampled_from(["0", "5", "-3", "17", "12", "3", "8000=5", "x", "-1"]),
                       st.one_of(st.none(), st.integers(0, 9)))

# messages that read slots; BMP only, where .NET's \d and Python's agree
slot_message = st.lists(st.one_of(
    st.text(st.sampled_from("ab {}-0123456789\N{ARABIC-INDIC DIGIT ZERO}\N{ARABIC-INDIC DIGIT THREE}"
                            "\N{DEVANAGARI DIGIT ZERO}\N{FULLWIDTH DIGIT ONE}"), max_size=6),
    st.builds("{{{}}}".format, slot_id),
    st.sampled_from(["{T}", "{t}", "{ T}", "{Pos.x}", "{pOs.Y}", "{POS.X}", "{Pos.z}", "{Pos}", "{[Jump]}", "{[BASH]}", "{[dash]}",
                     "{[gReNaDe]}", "{[ Jump]}", "{[Jumps]}", "{Jump}", "{Bash1}", "{[]}", "{[Jump}"]),
    st.builds("{{{}{}{}}}".format, st.sampled_from("Bbx"), st.one_of(st.integers(-1, 7), st.just("\N{ARABIC-INDIC DIGIT THREE}")),
              st.sampled_from(["", ".x", ".Y", ".z", ".xi", ".YI", ".r", ".R", ".ri"])),
), max_size=5).map("".join)
# which actions are held, by index into ACTIONS
held_actions = st.sets(st.integers(0, len(ACTIONS) - 1), max_size=4)
