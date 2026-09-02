using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

// The parts of a container that act during a run: its boxes, the condition that
// ends the attempt, a variant's loadout, and what the locations hold. Parsed at
// Begin, the boxes again whenever they are edited, and checked against Ori's
// position every frame.
public class PracticeSegment {
    public List<RandomizerBox> Boxes = new List<RandomizerBox>();

    public Rect? GoalArea;

    public List<string> EndItems = new List<string>();

    public List<int> EndLocations = new List<int>();

    public int EndCount = -1;

    public bool HasEnd {
        get { return GoalArea.HasValue || EndItems.Count > 0 || EndLocations.Count > 0 || EndCount >= 0; }
    }

    // What a variant adds to its segment: its own items to start with and its own
    // boxes on top of the shared ones. The ending is shared.
    public List<RandomizerAction> StartingItems = new List<RandomizerAction>();

    // the pause menu stays the game's own, and Exit keeps the session, when this is set
    public bool QuitToMenu;

    // The shared boxes, then the variant's; the goal is the first goal box among them.
    public static PracticeSegment Parse(BfrpFile file, string variant) {
        var seg = Parse(file.Segment);
        seg.Boxes.AddRange(file.Boxes(""));
        var json = file.VariantSegment(variant);
        if (json.IsObject) {
            if (json["end"].IsObject) {
                Randomizer.LogError("practice: variants share the segment's end condition; ignoring this one's");
            }

            seg.Boxes.AddRange(file.Boxes(variant));
            var items = json["inventory"];
            for (var i = 0; i < items.Count; i++) {
                var action = Action(items[i]);
                if (action != null) {
                    seg.StartingItems.Add(action);
                }
            }
        }

        foreach (var box in seg.Boxes) {
            if (box.Type == RandomizerBox.Kind.Goal) {
                seg.GoalArea = box.Area;
                break;
            }
        }

        return seg;
    }

    private static RandomizerAction Action(JsonValue value) {
        if (!value.IsString) {
            return null;
        }

        var bar = value.Str.IndexOf('|');
        if (bar < 1) {
            Randomizer.LogError("practice: '" + value.Str + "' is not a pickup");
            return null;
        }

        return new RandomizerAction(value.Str.Substring(0, bar), value.Str.Substring(bar + 1));
    }

    public static PracticeSegment Parse(JsonValue json) {
        var seg = new PracticeSegment();
        seg.QuitToMenu = json["qtm_enabled"].IsBool && json["qtm_enabled"].Flag;
        var end = json["end"];
        if (end.IsObject) {
            for (var i = 0; i < end["items"].Count; i++) {
                seg.EndItems.Add(end["items"][i].Str);
            }

            // Checked once here rather than every frame: an unknown coord makes
            // HaveCoord shout, and a typo would bury the screen in it.
            for (var i = 0; i < end["locations"].Count; i++) {
                var key = (int)end["locations"][i].Num;
                if (RandomizerLocationManager.LocationsByKey.ContainsKey(key)) {
                    seg.EndLocations.Add(key);
                } else {
                    Randomizer.LogError("practice: segment wants location " + key + ", which is not a place");
                }
            }

            if (end["count"].IsNumber) {
                seg.EndCount = (int)end["count"].Num;
            }
        }

        return seg;
    }

    // What the attempt's locations hold: the placement lines, shared then the
    // variant's, and then each shuffle group's pickups scattered over its
    // locations, chosen fresh.
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
            if (parts.Length < 3 || !int.TryParse(parts[0], out coord)) {
                Randomizer.LogError("practice: '" + line + "' is not a placement");
                continue;
            }

            table[coord] = new RandomizerAction(parts[1], parts[2]);
        }

        var random = new System.Random();
        var groups = file.Segment["shuffle"];
        for (var g = 0; g < groups.Count; g++) {
            var give = groups[g]["give"];
            var among = groups[g]["among"];
            var spots = new List<int>();
            for (var i = 0; i < among.Count; i++) {
                if (among[i].IsNumber) {
                    spots.Add((int)among[i].Num);
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

    public void Check(Vector2 at) {
        RandomizerBoxes.Check(at, Boxes);
    }

    // Every clause present must hold at once; a goal box is only satisfied
    // while Ori is standing in it.
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
        { 50, AbilityType.Dash },
        { 51, AbilityType.Grenade }
    };

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
