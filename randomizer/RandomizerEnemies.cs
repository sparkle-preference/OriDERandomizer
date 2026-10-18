using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Game;
using UnityEngine;
using Object = UnityEngine.Object;

// Enemy randomizer: a placeholder spawns the variant the seed rolled for it instead of its own enemy.
// The server rolls (seedbuilder/enemyroll.py); the //EnemyRolls= line indexes RandomizerEnemyData.Placements.
public static class RandomizerEnemies {
    public static bool Active;

    // the seed's //Enemies= line (seedbuilder/enemyopts.py); absent keys mean these
    public static string HpMode = "zone";        // zone, vanilla, or "lo-hi"
    public static string DmgMode = "zone";

    public static void ResetOptions() {
        HpMode = DmgMode = "zone";
        s_table = null;
    }

    public static void ParseOptions(string line) {
        foreach (var part in line.Trim().Split(';')) {
            var pair = part.Split(new[] { '=' }, 2);
            if (pair.Length != 2) {
                continue;
            }

            var key = pair[0].Trim().ToLower();
            var value = pair[1].Trim().ToLower();
            switch (key) {
                case "hp": HpMode = value; break;
                case "dmg": DmgMode = value; break;
            }
        }
    }

    // "version:12,,7,..." one variant per placement, empty for vanilla; a table from other data plays vanilla
    public static void SetRolls(string line) {
        Load();
        s_table = null;
        var colon = line.IndexOf(':');
        var rolls = colon < 0 ? new string[0] : line.Substring(colon + 1).Trim().Split(',');
        if (colon < 0 || line.Substring(0, colon).Trim() != RandomizerEnemyData.Version || rolls.Length != s_index.Count) {
            Randomizer.LogError("enemies: the seed's rolls don't match this build's enemy table, playing vanilla enemies");
            return;
        }

        var table = new int[rolls.Length];
        for (var i = 0; i < rolls.Length; i++) {
            // "b<N>" reads bonus N when the placeholder spawns; stored as -(N + 2), -1 is vanilla
            table[i] = rolls[i] == "" ? -1 : rolls[i].StartsWith("b") ? -2 - int.Parse(rolls[i].Substring(1), CultureInfo.InvariantCulture) : int.Parse(rolls[i], CultureInfo.InvariantCulture);
            if (table[i] >= s_variants.Count) {
                Randomizer.LogError("enemies: roll " + i + " names a variant this build doesn't have, playing vanilla enemies");
                return;
            }
        }

        s_table = table;
    }

    // zone, vanilla (the variant's own numbers), or zone times a seeded draw from lo-hi
    private static float ModeFactor(string mode, float zone, string salt) {
        if (mode == "vanilla") {
            return 1f;
        }

        var dash = mode.IndexOf('-');
        float lo, hi;
        if (dash > 0 && float.TryParse(mode.Substring(0, dash), NumberStyles.Float, CultureInfo.InvariantCulture, out lo)
                     && float.TryParse(mode.Substring(dash + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out hi)) {
            var random = new System.Random(Hash(SeedKey() + "|" + salt));
            return zone * (lo + (float)random.NextDouble() * (hi - lo));
        }

        return zone;
    }

    private class Kind {
        public string Name;
        public string Spots;
        public bool Clings;
    }

    private class Variant {
        public int Index;
        public string Prefab;
        public string Class;
        public string Settings;
        public float[] Home;
        public Kind Kind;
    }

    private static readonly string[] DamageFields = { "DamageOnTouch", "ProjectileDamage", "ExplosionDamage", "LaserDamage", "GroundStompDamage" };
    private static readonly string[] ExpFields = { "NumberOfExpOrbsToDrop", "TotalExpOrbsToDrop" };

    private static Dictionary<string, Kind> s_kindOf;
    private static List<Variant> s_variants;
    private static Dictionary<string, string> s_donors;
    private static HashSet<string> s_arena;
    private static Dictionary<string, float[]> s_zones;
    private static Dictionary<string, string> s_placements;
    private static Dictionary<string, int> s_index;   // placeholder guid -> its row in the seed's table

    private static int[] s_table;                      // the seed's variant per placement, -1 for vanilla
    private static string s_force;

    private static readonly Dictionary<string, RespawningPlaceholder> s_templates = new Dictionary<string, RespawningPlaceholder>();
    private static readonly HashSet<string> s_wanted = new HashSet<string>();

    // prefabs an on-screen placeholder is waiting for: their donors load first
    private static readonly List<string> s_urgent = new List<string>();
    private static readonly List<string> s_pins = new List<string>();
    private static float s_nextScan;
    private static float s_askedAt = -100f;
    private static bool s_warmed;

    // how far off its surface a turned walker spawns, so its capsule starts clear of the level
    public static float WalkerLift = 1.5f;

    // an elemental standing in for a spider is a small, slow one
    public static float StandInScale = 0.6f;
    public static float StandInPace = 4f / 3f;
    private static readonly string[] PacedFields = { "ChargeDuration", "ShootingDuration" };

    // how close Ori must be before a flying fish gives chase
    public static float FlyingFishReach = 14f;

    // enemies placed underwater move and fire this much slower
    public static float WetSpeed = 0.6f;

    private static readonly string[] WetSlowed = { "Speed", "MoveBackSpeed", "ProjectileSpeed", "LaserChaseSpeed", "Acceleration", "DashDistance" };
    private static readonly string[] WetDelayed = { "ChargeDuration", "ChargingDuration", "DashAlertDelay", "JumpDelay" };

    // how far a clinger looks for the surface its spot faces
    public static float SettleReach = 20f;

    // a slime admitted to a water spot faces the surface within this reach that admitted it
    public static float WaterReach = 8f;

    // a flier at a shark's spot hovers this far above it (the spot sits in the lava or the pool)
    public static float SharkLift = 6f;

    // a drop slime away from a roof hangs this far under the ceiling above its spot, as vanilla's do
    public static float DropHang = 1.4f;
    public static float HangReach = 20f;

    // testing: every rolled placeholder takes this kind or prefab; null undoes it
    public static void SetForce(string kindOrPrefab) {
        s_force = kindOrPrefab;
    }

    public static Entity Instantiate(RespawningPlaceholder target) {
        if (!Active) {
            return null;
        }

        var variant = VariantFor(target);
        var own = OwnPrefab(target);
        if (variant == null || own == null) {
            return null;
        }

        s_kindOf.TryGetValue(own.name, out var ownKind);
        var guidKey = target.MoonGuid.ToString();
        var spot = Spot(target, ownKind, out var gravity);
        var source = variant.Prefab == own.name ? target : Template(variant.Prefab);
        var maker = variant.Class == target.GetType().Name ? target : Template(variant.Prefab);
        if (source == null || maker == null) {
            return null;
        }

        var prefabField = PrefabField(maker.GetType());
        var settingsField = maker.GetType().GetField("Settings");
        var savedPrefab = prefabField.GetValue(maker);
        var savedSettings = settingsField.GetValue(maker);
        var settings = Copy(savedSettings, true);
        Apply(settings, variant.Settings);
        var factors = Factors(target, variant);
        factors[0] = ModeFactor(HpMode, factors[0], guidKey + "|hp");
        factors[1] = ModeFactor(DmgMode, factors[1], guidKey + "|dmg");
        Scale(settings, factors);
        if (maker == target) {
            KeepStomp(settings, savedSettings);
        }

        var t = maker.transform;
        var pose = new[] { t.position, t.eulerAngles, t.localScale };
        if (maker != target) {
            t.position = target.transform.position + (spot == "shark" && Takes(variant.Kind, "air") ? Vector3.up * SharkLift : Vector3.zero);
            t.rotation = !variant.Kind.Clings ? Quaternion.identity : ownKind.Clings ? target.transform.rotation : Quaternion.Euler(0f, 0f, gravity);
            Vector3 toward;
            if (spot == "water" && variant.Kind.Clings && SurfaceNear(target.transform.position, WaterReach, out toward)) {
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg + 90f);
            }
            t.localScale = new Vector3(target.transform.lossyScale.x < 0f ? -1f : 1f, 1f, 1f);
            if (variant.Kind.Name == "DropSlugs" && spot != "roof") {
                Hang(t, settings, target.transform.position);
            }
        }

        Entity entity;
        try {
            prefabField.SetValue(maker, PrefabField(source.GetType()).GetValue(source));
            settingsField.SetValue(maker, settings);
            entity = maker is ShootingSpiderPlaceholder && maker != target ? Spider((ShootingSpiderPlaceholder)maker) : maker.Instantiate();
        } finally {
            prefabField.SetValue(maker, savedPrefab);
            settingsField.SetValue(maker, savedSettings);
            if (maker != target) {
                t.position = pose[0];
                t.eulerAngles = pose[1];
                t.localScale = pose[2];
            }
        }

        if (maker != target) {
            entity.transform.parent = target.transform.parent;
            // a broken armour hands the bare rhino to the placeholder that built it, which must be the target
            if (entity is ArmouredRammingEnemy armoured) {
                armoured.OnReplacedAction = target.Adopt;
            }
        }

        if (variant.Kind.Name == "Elementals" && ownKind != null && ownKind.Name == "Spiders") {
            entity.transform.localScale *= StandInScale;
            Pace(entity, StandInPace);
        }

        // the root turns too: enemies find the player through it (PositionToPlayerPosition), and it carries the hitbox
        var walker = entity as GroundEnemy;
        if (walker != null && spot != "floor" && gravity != 0f) {
            walker.PlatformMovement.GravityAngle = gravity;
            walker.transform.rotation = Quaternion.Euler(0f, 0f, gravity);
            walker.transform.position -= walker.PlatformMovement.GravityDirection * WalkerLift;
            walker.gameObject.AddComponent<RandomizerEnemyGrip>().Walker = walker;
        }

        if (variant.Kind.Clings && maker != target) {
            Settle(entity);
        }

        var fish = entity as FishEnemy;
        if (fish != null && spot != "water") {
            Fly(fish, target);
        } else if (fish == null && (spot == "water" || WaterZone.PositionInWater(entity.transform.position))) {
            Soak(entity);
            entity.gameObject.AddComponent<RandomizerWaterBorn>();
        }

        // a shark away from its zone has nothing to call it up: a lure stands in for the zone
        var shark = entity as JumpShootShark;
        if (shark != null && spot != "shark") {
            var lure = entity.gameObject.AddComponent<RandomizerSharkLure>();
            lure.Shark = shark;
            lure.Spot = target.transform.position;
        }

        var kamikaze = entity as KamikazeSootEnemy;
        if (kamikaze != null) {
            kamikaze.Settings.ExplosionDamage = Mathf.Max(1, Mathf.RoundToInt(kamikaze.Settings.ExplosionDamage * factors[1]));
            kamikaze.DamageDealer.Damage = kamikaze.Settings.ExplosionDamage;
        }

        if (s_arena.Contains(guidKey)) {
            var health = Enemy.ScaleHealth(VanillaHealth(target));
            entity.DamageReciever.SetHealth(health);
            entity.DamageReciever.SetMaxHealth(health);
        }

        return entity;
    }

    // true while this placeholder's variant waits on a template; it spawns on a later frame
    public static bool Waiting(RespawningPlaceholder target) {
        if (!Active) {
            return false;
        }

        var variant = VariantFor(target);
        var own = OwnPrefab(target);
        if (variant == null || own == null || Template(variant.Prefab) != null) {
            return false;
        }

        if (variant.Prefab == own.name && variant.Class == target.GetType().Name) {
            return false;
        }

        s_wanted.Add(variant.Prefab);
        if (!s_urgent.Contains(variant.Prefab)) {
            s_urgent.Add(variant.Prefab);
        }

        return true;
    }

    // any placeholder can give its prefab and settings; its scene references stay behind
    public static void Offer(RespawningPlaceholder donor) {
        if (!Active || donor == null || donor.name.StartsWith("enemyTemplate ")) {
            return;
        }

        var prefab = OwnPrefab(donor);
        if (prefab == null || Template(prefab.name) != null) {
            return;
        }

        var go = new GameObject("enemyTemplate " + prefab.name);
        go.SetActive(false);
        Object.DontDestroyOnLoad(go);
        var template = (RespawningPlaceholder)go.AddComponent(donor.GetType());
        for (var type = donor.GetType(); type != typeof(RespawningPlaceholder); type = type.BaseType) {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
                field.SetValue(template, Copy(field.GetValue(donor), false));
            }
        }

        s_templates[prefab.name] = template;
        s_wanted.Remove(prefab.name);
        Randomizer.log("enemies: template " + prefab.name + " from " + donor.transform.root.name);
    }

    // templates outlive scenes, so every prefab is captured once per process, a donor scene at a time
    public static void Tick() {
        if (!Active || !Characters.Sein || Time.realtimeSinceStartup < s_nextScan) {
            return;
        }

        s_nextScan = Time.realtimeSinceStartup + 0.5f;
        if (s_table == null) {
            Active = false;  // no rolls to play: vanilla enemies, and no donor scenes
            return;
        }

        if (!s_warmed) {
            s_warmed = true;
            Load();
            foreach (var prefab in s_kindOf.Keys) {
                if (Template(prefab) == null) {
                    s_wanted.Add(prefab);
                }
            }
        }

        if (s_wanted.Count == 0 && s_pins.Count == 0) {
            return;
        }

        foreach (var found in Resources.FindObjectsOfTypeAll(typeof(RespawningPlaceholder))) {
            Offer(found as RespawningPlaceholder);
        }

        ReleaseDonors();
        s_urgent.RemoveAll(prefab => !s_wanted.Contains(prefab));
        if (s_urgent.Count > 0) {
            AskForDonor(s_urgent[0]);
            return;
        }

        foreach (var prefab in s_wanted) {
            AskForDonor(prefab);
            break;
        }
    }

    public static void Reset() {
        Active = false;
        s_wanted.Clear();
        s_urgent.Clear();
        Unpin();
        s_warmed = false;
        s_askedAt = -100f;
    }

    // one line per live placeholder: what it was, what it rolled, and where that enemy is now
    public static string Report() {
        var lines = new List<string>();
        foreach (var target in RespawningPlaceholder.All) {
            var own = OwnPrefab(target);
            var variant = VariantFor(target);
            var pos = target.transform.position;
            var entity = target.CurrentEntity;
            var now = entity == null ? (target.EntityIsDead ? "dead" : "unspawned")
                : string.Format("{0} at ({1:0.0},{2:0.0}) hp {3}/{4}", entity.name, entity.transform.position.x, entity.transform.position.y,
                    entity.DamageReciever.Health, entity.DamageReciever.MaxHealth);
            lines.Add(string.Format("{0} ({1:0.0},{2:0.0}) r{3:0} -> {4} | {5}", own == null ? "?" : own.name, pos.x, pos.y, target.transform.eulerAngles.z,
                variant == null ? "vanilla" : variant.Prefab + " #" + variant.Index + " x" + string.Join("/", Array.ConvertAll(Factors(target, variant), f => f.ToString("0.00"))) +
                (Template(variant.Prefab) == null ? " (waiting)" : ""), now));
        }

        return string.Join("\n", lines.ToArray());
    }

    // one tab-separated row per live placeholder, for the probe scripts
    public static string Probe() {
        var lines = new List<string>();
        foreach (var target in RespawningPlaceholder.All) {
            var own = OwnPrefab(target);
            var entity = target.CurrentEntity;
            var spot = "-";
            var gravity = 0f;
            if (own != null && s_kindOf != null && s_kindOf.TryGetValue(own.name, out var ownKind)) {
                spot = Spot(target, ownKind, out gravity);
            }

            var row = new List<string> {
                target.GetInstanceID().ToString(), own == null ? "?" : own.name, spot, gravity.ToString("0"), target.EntityIsDead ? "dead" : "alive",
                target.transform.position.x.ToString("0.0"), target.transform.position.y.ToString("0.0")
            };
            if (entity != null) {
                var walker = entity as GroundEnemy;
                var sprite = entity as SpriteEntity;
                row.AddRange(new[] {
                    entity.name, entity.GetInstanceID().ToString(), entity.transform.position.x.ToString("0.00"), entity.transform.position.y.ToString("0.00"),
                    entity.transform.eulerAngles.z.ToString("0"), walker == null ? "-" : walker.PlatformMovement.IsOnGround.ToString(),
                    sprite == null ? "-" : sprite.FaceLeft.ToString(), entity.PositionToPlayerPosition.x.ToString("0.0"), entity.IsInWater.ToString(),
                    entity.DamageReciever.Health.ToString("0")
                });
            }

            lines.Add(string.Join("\t", row.ToArray()));
        }

        return string.Join("\n", lines.ToArray());
    }

    // live projectiles with their owner, for checking which way turned enemies shoot
    public static string Shots() {
        var lines = new List<string>();
        foreach (var found in Object.FindObjectsOfType(typeof(Projectile))) {
            var shot = (Projectile)found;
            var owner = shot.Owner == null ? "-" : shot.Owner.transform.root == shot.Owner.transform ? shot.Owner.name : shot.Owner.transform.parent.name;
            lines.Add(string.Format("{0}\t{1:0.0}\t{2:0.0}\t{3:0.00}\t{4:0.00}", owner, shot.transform.position.x, shot.transform.position.y, shot.Direction.x, shot.Direction.y));
        }

        return string.Join("\n", lines.ToArray());
    }

    // every live placeholder drops its enemy and spawns afresh, dead ones included
    public static void RespawnAll() {
        foreach (var target in RespawningPlaceholder.All.ToArray()) {
            target.DestroyCurrentInstance();
            target.PerformRespawn();
        }
    }

    // a variant at a spot, outside any placeholder, for looking at
    public static Entity SpawnVariant(int index, float x, float y, float rotation, bool faceLeft) {
        Load();
        var variant = s_variants[index];
        var maker = Template(variant.Prefab);
        if (maker == null) {
            s_wanted.Add(variant.Prefab);
            return null;
        }

        var settingsField = maker.GetType().GetField("Settings");
        var saved = settingsField.GetValue(maker);
        var settings = Copy(saved, true);
        Apply(settings, variant.Settings);
        maker.transform.position = new Vector3(x, y, 0f);
        maker.transform.eulerAngles = new Vector3(0f, 0f, rotation);
        maker.transform.localScale = new Vector3(faceLeft ? -1f : 1f, 1f, 1f);
        try {
            settingsField.SetValue(maker, settings);
            return maker is ShootingSpiderPlaceholder ? Spider((ShootingSpiderPlaceholder)maker) : maker.Instantiate();
        } finally {
            settingsField.SetValue(maker, saved);
        }
    }

    public static string Variants() {
        Load();
        var lines = new List<string>();
        foreach (var variant in s_variants) {
            lines.Add(variant.Index + " " + variant.Prefab + " " + variant.Settings);
        }

        return string.Join("\n", lines.ToArray());
    }

    private static Variant VariantFor(RespawningPlaceholder target) {
        Load();
        int index;
        if (!s_index.TryGetValue(target.MoonGuid.ToString(), out index)) {
            return null;
        }

        var roll = s_table == null ? -2 : s_table[index];
        if (s_force != null && roll != -1) {
            var forced = s_variants.FindAll(v => v.Kind != null && (v.Kind.Name == s_force || v.Prefab == s_force));
            return forced.Count == 0 ? null : forced[new System.Random(Hash(SeedKey() + "|" + target.MoonGuid)).Next(forced.Count)];
        }

        if (roll <= -2 && s_table != null) {
            // bonus value v takes variant v - 1; unset or out of range stays vanilla
            var value = Characters.Sein == null ? 0 : Characters.Sein.Inventory.GetRandomizerItem(-2 - roll);
            return value >= 1 && value <= s_variants.Count ? s_variants[value - 1] : null;
        }

        return roll < 0 ? null : s_variants[roll];
    }

    private static bool Takes(Kind kind, string spot) {
        return Array.IndexOf(kind.Spots.Split(' '), spot) >= 0;
    }

    // what surface a placeholder's enemy stands on, and the gravity a walker needs there
    private static string Spot(RespawningPlaceholder target, Kind own, out float gravity) {
        gravity = 0f;
        if (own.Name == "DropSlugs") {
            gravity = 180f;
            return "roof";
        }

        if (!own.Clings) {
            return own.Name == "Fish" ? "water" : own.Name == "Sharks" ? "shark" : own.Name == "Rhinos" ? "rhino" : Takes(own, "air") ? "air" : "floor";
        }

        var angle = Mathf.DeltaAngle(0f, target.transform.eulerAngles.z);
        if (Mathf.Abs(angle) <= 30f) {
            return "floor";
        }

        gravity = angle;
        return Mathf.Abs(angle) >= 150f ? "roof" : "wall";
    }

    private static void Load() {
        if (s_variants != null) {
            return;
        }

        s_kindOf = new Dictionary<string, Kind>();
        foreach (var line in RandomizerEnemyData.Kinds) {
            var row = line.Split('|');
            var kind = new Kind { Name = row[0], Spots = row[1], Clings = Array.IndexOf(row[2].Split(' '), "clings") >= 0 };
            for (var i = 3; i < row.Length; i++) {
                s_kindOf[row[i]] = kind;
            }
        }

        s_variants = new List<Variant>();
        foreach (var line in RandomizerEnemyData.Variants) {
            var parts = line.Split(new[] { '|' }, 5);
            var variant = new Variant { Index = s_variants.Count, Prefab = parts[0], Class = parts[1], Home = Floats(parts[3]), Settings = parts[4] };
            s_variants.Add(variant);
            s_kindOf.TryGetValue(variant.Prefab, out variant.Kind);
        }

        s_donors = Pairs(RandomizerEnemyData.Donors);
        s_arena = new HashSet<string>(RandomizerEnemyData.Arena);
        s_placements = Pairs(RandomizerEnemyData.Placements);
        s_index = new Dictionary<string, int>();
        foreach (var row in RandomizerEnemyData.Placements) {
            s_index[row.Split('|')[0]] = s_index.Count;
        }
        s_zones = new Dictionary<string, float[]>();
        foreach (var pair in Pairs(RandomizerEnemyData.Zones)) {
            s_zones[pair.Key] = Floats(pair.Value);
        }
    }

    private static float[] Floats(string text) {
        var parts = text.Split(',');
        var values = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++) {
            values[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
        }

        return values;
    }

    // hp and damage multipliers moving a variant from its home zones to the target's zone
    private static float[] Factors(RespawningPlaceholder target, Variant variant) {
        var factors = new[] { 1f, 1f };
        if (!s_placements.TryGetValue(target.MoonGuid.ToString(), out var zone) || !s_zones.TryGetValue(zone, out var strength)) {
            return factors;
        }

        for (var i = 0; i < factors.Length; i++) {
            factors[i] = Mathf.Clamp(strength[i] / variant.Home[i], 0.25f, 4f);
        }

        return factors;
    }

    // a fronkey where a fronkey stood lands at least as hard as the old one
    private static void KeepStomp(object settings, object vanilla) {
        var field = settings.GetType().GetField("GroundStompDamage");
        if (field == null) {
            return;
        }

        if (field.FieldType == typeof(int)) {
            field.SetValue(settings, Mathf.Max((int)field.GetValue(settings), (int)field.GetValue(vanilla)));
        } else if (field.FieldType == typeof(float)) {
            field.SetValue(settings, Mathf.Max((float)field.GetValue(settings), (float)field.GetValue(vanilla)));
        }
    }

    // exp is the spot's own enemy's, give or take a fifth
    public static void VaryExp(RespawningPlaceholder target, Entity entity) {
        var field = Active && entity != null ? target.GetType().GetField("Settings") : null;
        var settings = field == null ? null : field.GetValue(target);
        var spawner = settings == null ? null : entity.GetComponentInChildren<OrbSpawner>();
        if (spawner == null) {
            return;
        }

        foreach (var name in ExpFields) {
            var exp = settings.GetType().GetField(name);
            if (exp == null || exp.FieldType != typeof(int) || (int)exp.GetValue(settings) <= 0) {
                continue;
            }

            var random = new System.Random(Hash(SeedKey() + "|" + target.MoonGuid + "|exp"));
            spawner.SetNumberOfExpOrbs(Mathf.Max(1, Mathf.RoundToInt((int)exp.GetValue(settings) * (0.8f + (float)random.NextDouble() * 0.4f))));
            return;
        }
    }

    private static void Scale(object settings, float[] factors) {
        foreach (var field in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) {
            var factor = field.Name == "Health" ? factors[0] : Array.IndexOf(DamageFields, field.Name) >= 0 ? factors[1] : 0f;
            if (factor == 0f) {
                continue;
            }

            if (field.FieldType == typeof(float)) {
                var value = (float)field.GetValue(settings);
                field.SetValue(settings, value > 0f ? Mathf.Max(1f, Mathf.Round(value * factor)) : value);
            } else if (field.FieldType == typeof(int)) {
                var value = (int)field.GetValue(settings);
                field.SetValue(settings, value > 0 ? Mathf.Max(1, Mathf.RoundToInt(value * factor)) : value);
            }
        }
    }

    private static Dictionary<string, string> Pairs(string[] rows) {
        var pairs = new Dictionary<string, string>();
        foreach (var row in rows) {
            var parts = row.Split(new[] { '|' }, 2);
            pairs[parts[0]] = parts[1];
        }

        return pairs;
    }

    // the seed's name, not its header, which differs between co-op partners
    private static string SeedKey() {
        var meta = Randomizer.SeedMeta ?? "";
        return meta.Substring(meta.LastIndexOf('|') + 1);
    }

    private static int Hash(string text) {
        var hash = 2166136261u;
        foreach (var c in text) {
            hash = (hash ^ c) * 16777619u;
        }

        return (int)(hash & 0x7fffffff);
    }

    private static RespawningPlaceholder Template(string prefab) {
        return s_templates.TryGetValue(prefab, out var template) && template != null ? template : null;
    }

    private static FieldInfo PrefabField(Type type) {
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public)) {
            if (field.FieldType == typeof(GameObject)) {
                return field;
            }
        }

        return null;
    }

    private static GameObject OwnPrefab(RespawningPlaceholder placeholder) {
        var field = PrefabField(placeholder.GetType());
        return field == null ? null : field.GetValue(placeholder) as GameObject;
    }

    private static float VanillaHealth(RespawningPlaceholder target) {
        var settings = target.GetType().GetField("Settings").GetValue(target);
        return (float)settings.GetType().GetField("Health").GetValue(settings);
    }

    // a fish in the air: water everywhere, a longer reach (it can't wait for Ori to swim by), and a buoy to wander after
    private static void Fly(FishEnemy fish, RespawningPlaceholder target) {
        fish.AirSwimmer = true;
        fish.Settings.EnterSwimRange = Mathf.Max(fish.Settings.EnterSwimRange, FlyingFishReach);
        fish.Settings.MaxSwimDistance = Mathf.Max(fish.Settings.MaxSwimDistance, FlyingFishReach + 10f);
        var buoy = new GameObject("flyingFishWander");
        buoy.transform.parent = target.transform.parent;
        buoy.transform.position = fish.transform.position;
        var wander = buoy.AddComponent<RandomizerFishWander>();
        wander.Fish = fish;
        wander.Center = fish.transform.position;
        fish.WanderTarget = buoy.transform;
    }

    private static void Soak(Entity entity) {
        var field = entity.GetType().GetField("Settings");
        var settings = field == null ? null : field.GetValue(entity);
        if (settings == null) {
            return;
        }

        foreach (var setting in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) {
            if (setting.FieldType != typeof(float)) {
                continue;
            }

            var value = (float)setting.GetValue(settings);
            if (Array.IndexOf(WetSlowed, setting.Name) >= 0) {
                setting.SetValue(settings, value * WetSpeed);
            } else if (Array.IndexOf(WetDelayed, setting.Name) >= 0) {
                setting.SetValue(settings, value / WetSpeed);
            }
        }
    }

    // spawned under water: its drown check is off
    public static bool WaterBorn(Entity entity) {
        return entity != null && entity.GetComponent<RandomizerWaterBorn>() != null;
    }

    public static bool WaterBornShot(GameObject owner) {
        return owner != null && owner.GetComponentInParent<RandomizerWaterBorn>() != null;
    }

    // the level only: enemies, shots and Ori don't count as ground
    private static bool LevelHit(Vector3 origin, Vector3 direction, float reach, out RaycastHit hit) {
        hit = default(RaycastHit);
        var best = float.MaxValue;
        foreach (var candidate in Physics.RaycastAll(origin, direction, reach, ~0, QueryTriggerInteraction.Ignore)) {
            var go = candidate.collider.gameObject;
            if (go.GetComponentInParent<Entity>() != null || go.GetComponentInParent<Projectile>() != null || go.GetComponentInParent<SeinCharacter>() != null) {
                continue;
            }

            if (candidate.distance < best) {
                best = candidate.distance;
                hit = candidate;
            }
        }

        return best < float.MaxValue;
    }

    private static bool SurfaceNear(Vector3 position, float reach, out Vector3 toward) {
        toward = Vector3.down;
        var best = float.MaxValue;
        RaycastHit hit;
        for (var i = 0; i < 8; i++) {
            var angle = i * Mathf.PI / 4f;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            if (LevelHit(position, direction, reach, out hit) && hit.distance < best) {
                best = hit.distance;
                toward = direction;
            }
        }

        return best < float.MaxValue;
    }

    private static void Pace(Entity entity, float factor) {
        var field = entity.GetType().GetField("Settings");
        var settings = field == null ? null : field.GetValue(entity);
        if (settings == null) {
            return;
        }

        foreach (var setting in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) {
            if (setting.FieldType == typeof(float) && Array.IndexOf(PacedFields, setting.Name) >= 0) {
                setting.SetValue(settings, (float)setting.GetValue(settings) * factor);
            }
        }
    }

    // the slime must see Ori standing on the floor below its spot before it can drop
    private static void Hang(Transform t, object settings, Vector3 spot) {
        RaycastHit ceiling, floor;
        if (!LevelHit(spot, Vector3.up, HangReach, out ceiling)) {
            return;
        }

        t.position = ceiling.point + Vector3.down * DropHang;
        var below = LevelHit(spot, Vector3.down, HangReach, out floor) ? floor.distance : 0f;
        var drop = settings.GetType().GetField("DropDistance");
        if (drop != null) {
            drop.SetValue(settings, Mathf.Max((float)drop.GetValue(settings), ceiling.distance + below - DropHang + 2f));
        }
    }

    // clingers have no gravity (slimes drift up off a missing surface), so they start on the surface they face
    private static void Settle(Entity entity) {
        var down = -entity.transform.up;
        var slug = entity as SlugEnemy;
        var body = slug != null ? slug.Movement.GetComponent<Rigidbody>() : null;
        RaycastHit hit;
        var found = body != null ? body.SweepTest(down, out hit, SettleReach) : LevelHit(entity.transform.position, down, SettleReach, out hit);
        if (!found) {
            return;
        }

        entity.transform.position += down * hit.distance;
        if (slug != null) {
            typeof(TraceGroundMovement).GetField("m_floorNormal", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slug.Movement, hit.normal);
            entity.transform.eulerAngles = new Vector3(0f, 0f, MoonMath.Angle.AngleFromVector(Vector3.Cross(Vector3.back, hit.normal)));
        }
    }

    // ShootingSpiderPlaceholder.Instantiate without a rope: it hovers instead of hanging
    private static Entity Spider(ShootingSpiderPlaceholder maker) {
        var settings = maker.Settings;
        var go = (GameObject)InstantiateUtility.Instantiate(maker.ShootingSpider, maker.transform.position, maker.transform.rotation);
        go.GetComponent<Rigidbody>().isKinematic = true;
        var spider = go.GetComponent<ShootingSpider>();
        spider.FaceLeft = maker.transform.localScale.x < 0f;
        var health = Enemy.ScaleHealth(settings.Health);
        spider.DamageReciever.SetHealth(health);
        spider.DamageReciever.SetMaxHealth(health);
        spider.DamageDealer.Damage = settings.DamageOnTouch;
        spider.Settings.ProjectileDamage = settings.ProjectileDamage;
        spider.Settings.ProjectileSpeed = settings.ProjectileSpeed;
        spider.Settings.SpreadShot = settings.SpreadShot;
        var orbs = spider.GetComponentInChildren<OrbSpawner>();
        orbs.NumberOfExpOrbs = settings.NumberOfExpOrbsToDrop;
        orbs.LootSettings = settings.LootSettings;
        go.name = maker.ShootingSpider.name;
        return spider;
    }

    // a fresh copy of a settings value; scene objects are dropped unless keepScene
    private static object Copy(object value, bool keepScene) {
        if (value == null) {
            return null;
        }

        var obj = value as Object;
        if (value is Object) {
            return obj != null && (keepScene || !InScene(obj)) ? value : null;
        }

        var type = value.GetType();
        if (type.IsArray) {
            var items = new List<object>();
            foreach (var item in (Array)value) {
                var copy = Copy(item, keepScene);
                if (copy != null || item == null) {
                    items.Add(copy);
                }
            }

            var array = Array.CreateInstance(type.GetElementType(), items.Count);
            for (var i = 0; i < items.Count; i++) {
                array.SetValue(items[i], i);
            }

            return array;
        }

        if (type.IsPrimitive || type.IsEnum || type.IsValueType || value is string || !type.IsSerializable) {
            return value;
        }

        var fresh = Activator.CreateInstance(type);
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
            field.SetValue(fresh, Copy(field.GetValue(value), keepScene));
        }

        return fresh;
    }

    private static bool InScene(Object obj) {
        var component = obj as Component;
        var go = component != null ? component.gameObject : obj as GameObject;
        return go != null && go.transform.root.GetComponent<SceneRoot>() != null;
    }

    // the survey's k=v;k={k=v} settings onto a copy
    private static void Apply(object target, string settings) {
        var depth = 0;
        var start = 0;
        for (var i = 0; i <= settings.Length; i++) {
            var c = i < settings.Length ? settings[i] : ';';
            if (c == '{') {
                depth++;
            } else if (c == '}') {
                depth--;
            } else if (c == ';' && depth == 0) {
                if (i > start) {
                    Set(target, settings.Substring(start, i - start));
                }

                start = i + 1;
            }
        }
    }

    private static void Set(object target, string pair) {
        var eq = pair.IndexOf('=');
        var field = target.GetType().GetField(pair.Substring(0, eq));
        var text = pair.Substring(eq + 1);
        if (field == null) {
            return;
        }

        if (text.StartsWith("{")) {
            var nested = field.GetValue(target) ?? Activator.CreateInstance(field.FieldType);
            Apply(nested, text.Substring(1, text.Length - 2));
            field.SetValue(target, nested);
        } else if (field.FieldType == typeof(float)) {
            field.SetValue(target, float.Parse(text, CultureInfo.InvariantCulture));
        } else if (field.FieldType == typeof(int)) {
            field.SetValue(target, int.Parse(text, CultureInfo.InvariantCulture));
        } else if (field.FieldType == typeof(bool)) {
            field.SetValue(target, text == "True");
        } else if (field.FieldType.IsEnum) {
            field.SetValue(target, Enum.Parse(field.FieldType, text));
        }
    }

    private static void AskForDonor(string prefab) {
        if (s_pins.Count > 0 || Time.realtimeSinceStartup - s_askedAt < 1f || !s_donors.TryGetValue(prefab, out var scene)) {
            return;
        }

        var manager = Core.Scenes.Manager;
        if (manager == null || manager.GetSceneManagerScene(scene) != null) {
            return;
        }

        var meta = manager.GetSceneInformation(scene);
        if (meta == null) {
            Randomizer.LogError("enemies: no scene called " + scene);
            s_wanted.Remove(prefab);
            return;
        }

        s_askedAt = Time.realtimeSinceStartup;
        var before = new List<SceneManagerScene>(manager.ActiveScenes);
        manager.PreloadScene(meta);
        foreach (var loaded in manager.ActiveScenes) {
            if (!before.Contains(loaded) && loaded.PreventUnloading) {
                s_pins.Add(loaded.MetaData.Scene);
            }
        }

        Randomizer.log("enemies: preloading " + scene + " for " + prefab);
    }

    // a donor scene goes once none of the prefabs still wanted come from it
    private static void ReleaseDonors() {
        var manager = Core.Scenes.Manager;
        if (manager == null || s_pins.Count == 0) {
            return;
        }

        foreach (var prefab in s_wanted) {
            if (s_donors.TryGetValue(prefab, out var scene) && s_pins.Contains(scene)) {
                return;
            }
        }

        Unpin();
    }

    private static void Unpin() {
        var manager = Core.Scenes.Manager;
        if (manager != null) {
            foreach (var name in s_pins) {
                var scene = manager.GetSceneManagerScene(name);
                if (scene != null) {
                    scene.PreventUnloading = false;
                }
            }
        }

        s_pins.Clear();
    }
}

public class RandomizerWaterBorn : MonoBehaviour {
}

// JumpShootSharkZone for a shark on dry land: it emerges under Ori when he stands on the floor near its spot.
public class RandomizerSharkLure : MonoBehaviour {
    public JumpShootShark Shark;
    public Vector3 Spot;
    public static float Reach = 14.3f;
    public static float Level = 3f;
    public static float Probe = 4f;

    public void FixedUpdate() {
        var sein = Characters.Sein;
        if (Shark == null) {
            Destroy(this);
            return;
        }

        if (sein == null || !sein.IsOnGround || Mathf.Abs(sein.Position.x - Spot.x) > Reach || Mathf.Abs(sein.Position.y - Spot.y) > Level) {
            return;
        }

        Shark.SetEmergeLocation(sein.Position);
        // the emerge burst lies on the ground it comes out of
        var burst = Shark.EmergePrefab;
        if (burst != null) {
            burst.UseRotation = true;
            burst.transform.rotation = Quaternion.FromToRotation(Vector3.up, GroundNormal(sein));
        }
    }

    // the surface under Ori by a ray past his own collider, straight up when nothing is there
    private static Vector3 GroundNormal(SeinCharacter sein) {
        var best = Vector3.up;
        var nearest = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(sein.Position + Vector3.up, Vector3.down, Probe, ~0, QueryTriggerInteraction.Ignore)) {
            if (hit.distance < nearest && !hit.collider.transform.IsChildOf(sein.transform)) {
                nearest = hit.distance;
                best = hit.normal;
            }
        }

        return best;
    }
}

// A walker on a turned surface lets go of it (gravity back to normal) if it walks off an edge or stays airborne too
// long: ceilings have no enemy stoppers, and nothing else would stop it falling "up" out of the level.
public class RandomizerEnemyGrip : MonoBehaviour {
    public GroundEnemy Walker;

    public static float JumpGrace = 2.5f;

    private bool m_landed;
    private bool m_jumped;
    private float m_air;
    private float m_age;

    public void FixedUpdate() {
        var move = Walker == null ? null : Walker.PlatformMovement;
        if (move == null || move.GravityAngle == 0f) {
            Destroy(this);
            return;
        }

        m_age += Time.fixedDeltaTime;
        if (move.IsOnGround) {
            m_landed = true;
            m_air = 0f;
            return;
        }

        if (m_air == 0f) {
            m_jumped = move.LocalSpeedY > 0f;
        }

        m_air += Time.fixedDeltaTime;
        if (m_landed ? (!m_jumped && m_air > 0.15f) || m_air > JumpGrace : m_age > JumpGrace) {
            move.GravityAngle = 0f;
            Walker.transform.rotation = Quaternion.identity;
            Destroy(this);
        }
    }
}

// A slow figure eight around a flying fish's spot: vanilla fish follow a moving scene object the same way.
public class RandomizerFishWander : MonoBehaviour {
    public FishEnemy Fish;
    public Vector3 Center;

    public static float Radius = 4f;
    public static float Period = 7f;

    public void Update() {
        if (Fish == null) {
            Destroy(gameObject);
            return;
        }

        var t = Time.time * 2f * Mathf.PI / Period + Center.x;
        transform.position = Center + new Vector3(Mathf.Cos(t) * Radius, Mathf.Sin(2f * t) * Radius * 0.5f, 0f);
    }
}
