"""Settings file, binds, Color.txt, stats formatting and the display version: what the client writes reads
back, the same in every locale, and agrees with the server where both name the same thing."""
import itertools
import os
import re
import unittest

from hypothesis import assume, given, note, settings, strategies as st

from util import display_version
from client_props.base import CLIENT, HERE, client_file, examples
from client_props.cs_bridge import CsError, outcome, slice_bridge
from client_props.strategies import settings as ss

WORK = os.path.join(os.environ.get("TEMP", HERE), "client_props", "work", "settings")
CS = os.path.join(HERE, "cs", "settings")
LOCALES = ["de-DE", "fr-FR", "tr-TR", "fi-FI"]
locale = st.sampled_from(LOCALES)

S = K = V = None    # settings + colors + stats, binds (links the game dll), display version
SETTINGS = {}       # name -> RandomizerSettings' own description of the setting
NETCODE = None
CORE, KEYS, PADS, DEFAULT_BINDS = [], [], [], {}


def setUpModule():
    global S, K, V, NETCODE
    S = slice_bridge("settings", ["randomizer/RandomizerSettings.cs", "randomizer/RandomizerColorManager.cs",
                                  "randomizer/RandomizerStatsManager.cs"],
                     extra=[os.path.join(CS, "Stubs.cs"), os.path.join(CS, "Glue.cs")])
    K = slice_bridge("settings_binds", ["randomizer/RandomizerRebinding.cs"],
                     extra=[os.path.join(CS, "BindsStubs.cs"), os.path.join(CS, "BindsGlue.cs")], game=True)
    V = slice_bridge("settings_version", [], extra=[version_source()])
    SETTINGS.update((d["name"], d) for d in S.call("SettingsGlue.Describe"))
    NETCODE = S.get("Randomizer.NETCODE_HOST")
    CORE[:], KEYS[:], PADS[:] = K.call("BindsGlue.CoreNames"), K.call("BindsGlue.KeyNames"), K.call("BindsGlue.PadNames")
    DEFAULT_BINDS.update(K.get("RandomizerRebinding.DefaultBinds"))


def version_source():
    """Randomizer.cs is too entangled to compile here, so its two version lines are lifted verbatim."""
    text = client_file("randomizer/Randomizer.cs").decode("utf-8-sig")
    lines = [m.group(0).strip() for m in re.finditer(r"^\s*public static \w+ (IsBeta|DisplayVersion) =>.*;\s*$", text, re.M)]
    assert len(lines) == 2, lines
    os.makedirs(os.path.join(WORK, "version"), exist_ok=True)
    path = os.path.join(WORK, "version", "DisplayVersion.cs")
    with open(path, "w", newline="\n") as f:
        f.write("static class Randomizer {\n    public static string VERSION;\n    %s\n}\n" % "\n    ".join(lines))
    return path


def fresh(folder, culture=""):
    """Every setting at its default, file IO in this test's folder (per example: a bridge restart resets the cwd)."""
    S.culture(culture)
    S.call("SettingsGlue.Use", os.path.join(WORK, folder))
    S.call("SettingsGlue.Fresh")


def in_locale(name, fn, *args):
    S.culture(name)
    try:
        return fn(*args)
    finally:
        S.culture("")


def kinds(*names):
    return sorted(n for n, d in SETTINGS.items() if d["kind"] in names)


described = st.deferred(lambda: st.sampled_from(sorted(SETTINGS))).map(lambda n: SETTINGS[n])


# --- open-bug classes: each property masks one, a repro below carries its expectedFailure ---

def names_a_number_or_a_list(text):
    """Enum.Parse reads a leading digit or sign as a number and a comma as a list of members."""
    t = text.strip()
    return "," in t or bool(re.match(r"[-+]?\d", t))


def port_not_plain_digits_in_range(text):
    parts = text.strip().rstrip("/").split(":")
    return len(parts) == 2 and not (re.fullmatch(r"\d+", parts[1]) and 1 <= int(parts[1]) <= 65535)


def non_finite_color_part(text):
    return any(p.strip().lstrip("+-").lower() in ("infinity", "nan") for p in text.split(","))


def missing_a_hidden_line(text):
    have = {line.split(":", 1)[0].strip() for line in text.splitlines()}
    return any(d["hidden"] and n != "Dev" and n not in have for n, d in SETTINGS.items())


def blank_chord_element(text):
    return any(e and not e.strip() for part in text.split(",") for e in part.strip().split("+"))


def has_a_decimal_part(text):
    return "." in text


def turkish_capital_i(words, culture):
    """Words the client lowercases in the current culture, where tr-TR turns I into a dotless i."""
    return culture == "tr-TR" and any("I" in w for w in words)


def legacy_words(text):
    """The values the settings parse lowercases to look for an old word."""
    out = []
    for line in re.split(r"\r\n|\r|\n", text):
        line = line.split("//", 1)[0]
        if ":" in line and line.split(":", 1)[0].strip() in ss.LEGACY:
            out.append(line.split(":", 1)[1])
    return out


def disco_sense_near_an_item(disco, distance):
    return disco and distance < 64


def empty_rotation(state):
    return state["rotation"] and not state["colors"]


def shows_a_pickup_rate(held, ids):
    """The PPM column and the peak-rate row print a fraction; masked to the decimal mark."""
    zones = [o for k, o in ids["offsets"].items() if k not in ("unknown", "offworld")]
    shown = any(held.get(ids["Time"] + o, 0) > 0 and held.get(ids["Pickups"] + o, 0) > 0 for o in zones)
    return shown or held.get(ids["PPM_max"], 0) % 100 != 0


def undocumented_zone_slot(id_, ids):
    """The Misc and offworld slots of the per-zone blocks."""
    o = ids["offsets"]
    return id_ in {ids[block] + o[zone] for block in ("Deaths", "Time", "Pickups") for zone in ("unknown", "offworld")}


# --- the settings file ---

class SettingsFile(unittest.TestCase):
    @settings(max_examples=examples(50))
    @given(st.deferred(lambda: ss.settings_file([SETTINGS[n] for n in sorted(SETTINGS)])))
    def test_a_parsed_file_rewrites_to_a_fixed_point(self, text):
        fresh("fixed_point")
        S.call("SettingsGlue.Parse", text)
        first, state = S.call("SettingsGlue.Rewrite"), S.call("SettingsGlue.Snapshot")
        said = len(S.get("Randomizer.Shown"))
        S.call("SettingsGlue.Parse", first)
        self.assertEqual(S.call("SettingsGlue.Snapshot"), state)
        self.assertEqual(S.get("Randomizer.Shown")[said:], [], "a file the client wrote reads without a notice")
        self.assertEqual(S.call("SettingsGlue.Rewrite"), first)

    @settings(max_examples=examples(30))
    @given(st.deferred(lambda: ss.settings_file([SETTINGS[n] for n in sorted(SETTINGS)])), locale)
    def test_a_settings_file_reads_the_same_in_every_locale(self, text, culture):
        def read(name):
            fresh("file_locales", name)
            S.call("SettingsGlue.Parse", text)
            return S.call("SettingsGlue.Snapshot"), S.call("SettingsGlue.Rewrite")

        want, got = read(""), in_locale(culture, read, culture)
        if turkish_capital_i(legacy_words(text), culture):
            want, got = ({k: v for k, v in s.items() if k not in ss.LEGACY} for s in (want[0], got[0]))
        self.assertEqual(got, want)

    @unittest.expectedFailure
    def test_a_legacy_setting_word_reads_in_every_locale(self):
        def read(name):
            fresh("file_locales", name)
            S.call("SettingsGlue.Parse", "Hints: SKILLED")
            return S.call("SettingsGlue.Snapshot")["Hints"]

        self.assertEqual(in_locale("tr-TR", read, "tr-TR"), read(""))

    @settings(max_examples=examples(60))
    @given(st.data())
    def test_a_value_set_in_a_menu_reads_back_after_a_write(self, data):
        fresh("menu_values")
        menu = kinds("FloatSetting", "ColorSetting", "BoolSetting", "EnumSetting`1", "DirtyWaterSetting")
        for name in data.draw(st.lists(st.sampled_from(menu), min_size=1, max_size=6, unique=True)):
            d = SETTINGS[name]
            if d["kind"] == "FloatSetting":
                S.call("SettingsGlue.SetFloat", name, data.draw(ss.floats_between(d["min"], d["max"])))
            elif d["kind"] == "ColorSetting":
                S.call("SettingsGlue.SetColor", name, *data.draw(st.lists(ss.floats_between(0, 4), min_size=4, max_size=4)))
            elif d["kind"] == "BoolSetting":
                S.call("SettingsGlue.SetBool", name, data.draw(st.booleans()))
            else:
                S.call("SettingsGlue.SetEnum", name, data.draw(st.integers(0, len(d["names"]) - 1)))
        state = S.call("SettingsGlue.Snapshot")
        S.call("SettingsGlue.Parse", S.call("SettingsGlue.Rewrite"))
        self.assertEqual(S.call("SettingsGlue.Snapshot"), state)

    @settings(max_examples=examples(50))
    @given(st.data())
    def test_a_bad_value_resets_only_its_own_setting(self, data):
        names = [n for n in sorted(SETTINGS) if n not in ("Dev", "Built-in Netcode Host", "Netcode Host")]
        values = {n: data.draw(ss.value_for(SETTINGS[n]), label=n) for n in names}
        refusable = [n for n in names if ss.junk_for(SETTINGS[n]) is not None]
        bad = data.draw(st.sets(st.sampled_from(refusable), min_size=1, max_size=5), label="bad")
        before = dict(values)
        for n in bad:
            values[n] = data.draw(ss.junk_for(SETTINGS[n]), label="bad " + n)
        fresh("bad_values")
        want = {}
        for n in names:
            ok, held = S.call("SettingsGlue.ParseOne", n, values[n])
            assume(ok == (n not in bad))
            want[n] = held if ok else SETTINGS[n]["default"]

        def body(vals):
            lines = ["%s: %s" % (n, vals[n]) for n in names] + ["Built-in Netcode Host: " + NETCODE]
            return "\n".join(data.draw(st.permutations(lines)))

        # a reload: the bad lines held good values a moment ago
        fresh("bad_values")
        S.call("SettingsGlue.Parse", body(before))
        S.call("SettingsGlue.Parse", body(values))
        got = S.call("SettingsGlue.Snapshot")
        self.assertEqual({n: got[n] for n in names}, want)
        failed = sorted(s.split(": failed to parse", 1)[0] for s in S.get("Randomizer.Shown") if ": failed to parse" in s)
        self.assertEqual(failed, sorted(bad))
        self.assertEqual(S.get("Randomizer.Errors"), [])

    @settings(max_examples=examples(80))
    @given(st.lists(st.one_of(
        st.tuples(described, st.text(max_size=12)).map(lambda t: "%s: %s" % (t[0]["name"], t[1])),
        st.text(max_size=30)), max_size=10), ss.newline)
    def test_any_file_parses_without_aborting(self, lines, nl):
        fresh("any_text")
        S.call("SettingsGlue.Parse", nl.join(lines))
        self.assertEqual(S.get("Randomizer.Errors"), [])
        notices = S.get("Randomizer.Shown")
        self.assertEqual(len(notices), len(set(notices)))

    @settings(max_examples=examples(200))
    @given(st.deferred(lambda: st.sampled_from(kinds("EnumSetting`1", "DirtyWaterSetting"))).flatmap(
        lambda n: st.tuples(st.just(n), ss.enum_text(SETTINGS[n]))))
    def test_an_enum_setting_holds_only_named_values(self, case):
        name, text = case
        fresh("enums")
        S.call("SettingsGlue.ParseOne", name, text)
        if names_a_number_or_a_list(text):
            return
        self.assertTrue(S.call("SettingsGlue.HoldsANamedValue", name))

    @unittest.expectedFailure
    def test_an_enum_setting_never_holds_a_number_no_member_has(self):
        fresh("enums")
        S.call("SettingsGlue.ParseOne", "Autofire", "5")
        self.assertTrue(S.call("SettingsGlue.HoldsANamedValue", "Autofire"))

    @settings(max_examples=examples(40))
    @given(st.one_of(st.none(), st.deferred(lambda: st.just(NETCODE)), st.just(""), ss.hostname),
           st.one_of(st.none(), ss.host_value), st.booleans())
    def test_the_netcode_host_follows_the_built_in_host(self, marker, host, dev):
        lines = ["Dev: %s" % dev]
        if marker is not None:
            lines.append("Built-in Netcode Host: " + marker)
        if host is not None:
            lines.append("Netcode Host: " + host)
        fresh("netcode_host")
        S.call("SettingsGlue.Parse", "\n".join(lines))
        got = S.call("SettingsGlue.Snapshot")
        # a blank string setting reads as its default, so a blank marker reads as this build's host
        matched = marker is not None and (marker or NETCODE) == NETCODE
        self.assertEqual(got["Built-in Netcode Host"], NETCODE)
        self.assertEqual(got["Netcode Host"], host if matched and host is not None else NETCODE)
        S.call("SettingsGlue.Parse", S.call("SettingsGlue.Rewrite"))
        self.assertEqual(S.call("SettingsGlue.Snapshot"), got)

    @settings(max_examples=examples(150))
    @given(ss.hostname, st.one_of(st.none(), ss.port, ss.odd_port))
    def test_a_host_setting_takes_only_a_host_and_a_port(self, host, port):
        text = host if port is None else host + ":" + port
        fresh("host")
        got = S.call("SettingsGlue.ParseOne", "Netcode Host", text)
        if port_not_plain_digits_in_range(text):
            return
        self.assertEqual(got, [True, text])

    @unittest.expectedFailure
    def test_a_host_port_is_plain_digits_in_range(self):
        fresh("host")
        ok, _ = S.call("SettingsGlue.ParseOne", "Netcode Host", "x.test:+80")
        self.assertFalse(ok)

    @settings(max_examples=examples(30))
    @given(st.deferred(lambda: ss.settings_file([SETTINGS[n] for n in sorted(SETTINGS) if n != "Dev"], junk=False)))
    def test_hidden_settings_at_their_default_stay_out_of_the_file(self, text):
        fresh("hidden")
        S.call("SettingsGlue.Parse", text)
        written = S.call("SettingsGlue.Rewrite")
        after = S.call("SettingsGlue.Parse", written)
        if missing_a_hidden_line(written):
            after = after[:len(written)]
        state = S.call("SettingsGlue.Snapshot")
        for body in (written, after):
            for line in body.splitlines():
                name = line.split(":", 1)[0].strip()
                if name in SETTINGS and SETTINGS[name]["hidden"]:
                    self.assertNotEqual(state[name], SETTINGS[name]["default"], line)

    @unittest.expectedFailure
    def test_a_reload_never_appends_a_hidden_setting_at_its_default(self):
        fresh("hidden")
        after = S.call("SettingsGlue.Parse", S.call("SettingsGlue.Rewrite"))
        self.assertNotIn("Netcode Plain HTTP:", after)

    @settings(max_examples=examples(200))
    @given(st.deferred(lambda: st.sampled_from(kinds("FloatSetting", "ColorSetting"))), st.data(), locale)
    def test_floats_and_colors_read_and_write_alike_in_every_locale(self, name, data, culture):
        d = SETTINGS[name]
        if d["kind"] == "FloatSetting":
            text = data.draw(st.one_of(ss.float_value(d), ss.float_junk(d), st.sampled_from(["Infinity", "-0", "1e39"])))
        else:
            text = data.draw(st.one_of(ss.color_value(part=ss.any_color_part), ss.color_junk))
        fresh("locales")
        want = S.call("SettingsGlue.ParseOne", name, text)
        fresh("locales")
        got = in_locale(culture, S.call, "SettingsGlue.ParseOne", name, text)
        if d["kind"] == "ColorSetting" and non_finite_color_part(text):
            got, want = got[:1], want[:1]
        self.assertEqual(got, want)

    @unittest.expectedFailure
    def test_a_color_written_in_any_locale_reads_back(self):
        fresh("locales")
        S.culture("de-DE")
        try:
            S.call("SettingsGlue.ParseOne", "Cold Color", "Infinity, 0, 0, 255")
            S.call("SettingsGlue.Parse", S.call("SettingsGlue.Rewrite"))
            red = S.call("SettingsGlue.ColorOf", "Cold Color")[0]
        finally:
            S.culture("")
        self.assertEqual(red, float("inf"))


# --- binds ---

TOKEN = st.deferred(lambda: ss.bind_token(CORE, KEYS, PADS))
ACTION = "Show Progress"


def binds_fresh(folder):
    K.call("BindsGlue.Use", os.path.join(WORK, folder))
    K.call("BindsGlue.Fresh")


def fires(physical):
    """Every set of buttons that presses one of the binds."""
    return {frozenset(combo) for bind in physical for combo in itertools.product(*bind)}


class Binds(unittest.TestCase):
    @settings(max_examples=examples(200))
    @given(ss.bind_text(TOKEN))
    def test_a_bind_set_reads_back_from_its_own_text(self, text):
        first = outcome(K.call, "BindsGlue.Text", ACTION, text)
        self.assertEqual(first[0], "ok", text)
        written = first[1]
        self.assertEqual(K.call("BindsGlue.Text", ACTION, written), written)
        self.assertEqual(K.call("BindsGlue.PhysicalOf", ACTION, written), K.call("BindsGlue.PhysicalOf", ACTION, text))

    @settings(max_examples=examples(200))
    @given(ss.sided_bind_set())
    def test_collapse_is_idempotent_and_keeps_the_same_buttons(self, text):
        once, _, twice, again = K.call("BindsGlue.CollapseTwice", ACTION, text)
        note(once)
        self.assertEqual(twice, once)
        self.assertFalse(again)
        self.assertEqual(fires(K.call("BindsGlue.PhysicalAfterCollapse", ACTION, text)),
                         fires(K.call("BindsGlue.PhysicalOf", ACTION, text)))

    @settings(max_examples=examples(200))
    @given(st.deferred(lambda: st.sampled_from(sorted(DEFAULT_BINDS))),
           st.one_of(ss.bind_text(TOKEN), ss.bind_junk, ss.messy_bind_text, st.text(max_size=12)))
    def test_parse_or_default_never_throws(self, action, text):
        binds_fresh("or_default")
        got = outcome(K.call, "BindsGlue.OrDefault", action, text)
        self.assertEqual(got[0], "ok")
        parsed = outcome(K.call, "BindsGlue.Text", action, text)
        self.assertEqual(got[1], parsed[1] if parsed[0] == "ok" else K.call("BindsGlue.Text", action, DEFAULT_BINDS[action]))

    @settings(max_examples=examples(200))
    @given(st.one_of(ss.bind_text(TOKEN), ss.messy_bind_text))
    def test_every_parsed_input_names_a_button(self, text):
        inputs = outcome(K.call, "BindsGlue.Inputs", ACTION, text)
        if inputs[0] != "ok" or blank_chord_element(text):
            return
        for kind, key, raw in inputs[1]:
            if kind == "KeyCode" and key == 0:
                self.assertEqual(raw.lower(), "none", text)

    @settings(max_examples=examples(150))
    @given(st.one_of(ss.bind_text(TOKEN), ss.messy_bind_text), locale)
    def test_bind_text_reads_the_same_in_every_locale(self, text, culture):
        want = outcome(K.call, "BindsGlue.Text", "Double Bash", text)
        K.culture(culture)
        try:
            got = outcome(K.call, "BindsGlue.Text", "Double Bash", text)
        finally:
            K.culture("")
        self.assertEqual(got, want)

    @unittest.expectedFailure
    def test_a_blank_chord_element_is_a_syntax_error(self):
        with self.assertRaises(CsError):
            K.call("BindsGlue.Text", ACTION, "Alt+ +T")

    def test_core_names_are_exact_case_and_everything_else_is_not(self):
        self.assertEqual(outcome(K.call, "BindsGlue.Text", ACTION, "jump")[0], "err")
        self.assertEqual(K.call("BindsGlue.Text", ACTION, "Jump"), "Jump")
        self.assertEqual(K.call("BindsGlue.Text", ACTION, "aLT+pAGEuP, _rb, sHIFT"), "Alt+PageUp, _RB, Shift")

    def test_a_key_with_two_names_reads_back_as_the_same_key(self):
        # which alias Enum.ToString picks is the runtime's choice; Mono may print the other one
        for left, right in (("LeftApple", "LeftCommand"), ("RightApple", "RightCommand")):
            self.assertEqual(K.call("BindsGlue.KeyValue", left), K.call("BindsGlue.KeyValue", right))
            written = K.call("BindsGlue.Text", ACTION, left + "+T")
            self.assertEqual(K.call("BindsGlue.PhysicalOf", ACTION, written), K.call("BindsGlue.PhysicalOf", ACTION, left + "+T"))

    @settings(max_examples=examples(40))
    @given(st.data())
    def test_a_bind_file_reads_back_to_the_same_file(self, data):
        text = data.draw(bind_file())
        binds_fresh("bind_file")
        K.call("BindsGlue.Parse", text)
        first, held = K.call("BindsGlue.Rewrite"), K.call("BindsGlue.Binds")
        K.call("BindsGlue.Parse", first)
        self.assertEqual(K.call("BindsGlue.Binds"), held)
        self.assertEqual(K.call("BindsGlue.Rewrite"), first)

    @settings(max_examples=examples(40))
    @given(st.data())
    def test_each_bind_line_lands_on_its_action(self, data):
        text = data.draw(bind_file())
        want = {a: K.call("BindsGlue.Text", a, d) for a, d in DEFAULT_BINDS.items()}
        seen = set()
        for line in text.splitlines():
            if ":" not in line:
                continue
            action, value = (p.strip() for p in line.split(":", 1))
            action = OLD_NAMES.get(action, action)
            if action not in DEFAULT_BINDS:
                continue
            seen.add(action)
            parsed = outcome(K.call, "BindsGlue.CollapseTwice", action, value)
            want[action] = parsed[1][0] if parsed[0] == "ok" else K.call("BindsGlue.Text", action, DEFAULT_BINDS[action])
            if action in REQUIRED and not want[action]:
                want[action] = K.call("BindsGlue.Text", action, DEFAULT_BINDS[action])
        if "Show Keysanity Progress" not in seen and want["Toggle Chaos"] == "Alt+K":
            want["Toggle Chaos"] = ""
        binds_fresh("bind_lines")
        K.call("BindsGlue.Parse", text)
        self.assertEqual(K.call("BindsGlue.Binds"), want)


OLD_NAMES = {"Free Grenade Jump": "Grenade Jump", "Return to Start": "Warp", "Save Select Back 3": "Menu Skip Backwards",
             "Save Select Forward 3": "Menu Skip Forwards", "Save Select Back 10": "Menu Home",
             "Save Select Forward 10": "Menu End"}
REQUIRED = {"Warp", "Map Warp", "Reload Seed"}


@st.composite
def bind_file(draw):
    lines = ["Bind syntax: a header line with a colon", "--------"]
    for action in sorted(DEFAULT_BINDS):
        if not draw(st.booleans()):
            continue
        old = [o for o, new in OLD_NAMES.items() if new == action]
        name = draw(st.sampled_from([action] + old))
        value = draw(st.one_of(ss.bind_text(TOKEN, max_binds=2), ss.bind_junk, ss.cased("unbound"), st.just(""),
                               ss.sided_bind_set(), st.just("Alt+K")))
        lines.append("%s: %s" % (name, value))
    return draw(ss.newline).join(draw(st.permutations(lines)))


# --- Color.txt and the color tick ---

ROTATION = "customrotation\n" + "\n".join("%d,%d,%d,255" % (i * 10, 0, 0) for i in range(23)) + "\n"


class Colors(unittest.TestCase):
    @settings(max_examples=examples(150))
    @given(ss.color_txt(st.integers(0, 9).flatmap(lambda i: ss.decimal_part if i == 0 else st.integers(0, 511).map(str))),
           locale)
    def test_color_txt_reads_the_same_in_every_locale(self, text, culture):
        fresh("color_txt")
        want = S.call("ColorGlue.Load", text)
        got = in_locale(culture, S.call, "ColorGlue.Load", text)
        if has_a_decimal_part(text) or turkish_capital_i(re.split(r"\r?\n", text)[:1], culture):
            return
        self.assertEqual(got, want)

    @unittest.expectedFailure
    def test_a_color_txt_decimal_reads_alike_in_every_locale(self):
        fresh("color_txt")
        want = S.call("ColorGlue.Load", "127.5,0,0")
        self.assertEqual(in_locale("de-DE", S.call, "ColorGlue.Load", "127.5,0,0"), want)

    @unittest.expectedFailure
    def test_a_rotation_header_reads_in_every_locale(self):
        fresh("color_txt")
        text = "CUSTOMROTATION\n255,0,0,255\n"
        self.assertEqual(in_locale("tr-TR", S.call, "ColorGlue.Load", text), S.call("ColorGlue.Load", text))

    @settings(max_examples=examples(150))
    @given(st.one_of(st.none(), ss.color_txt()), st.integers(1, 40), st.floats(0, 200), st.booleans())
    def test_the_color_tick_stays_inside_the_rotation(self, text, frames, distance, disco):
        fresh("color_tick")
        state = S.call("ColorGlue.Load", text)
        if empty_rotation(state) or (state["rotation"] and disco_sense_near_an_item(disco, distance)):
            return
        self.assertEqual(S.call("ColorGlue.Tick", frames, distance, disco), [])

    @unittest.expectedFailure
    def test_disco_sense_stays_inside_the_rotation(self):
        fresh("color_tick")
        S.call("ColorGlue.Load", ROTATION)
        self.assertEqual(S.call("ColorGlue.Tick", 30, 10.0, True), [])

    @unittest.expectedFailure
    def test_an_empty_rotation_ticks_without_errors(self):
        fresh("color_tick")
        S.call("ColorGlue.Load", "customrotation\n")
        self.assertEqual(S.call("ColorGlue.Tick", 3, 100.0, False), [])


# --- stats ---

def stats_ids():
    """Every inventory id the stats manager writes, by role, and the per-zone offsets."""
    S.call("StatsGlue.Fresh")
    g = lambda f: S.get("RandomizerStatsManager." + f)
    ids = {f: g(f) for f in ("Deaths", "Time", "Pickups", "PPM_max")}
    ids["offsets"] = g("Offsets")
    roles = []
    for block, role in (("Deaths", "count"), ("Time", "time"), ("Pickups", "count")):
        roles += [(role, g(block) + o) for o in ids["offsets"].values()]
    for f in ("DSLS", "PSLDOS", "DSLS_max", "PSLDOS_max", "Saves", "EnemiesKilled", "ExpGained", "ExpBonus",
              "PPM_max", "PPM_max_count", "Reloads", "AltRCount", "TeleporterCount", "LevelUpKills", "FragCount"):
        roles.append(("count", g(f)))
    for f in ("TSLD", "TSLDOS", "TSLD_max", "TSLDOS_max", "shoof_sum", "PPM_max_time", "Drought", "Drought_max",
              "Drought_max_end"):
        roles.append(("time", g(f)))
    tables = goal_tables()
    for base, n in (("KeyItemTime", g("KeyItemOffsets").values()), ("TeleporterTime", range(tables["teleporters"])),
                    ("TreeTime", tables["trees"]), ("RelicTime", tables["relics"]), ("MapstoneTime", tables["maps"])):
        roles += [("stamp", g(base) + i) for i in n]
    ids["FragFirst"] = g("FragFirst")
    return ids, roles


def goal_tables():
    """The goal tables the stamp blocks index, read from the client source they live in."""
    tracked = client_file("randomizer/RandomizerTrackedDataManager.cs").decode("utf-8-sig")
    bingo = client_file("bingo/BingoController.cs").decode("utf-8-sig")

    def table(name):
        return re.search(r"\b%s = new Dictionary<[^>]+> \{(.*?)\};" % name, tracked, re.S).group(1)

    trees = [int(k) for k in re.findall(r"\{\s*(\d+),", table("Trees"))]
    relics = [int(v) for v in re.findall(r'"\w+",\s*(\d+)\s*\}', table("RelicFound"))]
    maps = [int(v) for v in re.findall(r'"\w+",\s*(\d+)\s*\}', table("MapBitsByArea"))]
    tps = re.search(r"string\[\] Teleporters = \{(.*?)\};", bingo, re.S).group(1)
    out = {"trees": trees, "relics": relics, "maps": maps, "teleporters": len(re.findall(r'"\w+"', tps))}
    assert all(out.values()), out
    return out


def id_doc():
    """RandomizerItemIDs.txt as (first, last, text) entries, in file order, and where the persist zone starts."""
    entries, persist = [], None
    for line in client_file("RandomizerItemIDs.txt").decode("utf-8-sig").splitlines():
        if "Begin Persists Through Death" in line:
            persist = len(entries)
        m = re.match(r"^(\d+)(?:-(\d+)|(\+))? - (.*)$", line.strip())
        if m:
            first = int(m.group(1))
            last = int(m.group(2)) if m.group(2) else (10 ** 9 if m.group(3) else first)
            entries.append((first, last, m.group(4)))
    return entries, persist


def documented(id_, entries):
    """The entries naming id_: its own line, a range holding it, or a 'base + index' block up to the next entry."""
    hits = [e for e in entries if e[0] <= id_ <= e[1]]
    later = sorted(e[0] for e in entries)
    for first, last, text in entries:
        if first == last and "+" in text and first < id_:
            nxt = min([x for x in later if x > first] or [10 ** 9])
            if id_ < nxt:
                hits.append((first, last, text))
    return hits


def literal_writes_outside_stats():
    """Ids other client files write by literal number."""
    found = set()
    for root, _, files in os.walk(CLIENT):
        if any(part in root for part in (".git", "Managed", "obj")):
            continue
        for f in files:
            if f.endswith(".cs") and f != "RandomizerStatsManager.cs":
                with open(os.path.join(root, f), encoding="utf-8-sig", errors="replace") as fh:
                    found.update(int(n) for n in re.findall(r"(?:Set|Inc)RandomizerItem\(\s*(\d+)\s*,", fh.read()))
    return found


STATS, ROLES = {}, []


class Stats(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        ids, roles = stats_ids()
        STATS.update(ids)
        ROLES[:] = roles

    @settings(max_examples=examples(300))
    @given(st.integers(1, 2 ** 31 - 1), locale)
    def test_format_time_reads_back_as_its_seconds(self, seconds, culture):
        text = in_locale(culture, S.call, "RandomizerStatsManager.FormatTime", seconds)
        self.assertEqual(S.call("RandomizerStatsManager.FormatTime", seconds, False), text.strip())
        head, *rest = text.strip().split(":")
        self.assertTrue(rest and all(re.fullmatch(r"[0-5]\d", p) for p in rest), text)
        total = int(head)
        for p in rest:
            total = total * 60 + int(p)
        self.assertEqual(total, seconds)

    @settings(max_examples=examples(200))
    @given(st.integers(-2 ** 31, 0))
    def test_format_time_takes_any_int(self, seconds):
        text = S.call("RandomizerStatsManager.FormatTime", seconds)
        if seconds == 0:
            self.assertEqual(text, "   N/A")

    # Packed never sees "total": no zone the player stands in is called that
    @settings(max_examples=examples(300))
    @given(st.tuples(st.one_of(st.deferred(lambda: st.sampled_from(sorted(STATS["offsets"]))), st.text(max_size=6)),
                     st.integers(0, 2 ** 31 - 1)).filter(lambda kt: kt[0] != "total" or kt[1] > 0))
    def test_a_zone_stamp_reads_back_as_its_time_and_zone(self, case):
        key, t = case
        S.call("StatsGlue.Fresh")
        S.call("StatsGlue.Hold", {STATS["Time"]: t})
        stamp = S.call("RandomizerStatsManager.Packed", key)
        S.call("StatsGlue.Hold", {4040: stamp})
        line, (time,) = S.call("RandomizerStatsManager.Stamped", "Wall Jump", 4040)
        zone = key if key in STATS["offsets"] else "unknown"
        self.assertEqual(time, min(t, (1 << 18) - 1))
        pretty = S.get("RandomizerStatsManager.ZonePrettyNames")[zone].strip()
        self.assertTrue(line.endswith("\t" + S.call("RandomizerStatsManager.FormatTime", time) + "\t" + pretty), line)

    @settings(max_examples=examples(60))
    @given(st.deferred(lambda: ss.stat_inventory(ROLES, sorted(STATS["offsets"].values()))),
           st.booleans(), st.booleans(), st.booleans(), st.lists(st.sampled_from(["Glades", "Swamp", "Horu"]), unique=True),
           locale)
    def test_stats_pages_read_the_same_in_every_locale(self, held, trees, relics, maps, relic_zones, culture):
        def pages():
            S.call("StatsGlue.Fresh")
            S.call("StatsGlue.Goals", trees, relics, maps, relic_zones)
            S.call("StatsGlue.Hold", held)
            S.call("SettingsGlue.Use", os.path.join(WORK, "stats_pages"))
            out = [S.call("RandomizerStatsManager.GetStatsPage", p, f) for p in range(4) for f in (False, True)]
            return out + [S.call("StatsGlue.StatsFile", "flags|seed"), S.get("Randomizer.Errors")]

        want = pages()
        self.assertIsNotNone(want[-2])
        self.assertEqual(want[-1], [])
        got = in_locale(culture, pages)
        if shows_a_pickup_rate(held, STATS):
            got = [re.sub(r"(\d),(\d)", r"\1.\2", g) if isinstance(g, str) else g for g in got]
        self.assertEqual(got, want)

    @unittest.expectedFailure
    def test_a_pickup_rate_prints_a_dot_in_every_locale(self):
        S.call("StatsGlue.Fresh")
        S.call("StatsGlue.Hold", {STATS["Time"] + 1: 600, STATS["Pickups"] + 1: 15})
        page = in_locale("de-DE", S.call, "RandomizerStatsManager.GetStatsPage", 0, False)
        self.assertIn("\t1.5", page)

    def test_stats_ids_are_kept_through_death_and_never_shared(self):
        entries, persist = id_doc()
        others = literal_writes_outside_stats()
        # fragments run open-ended from FragFirst; the list names only the first
        frags = [("stamp", STATS["FragFirst"] + i) for i in range(64)]
        seen = {}
        for role, id_ in ROLES + frags:
            self.assertNotIn(id_, seen, "%d is both %s and %s" % (id_, seen.get(id_), role))
            seen[id_] = role
            self.assertNotIn(id_, others, "%d is written outside the stats manager too" % id_)
            if undocumented_zone_slot(id_, STATS) or (role, id_) in frags[1:]:
                continue
            hits = documented(id_, entries)
            self.assertEqual(len(hits), 1, "%d: %s" % (id_, hits))
            self.assertTrue(4000 <= id_ < 10000 or entries.index(hits[0]) < persist, "%d rolls back undocumented" % id_)

    @unittest.expectedFailure
    def test_every_stats_id_is_in_the_id_list(self):
        entries, _ = id_doc()
        self.assertEqual([i for _, i in ROLES if not documented(i, entries)], [])


# --- the display version ---

class DisplayVersion(unittest.TestCase):
    @settings(max_examples=examples(200))
    @given(ss.dll_version)
    def test_the_client_names_a_version_as_the_server_does(self, version):
        V.set("Randomizer.VERSION", version)
        self.assertEqual(V.get("Randomizer.DisplayVersion"), display_version(version))
