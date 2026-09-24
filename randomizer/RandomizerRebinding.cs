using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Input = Core.Input;

public static class RandomizerRebinding {
    // relative, so it lands in the game folder
    public const string BindsFile = "RandomizerRebinding.txt";

    // the Double Bash line still said Tap; RandomizerSettings imports it and clears this
    public static bool TapWasBound;

    // A failed write is logged, not thrown: the binds in memory apply either way.
    public static void WriteBindsToFile() {
        try {
            using (var streamWriter = new StreamWriter(BindsFile)) {
                WriteBinds(streamWriter);
            }
        } catch (Exception e) {
            Randomizer.log("Could not write " + BindsFile + ": " + e.Message);
        }
    }

    private static void WriteBinds(StreamWriter streamWriter) {
        streamWriter.WriteLine("Bind syntax: Key1+Key2, Key1+Key3+Key4, ... Syntax errors will load default binds.");
        streamWriter.WriteLine("Alt, Shift, Control, Command and Windows mean either side; name a side to want only that one.");
        streamWriter.WriteLine("Functions are unbound if the binding is empty or the word Unbound.");
        streamWriter.WriteLine("Supported binds are Unity KeyCodes (https://docs.unity3d.com/ScriptReference/KeyCode.html) and the following actions:");
        streamWriter.WriteLine("Jump, SpiritFlame, Bash, SoulFlame, ChargeJump, Glide, Dash, Grenade, Left, Right, Up, Down, LeftStick, RightStick, Start, Select");
        streamWriter.WriteLine("");
        streamWriter.WriteLine("In addition, Xbox-style controller buttons are supported, and must be preceded by an underscore:");
        streamWriter.WriteLine("_A (bottom), _B (right), _X (left), _Y (top), _LB, _RB, _LT, _RT, _DUp, _DDown, _DLeft, _DRight,");
        streamWriter.WriteLine("_LUp, _LDown, _LLeft, _LRight, _RUp, _RDown, _RLeft, _RRight, _LS, _RS, _Start, _Back");
        streamWriter.WriteLine("--------");
        streamWriter.WriteLine("");
        foreach (var bindparts in rebindMap) {
            if (bindparts.Value.HasBind()) {
                streamWriter.WriteLine($"{bindparts.Key}: {bindparts.Value}");
            } else {
                streamWriter.WriteLine($"{bindparts.Key}: {Unbound}");
            }
        }
    }

    // every action to its default, in memory; the caller writes the file
    public static void UseDefaults() {
        foreach (var pair in DefaultBinds) {
            SetBinds(pair.Key, pair.Value);
        }
    }


    public static void ParseRebinding() {
        var dirty = false;

        try {
            if (!File.Exists(BindsFile)) {
                UseDefaults();
                WriteBindsToFile();
            }

            var lines = File.ReadAllLines(BindsFile);
            var unseenActions = new ArrayList(DefaultBinds.Keys);
            var writeList = new List<string>();

            // parse step 1: read binds from file
            foreach (var line in lines) {
                if (!line.Contains(":")) {
                    continue;
                }

                var parts = line.Split(new[] { ':' }, 2);
                var action = parts[0].Trim();
                if (action == "Free Grenade Jump") {
                    action = "Grenade Jump";
                    dirty = true;
                } else if (action == "Return to Start") {
                    action = "Warp";
                    dirty = true;
                } else if (renamed.ContainsKey(action)) {
                    action = renamed[action];
                    dirty = true;
                }

                if (!DefaultBinds.ContainsKey(action)) {
                    continue;
                }

                var bindingString = parts[1].Trim();
                AssignBind(action, bindingString, writeList);
                if (rebindMap.ContainsKey(action) && rebindMap[action].Collapse()) {
                    dirty = true;
                }

                if (Restore(action, writeList)) {
                    dirty = true;
                }

                unseenActions.Remove(action);
            }

            // parse step 2: load defaults for missing binds
            foreach (string missingAction in unseenActions) {
                AssignBind(missingAction, null, writeList);
                // files from before Show Keysanity Progress had Toggle Chaos on its Alt+K
                if (missingAction == "Show Keysanity Progress" && ToggleChaos.ToString() == "Alt+K") {
                    AssignBind("Toggle Chaos", null, writeList);
                    dirty = true;
                }
            }

            if (writeList.Count > 0) {
                var warnList = new List<string>();

                foreach (var writeAction in writeList) {
                    if (DefaultBinds[writeAction] != "") {
                        warnList.Add(writeAction);
                    }
                }

                if (warnList.Count > 0) {
                    Randomizer.printInfo("Default Binds written for these missing binds: " + String.Join(", ", warnList.ToArray()) + ".", 480);
                }

                var writeText = "";
                foreach (var writeAction in writeList) {
                    writeText += Environment.NewLine + writeAction + ": " + DefaultBinds[writeAction];
                }

                File.AppendAllText(BindsFile, writeText);
            }

            if (dirty) {
                // this is redundant with the above but who cares! :D 
                WriteBindsToFile();
            }
        } catch (Exception e) {
            // queued: at boot this runs before the UI can draw a message
            Randomizer.log("Error parsing bindings: " + e.Message);
            Randomizer.Print("Error parsing bindings: " + e.Message, 15, false, false, true, false);
        }
    }

    // a Required action left empty gets its default back
    private static bool Restore(string action, List<string> writeList) {
        if (!Required.Contains(action) || !rebindMap.ContainsKey(action) || rebindMap[action].HasBind()) {
            return false;
        }

        AssignBind(action, DefaultBinds[action], writeList);
        return true;
    }

    public static void AssignBind(string action, string bindingString, List<string> writeList) {
        if (!rebindMap.ContainsKey(action)) {
            return;
        }

        rebindMap[action].Binds = ParseOrDefault(action, bindingString, writeList).Binds;
        rebindMap[action].deprecated_wasPressed = true;
    }

    public static BindSet ParseOrDefault(string action, string bindingString, List<string> writeList) {
        var defaultBinds = DefaultBinds[action];
        if (bindingString == null) {
            bindingString = defaultBinds;
            writeList.Add(action);
        }

        try {
            return ParseBinds(action, bindingString);
        } catch (Exception) {
            Randomizer.printInfo("@" + action + ": failed to parse '" + bindingString + "'. Using default value: '" + defaultBinds + "'@", 240);
            bindingString = defaultBinds;
        }

        return ParseBinds(action, bindingString);
    }

    public static KeyCode StringToKeyBinding(string s) {
        if (s != "") {
            var key = (KeyCode)Enum.Parse(typeof(KeyCode), s, true);
            // Enum.Parse takes any number, which no key answers to
            if (!Enum.IsDefined(typeof(KeyCode), key)) {
                throw new ArgumentException("no key " + s);
            }

            return key;
        }

        return KeyCode.None;
    }

    public static BindSet ParseBinds(string action, string bindingString) {
        var binds = new List<SingleBind>();
        if (String.Equals(bindingString.Trim(), Unbound, StringComparison.OrdinalIgnoreCase)) {
            return new BindSet(binds);
        }

        foreach (var bind in bindingString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) {
            var singleBind = new List<SingleInput>();
            foreach (var input in bind.Trim().Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (action == "Double Bash" && input.Trim().ToLower() == "tap") {
                    TapWasBound = true;
                } else {
                    singleBind.Add(new SingleInput(input.Trim()));
                }
            }

            if (singleBind.Count > 0) {
                binds.Add(new SingleBind(singleBind));
            }
        }

        return new BindSet(binds);
    }

    public static void FixedUpdate() {
        foreach (var bindSet in rebindMap.Values) {
            bindSet.FixedUpdate();
        }
    }

    public static Dictionary<string, Input.InputButtonProcessor> CoreInputMap = new Dictionary<string, Input.InputButtonProcessor> {
        { "Jump", Input.Jump },
        { "SpiritFlame", Input.SpiritFlame },
        { "Bash", Input.Bash },
        { "SoulFlame", Input.SoulFlame },
        { "ChargeJump", Input.ChargeJump },
        { "Glide", Input.Glide },
        { "Dash", Input.RightShoulder },
        { "Grenade", Input.LeftShoulder },
        { "Left", Input.Left },
        { "Right", Input.Right },
        { "Up", Input.Up },
        { "Down", Input.Down },
        { "LeftStick", Input.LeftStick },
        { "RightStick", Input.RightStick },
        { "Start", Input.Start },
        { "Select", Input.Select }
    };

    // Applies at once; the caller writes the file.
    public static void SetBinds(string action, string bindingString) {
        var set = BindNamed(action);
        if (set == null) {
            return;
        }

        set.Binds = ParseBinds(action, bindingString).Binds;
        set.deprecated_wasPressed = true;
    }

    public static BindSet BindNamed(string name) {
        BindSet bind;
        return rebindMap.TryGetValue(name.Trim(), out bind) ? bind : null;
    }

    // "[[Map Warp]]" -> its first bind. Must run before the game's parser, which resolves the
    // "[Grenade]" a game-action bind turns into; unknown names stay as written.
    public static string ResolveBindNames(string text) {
        if (String.IsNullOrEmpty(text) || text.IndexOf("[[") < 0) {
            return text;
        }

        return bindPattern.Replace(text, match => {
            var bind = BindNamed(match.Groups[1].Value);
            return bind == null ? match.Value : bind.FirstBindName();
        });
    }

    private static readonly Regex bindPattern = new Regex(@"\[\[([^\[\]]+)\]\]");

    // deliberately empty, which the parse must not refill with the default
    public const string Unbound = "Unbound";

    // Never left empty: the menu keeps the last bind and the parse restores the default.
    public static readonly List<string> Required = new List<string> {
        "Warp", "Map Warp", "Reload Seed"
    };

    public static Dictionary<string, string> DefaultBinds = new Dictionary<string, string> {
        { "Replay Message", "Alt+T" },
        { "Warp", "Alt+R" },
        { "Reload Seed", "Alt+L" },
        { "Toggle Chaos", "" },
        { "Chaos Verbosity", "Alt+V" },
        { "Force Chaos Effect", "Alt+F" },
        { "Show Progress", "Alt+P" },
        { "Color Shift", "Alt+C" },
        { "Double Bash", "Grenade" },
        { "Toggle Map Mode", "Grenade" },
        { "Map Warp", "Bash" },
        { "Grenade Jump", "Grenade+Jump" },
        { "Show Bonuses", "Alt+B" },
        { "Bonus Switch", "Alt+Q" },
        { "Bonus Toggle", "Alt+Mouse1" },
        { "Reset Grenade Aim", "" },
        { "Suppress Autofire", "" },
        { "List Trees", "Alt+Alpha1" },
        { "List Map Altars", "Alt+Alpha2" },
        { "List Teleporters", "Alt+Alpha3" },
        { "List Relics", "Alt+Alpha4" },
        { "Show Stats", "Alt+Alpha5" },
        { "Show Keysanity Progress", "Alt+K" },
        { "Grant Test Pickup", "" },
        { "Start Practice Debug", "" },
        { "Practice Menu", "" },
        { "Create Practice Segment", "Alt+M" },
        { "Open Practice Editor Page", "" },
        { "Retry Practice Segment", "Alt+L" },
        { "Spawn Echo", "" },
        { "Clear All Echoes", "" },
        { "Menu Skip Backwards", "PageUp" },
        { "Menu Skip Forwards", "PageDown" },
        { "Menu Home", "Shift+PageUp" },
        { "Menu End", "Shift+PageDown" },
        { "Bonus 1", "" },
        { "Bonus 2", "" },
        { "Bonus 3", "" },
        { "Bonus 4", "" },
        { "Bonus 5", "" },
        { "Bonus 6", "" },
        { "Bonus 7", "" },
        { "Bonus 8", "" },
        { "Bonus 9", "" },
    };

    public static BindSet ReplayMessage = new BindSet(new List<SingleBind>());
    public static BindSet ReturnToStart = new BindSet(new List<SingleBind>());
    public static BindSet ReloadSeed = new BindSet(new List<SingleBind>());
    public static BindSet ToggleChaos = new BindSet(new List<SingleBind>());
    public static BindSet ChaosVerbosity = new BindSet(new List<SingleBind>());
    public static BindSet ForceChaosEffect = new BindSet(new List<SingleBind>());
    public static BindSet ShowProgress = new BindSet(new List<SingleBind>());
    public static BindSet ColorShift = new BindSet(new List<SingleBind>());
    public static BindSet DoubleBash = new BindSet(new List<SingleBind>());
    public static BindSet FreeGrenadeJump = new BindSet(new List<SingleBind>());
    public static BindSet ToggleMapMode = new BindSet(new List<SingleBind>());
    public static BindSet MapWarp = new BindSet(new List<SingleBind>());
    public static BindSet ShowBonuses = new BindSet(new List<SingleBind>());
    public static BindSet BonusSwitch = new BindSet(new List<SingleBind>());
    public static BindSet BonusToggle = new BindSet(new List<SingleBind>());
    public static BindSet ResetGrenadeAim = new BindSet(new List<SingleBind>());
    public static BindSet SuppressAutofire = new BindSet(new List<SingleBind>());
    public static BindSet ListTrees = new BindSet(new List<SingleBind>());
    public static BindSet ListRelics = new BindSet(new List<SingleBind>());
    public static BindSet ListMapAltars = new BindSet(new List<SingleBind>());
    public static BindSet ListTeleporters = new BindSet(new List<SingleBind>());
    public static BindSet ShowStats = new BindSet(new List<SingleBind>());
    public static BindSet ShowKeysanityProgress = new BindSet(new List<SingleBind>());
    public static BindSet GrantTestPickup = new BindSet(new List<SingleBind>());

    public static BindSet StartPracticeDebug = new BindSet(new List<SingleBind>());
    public static BindSet OpenPracticeMenu = new BindSet(new List<SingleBind>());
    public static BindSet CreatePracticeSegment = new BindSet(new List<SingleBind>());
    public static BindSet OpenPracticeEditorPage = new BindSet(new List<SingleBind>());
    public static BindSet RetryPracticeSegment = new BindSet(new List<SingleBind>());
    public static BindSet SpawnEcho = new BindSet(new List<SingleBind>());
    public static BindSet ClearAllEchoes = new BindSet(new List<SingleBind>());
    public static BindSet MenuSkipBackwards = new BindSet(new List<SingleBind>());
    public static BindSet MenuSkipForwards = new BindSet(new List<SingleBind>());
    public static BindSet MenuHome = new BindSet(new List<SingleBind>());
    public static BindSet MenuEnd = new BindSet(new List<SingleBind>());
    public static BindSet Bonus1 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus2 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus3 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus4 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus5 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus6 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus7 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus8 = new BindSet(new List<SingleBind>());
    public static BindSet Bonus9 = new BindSet(new List<SingleBind>());

    // the first bind as display text (key caps where there are any), for messages
    public static string NameOf(string action) {
        BindSet set;
        return rebindMap.TryGetValue(action, out set) ? set.FirstBindName() : "<NO BIND>";
    }

    // old action names; a match marks the file dirty so it is rewritten under the new one
    private static Dictionary<string, string> renamed = new Dictionary<string, string> {
        { "Save Select Back 3", "Menu Skip Backwards" },
        { "Save Select Forward 3", "Menu Skip Forwards" },
        { "Save Select Back 10", "Menu Home" },
        { "Save Select Forward 10", "Menu End" },
    };

    private static Dictionary<string, BindSet> rebindMap = new Dictionary<string, BindSet> {
        { "Replay Message", ReplayMessage },
        { "Warp", ReturnToStart },
        { "Reload Seed", ReloadSeed },
        { "Show Progress", ShowProgress },
        { "Color Shift", ColorShift },
        { "Double Bash", DoubleBash },
        { "Grenade Jump", FreeGrenadeJump },
        { "Toggle Map Mode", ToggleMapMode },
        { "Map Warp", MapWarp },
        { "Show Bonuses", ShowBonuses },
        { "Bonus Switch", BonusSwitch },
        { "Bonus Toggle", BonusToggle },
        { "Reset Grenade Aim", ResetGrenadeAim },
        { "Suppress Autofire", SuppressAutofire },
        { "List Trees", ListTrees },
        { "List Relics", ListRelics },
        { "List Map Altars", ListMapAltars },
        { "List Teleporters", ListTeleporters },
        { "Show Stats", ShowStats },
        { "Show Keysanity Progress", ShowKeysanityProgress },
        { "Grant Test Pickup", GrantTestPickup },
        { "Start Practice Debug", StartPracticeDebug },
        { "Practice Menu", OpenPracticeMenu },
        { "Create Practice Segment", CreatePracticeSegment },
        { "Open Practice Editor Page", OpenPracticeEditorPage },
        { "Retry Practice Segment", RetryPracticeSegment },
        { "Spawn Echo", SpawnEcho },
        { "Clear All Echoes", ClearAllEchoes },
        { "Menu Skip Backwards", MenuSkipBackwards },
        { "Menu Skip Forwards", MenuSkipForwards },
        { "Menu Home", MenuHome },
        { "Menu End", MenuEnd },
        { "Bonus 1", Bonus1 },
        { "Bonus 2", Bonus2 },
        { "Bonus 3", Bonus3 },
        { "Bonus 4", Bonus4 },
        { "Bonus 5", Bonus5 },
        { "Bonus 6", Bonus6 },
        { "Bonus 7", Bonus7 },
        { "Bonus 8", Bonus8 },
        { "Bonus 9", Bonus9 },
        { "Toggle Chaos", ToggleChaos },
        { "Chaos Verbosity", ChaosVerbosity },
        { "Force Chaos Effect", ForceChaosEffect }
    };

    private static Dictionary<string, string> coreInputNameRepl = new Dictionary<string, string> {
        { "Grenade", "LightSpheres" },
        { "ChargeJump", "ChargeJumpCharge" }
    };

    public class SingleInput : Input.InputButtonProcessor {
        public SingleInput(string input) {
            raw = input;
            if (input.StartsWith("_")) {
                Type = ActionType.ControllerButton;
                Button = (PlayerInputRebinding.ControllerButton)Enum.Parse(typeof(PlayerInputRebinding.ControllerButton), input.Substring(1), true);
                // Enum.Parse takes any number, which no pad button answers to
                if (!Enum.IsDefined(typeof(PlayerInputRebinding.ControllerButton), Button)) {
                    throw new ArgumentException("no controller button " + input);
                }
            } else if (CoreInputMap.ContainsKey(input)) {
                Type = ActionType.CoreInput;
                CoreInput = CoreInputMap[input];
            } else if (Unside(input) != null) {
                Type = ActionType.EitherKey;
                either = Unside(input);
                Key = StringToKeyBinding("Left" + either);
                KeyAlt = StringToKeyBinding("Right" + either);
            } else {
                Type = ActionType.KeyCode;
                Key = StringToKeyBinding(input);
            }
        }

        // written without a side, these match either; Unity's names are "Left"/"Right" + these
        private static readonly string[] Unsided = {
            "Alt", "Shift", "Control", "Command", "Windows"
        };

        // the canonical spelling of an unsided modifier, or null for anything else
        public static string Unside(string input) {
            foreach (var name in Unsided) {
                if (String.Equals(input, name, StringComparison.OrdinalIgnoreCase)) {
                    return name;
                }
            }

            return null;
        }

        // the unsided name of a sided modifier key, or null for anything else
        public static string SideOf(KeyCode key) {
            foreach (var name in Unsided) {
                if (key == StringToKeyBinding("Left" + name) ||
                        key == StringToKeyBinding("Right" + name)) {
                    return name;
                }
            }

            return null;
        }

        public void FixedUpdate() {
            switch (Type) {
                case ActionType.CoreInput:
                    Update(CoreInput.Pressed);
                    break;
                case ActionType.ControllerButton:
                    // built on first use: PlayerInput may not exist yet when the file is parsed
                    if (pad == null && PlayerInput.Instance != null) {
                        pad = PlayerInput.Instance.ControllerButtonToButtonInput(Button);
                    }

                    Update(pad != null && pad.GetButton());
                    break;
                case ActionType.KeyCode:
                    Update(MoonInput.GetKey(Key));
                    break;
                case ActionType.EitherKey:
                    Update(MoonInput.GetKey(Key) || MoonInput.GetKey(KeyAlt));
                    break;
            }
        }

        public override string ToString() {
            switch (Type) {
                case ActionType.CoreInput:
                    return $"[{(coreInputNameRepl.ContainsKey(raw) ? coreInputNameRepl[raw] : raw)}]";
                case ActionType.ControllerButton:
                    return "_" + Button;
                case ActionType.KeyCode:
                    return RandomizerKeyIcons.Caption(Key);
                case ActionType.EitherKey:
                    // the left one of the pair, because a cap for a modifier says no side
                    return RandomizerKeyIcons.Caption(Key);
                default:
                    return "";
            }
        }

        public string RawStr() {
            switch (Type) {
                case ActionType.CoreInput:
                    return raw;
                case ActionType.ControllerButton:
                    return $"_{Button}";
                case ActionType.KeyCode:
                    return $"{Key}";
                case ActionType.EitherKey:
                    return either;
                default:
                    return "";
            }
        }


        public KeyCode Key;

        public KeyCode KeyAlt;

        private string either;

        private string raw;

        public Input.InputButtonProcessor CoreInput;

        public PlayerInputRebinding.ControllerButton Button;

        private SmartInput.IButtonInput pad;

        public ActionType Type;

        public enum ActionType {
            CoreInput,
            ControllerButton,
            KeyCode,
            EitherKey
        }
    }

    public class SingleBind : Input.InputButtonProcessor {
        public SingleBind(List<SingleInput> inputs) {
            Inputs = inputs;
        }

        public void FixedUpdate() {
            var pressed = true;

            foreach (var input in Inputs) {
                input.FixedUpdate();

                if (input.Released) {
                    pressed = false;
                }
            }

            Update(pressed);
        }

        public override string ToString() => String.Join("+", Inputs.Select(input => input.ToString()).ToArray());
        public string RawStr() => String.Join("+", Inputs.Select(input => input.RawStr()).ToArray());


        public List<SingleInput> Inputs;
    }

    public class BindSet : Input.InputButtonProcessor {
        public BindSet(List<SingleBind> binds) {
            deprecated_wasPressed = true;
            Binds = binds;
        }

        public override string ToString() => String.Join(", ", Binds.Select(binds => binds.RawStr()).ToArray());

        public string FirstBindName() {
            return HasBind() ? Binds[0].ToString() : "<NO BIND>";
        }

        // Binds differing only in one modifier's side merge into the unsided form; a lone sided
        // bind stays sided. True if anything merged, so the file is rewritten.
        public bool Collapse() {
            var changed = false;
            while (CollapseOnce()) {
                changed = true;
            }

            return changed;
        }

        private bool CollapseOnce() {
            for (var i = 0; i < Binds.Count; i++) {
                for (var j = i + 1; j < Binds.Count; j++) {
                    var at = Mirrored(Binds[i], Binds[j]);
                    if (at < 0) {
                        continue;
                    }

                    Binds[i].Inputs[at] = new SingleInput(SingleInput.SideOf(Binds[i].Inputs[at].Key));
                    Binds.RemoveAt(j);
                    return true;
                }
            }

            return false;
        }

        // The one place two otherwise identical binds hold the two sides of one modifier, or -1.
        private static int Mirrored(SingleBind a, SingleBind b) {
            if (a.Inputs.Count != b.Inputs.Count) {
                return -1;
            }

            var found = -1;
            for (var i = 0; i < a.Inputs.Count; i++) {
                if (a.Inputs[i].RawStr() == b.Inputs[i].RawStr()) {
                    continue;
                }

                var side = SingleInput.SideOf(a.Inputs[i].Key);
                if (found >= 0 || side == null ||
                        a.Inputs[i].Type != SingleInput.ActionType.KeyCode ||
                        b.Inputs[i].Type != SingleInput.ActionType.KeyCode ||
                        side != SingleInput.SideOf(b.Inputs[i].Key)) {
                    return -1;
                }

                found = i;
            }

            return found;
        }


        public bool HasBind() => Binds.Count > 0;

        // level; IsPressed() below is the consuming edge
        public bool Held() {
            foreach (var bind in Binds) {
                if (bind.Pressed) {
                    return true;
                }
            }

            return false;
        }

        public bool IsPressed() {
            foreach (var bind in Binds) {
                if (bind.Pressed) {
                    if (deprecated_wasPressed) {
                        return false;
                    }

                    deprecated_wasPressed = true;
                    return true;
                }
            }

            deprecated_wasPressed = false;
            return false;
        }

        public void FixedUpdate() {
            var pressed = false;

            foreach (var bind in Binds) {
                bind.FixedUpdate();

                if (bind.Pressed) {
                    pressed = true;
                }
            }

            Update(pressed);
        }

        public List<SingleBind> Binds;

        public bool deprecated_wasPressed;
    }
}
