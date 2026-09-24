using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

// What a container does during a run: boxes, the end condition, a variant's loadout, placements.
// Parsed at Begin and on every edit; Check and Met run every frame.
public class PracticeSegment {
    public List<RandomizerBox> Boxes = new List<RandomizerBox>();

    public Rect? GoalArea;

    public List<string> EndItems = new List<string>();

    public List<int> EndLocations = new List<int>();

    public int EndCount = -1;

    public bool HasEnd {
        get { return GoalArea.HasValue || EndItems.Count > 0 || EndLocations.Count > 0 || EndCount >= 0; }
    }

    // a variant's own loadout; its boxes join the shared ones, and the ending stays shared
    public List<RandomizerAction> StartingItems = new List<RandomizerAction>();

    // the pause menu stays the game's own, and Exit keeps the session, when this is set
    public bool QuitToMenu;

    // what the segment is for, in the maker's words; shown once a session before the first run
    public string About = "";

    // set while the chooser lists segments: parse problems go to the log under the file's name
    private static string quietFile;

    // HasEnd for the chooser, which must not paint every file's problems over itself
    public static bool Ends(BfrpFile file, string variant) {
        quietFile = file.Path;
        try {
            return Parse(file, variant).HasEnd;
        } finally {
            quietFile = null;
        }
    }

    public static void Report(string problem) {
        if (quietFile == null) {
            Randomizer.LogError("practice: " + problem);
        } else {
            Randomizer.log("practice: " + quietFile + ": " + problem);
        }
    }

    // The shared boxes, then the variant's; the goal is the first shared goal box.
    public static PracticeSegment Parse(BfrpFile file, string variant) {
        var seg = Parse(file.Segment);
        var about = file.Segment["about"];
        if (about.IsString) {
            seg.About = about.Str;
        }

        seg.Boxes.AddRange(file.Boxes(""));
        var json = file.VariantSegment(variant);
        if (json.IsObject) {
            if (json["end"].IsObject) {
                Report("variants share the segment's end condition; ignoring this one's");
            }

            var own = file.Boxes(variant);
            if (own.RemoveAll(box => box.Goal) > 0) {
                Report("variants share the segment's goal box; ignoring this one's");
            }

            seg.Boxes.AddRange(own);
            var items = json["inventory"];
            for (var i = 0; i < items.Count; i++) {
                var action = Action(items[i]);
                if (action != null) {
                    seg.StartingItems.Add(action);
                }
            }
        }

        foreach (var box in seg.Boxes) {
            if (box.Goal) {
                seg.GoalArea = box.Rect;
                break;
            }
        }

        return seg;
    }

    private static RandomizerAction Action(JsonValue value) {
        if (!value.IsString) {
            return null;
        }

        var action = Pickup(value.Str);
        if (action == null) {
            Report("'" + value.Str + "' is not a pickup");
        }

        return action;
    }

    public static bool IsPickup(string code) {
        return Pickup(code) != null;
    }

    // null for a code RandomizerAction cannot build, like SK|abc or EX|1.5
    private static RandomizerAction Pickup(string code) {
        var bar = code.IndexOf('|');
        return bar < 1 ? null : Pickup(code.Substring(0, bar), code.Substring(bar + 1));
    }

    private static RandomizerAction Pickup(string kind, string value) {
        try {
            return new RandomizerAction(kind, value);
        } catch (FormatException) {
            return null;
        } catch (OverflowException) {
            return null;
        }
    }

    public static PracticeSegment Parse(JsonValue json) {
        var seg = new PracticeSegment();
        seg.QuitToMenu = json["qtm_enabled"].IsBool && json["qtm_enabled"].Flag;
        var end = json["end"];
        if (end.IsObject) {
            // validated here, never per frame: Met must neither throw nor make HaveCoord print
            for (var i = 0; i < end["items"].Count; i++) {
                var item = end["items"][i];
                if (item.IsString && Holdable(item.Str)) {
                    seg.EndItems.Add(item.Str);
                } else {
                    Report("segment wants item " + item.Serialize(false) + ", which is not a skill or world event");
                }
            }

            for (var i = 0; i < end["locations"].Count; i++) {
                var key = (int)end["locations"][i].Num;
                if (CanEnd(key)) {
                    seg.EndLocations.Add(key);
                } else if (RandomizerLocationManager.LocationsByKey.ContainsKey(key)) {
                    Report("segment wants location " + key + ", which cannot end a segment");
                } else {
                    Report("segment wants location " + key + ", which is not a place");
                }
            }

            if (end["count"].IsNumber) {
                seg.EndCount = (int)end["count"].Num;
            }
        }

        return seg;
    }

    // Sein gives Spirit Flame before practice sees it, and Map1-9 are only reached past practice's branch
    public static bool NeverGiven(int key) {
        RandomizerLocationManager.Location location;
        return RandomizerLocationManager.LocationsByKey.TryGetValue(key, out location)
            && (location.Type == RandomizerLocationManager.Location.LocationType.ProgressiveMap
                || location.Type == RandomizerLocationManager.Location.LocationType.Skill && location.SpecialIndex == 0);
    }

    // Met reads coord bits, and practice sets one only for a CoordsMap location it gives
    public static bool CanEnd(int key) {
        return RandomizerTrackedDataManager.CoordsMap.ContainsKey(key) && !NeverGiven(key);
    }

    // What the attempt's locations hold: placement lines, shared then the variant's, then each
    // shuffle group's gives scattered afresh over its candidates, overriding a line.
    public static Dictionary<int, RandomizerAction> ResolvePlacements(BfrpFile file, string variant) {
        var table = new Dictionary<int, RandomizerAction>();
        var lines = file.PlacementLines("");
        lines.AddRange(file.PlacementLines(variant));
        foreach (var line in lines) {
            if (RandomizerBox.IsLine(line) || line.StartsWith("//")) {
                continue;
            }

            var parts = line.Split('|');
            int coord;
            RandomizerAction action;
            if (parts.Length < 3 || !int.TryParse(parts[0], out coord) || (action = Pickup(parts[1], parts[2])) == null) {
                Report("'" + line + "' is not a placement");
                continue;
            }

            table[coord] = action;
        }

        var random = new System.Random();
        var groups = file.Segment["shuffle"];
        for (var g = 0; g < groups.Count; g++) {
            var give = groups[g]["give"];
            var among = groups[g]["among"];
            var spots = new List<int>();
            for (var i = 0; i < among.Count; i++) {
                if (!among[i].IsNumber || spots.Contains((int)among[i].Num)) {
                    continue;
                }

                var spot = (int)among[i].Num;
                if (NeverGiven(spot)) {
                    Report("shuffle group wants location " + spot + ", which cannot hold a pickup");
                } else {
                    spots.Add(spot);
                }
            }

            for (var i = spots.Count - 1; i > 0; i--) {
                var j = random.Next(i + 1);
                var swap = spots[i];
                spots[i] = spots[j];
                spots[j] = swap;
            }

            for (var i = 0; i < give.Count && i < spots.Count; i++) {
                var action = Action(give[i]);
                if (action != null) {
                    table[spots[i]] = action;
                }
            }
        }

        return table;
    }

    // every clause present must hold at once; the goal box only while Ori is inside it
    public bool Met(Vector2 at) {
        if (!HasEnd) {
            return false;
        }

        if (GoalArea.HasValue && !GoalArea.Value.Contains(at)) {
            return false;
        }

        if (EndCount >= 0 && PracticeController.Get(PracticeController.Pickups) < EndCount) {
            return false;
        }

        foreach (var location in EndLocations) {
            if (!Randomizer.HaveCoord(location)) {
                return false;
            }
        }

        foreach (var item in EndItems) {
            if (!Holds(item)) {
                return false;
            }
        }

        return true;
    }

    // The skill ids a seed uses, as the ability enum the player actually holds
    private static readonly Dictionary<int, AbilityType> Abilities = new Dictionary<int, AbilityType> {
        { 0, AbilityType.Bash },
        { 2, AbilityType.ChargeFlame },
        { 3, AbilityType.WallJump },
        { 4, AbilityType.Stomp },
        { 5, AbilityType.DoubleJump },
        { 8, AbilityType.ChargeJump },
        { 12, AbilityType.Climb },
        { 14, AbilityType.Glide },
        { 15, AbilityType.SpiritFlame },
        { 50, AbilityType.Dash },
        { 51, AbilityType.Grenade }
    };

    // what Holds can answer: a skill in Abilities or one of the six world events
    private static bool Holdable(string item) {
        var bar = item.IndexOf('|');
        int id;
        if (bar < 1 || !int.TryParse(item.Substring(bar + 1), out id)) {
            return false;
        }

        var kind = item.Substring(0, bar);
        return kind == "SK" ? Abilities.ContainsKey(id) : kind == "EV" && id >= 0 && id <= 5;
    }

    // "SK|3" and "EV|0" shaped: the two families v1 lets a segment ask for
    private static bool Holds(string item) {
        var bar = item.IndexOf('|');
        if (bar < 1) {
            return false;
        }

        var kind = item.Substring(0, bar);
        int id;
        if (!int.TryParse(item.Substring(bar + 1), out id)) {
            return false;
        }

        if (kind == "SK") {
            return Characters.Sein != null && Abilities.ContainsKey(id)
                && Characters.Sein.PlayerAbilities.HasAbility(Abilities[id]);
        }

        if (kind != "EV") {
            return false;
        }

        switch (id) {
            case 0: return Sein.World.Keys.GinsoTree;
            case 1: return Sein.World.Events.WaterPurified;
            case 2: return Sein.World.Keys.ForlornRuins;
            case 3: return Sein.World.Events.WindRestored;
            case 4: return Sein.World.Keys.MountHoru;
            case 5: return Sein.World.Events.WarmthReturned;
        }

        return false;
    }
}
