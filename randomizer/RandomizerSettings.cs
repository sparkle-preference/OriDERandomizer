using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEngine;
using Input = Core.Input;

public static class RandomizerSettings {
    public static void WriteDefaultFile() {
        dirty = true;
        WriteSettings();
    }

    // move this with every new setting, or existing installs take the nag path on update
    public static string LastAddedSetting = "Built-in Netcode Host";

    private static string StripComment(string line) {
        var at = line.IndexOf("//");
        return at < 0 ? line : line.Substring(0, at);
    }

    public static void ParseSettings() {
        if (!File.Exists("RandomizerSettings.txt")) {
            WriteDefaultFile();
        }

        try {
            var unseenSettings = new List<string>(All.Keys);
            unseenSettings.Remove("Dev");
            var writeList = new List<string>();
            var lines = File.ReadAllLines("RandomizerSettings.txt");

            // parse step 1: read settings from file
            foreach (var rawLine in lines) {
                var line = StripComment(rawLine);

                if (!line.Contains(":")) {
                    continue;
                }

                var parts = line.Split(new[] { ':' }, 2);
                var setting = parts[0].Trim();
                if (!All.ContainsKey(setting)) {
                    continue;
                }

                var value = parts[1].Trim();
                if (setting == "Grenade Jump Mode" && value.ToLower() == "free") {
                    dirty = true;
                    value = "Auto";
                }

                if (setting == "Hints" && value.ToLower() == "skilled") {
                    dirty = true;
                    value = "Experienced";
                }

                if (setting == "Default Map Filter" && value.ToLower() == "all") {
                    dirty = true;
                    value = "Uncollected";
                }

                ParseSettingLine(setting, value);
                unseenSettings.Remove(setting);
            }

            var firstSight = unseenSettings.Contains(DevSettings.BuiltinHost.Name);
            foreach (var missing in unseenSettings) {
                All[missing].Reset();
                writeList.Add(missing);
                if (missing == LastAddedSetting) {
                    dirty = true;
                }
            }

            MoveNetcodeHost(firstSight);
            if (writeList.Count > 0 && !dirty) {
                var writeText = "";
                var nagList = new List<string>();
                foreach (var writeKey in writeList) {
                    var setting = All[writeKey];
                    writeText += Environment.NewLine + writeKey + ": " + setting.ToString();
                    if (setting.Nag) {
                        nagList.Add(writeKey);
                    }
                }

                if (nagList.Count > 0) {
                    Notice("Default settings written for: " + String.Join(", ", nagList.ToArray()), 120 + 60 * nagList.Count);
                }

                File.AppendAllText("RandomizerSettings.txt", writeText);
            }

            CurrentFilter = Customization.DefaultMapFilter.Value;
            if (dirty) {
                WriteSettings();
            }

            if (Randomizer.MessageQueue != null) {
                foreach (var notice in pendingNotices) {
                    Randomizer.printInfo(notice.Key, notice.Value);
                }

                pendingNotices.Clear();
            }

            Announce();
        } catch (Exception e) {
            Randomizer.LogError("Error parsing settings: " + e.Message);
        }
    }

    public static void ParseSettingLine(string setting, string value) {
        try {
            if (All.ContainsKey(setting)) {
                All[setting].Parse(value);
            }
        } catch (Exception) {
            All[setting].Reset();
            Notice("@" + setting + ": failed to parse value '" + value + "'. Using default value: '" + All[setting].ToString() + "'@", 240);
        }
    }

    // A dll built for another host moves the live host along, whatever it was set to:
    // once per host change, in either direction.
    private static void MoveNetcodeHost(bool firstSight) {
        var builtin = DevSettings.BuiltinHost;
        var host = DevSettings.NetcodeHost;
        if (!firstSight && builtin.IsDefault()) {
            return;
        }

        var detail = firstSight ? "first run with a built-in host" : $"was {host.Value}, built-in host was {builtin.Value}";
        Randomizer.log($"netcode host: now {host.Default} ({detail})");
        if (Dev.Value && (firstSight || host.Value != builtin.Value)) {
            Notice($"Netcode host is now {host.Default} ({detail})", 600);
        }

        host.Reset();
        builtin.Reset();
        dirty = true;
    }

    // The boot-time parse runs before the message queue exists, and initialize() makes a
    // fresh queue anyway, so notices wait for the next parse that can show them.
    private static void Notice(string message, int frames) {
        pendingNotices.Add(new KeyValuePair<string, int>(message, frames));
    }

    private static readonly List<KeyValuePair<string, int>> pendingNotices = new List<KeyValuePair<string, int>>();

    public static void WriteSettings() {
        if (!dirty) {
            return;
        }

        using (var writer = new StreamWriter("RandomizerSettings.txt", false)) {
            writer.WriteLine("// This file contains a variety of randomizer-specific settings.");
            writer.WriteLine("// Lines that start with // are comments - they explain what this file does and how it works");
            writer.WriteLine("// Edit values of settings by changing the text after the \":\" and then saving the file.");
            writer.WriteLine("// After saving, reload the randomizer (alt+L by default) to update your settings without restarting the game.");
            writer.WriteLine("");
            writer.WriteLine("// Words in square brackets ([]) are Ori base game binds (e.g. [Jump], [Climb])");
            writer.WriteLine("//    These binds can be changed in the in-game rebinding editor, or using the editor at orirando.com/rebinds");
            writer.WriteLine("// Words double square brackets ([[]]) are rando-specific binds (e.g. [[Grenade Jump]])");
            writer.WriteLine("//    and can be changed in RandomizerRebinding.txt");
            writer.WriteLine("");
            writer.WriteLine("// If you have any questions, please ask for help in the discord (orirando.com/discord, #bf-randomizer)");
            writer.WriteLine("");
            foreach (var setting in All) {
                if (setting.Value.Hidden && !Dev.Value && setting.Value.IsDefault()) {
                    continue;
                }

                if (setting.Value.Comment != "") {
                    writer.WriteLine($"// {setting.Value.Comment.Replace("\n", "\n// ")}");
                }

                writer.Write(setting.Key);
                writer.Write(": ");
                writer.WriteLine($"{setting.Value.ToString()}\n");
            }
        }

        dirty = false;
    }

    public static bool IsSwimBoosting() {
        if (Controls.InvertSwim) {
            return !Input.Jump.IsPressed;
        }

        return Input.Jump.IsPressed;
    }

    public static bool SwimBoostPressed() {
        if (Controls.InvertSwim) {
            return Input.Jump.OnReleased;
        }

        return Input.Jump.OnPressed;
    }

    // Settings something has to be told about rather than read from when it needs them.
    public static void Announce() {
        RandomizerGhostSignal.Apply();
    }

    public static void SetDirty() {
        dirty = true;
    }

    static RandomizerSettings() {
        Controls.BashDeadzone = new FloatSetting("Controller Bash Deadzone", 0.5f, "(0.0-1.0, Default=0.5): Size of the controller stick deadzone when aiming Bash.");
        Controls.FastGrenadeAim = new BoolSetting("Instant Grenade Aim", false, "True: When aiming Grenade on a controller, throw the grenade in the direction and distance the stick is aimed.\nFalse (Default): Vanilla behavior (move the stick to move the target location).");
        Controls.GrenadeAimSpeed = new FloatSetting("Grenade Aim Speed", 1.0f, "(Default 1.0 - higher numbers are faster): The speed at which controller/wsad inputs move the Grenade target.");
        Controls.InvertSwim = new BoolSetting("Invert Swim", false, "True: Ori swims fast by default, and slows down while pressing [Jump].\nFalse (default): Vanilla behavior (hold [Jump] to swim faster).");
        Controls.InvertClimb = new BoolSetting("Invert Climb", false, "True: Ori Climbs on walls by default, and lets go when holding [Climb]\nFalse (default): Vanilla behavior (hold [Climb] to Climb).");
        Controls.GrenadeJump = new EnumSetting<GrenadeJumpMode>("Grenade Jump Mode", GrenadeJumpMode.Auto, "Auto (default): Grenade Jump by pressing [[Grenade Jump]] (Default [LightSpheres]+[Jump]).\nManual: Vanilla behavior (Grenade Jump by using Grenade, then Jump 1 frame later).");
        Controls.WallChargeMouseAim = new BoolSetting("Wall Charge Mouse Aim", true, "True (default): On Keyboard+Mouse, allows aiming Wall Charge Jumps with the mouse.\nFalse: Vanilla behavior.");
        Controls.SwimmingMouseAim = new BoolSetting("Swimming Mouse Aim", false, "True: On Keyboard+Mouse, Ori will swim towards the mouse cursor.\nFalse (default): Vanilla behavior.");
        Controls.SlowClimbVault = new BoolSetting("Slow Climb Vault", true, "True (default): slightly slows Climb vaults, making it easier to land on small vertical platforms with Climb.\nFalse: Vanilla behavior.");
        Controls.Autofire = new EnumSetting<AutofireMode>("Autofire", AutofireMode.Off, "Hold: When [Listen] is held, autofire. Charge Flame by holding [[Suppress Autofire]] and [Listen].\nToggle: Press [Listen] to start autofiring. Press it again to stop. (Charge Flame as normal).\nOff: Vanilla behavior (no autofire).");
        Controls.LongerBashAimTime = new BoolSetting("Longer Bash Aim Time", false, "True: Allows holding [Bash] for about 3x as long, giving you more time to aim.\nFalse (default): Vanilla behavior (about 1.7 seconds of Bash aiming time).");

        Customization.ColdColor = new ColorSetting("Cold Color", new Color(0f, 0.5f, 0.5f, 0.5f), HeadroomScale, "Red, Blue, Green, Transparency (0-255 for each): The color Ori turns when Sensing an item at max range.");
        Customization.HotColor = new ColorSetting("Hot Color", new Color(0.5f, 0.1666667f, 0f, 0.5f), HeadroomScale, "Red, Blue, Green, Transparency (0-255 for each): The color Ori turns when Sensing an item at range 0.");
        Customization.DiscoSense = new BoolSetting("Disco Sense", false, "True: Ignore sense colors, and instead speed up the color.txt rotation when sense is active.\nFalse (default): colors.txt rotation is overwritten by Sense colors.", false);
        Customization.TouchedVisibility = new FloatSetting("Touched Pickup Visibility", 0.5f, "(0.0-1.0, Default=0.5): How much of a pickup that has nothing left to give is still drawn on the map. 0 hides them.");
        Customization.MapWarpHold = new FloatSetting("Map Warp Hold", 1f, "(0.1-3.0, Default=1.0): How long [[Map Warp]] must be held on a Spirit Well in the map to warp there.");
        Customization.ShowOtherPlayers = new BoolSetting("Show Other Players", true, "True (default): the other players in your game appear as translucent ghosts, and as markers on the map.\nFalse: they are not shown to you, and your position is not shared with them. The two go together -- there is no setting that lets you watch without being watched.", true);
        Customization.MultiplePickupMessages = new BoolSetting("Display Multiple Pickup Messages", false, "True: Shows up to 5 pickup messages at once on the left side of the screen. Hold [[Replay Message]] to show more.\nFalse (default): New pickup messages display one at a time at the top center of the screen.", false);
        Customization.AlwaysShowLastFivePickups = new BoolSetting("Always Show Last Five Pickup Messages", false, "True: Always show the last 5 pickup messages. Only works if Display Multiple Pickups is set to True.\nFalse (default): Only show pickups when found or on pressing [[Replay Message]].", false);
        Customization.WarpTeleporterColor = new ColorSetting("Warp Teleporter Color", new Color(202f / 255f, 57f / 255f, 243f / 255f, 1f), FullScale, "Red, Blue, Green, Transparency (0-255 for each): The color that Warp-created Teleporters are on the map.");
        Customization.DefaultMapFilter = new EnumSetting<MapFilterMode>("Default Map Filter", MapFilterMode.InLogic, "InLogic (default): Select the In Logic map filter when first opening the map.\nUncollected: Select the Uncollected map filter when first opening the map.", false);
        Customization.HintLevel = new EnumSetting<HintLevels>("Hints", HintLevels.NewPlayer, "NewPlayer (default): Show loading tips intended for new rando players.\nExperienced: Show loading tips intended for more experienced rando players.\nDisabled: do not show loading screen tips.", false);
        Customization.RandomizedExpNames = new BoolSetting("Randomized Experience Names", false, "True: Replace the word \"Experience\" with a random currency name whenever you gain experience from a pickup.\nFalse (default): Experience pickups are just called Experience.", false);
        Customization.AlwaysShowDoorHints = new BoolSetting("Always Show Keysanity Door Hints", false, "True: Always show any unlocked Keysanity door hints when viewing the map.\nFalse (default): Only show Keysanity door hints in the map when hovering a door.");
        Customization.KeyLockWarnings = new BoolSetting("Keystone Door Logic Warnings", true, "True (default): Warn when touching a keystone door that is not currently in logic, since opening it early could make the seed uncompletable.\nFalse: no warning.");
        Customization.PickupMessageBgColor = new ColorSetting("Pickup Message Background Color", new Color(0f, 0f, 0f, 0.5f), HeadroomScale, "Red, Blue, Green, Transparency (0-255 for each): Background color for pickup messages.\nDefault: 0, 0, 0, 255", false);
        Customization.MwPickupMessageBgColor = new ColorSetting("Multiworld Outbound Message Background Color", new Color(64f / 510f, 64f / 510f, 64f / 510f, 255f / 510f), HeadroomScale, "Red, Blue, Green, Transparency (0-255 for each): Background color for pickup messages sent to another Player.\nBoth local pickups and pickups received from another player will use the \"Pickup Message Background Color\" color.\nThis is for pickups that you find in your seed and send to another player.\nDefault: 64, 64, 64, 255", false);
        Customization.DisableTempResourceRows = new BoolSetting("Disable Temporary Resource Rows", false, "True: temporary health and energy draw inline past your normal cells, as older versions did.\nFalse (default): temporary health and energy get their own smaller row above each bar.", false);
        Customization.TempRowSpacing = new FloatSetting("Temp Row Spacing", 0.8f, "Vertical gap between a HUD bar and its temporary-resource row, in strip heights.", false);
        Customization.TempRowHorizontalOffset = new FloatSetting("Temp Row Horizontal Offset", 0f, "Horizontal shift of the temporary-resource rows, in strip heights. Positive pushes away from the experience wheel.", false);
        Customization.TempRowScale = new FloatSetting("Temp Row Scale", 0.7f, "Temporary-resource row size relative to the base bar.", false);
        Customization.TempRowBrightness = new FloatSetting("Temp Row Brightness", 0.8f, "Brightness of the temporary-resource rows, relative to the base bars.", false);

        QOL.AbilityMenuOpacity = new FloatSetting("Ability Menu Opacity", 0.5f, "(0.0-1.0) The opacity of the ability menu when performing a Save Anywhere.", false);
        QOL.CursorLock = new BoolSetting("Cursor Lock", false, "True: Locks the mouse cursor inside the window\nFalse (default): Vanilla behavior (cursor can leave the Ori window in borderless / windowed mode).", false);

        Practice.Folder = new StringSetting("Practice Folder", "practice", "Where practice segments (.bfrp files) are kept: a folder name inside the game folder, or a full path.", false);
        Practice.Ghost = new EnumSetting<PracticeGhost>("Practice Ghost", PracticeGhost.Segment, "Which ghost to race in practice mode.\nSegment (default): whatever the segment says, which is the pinned run if there is one and the fastest otherwise.\nFastest, Pinned, Recent: always that one. None: no ghost.", false);
        Practice.Timer = new BoolSetting("Practice Timer", true, "Show the running clock, and the time to beat, in the top right during a practice run.", false);

        Game.DefaultDifficulty = new EnumSetting<Difficulty>("Default Difficulty", Difficulty.Relaxing, "(Relaxing (default), Challenging, Punishing, OneLife): The default difficulty on new file selection.", false);

        Accessibility.ApplySoundCompression = new BoolSetting("Apply Sound Compression", false, "True: Caps sound from getting too loud (relevant when e.g. charge jumping very echo-y areas, like Spirit Caverns).\nFalse (default): Vanilla behavior.", false);
        Accessibility.SoundCompressionFactor = new FloatSetting("Sound Compression Factor", 0.6f, "(0.0-1.0) Higher values mean more sound compression (fewer sounds louder than the rest of them).", false);
        Accessibility.CameraShakeFactor = new FloatSetting("Camera Shake Factor", 1f, "(0.0-1.0) Reduce the intensity of camera shake effects in the game. Set to 0 to disable camera shake entirely.", false);
        Accessibility.DisableMenuBlur = new BoolSetting("Disable Menu Blur", false, "True: Disables the blur effect applied to the game during Save Anywhere.\nFalse (default): Vanilla behavior.", false);

        Dev = new BoolSetting("Dev", false, "", false, true);

        DevSettings.AreasOri = new BoolSetting("Keep Areas.Ori Updated", true, "Update areas.ori from the server. Set to False to disable for local development.", false, true);
        DevSettings.BlackrootOrbRoomClimbAssist = new BoolSetting("Blackroot Orb Room Climb Assist", true, "", false, true);
        DevSettings.NetcodeHost = new HostSetting("Netcode Host", Randomizer.NETCODE_HOST, "The server the netcode talks to: a host name, no protocol (https and wss are implied).\nChanging this breaks netcode.", false, true);
        DevSettings.PlainHttp = new BoolSetting("Netcode Plain HTTP", false, "True: talk http and ws to the Netcode Host instead of https and wss, for a local server.\nFalse (default): TLS.", false, true);
        DevSettings.BuiltinHost = new StringSetting("Built-in Netcode Host", Randomizer.NETCODE_HOST, "Where this dll pointed when it was built. Not a setting: when a newer dll points somewhere else,\nNetcode Host is moved there, whatever it was set to, and this line follows.", false);
        DevSettings.DisableWebsocket = new BoolSetting("Disable Websocket", false, "True: never use the websocket netcode transport; poll over http like older versions.\nFalse (default): use the websocket when available, falling back to http.", false, true);
    }

    // 0-255 is the "normal" range and lands at half intensity, leaving room above 255 to
    // over-saturate. The seed format's BGCOLOR string documents the same headroom, so a
    // message colour written in a seed and one written here mean the same thing.
    public const float HeadroomScale = 510f;

    // a plain colour, full intensity at 255
    public const float FullScale = 255f;

    public static Dictionary<string, SettingBase> All = new Dictionary<string, SettingBase>();

    public static BoolSetting Dev;

    public static MapFilterMode CurrentFilter = MapFilterMode.InLogic;

    private static bool dirty;

    public enum AutofireMode {
        Off,
        Hold,
        Toggle
    }

    public enum Difficulty {
        Relaxing,
        Challenging,
        Punishing,
        OneLife
    }

    public enum HintLevels {
        NewPlayer,
        Experienced,
        Disabled,
    }

    public enum GrenadeJumpMode {
        Manual,
        Auto
    }

    public enum MapFilterMode {
        [Description("In Logic")] InLogic,
        [Description("Uncollected")] Uncollected
    }

    public static class Controls {
        public static FloatSetting BashDeadzone;

        public static BoolSetting FastGrenadeAim;

        public static FloatSetting GrenadeAimSpeed;

        public static BoolSetting InvertSwim;

        public static BoolSetting InvertClimb;

        public static EnumSetting<GrenadeJumpMode> GrenadeJump;

        public static BoolSetting WallChargeMouseAim;

        public static BoolSetting SwimmingMouseAim;

        public static BoolSetting SlowClimbVault;

        public static EnumSetting<AutofireMode> Autofire;

        public static BoolSetting LongerBashAimTime;
    }

    public static class Customization {
        public static ColorSetting ColdColor;

        public static ColorSetting HotColor;

        public static BoolSetting DiscoSense;

        public static BoolSetting ShowOtherPlayers;

        public static FloatSetting TouchedVisibility;

        public static FloatSetting MapWarpHold;

        public static BoolSetting MultiplePickupMessages;

        public static BoolSetting AlwaysShowLastFivePickups;

        public static ColorSetting WarpTeleporterColor;

        public static EnumSetting<MapFilterMode> DefaultMapFilter;

        public static EnumSetting<HintLevels> HintLevel;

        public static BoolSetting RandomizedExpNames;

        public static BoolSetting AlwaysShowDoorHints;

        public static BoolSetting KeyLockWarnings;

        public static BoolSetting DisableTempResourceRows;

        public static FloatSetting TempRowSpacing;

        public static FloatSetting TempRowHorizontalOffset;

        public static FloatSetting TempRowScale;

        public static FloatSetting TempRowBrightness;

        public static ColorSetting PickupMessageBgColor;

        public static ColorSetting MwPickupMessageBgColor;
    }

    public static class QOL {
        public static FloatSetting AbilityMenuOpacity;

        public static BoolSetting CursorLock;
    }

    public static class Practice {
        public static StringSetting Folder;

        public static EnumSetting<PracticeGhost> Ghost;
        public static BoolSetting Timer;
    }

    public enum PracticeGhost {
        Segment,
        Fastest,
        Pinned,
        Recent,
        None
    }

    public static class Game {
        public static EnumSetting<Difficulty> DefaultDifficulty;
    }

    public static class Accessibility {
        public static BoolSetting ApplySoundCompression;

        public static FloatSetting SoundCompressionFactor;

        public static FloatSetting CameraShakeFactor;

        public static BoolSetting DisableMenuBlur;
    }


    public static class DevSettings {
        public static BoolSetting AreasOri;
        public static BoolSetting BlackrootOrbRoomClimbAssist;
        public static HostSetting NetcodeHost;
        public static BoolSetting PlainHttp;
        public static StringSetting BuiltinHost;
        public static BoolSetting DisableWebsocket;
    }

    public abstract class SettingBase {
        public SettingBase(string name, string comment = "", bool nag = true, bool hidden = false) {
            Name = name;
            All[name] = this;
            Nag = nag;
            Hidden = hidden;
            Comment = comment;
        }

        public abstract bool IsDefault();

        public abstract void Parse(string value);

        public abstract new string ToString();

        public abstract void Reset();

        public virtual string ValidValues() => "";

        public string Name;

        public bool Nag;

        public string Comment;

        public bool Hidden;
    }

    public abstract class Setting<T> : SettingBase {
        public Setting(string name, T defaultValue, string comment = "", bool nag = true, bool hidden = false) : base(name, comment, nag, hidden) {
            Default = defaultValue;
            Value = Default;
        }

        public override bool IsDefault() => Value.Equals(Default);

        public override string ToString() {
            return Value.ToString();
        }

        public override void Reset() {
            Value = Default;
        }

        public static implicit operator T(Setting<T> setting) => setting.Value;

        public T Default;

        public T Value;
    }

    public class BoolSetting : Setting<bool> {
        public BoolSetting(string name, bool defaultValue, string comment = "", bool nag = true, bool hidden = false) : base(name, defaultValue, comment, nag, hidden) {
        }

        public override void Parse(string value) {
            Value = bool.Parse(value);
        }

        public override string ValidValues() => "[True|False]";
    }

    public class FloatSetting : Setting<float> {
        public FloatSetting(string name, float defaultValue, string comment = "", bool nag = true, bool hidden = false) : base(name, defaultValue, comment, nag, hidden) {
        }

        public override void Parse(string value) {
            Value = float.Parse(value);
        }

        public override string ValidValues() => "A decimal number";
    }

    public class HostSetting : Setting<String> {
        public HostSetting(string name, string defaultValue, string comment = "", bool nag = true, bool hidden = false) : base(name, defaultValue, comment, nag, hidden) {
        }

        // host or host:port; anything else falls back to the default with the usual nag
        public override void Parse(string value) {
            var parts = (value ?? "").Trim().TrimEnd('/').Split(':');
            int port;
            if (parts.Length > 2 || Uri.CheckHostName(parts[0]) == UriHostNameType.Unknown
                    || (parts.Length == 2 && !int.TryParse(parts[1], out port))) {
                throw new FormatException("expected host or host:port");
            }

            Value = string.Join(":", parts);
        }

        public override string ValidValues() => "A host name (host or host:port), no protocol";
    }

    public class StringSetting : Setting<String> {
        public StringSetting(string name, string defaultValue, string comment = "", bool nag = true, bool hidden = false) : base(name, defaultValue, comment, nag, hidden) {
        }

        public override void Parse(string value) {
            Value = string.IsNullOrEmpty(value) ? Default : value.Trim();
        }

        public override string ValidValues() => "Text";
    }

    public class ColorSetting : Setting<Color> {
        public ColorSetting(string name, Color defaultValue, float divisor, string comment = "", bool nag = true, bool hidden = false) : base(name, defaultValue, comment, nag, hidden) {
            this.divisor = divisor;
        }

        public override string ValidValues() => "R,G,B,A (more details at top of file)";

        public override void Parse(string value) {
            var parts = value.Split(',');
            Value = new Color(float.Parse(parts[0]) / divisor, float.Parse(parts[1]) / divisor, float.Parse(parts[2]) / divisor, float.Parse(parts[3]) / divisor);
        }

        public override string ToString() {
            return String.Format("{0:F0}, {1:F0}, {2:F0}, {3:F0}", Value.r * divisor, Value.g * divisor, Value.b * divisor, Value.a * divisor);
        }

        public float divisor;
    }

    public class EnumSetting<T> : Setting<T> where T : Enum {
        public EnumSetting(string name, T defaultValue, string comment = "", bool nag = true, bool hidden = false) : base(name, defaultValue, comment, nag, hidden) {
        }

        public override void Parse(string value) {
            Value = (T)Enum.Parse(typeof(T), value, true);
        }

        public override string ValidValues() => $"{String.Join("|", Enum.GetNames(typeof(T)))}";
    }
}
