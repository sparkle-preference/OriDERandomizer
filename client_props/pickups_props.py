"""Pickup codes: multipickup values, cross-world lines, names and RI slot expressions read as the server writes them."""
import logging
import operator
import os
import re
import unicodedata
import unittest

from hypothesis import event, given, settings, strategies as st

from client_props.base import CLIENT, HERE, examples
from client_props.cs_bridge import outcome, slice_bridge
from client_props.strategies.pickups import (BROKEN_RULES, INT_MAX, INT_MIN, EDGE_SLOTS, EDGE_VALUES, any_code, any_id,
                                             emitted, inventory, mw_line, name_query, name_rules, ri_stmt, slot_id,
                                             slot_message, value_expr)
from pickups import Event, MultiworldItem, Pickup, Skill, Teleporter, Upgrade
from test.js_bridge import call as js
from test.strategies.wire import line_text, pieces, raw_multi, real_pickup, slashy_text
from util import compose_multi_value, decompose_multi_value, normalize_multi_value

SOURCES = ["randomizer/RandomizerAction.cs", "randomizer/RandomizerItems.cs", "randomizer/RandomizerInventory.cs"]
MULTI = ("MU", "RP")
CULTURES = ["tr-TR", "fr-FR", "de-DE", "ar-SA"]
B = None


def setUpModule():
    global B
    cs = os.path.join(HERE, "cs", "pickups")
    B = slice_bridge("pickups", SOURCES, extra=[os.path.join(cs, "Stubs.cs"), os.path.join(cs, "Glue.cs")], game=True)


class Quiet(unittest.TestCase):
    """The server's readers log each malformed value they meet."""

    def setUp(self):
        super().setUp()
        self.addCleanup(logging.disable, logging.root.manager.disable)
        logging.disable(logging.CRITICAL)


# .NET's int.TryParse under NumberStyles.Integer (trailing NULs included), and String.Trim
_INT = re.compile(r"[ \t\n\v\f\r]*[+-]?[0-9]+[ \t\n\v\f\r]*\0*\Z")
_TRIM = "".join(map(chr, [9, 10, 11, 12, 13, 32, 0x85, 0xA0, 0x1680, *range(0x2000, 0x200B), 0x2028, 0x2029, 0x202F,
                          0x205F, 0x3000]))


def cs_int(s):
    if s is None or not _INT.match(s):
        return None
    n = int(s.strip(" \t\n\v\f\r\0"))
    return n if INT_MIN <= n <= INT_MAX else None


def cs_trim(s):
    return s.strip(_TRIM)


def wrap(n):
    return (n - INT_MIN) % 2 ** 32 + INT_MIN


def cs_div(a, b):
    q = abs(a) // abs(b)
    return q if (a < 0) == (b < 0) else -q


def lead_safe(id):
    return " " + id if id.startswith("/") else id


def multi_parts(value):
    return [tuple(p) for p in B.call("RandomizerItems.MultiParts", value)]


def decompose(code, value):
    return [tuple(p) for p in B.call("PickupsGlue.Decompose", code, value)]


def construct(code, id):
    got = outcome(B.call, "PickupsGlue.Construct", code, id)
    return tuple(got[1]) if got[0] == "ok" else None


def name(code, id):
    return B.call("RandomizerItems.Name", code, id)


def a_code_with_a_slash(ps):
    """Outside compose's domain: it writes codes as they are."""
    return any("/" in c for c, _ in ps)


# masks: each names the input class of an open finding

def an_empty_id_before_the_last(ps):
    """The server suite's mask of the same name: compose writes it as an escaped slash."""
    return any(i == "" for _, i in ps[:-1])


def _mark(ch):
    return ch == "\N{ARABIC TATWEEL}" or unicodedata.category(ch) in ("Mn", "Me", "Mc", "Cf", "Cc", "Co", "Cn")


def an_id_opening_with_a_marked_slash(ps):
    """An id opening with marks then a slash, or a slash then a mark (combining, format, control)."""
    def marked(i):
        n = next((k for k, c in enumerate(i) if not _mark(c)), len(i))
        return n > 0 and i[n:n + 1] == "/" or i[:1] == "/" and len(i) > 1 and _mark(i[1])
    return any(marked(i) for _, i in ps)


def names_the_lowest_int(code, id, depth=0):
    """An id (or a multipickup piece's id) that reads as -2147483648."""
    if cs_int(id) == INT_MIN:
        return True
    return code in MULTI and depth < 8 and any(names_the_lowest_int(c, i, depth + 1) for c, i in multi_parts(id))


def the_extra_items_counter(slot):
    return slot == EXTRA_ITEMS_APPLIED


def a_bonus_removal(code, id):
    """An RB with a negative id."""
    n = cs_int(id)
    return code == "RB" and n is not None and n < 0


composed = st.lists(real_pickup, max_size=4).map(compose_multi_value)
string_piece = st.tuples(st.sampled_from(["SH", "RI", "BM", "NO", "WT"]), st.one_of(slashy_text, line_text))


class Multipickups(Quiet):

    @settings(max_examples=examples(400))
    @given(pieces)
    def test_the_client_reads_what_compose_writes(self, ps):
        if an_empty_id_before_the_last(ps):
            event("masked: an empty id before the last")
            return
        self.assertEqual(multi_parts(compose_multi_value(ps)), [(c, lead_safe(i)) for c, i in ps])

    @settings(max_examples=examples(300))
    @given(st.one_of(raw_multi, slashy_text, line_text))
    def test_the_client_splits_a_value_as_the_editor_does(self, v):
        self.assertEqual(multi_parts(v), [tuple(p) for p in js("multipickup.js", "decompose_pickup", "MU", v)])

    @settings(max_examples=examples(400))
    @given(st.one_of(raw_multi, slashy_text))
    def test_the_client_splits_a_value_as_the_server_does_but_for_a_bare_message(self, v):
        server = decompose_multi_value(v)
        bare_message = normalize_multi_value(v) != v
        self.assertEqual(multi_parts(v), server[:-1] if bare_message else server)

    @settings(max_examples=examples(400))
    @given(st.one_of(raw_multi, slashy_text))
    def test_the_client_reads_a_normalized_value_as_the_server_reads_the_raw_one(self, v):
        server = decompose_multi_value(v)
        if a_code_with_a_slash(server):
            return
        if an_empty_id_before_the_last(server):
            event("masked: an empty id before the last")
            return
        self.assertEqual(multi_parts(normalize_multi_value(v)), [(c, lead_safe(i)) for c, i in server])

    @settings(max_examples=examples(400))
    @given(st.sampled_from(MULTI), st.one_of(raw_multi, slashy_text, line_text, composed))
    def test_the_grant_and_the_name_read_the_same_pieces(self, code, v):
        B.call("PickupsGlue.Reset", {})
        built = [construct(c, i) for c, i in multi_parts(v)]
        got = decompose(code, v)
        self.assertEqual(got, [b for b in built if b is not None])
        if len(got) < len(built):
            self.assertTrue(B.get("Randomizer.Errors"), "a dropped piece is reported")

    @settings(max_examples=examples(400))
    @given(st.lists(st.one_of(real_pickup, string_piece), min_size=1, max_size=5), st.booleans())
    def test_as_multi_writes_what_compose_writes_and_reads_back(self, ps, repeatable):
        if an_empty_id_before_the_last(ps):
            event("masked: an empty id before the last")
            return
        if an_id_opening_with_a_marked_slash(ps):
            event("masked: an id opening with a marked slash")
            return
        code, value = B.call("PickupsGlue.AsMulti", [c for c, _ in ps], [i for _, i in ps], repeatable)
        self.assertEqual(code, "RP" if repeatable else "MU")
        self.assertEqual(value, compose_multi_value(ps))
        self.assertEqual(decompose(code, value), [(c, lead_safe(i)) for c, i in ps])

    @settings(max_examples=examples(400))
    @given(st.sampled_from(MULTI), st.one_of(raw_multi, slashy_text, composed))
    def test_repacking_a_multipickup_keeps_its_pieces(self, code, v):
        once = decompose(code, v)
        if a_code_with_a_slash(once):
            return
        if an_id_opening_with_a_marked_slash(once):
            event("masked: an id opening with a marked slash")
            return
        packed = B.call("PickupsGlue.Repack", code, v)
        self.assertEqual(packed[0], code)
        self.assertEqual(packed[1], compose_multi_value(once))
        self.assertEqual(decompose(code, packed[1]), [(c, lead_safe(i)) for c, i in once])

    @unittest.expectedFailure
    def test_a_packed_piece_opening_with_a_slash_and_a_mark_reads_back(self):
        code, value = B.call("PickupsGlue.AsMulti", ["SH", "SK"], ["/\N{COMBINING ACUTE ACCENT}", "0"], False)
        self.assertEqual(decompose(code, value), [("SH", " /\N{COMBINING ACUTE ACCENT}"), ("SK", "0")])

    @unittest.expectedFailure
    def test_a_packed_piece_opening_with_a_format_character_keeps_its_text(self):
        code, value = B.call("PickupsGlue.AsMulti", ["SH", "SK"], ["\N{SOFT HYPHEN}/", "0"], False)
        self.assertEqual(decompose(code, value), [("SH", "\N{SOFT HYPHEN}/"), ("SK", "0")])


class CrossWorldLines(Quiet):

    @settings(max_examples=examples(400))
    @given(mw_line())
    def test_inner_reads_the_item_the_server_wrote(self, m):
        players, (code, id), line = m
        self.assertEqual(B.call("RandomizerItems.Inner", line, players), (True, [code, id]))
        mw = MultiworldItem(line, players)
        self.assertEqual((mw.item_code, mw.item_id), (code, id))

    @settings(max_examples=examples(400))
    @given(st.one_of(mw_line().map(lambda m: m[2]),
                     st.text(st.sampled_from("0123456789,,-+ SKAPx\N{ARABIC-INDIC DIGIT ONE}"), max_size=16)),
           st.integers(0, 4))
    def test_where_both_read_a_line_they_read_the_same_item(self, line, players):
        ok, (code, id) = B.call("RandomizerItems.Inner", line, players)
        try:
            mw = MultiworldItem(line, players or None)
        except ValueError:
            # the server raises on an int-coded item whose id isn't an int
            mw = None
        event("client %s, server %s" % ("reads" if ok else "refuses", "reads" if mw else "refuses"))
        if ok and mw is not None:
            self.assertEqual((code, id), (mw.item_code, mw.item_id))

    @settings(max_examples=examples(300))
    @given(mw_line())
    def test_a_cross_world_item_is_named_as_the_server_names_it(self, m):
        players, (code, id), line = m
        if wording_differs(code, id):
            return
        if a_bonus_removal(code, id):
            event("masked: a bonus removal")
            return
        self.assertEqual(name(code, id).lower(), MultiworldItem(line, players).shown.lower())

    @unittest.expectedFailure
    def test_the_server_reads_a_cross_world_line_whose_item_id_is_not_a_number(self):
        self.assertIsNotNone(MultiworldItem("1,0,SK,x", 2))

    @unittest.expectedFailure
    def test_a_cross_world_removal_is_named_as_a_removal(self):
        self.assertEqual(MultiworldItem("2,0,RB,-6", 2).shown, name("RB", "-6"))


# wording the two sides choose differently for one item; the table is in logs/pickups.log
RB_WORDING = {31, 32, 34, 35, 81, 104, 105, 113, 211, 212, 217, 241, 242, 247, 1100, 1110, 1587, 4087}
# codes the client never names: their grant has its own text, or names the item it carries
UNNAMED = {"WT", "WP", "WS", "NO", "HN", "RI", "BM", "MW"}


def wording_differs(code, id):
    # the server names any empty id CODE|
    n = cs_int(id)
    return id == "" or code in UNNAMED or code == "RB" and n is not None and abs(n) in RB_WORDING


def server_pickups():
    items = [("SK", str(i)) for i in Skill.names] + [("EV", str(i)) for i in Event.names]
    items += [("TP", t) for t in Teleporter.bits] + [("RB", str(i)) for i in sorted(Upgrade.names)]
    items += [("RB", "-%d" % i) for i in sorted(Upgrade.names) if i > 0]
    items += [(c, n) for c in ["HC", "EC", "AC", "KS", "MS"] for n in ["1", "-1"]]
    items += [("EX", "15"), ("EX", "-15"), ("SH", "a, b-c"), ("TW", "Warp to Grove,1,-2,Node"), ("NO", "1"),
              ("WT", "#Fil's Bracelet#\\nA band."), ("RI", "8000=5"), ("BM", "3=1"), ("MW", "2,17,SK,0")]
    return items


class Names(Quiet):

    def test_the_client_names_each_server_pickup_as_the_server_does_but_for_known_wording(self):
        differs = set()
        for code, id in server_pickups():
            if name(code, id).lower() != Pickup.name(code, id).lower():
                differs.add((code, id))
        self.assertEqual(differs, {(c, i) for c, i in server_pickups() if wording_differs(c, i)})

    @settings(max_examples=examples(400))
    @given(emitted())
    def test_every_pickup_the_server_emits_constructs_and_names(self, cip):
        code, id, ps = cip
        if an_empty_id_before_the_last(ps):
            event("masked: an empty id before the last")
            return
        server = decompose_multi_value(id) if code in MULTI else [(code, id)]
        self.assertIsNotNone(construct(code, id))
        B.call("PickupsGlue.Reset", {})
        self.assertEqual(decompose(code, id), server)
        self.assertEqual(B.get("Randomizer.Errors"), [])
        names = [name(c, i) for c, i in server]
        whole = ", ".join(names)
        self.assertEqual(name(code, id), "Repeatable: " + whole if code == "RP" else whole)
        for (c, i), n in zip(server, names):
            if not wording_differs(c, i):
                self.assertEqual(n.lower(), Pickup.name(c, i).lower())

    @settings(max_examples=examples(500))
    @given(any_code, any_id)
    def test_names_and_messages_never_throw(self, code, id):
        if names_the_lowest_int(code, id):
            event("masked: an id that reads as the lowest int")
            return
        self.assertEqual(outcome(name, code, id)[0], "ok")
        self.assertEqual(outcome(B.call, "PickupsGlue.Message", code, id, False)[0], "ok")

    @settings(max_examples=examples(300))
    @given(st.one_of(st.tuples(any_code, any_id), emitted().map(lambda e: e[:2])))
    def test_text_cues_only_put_a_cue_in_front(self, ci):
        code, id = ci
        if names_the_lowest_int(code, id):
            event("masked: an id that reads as the lowest int")
            return
        plain = B.call("PickupsGlue.Message", code, id, False)
        cued = B.call("PickupsGlue.Message", code, id, True)
        self.assertIn(cued, [plain] + ["(%s) %s" % (cue, plain) for cue in ("Skill", "Key", "World")])

    @unittest.expectedFailure
    def test_removing_the_lowest_int_has_a_name(self):
        self.assertEqual(outcome(name, "HC", "-2147483648")[0], "ok")

    @settings(max_examples=examples(400))
    @given(any_code, any_id)
    def test_a_pickup_fails_to_construct_only_as_a_bad_number(self, code, id):
        got = outcome(B.call, "PickupsGlue.Construct", code, id)
        if code in B.get("RandomizerAction.StringValPickupTypes"):
            self.assertEqual(got, ("ok", [code, id]))
        elif got[0] == "ok":
            self.assertEqual(got[1], [code, str(cs_int(id))])
        else:
            self.assertIn(got[1], ("System.FormatException", "System.OverflowException"))

    @settings(max_examples=examples(300))
    @given(st.text(max_size=16), st.integers(-1, 6))
    def test_inner_never_throws(self, line, players):
        self.assertEqual(outcome(B.call, "RandomizerItems.Inner", line, players)[0], "ok")


COMPARATORS = ["==", "!=", "<=", ">=", "<", ">"]
HOLDS = {"==": operator.eq, "!=": operator.ne, "<=": operator.le, ">=": operator.ge, "<": operator.lt, ">": operator.gt}
BOXES = range(1700, 2000)
EXTRA_ITEMS_APPLIED = 948


def writable(slot):
    """Seeds keep off the MW and AP bookkeeping (940-948) and the client's own block (10000-19999)."""
    return slot >= 0 and not 940 <= slot <= EXTRA_ITEMS_APPLIED and not 10000 <= slot <= 19999


class Model:
    """RI Format.txt and the comments in RandomizerInventory, as Python: one instance per expression."""

    def __init__(self, inv):
        self.inv, self.errors, self.logs, self.masks = dict(inv), 0, 0, []

    def read(self, slot):
        if slot < 0:
            self.errors += 1
            return 0
        return self.inv.get(slot, 0)

    def write(self, slot, value):
        if not writable(slot):
            self.errors += 1
            return
        self.inv[slot] = value
        if slot in BOXES:
            self.masks.append(slot)

    def operand(self, text):
        t = cs_trim(text)
        if len(t) > 2 and t[0] == "{" and t[-1] == "}":
            slot = cs_int(cs_trim(t[1:-1]))
            if slot is not None:
                return self.read(slot)
        return cs_int(t)

    def comparison(self, text):
        for op in COMPARATORS:
            at = text.find(op)
            if at < 0:
                continue
            left = self.operand(text[:at])
            right = None if left is None else self.operand(text[at + len(op):])
            return None if right is None else int(HOLDS[op](left, right))
        return None

    def parse_value(self, text):
        t = cs_trim(text or "")
        if len(t) > 2 and t[0] == "(" and t[-1] == ")":
            return self.comparison(t[1:-1])
        return self.operand(t)

    def apply(self, text):
        eq = text.find("=")
        if eq < 1:
            self.errors += 1
            return
        left = cs_trim(text[:eq])
        op = left[-1] if left and left[-1] in "+-*/%" else "="
        slot = cs_int(cs_trim(left[:-1]) if op != "=" else left)
        value = None if slot is None else self.parse_value(text[eq + 1:])
        if value is None:
            self.errors += 1
            return
        if op == "=":
            return self.write(slot, value)
        was = self.read(slot)
        if op in "/%" and (value == 0 or was == INT_MIN and value == -1):
            self.errors += 1
            return
        q = cs_div(was, value) if op in "/%" else 0
        self.write(slot, wrap({"+": was + value, "-": was - value, "*": was * value, "/": q, "%": was - value * q}[op]))

    def resolve(self, text):
        if "{" not in text:
            return text

        def one(m):
            slot = cs_int(m.group(1))
            if slot is None:
                self.logs += 1
                return "0"
            return str(self.read(slot))
        return re.sub(r"\{(-?\d+)\}", one, text)


def errors():
    return len(B.get("Randomizer.Errors"))


def snapshot():
    return {int(k): v for k, v in B.call("PickupsGlue.Snapshot").items()}


def statement_slot(text):
    eq = text.find("=")
    left = cs_trim(text[:max(eq, 0)])
    return cs_int(cs_trim(left[:-1]) if left and left[-1] in "+-*/%" else left)


class SlotExpressions(Quiet):

    def test_the_stubbed_ranges_match_the_source(self):
        mw = open(os.path.join(CLIENT, "randomizer", "RandomizerMW.cs"), encoding="utf-8-sig").read()
        box = open(os.path.join(CLIENT, "randomizer", "RandomizerBox.cs"), encoding="utf-8-sig").read()
        const = lambda text, name: int(re.search(r"const int %s = (\d+);" % name, text).group(1))
        for text, member in [(mw, "RandomizerMW.GrantedSlotsBase"), (mw, "RandomizerMW.GrantedSlotsLast"),
                             (box, "RandomizerBoxes.FirstBitId"), (box, "RandomizerBoxes.LastBitId")]:
            self.assertEqual(const(text, member.split(".")[1]), B.get(member), member)
        self.assertEqual(const(mw, "ExtraItemsApplied"), EXTRA_ITEMS_APPLIED)

    @settings(max_examples=examples(500))
    @given(st.one_of(st.sampled_from(EDGE_SLOTS), slot_id, st.integers(930, 960), st.integers(9990, 10010),
                     st.integers(19990, 20010)))
    def test_a_seed_writes_only_its_own_slots(self, slot):
        if the_extra_items_counter(slot):
            event("masked: the extra items counter")
            return
        self.assertEqual(B.call("RandomizerInventory.Writable", slot), writable(slot))

    @unittest.expectedFailure
    def test_a_seed_cannot_write_the_extra_items_counter(self):
        self.assertFalse(B.call("RandomizerInventory.Writable", EXTRA_ITEMS_APPLIED))

    @settings(max_examples=examples(600))
    @given(value_expr(), inventory)
    def test_parse_value_matches_the_format(self, text, inv):
        B.call("PickupsGlue.Reset", inv)
        model = Model(inv)
        want = model.parse_value(text)
        event("parses" if want is not None else "refused")
        self.assertEqual(B.call("RandomizerInventory.ParseValue", text), (want is not None, [want or 0]))
        self.assertEqual(errors(), model.errors)

    @settings(max_examples=examples(600))
    @given(ri_stmt(), inventory)
    def test_a_slot_write_matches_the_format(self, text, inv):
        if the_extra_items_counter(statement_slot(text)):
            event("masked: the extra items counter")
            return
        B.call("PickupsGlue.Reset", inv)
        model = Model(inv)
        model.apply(text)
        event("refused" if model.errors else "written")
        B.call("RandomizerInventory.Apply", text)
        self.assertEqual(snapshot(), model.inv)
        self.assertEqual(errors(), model.errors)
        self.assertEqual(B.get("RandomizerBoxes.Masks"), model.masks)

    @settings(max_examples=examples(400))
    @given(st.sampled_from([0, 8000, 9999, 20000, INT_MAX]), st.sampled_from("+-*/%"),
           st.one_of(st.sampled_from(EDGE_VALUES), st.integers(-9, 9), st.integers(INT_MIN, INT_MAX)),
           st.one_of(st.sampled_from(EDGE_VALUES), st.integers(-9, 9), st.integers(INT_MIN, INT_MAX)))
    def test_slot_arithmetic_wraps_and_refuses_what_would_throw(self, slot, op, was, value):
        B.call("PickupsGlue.Reset", {slot: was})
        model = Model({slot: was})
        text = "%d%s=%d" % (slot, op, value)
        model.apply(text)
        B.call("RandomizerInventory.Apply", text)
        self.assertEqual(snapshot(), model.inv)
        self.assertEqual(errors(), model.errors)

    @settings(max_examples=examples(400))
    @given(slot_message, inventory)
    def test_a_message_reads_the_slots_it_names(self, text, inv):
        B.call("PickupsGlue.Reset", inv)
        model = Model(inv)
        self.assertEqual(B.call("RandomizerInventory.ResolveSlots", text), model.resolve(text))
        self.assertEqual(errors(), model.errors)
        self.assertEqual(len(B.get("Randomizer.Logs")), model.logs)

    @settings(max_examples=examples(300))
    @given(st.text(max_size=20), inventory)
    def test_slot_expressions_never_throw(self, text, inv):
        B.call("PickupsGlue.Reset", inv)
        for fn in ("ParseValue", "Apply", "ResolveSlots"):
            self.assertEqual(outcome(B.call, "RandomizerInventory." + fn, text)[0], "ok", fn)


WIDEST = 2 ** 63 - 1


def rule_names(keys, code, id, count):
    """PickupNames.txt's order: exact id, narrowest range, *; then the narrower count; then the later key."""
    best = None
    for order, k in enumerate(keys):
        n = cs_int(id)
        hit = k["code"] == code and (k["kind"] == "any" or k["kind"] == "id" and k["id"] == id
                                     or k["kind"] == "range" and n is not None and k["lo"] <= n <= k["hi"])
        if "count" in k:
            hit = hit and count is not None and k["count"][0] <= count <= k["count"][1]
        if not hit:
            continue
        width = {"id": 0, "any": WIDEST}.get(k["kind"]) if k["kind"] != "range" else k["hi"] - k["lo"] + 1
        rank = (width, k["count"][1] - k["count"][0] if "count" in k else WIDEST, -order)
        if best is None or rank < best[0]:
            best = (rank, k["names"])
    return best and best[1]


class PickupNameFile(Quiet):

    @settings(max_examples=examples(300))
    @given(name_rules(), st.lists(name_query, min_size=1, max_size=6))
    def test_the_most_specific_rule_names_a_pickup(self, rules, queries):
        lines, keys = rules
        self.assertEqual(len(B.call("PickupsGlue.NameProblems", lines)), sum(l in BROKEN_RULES for l in lines))
        parsed = B.keep("PickupNameRules.Parse", lines, [])
        self.assertEqual(parsed.get("Count"), len(keys))
        for code, id, count in queries:
            self.assertEqual(parsed.call("For", code, id, count), rule_names(keys, code, id, count), (code, id, count))

    @settings(max_examples=examples(300))
    @given(st.lists(st.one_of(st.text(st.sampled_from("SKRB|*=-:,\\ 05/"), max_size=14), st.text(max_size=10)),
                    max_size=5), any_code, any_id, st.one_of(st.none(), st.integers(INT_MIN, INT_MAX)))
    def test_reading_a_names_file_never_throws(self, lines, code, id, count):
        self.assertEqual(outcome(B.call, "PickupsGlue.NameProblems", lines)[0], "ok")
        self.assertEqual(outcome(B.keep("PickupNameRules.Parse", lines, []).call, "For", code, id, count)[0], "ok")


class Cultures(Quiet):
    """Mono's culture tables are not .NET's: a difference here is confirmed in game before it counts."""

    def tearDown(self):
        B.culture("")
        super().tearDown()

    def results(self, code, id, multi, expr, inv):
        B.call("PickupsGlue.Reset", inv)
        out = [outcome(name, code, id), outcome(B.call, "PickupsGlue.Construct", code, id),
               outcome(B.call, "PickupsGlue.Decompose", "MU", multi), outcome(B.call, "PickupsGlue.Repack", "MU", multi),
               outcome(B.call, "RandomizerItems.Inner", "2,7,%s,%s" % (code, id), 2),
               outcome(B.call, "RandomizerInventory.ParseValue", expr),
               outcome(B.call, "RandomizerInventory.ResolveSlots", "{8000} {-1} " + expr)]
        B.call("RandomizerInventory.Apply", "8000+=" + expr)
        return out + [snapshot()]

    @settings(max_examples=examples(100))
    @given(st.one_of(emitted().map(lambda e: e[:2]), real_pickup,
                     st.tuples(any_code, st.one_of(st.sampled_from(["-1", "-15", "+3", " 7"]), any_id))),
           st.one_of(raw_multi, st.lists(real_pickup, max_size=3).map(compose_multi_value)), value_expr(), inventory)
    def test_pickups_read_the_same_in_every_culture(self, ci, multi, expr, inv):
        code, id = ci
        if names_the_lowest_int(code, id):
            event("masked: an id that reads as the lowest int")
            return
        B.culture("")
        want = self.results(code, id, multi, expr, inv)
        for culture in CULTURES:
            B.culture(culture)
            self.assertEqual(self.results(code, id, multi, expr, inv), want, culture)
