"""The bridge itself: values cross both ways unchanged, and failures come back as the right kind."""
import math
import os
import unittest

from hypothesis import given, settings, strategies as st

from client_props.base import HERE, examples
from client_props.cs_bridge import BridgeDied, CsError, slice_bridge

B = None


def setUpModule():
    global B
    B = slice_bridge("selftest", [], extra=[os.path.join(HERE, "bridge", "SelfTest.cs")], timeout=5)


class Values(unittest.TestCase):
    @settings(max_examples=examples(300))
    @given(st.text(st.characters(codec=None)))
    def test_strings_survive(self, s):
        self.assertEqual(B.call("SelfTest.Echo", s), s)

    @settings(max_examples=examples(300))
    @given(st.floats(allow_nan=False))
    def test_doubles_keep_their_bits(self, x):
        self.assertEqual(B.call("SelfTest.Twice", x), x * 2)

    def test_non_finite(self):
        self.assertTrue(math.isnan(B.call("SelfTest.Twice", math.nan)))
        self.assertEqual(B.call("SelfTest.Twice", -math.inf), -math.inf)

    @settings(max_examples=examples(100))
    @given(st.integers(-2**63, 2**63 - 1), st.binary(max_size=300))
    def test_longs_and_bytes(self, n, b):
        self.assertEqual(B.call("SelfTest.Wide", n), n)
        self.assertEqual(B.call("SelfTest.Bytes", b), b)

    @settings(max_examples=examples(100))
    @given(st.lists(st.text(max_size=5), max_size=10))
    def test_collections(self, xs):
        self.assertEqual(B.call("SelfTest.Rev", xs), xs[::-1])
        counts = B.call("SelfTest.Counts", xs)
        self.assertEqual(counts, {x: xs.count(x) for x in xs})
        d = {x: x[::-1] for x in xs}
        self.assertEqual(B.call("SelfTest.Same", d), d)

    def test_out_and_ref(self):
        self.assertEqual(B.call("SelfTest.Parse", "42"), (True, [42]))
        self.assertEqual(B.call("SelfTest.Parse", "x"), (False, [0]))
        self.assertEqual(B.call("SelfTest.Bump", 4), (None, [5]))

    def test_privates_and_fields(self):
        self.assertEqual(B.call("SelfTest.Secret"), 7)
        B.set("SelfTest.hidden", 9)
        self.assertEqual(B.get("SelfTest.hidden"), 9)
        B.set("SelfTest.hidden", 7)

    def test_handles(self):
        c = B.new("Counter", 3)
        self.assertEqual(c.call("Add", 4), 7)
        self.assertEqual(c.get("Count"), 7)

    def test_culture(self):
        B.culture("de-DE")
        try:
            self.assertEqual(B.call("SelfTest.Format", 2.5), "2,5")
        finally:
            B.culture("")


class Failures(unittest.TestCase):
    def test_throw_is_a_cs_error(self):
        with self.assertRaises(CsError) as cm:
            B.call("SelfTest.Boom", "nope")
        self.assertEqual(cm.exception.type, "System.InvalidOperationException")

    def test_bad_args_name_the_method(self):
        with self.assertRaises(CsError) as cm:
            B.call("SelfTest.Echo", 3)
        self.assertIn("SelfTest.Echo", cm.exception.msg)

    def test_stack_overflow_is_a_death_and_the_bridge_comes_back(self):
        with self.assertRaises(BridgeDied):
            B.call("SelfTest.Deep", 10**7)
        self.assertEqual(B.call("SelfTest.Echo", "back"), "back")

    def test_hang_is_a_death(self):
        with self.assertRaises(BridgeDied):
            B.call("SelfTest.Spin", timeout=1)
        self.assertEqual(B.call("SelfTest.Echo", "back"), "back")
