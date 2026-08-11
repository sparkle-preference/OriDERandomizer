using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Game;
using Protogen;
using UnityEngine;

public class RandomizerLocationManager {
    public static void Initialize() {
        var reader = new StringReader(RandomizerLocationData.All);
        var line = reader.ReadLine();
        while (line != null) {
            var newLocation = new Location(line.Trim());
            LocationsByName[newLocation.Name] = newLocation;
            LocationsByKey[newLocation.Key] = newLocation;

            if (newLocation.Type == Location.LocationType.ProgressiveMap) {
                ProgressiveMapLocations[newLocation.Difficulty] = newLocation;
            } else {
                LocationsByGuid[newLocation.MoonGuid] = newLocation;
                LocationsByWorldMapGuid[newLocation.WorldMapGuid] = newLocation;
            }

            line = reader.ReadLine();
        }

        for (var index = 0; index < RandomizerLocationData.KeystoneDoors.Count; ++index) {
            var ksDoor = new KeystoneDoor(index, RandomizerLocationData.KeystoneDoors[index]);
            KeystoneDoors[ksDoor.MoonGuid] = ksDoor;
            KeystoneDoorMapGuidToMoonGuid[ksDoor.MapGuid] = ksDoor.MoonGuid;
        }

        if (!HaveDownloadedAreas && DLThread == null) {
            DLThread = new Thread(DownloadAreas);
            DLThread.Start();
        }
    }

    public static void InitializeLogic() {
        if (!HaveDownloadedAreas) {
            return; //Areas thread hasn't returned yet, skip. It'll run this on its own on completion.
        }

        var paths = InitializePaths();

        if (!File.Exists("areas.ori")) {
            Areas = null;
            s_logicLastUpdated = DateTime.MinValue;
            s_lastLogicPaths = paths;
            Randomizer.log("No areas.ori found, will not update logic.");
            RandomizerSettings.CurrentFilter = RandomizerSettings.MapFilterMode.Uncollected;
            return;
        }

        spawnNodeName = "SunkenGladesRunaway";
        if (Randomizer.SpawnWith.Contains("WS")) {
            foreach (var kvp in stupidBullshit) {
                if (Randomizer.SpawnWith.EndsWith(kvp.Key)) {
                    spawnNodeName = kvp.Value;
                    break;
                }
            }
        }

        if (s_logicLastUpdated == DateTime.MinValue || File.GetLastWriteTime("areas.ori") > s_logicLastUpdated || !paths.SetEquals(s_lastLogicPaths)) {
            Areas = OriParse.Parse("areas.ori", paths);
            s_logicLastUpdated = File.GetLastWriteTime("areas.ori");
            s_lastLogicPaths = paths;

            foreach (var location in LocationsByName.Values) {
                location.Reachable = false;
            }
        }
    }

    private static HashSet<string> InitializePaths() {
        var paths = new HashSet<string>();

        var flagLine = Randomizer.SeedMeta.Split(new[] { '|' }, 1)[0];
        var firstComma = flagLine.IndexOf(',');
        if (firstComma < 0) {
            return paths;
        }

        var preset = flagLine.Substring(0, firstComma);

        if (preset.StartsWith("Sync")) {
            var secondComma = flagLine.IndexOf(',', firstComma + 1);
            if (secondComma < 0) {
                return paths;
            }

            preset = flagLine.Substring(firstComma + 1, secondComma - firstComma - 1);
        }

        switch (preset) {
            case "Casual":
                paths.Add("casual-core");
                paths.Add("casual-dboost");
                break;
            case "Standard":
                paths.Add("casual-core");
                paths.Add("casual-dboost");
                paths.Add("standard-core");
                paths.Add("standard-dboost");
                paths.Add("standard-lure");
                paths.Add("standard-abilities");
                break;
            case "Expert":
                paths.Add("casual-core");
                paths.Add("casual-dboost");
                paths.Add("standard-core");
                paths.Add("standard-dboost");
                paths.Add("standard-lure");
                paths.Add("standard-abilities");
                paths.Add("expert-core");
                paths.Add("expert-dboost");
                paths.Add("expert-lure");
                paths.Add("expert-abilities");
                paths.Add("dbash");
                break;
            case "Master":
                paths.Add("casual-core");
                paths.Add("casual-dboost");
                paths.Add("standard-core");
                paths.Add("standard-dboost");
                paths.Add("standard-lure");
                paths.Add("standard-abilities");
                paths.Add("expert-core");
                paths.Add("expert-dboost");
                paths.Add("expert-lure");
                paths.Add("expert-abilities");
                paths.Add("dbash");
                paths.Add("master-core");
                paths.Add("master-dboost");
                paths.Add("master-lure");
                paths.Add("master-abilities");
                paths.Add("gjump");
                break;
            default:
                if (preset.StartsWith("Custom")) {
                    if (int.TryParse(preset.Remove(0, "Custom".Length), out var pathMask)) {
                        var newPaths = OriParse.PathMaskToPathSet(pathMask);
                        if (newPaths != null) {
                            //Randomizer.log("Got custom pathset: " + OriParse.PathMaskToString(pathMask));
                            paths = newPaths;
                        }
                    }
                }

                paths.Add("casual-core");
                break;
        }

        return paths;
    }

    public static RandomizerPickupAction AddPickupAction(GameObject parentObj, string pickupName, string actionName = null) {
        if (!LocationsByName.ContainsKey(pickupName)) {
            return null;
        }

        var obj = new GameObject(actionName != null ? actionName : "pickupAction");
        obj.transform.parent = parentObj.transform;

        var pickupAction = obj.AddComponent<RandomizerPickupAction>();
        pickupAction.MoonGuid = new MoonGuid(LocationsByName[pickupName].MoonGuid);
        pickupAction.LocationName = pickupName;
        return pickupAction;
    }

    public static void PlacePickup(int key, string action, object value, bool repeatable = false) {
        if (!LocationsByKey.ContainsKey(key)) {
            Randomizer.printInfo("Error: Unknown location key " + key + " in seed file " + Randomizer.SeedFilePath);
            return;
        }

        var pickupLocation = LocationsByKey[key];
        pickupLocation.Pickup = new RandomizerAction(action, value);
        pickupLocation.Repeatable = repeatable;
    }

    public static bool IsPickupCollected(MoonGuid pickupGuid) {
        if (LocationsByGuid.ContainsKey(pickupGuid)) {
            return LocationsByGuid[pickupGuid].Collected;
        }

        return false;
    }

    public static bool HasPickupBeenTouched(MoonGuid pickupGuid) {
        if (LocationsByGuid.ContainsKey(pickupGuid)) {
            return LocationsByGuid[pickupGuid].Touched;
        }

        return false;
    }

    public static bool IsPickupRepeatable(MoonGuid pickupGuid) {
        if (LocationsByGuid.ContainsKey(pickupGuid)) {
            return LocationsByGuid[pickupGuid].Repeatable;
        }

        return false;
    }

    public static void GivePickup(MoonGuid pickupGuid) {
        if (LocationsByGuid.ContainsKey(pickupGuid)) {
            LocationsByGuid[pickupGuid].Give();
        }
    }

    public static void GivePickupByWorldMapGuid(MoonGuid pickupMapGuid) {
        if (LocationsByWorldMapGuid.ContainsKey(pickupMapGuid)) {
            LocationsByWorldMapGuid[pickupMapGuid].Give();
        }
    }

    public static void OpenDoorByGuid(MoonGuid doorGuid) {
        var door = KeystoneDoors[doorGuid];
        var current = Randomizer.Inventory.GetRandomizerItem(72);
        Randomizer.Inventory.SetRandomizerItem(72, current | (1 << door.Index));
        UpdateReachable();
    }

    public static bool IsDoorOpen(MoonGuid doorGuid) => 1 == (1 & Randomizer.Inventory.GetRandomizerItem(72) >> KeystoneDoors[doorGuid].Index);

    public static void UpdateReachable() {
        if (LogicThread != null && LogicThread.IsAlive) {
            LogicThread.Abort();
            Randomizer.log("Killing existing logic thread");
        }

        LogicThread = new Thread(UpdateReachableWorker);
        LogicThread.Start();
    }

    // logic init waits on this download, and once the plain-http host is
    // retired the endpoint may black-hole instead of refusing — a capped
    // timeout keeps the worst case at a 5s one-time delay before falling
    // back to the shipped areas.ori
    private class QuickWebClient : WebClient {
        protected override WebRequest GetWebRequest(Uri address) {
            var req = base.GetWebRequest(address);
            req.Timeout = 5000;
            return req;
        }
    }

    // websocket areas refresh: the client offers its local hash after
    // connecting; a server with newer logic answers with the whole file
    // (areas:<content> frame). Survives the http host's retirement.
    public static string AreasHash() {
        try {
            if (!File.Exists("areas.ori")) {
                return "none";
            }

            using (var sha = SHA256.Create()) {
                var h = sha.ComputeHash(File.ReadAllBytes("areas.ori"));
                var sb = new StringBuilder();
                foreach (var b in h) {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        } catch (Exception e) {
            Randomizer.log($"AreasHash: {e.Message}");
            return "none";
        }
    }

    public static void ApplyAreasUpdate(string content) {
        new Thread(() => ApplyAreasWorker(content)).Start();
    }

    private static void ApplyAreasWorker(string content) {
        try {
            if (!RandomizerSettings.DevSettings.AreasOri) {
                return; // flag disables areas worker
            }

            if (File.Exists("areas.ori")) {
                File.Move("areas.ori", "areas.ori.old"); // backup
            }

            File.WriteAllText("areas.ori", content);
            if (File.Exists("areas.ori.old")) {
                File.Delete("areas.ori.old");
            }

            Randomizer.log("ws: areas.ori updated from server, reloading logic");
            InitializeLogic();
        } catch (Exception e) {
            Randomizer.log($"ApplyAreasUpdate: {e}");
            if (!File.Exists("areas.ori") && File.Exists("areas.ori.old")) {
                File.Move("areas.ori.old", "areas.ori");
            }
        }
    }

    public static void DownloadAreas() {
        if (RandomizerSettings.DevSettings.AreasOri) {
            var webClient = new QuickWebClient();
            try {
                if (File.Exists("areas.ori")) {
                    File.Move("areas.ori", "areas.ori.old"); // backup
                }

                ServicePointManager.ServerCertificateValidationCallback = (a, b, c, d) => true;
                webClient.DownloadFile(AreasURL(), "areas.ori");
                if (File.Exists("areas.ori.old")) {
                    File.Delete("areas.ori.old"); // clean backup
                }
            } catch (Exception e) {
                Randomizer.LogError($"Failed to download areas.ori: ${e}");
                if (File.Exists("areas.ori")) {
                    File.Delete("areas.ori"); // remove broken / failed
                }

                if (File.Exists("areas.ori.old")) {
                    File.Move("areas.ori.old", "areas.ori"); // restore backup
                }
            }
        }

        HaveDownloadedAreas = true;
        InitializeLogic();
    }

    public static void UpdateReachableWorker() {
        if (Areas == null) {
            return;
        }

        var currentInventory = Inventory.FromCharacter();
        currentInventory.Unlocks.Add("Mapstone");

        if (Randomizer.Inventory.GetRandomizerItem(71) > currentInventory.Mapstones) {
            currentInventory.Mapstones = Randomizer.Inventory.GetRandomizerItem(71);
        }

        if (Randomizer.OpenMode) {
            currentInventory.Unlocks.Add("Open");
        }

        if (Randomizer.OpenWorld) {
            currentInventory.Unlocks.Add("OpenWorld");
        }

        var primedPaths = new Dictionary<string, HashSet<string>>();
        var keystoneDoorsOpened = Randomizer.Inventory.GetRandomizerItem(72);

        foreach (var ksDoor in KeystoneDoors.Values) {
            if ((keystoneDoorsOpened & (1 << ksDoor.Index)) == 0) {
                continue;
            }

            if (!primedPaths.ContainsKey(ksDoor.Source)) {
                primedPaths[ksDoor.Source] = new HashSet<string>();
            }

            primedPaths[ksDoor.Source].Add(ksDoor.Destination);
        }

        if (Randomizer.InLogicWarps) {
            foreach (var teleporter in TeleporterController.Instance.Teleporters) {
                if (Randomizer.WarpLogicLocations.Contains(teleporter.Identifier)) {
                    if (!primedPaths.ContainsKey(spawnNodeName)) {
                        primedPaths[spawnNodeName] = new HashSet<string>();
                    }

                    primedPaths[spawnNodeName].Add((string)Randomizer.WarpLogicLocations[teleporter.Identifier]);
                }
            }
        }

        var reachable = OriReachable.Reachable(Areas, currentInventory, spawnNodeName, primedPaths);

        if (reachable.Contains("FronkeyFight")) {
            // hacky hack hack
            reachable.Add("FirstEnergyCell");
            reachable.Add("Sein");
        }

        if (reachable.Contains("ForlornEscape")) {
            reachable.Add("ForlornEscapePlant");
        }

        foreach (var item in LocationsByName) {
            item.Value.Reachable = reachable.Contains(item.Key);
        }

        // key-lock warning mask. KeyTiers seeds charge per-door tiers against
        // lifetime keystones (item 70); other seeds share the conservative
        // gate: unspent supply must cover every visible unopened door
        var doorMask = 0;
        var ksCollected = Randomizer.Inventory.GetRandomizerItem(70);
        var ksUnspent = ksCollected - SpentKeystones();
        var visibleCost = 0;
        foreach (var ksDoor in KeystoneDoors.Values) {
            if ((keystoneDoorsOpened & (1 << ksDoor.Index)) != 0 || !reachable.Contains(ksDoor.Source)) {
                continue;
            }

            var pos = DoorWirePos[ksDoor.Source];
            if (Randomizer.OpenWorld && pos == 0) {
                continue;   // the Glades door is pre-opened
            }

            if (KeyTiers != null) {
                if (pos < KeyTiers.Length && KeyTiers[pos] > 0 && ksCollected >= KeyTiers[pos]) {
                    doorMask |= 1 << ksDoor.Index;
                }
            } else {
                visibleCost += DoorCosts[pos];
            }
        }
        if (KeyTiers == null && ksUnspent >= visibleCost) {
            foreach (var ksDoor in KeystoneDoors.Values) {
                if ((keystoneDoorsOpened & (1 << ksDoor.Index)) == 0 && reachable.Contains(ksDoor.Source)) {
                    doorMask |= 1 << ksDoor.Index;
                }
            }
        }

        DoorsInLogicMask = doorMask;
        DoorLogicValid = true;
/* can toggle this on for debugging but logging in a thread is spoopy and the conditionals are more work than overwriting bools
        {
            if (reachable.Contains(item.Key))
            {
                item.Value.Reachable = true;
            }
            else if (item.Value.Reachable)
            {
                Randomizer.log("!!!! " + item.Key + " became unreachable!"); // can toggle this on for debugging but logging in a thread is spoopy
                item.Value.Reachable = false;
            }
        }*/
    }

    public static Dictionary<MoonGuid, Location> LocationsByGuid = new Dictionary<MoonGuid, Location>();

    public static Dictionary<MoonGuid, Location> LocationsByWorldMapGuid = new Dictionary<MoonGuid, Location>();

    public static Dictionary<string, Location> LocationsByName = new Dictionary<string, Location>();

    public static Dictionary<int, Location> LocationsByKey = new Dictionary<int, Location>();

    public static Location[] ProgressiveMapLocations = new Location[9];

    public static AreaGraph Areas;

    public static Thread LogicThread;

    public static Thread DLThread;

    public static bool HaveDownloadedAreas;

    public static string AreasURL() => $"http://{RandomizerSettings.DevSettings.WebEndpoint.Value}/netcode/areas";

    private static DateTime s_logicLastUpdated = DateTime.MinValue;

    private static HashSet<string> s_lastLogicPaths;

    private static Dictionary<string, string> stupidBullshit = new Dictionary<string, string> {
        { "-159,-114,force", "SpiritTreeRefined" },
        { "491,-73,force", "SwampTeleporter" },
        { "519,-174,force", "MoonGrotto" },
        { "-914,-298,force", "ForlornTeleporter" },
        { "-430,0,force", "ValleyTeleporter" },
        { "88,142,force", "HoruTeleporter" },
        { "570,539,force", "GinsoTeleporter" },
        { "-594,496,force", "SorrowTeleporter" },
        { "381,-297,force", "BlackrootGrottoConnection" }
    };

    private static string spawnNodeName;

    public static Dictionary<MoonGuid, KeystoneDoor> KeystoneDoors = new Dictionary<MoonGuid, KeystoneDoor>();

    // --- key-lock warnings (non-keysanity): is each unopened door in logic? ---
    // recomputed with reachability; RAM-only, so nothing can warn before the
    // first sweep of a loaded save
    public static int DoorsInLogicMask;
    public static bool DoorLogicValid;
    // wire order, from the seed's KeyTiers flag (AP exported-keystone seeds)
    public static int[] KeyTiers;
    private static HashSet<MoonGuid> warnedDoors = new HashSet<MoonGuid>();
    // wire position by home node -- archipelago shared.KEYSTONE_DOORS order --
    // and in-game key costs by wire position
    private static readonly Dictionary<string, int> DoorWirePos = new Dictionary<string, int> {
        {"GladesFirstKeyDoor", 0}, {"SpiritCavernsDoor", 1}, {"GumoHideout", 2},
        {"SwampKeyDoorPlatform", 3}, {"SpiritTreeDoor", 4}, {"BashTreeDoorClosed", 5},
        {"UpperGinsoDoorClosed", 6}, {"MistyBeforeMiniBoss", 7}, {"ForlornKeyDoor", 8},
        {"LowerSorrow", 9}, {"LeftSorrowMiddleDoorClosed", 10}, {"ChargeJumpDoor", 11}};
    private static readonly int[] DoorCosts = {2, 2, 2, 2, 4, 4, 4, 4, 4, 4, 4, 4};

    public static void ResetDoorLogic() {
        DoorsInLogicMask = 0;
        DoorLogicValid = false;
        KeyTiers = null;
        warnedDoors.Clear();
    }

    // keys sunk into opened doors; refunds mean partial fills never count
    public static int SpentKeystones() {
        var opened = Randomizer.Inventory.GetRandomizerItem(72);
        var spent = 0;
        foreach (var ksDoor in KeystoneDoors.Values) {
            if ((opened & (1 << ksDoor.Index)) != 0) {
                spent += DoorCosts[DoorWirePos[ksDoor.Source]];
            }
        }

        return spent;
    }

    /// <summary>
    /// DoorWithSlots.Highlight: doors take one click per key, so this is
    /// advice ahead of the first click, never a block. Quiet for keysanity
    /// (its doors can't waste keys), quiet once per door, and quiet forever
    /// on a save that has keyduped (the counters stop meaning anything).
    /// </summary>
    public static void WarnIfDoorOutOfLogic(MoonGuid doorGuid) {
        if (!RandomizerSettings.Customization.KeyLockWarnings.Value || Randomizer.Keysanity.IsActive
            || !DoorLogicValid || !KeystoneDoors.ContainsKey(doorGuid)) {
            return;
        }

        var door = KeystoneDoors[doorGuid];
        if (IsDoorOpen(doorGuid) || (DoorsInLogicMask & (1 << door.Index)) != 0 || warnedDoors.Contains(doorGuid)) {
            return;
        }

        if (Randomizer.Inventory.GetRandomizerItem(73) != 0) {
            return;
        }

        if (Characters.Sein.Inventory.Keystones + SpentKeystones() > Randomizer.Inventory.GetRandomizerItem(70)) {
            Randomizer.Inventory.SetRandomizerItem(73, 1);
            return;
        }
        warnedDoors.Add(doorGuid);
        RandomizerSwitch.PickupMessage("This door is not currently in logic.\n Opening it at this time might render your seed uncompletable.", 300);
    }

    public static Dictionary<MoonGuid, MoonGuid> KeystoneDoorMapGuidToMoonGuid = new Dictionary<MoonGuid, MoonGuid>();

    public class Location {
        public Location(string locationData) {
            var parts = locationData.Split();
            Name = parts[0];
            FriendlyName = Regex.Replace(Name, "([A-Z0-9]+)", " $1") + "\n" + parts[5];
            Position = new Vector2(float.Parse(parts[1]), float.Parse(parts[2]));
            Type = (LocationType)Enum.Parse(typeof(LocationType), parts[3]);
            Difficulty = int.Parse(parts[4]);
            Zone = parts[5];

            MoonGuid = new MoonGuid(int.Parse(parts[6]), int.Parse(parts[7]), int.Parse(parts[8]), int.Parse(parts[9]));

            if (parts.Length >= 14) {
                WorldMapGuid = new MoonGuid(int.Parse(parts[10]), int.Parse(parts[11]), int.Parse(parts[12]), int.Parse(parts[13]));
            } else {
                WorldMapGuid = MoonGuid;
            }

            if (Type == LocationType.Skill || Type == LocationType.Map) {
                SpecialIndex = int.Parse(parts[parts.Length - 1]);
            }
        }

        public void Give() {
            // special case for Sein pickup because it doesn't technically have a valid location key
            if (Type == LocationType.Skill && SpecialIndex == 0) {
                RandomizerTrackedDataManager.SetTree(0);
                Characters.Sein.PlayerAbilities.SetAbility(AbilityType.SpiritFlame, true);
                if (Randomizer.EnhancedMode) {
                    RandomizerBonus.UpgradeID(410);
                }

                TeleporterController.Activate("sunkenGlades");
                return;
            }

            if (Collected) {
                return;
            }

            switch (Type) {
                case LocationType.Map:
                    RandomizerTrackedDataManager.SetMapstone(SpecialIndex);
                    break;
                case LocationType.Skill:
                    RandomizerTrackedDataManager.SetTree(SpecialIndex);
                    break;
            }

            if (Type == LocationType.Map && Randomizer.ProgressiveMapStones) {
                ProgressiveMapLocations[RandomizerBonus.MapStoneProgression()].Give();
                return;
            }

            if (Randomizer.ColorShift) {
                Randomizer.changeColor();
            }

            if (Type == LocationType.ProgressiveMap) {
                RandomizerBonus.CollectMapstone();
                RandomizerStatsManager.FoundMapstone();
            } else {
                RandomizerStatsManager.IncPickup(Key);
            }

            BingoController.OnLoc(Key);
            if (Key == -7320236) {
                Randomizer.Inventory.SetRandomizerItem(1106, 1);
            }

            RandomizerSwitch.GivePickup(Pickup, Key);
            UpdateReachable();

            if (Randomizer.HotColdItems.ContainsKey(Key)) {
                Randomizer.Inventory.SetRandomizerItem(Randomizer.HotColdItems[Key].Id, 1);
                RandomizerColorManager.UpdateHotColdTarget();
            } else if (Randomizer.HotColdFrags.ContainsKey(Key)) {
                Randomizer.Inventory.SetRandomizerItem(Randomizer.HotColdFrags[Key].Id, 1);
                RandomizerColorManager.UpdateHotColdTarget();
            }

            if (Type == LocationType.Skill) {
                Randomizer.showProgress();
            }
        }

        public int Key => (int)(Mathf.Floor((int)Position.x / 4f) * 4f) * 10000 + (int)(Mathf.Floor((int)Position.y / 4f) * 4f);

        public bool Collected => Repeatable ? false : Type == LocationType.Map ? RandomizerTrackedDataManager.GetMapstone(SpecialIndex) : Randomizer.HaveCoord(Key);

        // a self-AP location whose slot is already granted has nothing left to
        // give, so the in-logic filter should stop showing it even after a
        // death rolled the coord bit back
        public bool Touched => Collected || Repeatable && Randomizer.HaveCoord(Key)
            || RandomizerMW.SelfItemCollected(Key);

        public MoonGuid MoonGuid;

        public string Name;

        public string FriendlyName;

        public Vector2 Position;

        public LocationType Type;

        public int Difficulty;

        public string Zone;

        public MoonGuid WorldMapGuid;

        public RandomizerAction Pickup;

        public bool Repeatable;

        public int SpecialIndex;

        public bool Reachable;

        public enum LocationType {
            ExpSmall,
            ExpMedium,
            ExpLarge,
            HealthCell,
            EnergyCell,
            AbilityCell,
            Keystone,
            Mapstone,
            Skill,
            Plant,
            Map,
            Event,
            Cutscene,
            ProgressiveMap
        }
    }

    public class KeystoneDoor {
        public KeystoneDoor(int index, string doorData) {
            var parts = doorData.Split();
            Index = index;
            Source = parts[0];
            Destination = parts[1];
            MoonGuid = new MoonGuid(int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5]));
            MapGuid = new MoonGuid(int.Parse(parts[6]), int.Parse(parts[7]), int.Parse(parts[8]), int.Parse(parts[9]));
        }

        public int Index;

        public string Source;

        public string Destination;

        public MoonGuid MoonGuid;

        public MoonGuid MapGuid;
    }
}
