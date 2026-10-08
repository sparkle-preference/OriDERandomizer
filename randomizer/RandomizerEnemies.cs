using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Game;
using UnityEngine;
using Object = UnityEngine.Object;

// Enemy randomizer: a placeholder spawns a seeded variant from its own category instead of its own enemy.
public static class RandomizerEnemies {
    public static bool Active;

    // the game's fast respawners stay put; plain bash targets among them shuffle between bashable kinds
    public static bool PinBash = true;

    private class Kind {
        public string Name;
        public string Spots;
        public bool Clings;
        public bool Bashable;
        public int Weight;
        public readonly List<Variant> Variants = new List<Variant>();
    }

    private class Variant {
        public int Index;
        public string Prefab;
        public string Class;
        public int Weight;
        public string Settings;
        public float[] Home;
        public Kind Kind;
    }

    // name, spots it can take, flags, prefabs; anything unlisted (fish, sharks) stays vanilla.
    // Walkers on a roof or wall spot get their gravity turned toward that surface.
    private static readonly string[][] Kinds = {
        new[] { "Fronkeys", "floor roof wall", "bash", "jumperEnemy" },
        new[] { "Frogs", "floor roof wall", "bash", "spitterEnemy", "fastSpitterEnemy" },
        new[] { "Slimes", "floor roof wall", "clings bash", "acidSlugEnemy", "fireSlugEnemy", "iceSlugEnemy", "starSlugEnemy" },
        new[] { "Banelings", "floor", "bash", "kamikazeSootEnemy" },
        new[] { "DropSlugs", "roof", "bash", "dropSlugEnemy", "dropSlugFireEnemy" },
        new[] { "Worms", "floor roof wall", "clings", "mortarWormEnemy", "mortarWormFireEnemy" },
        new[] { "Rhinos", "rhino", "bash", "armouredRammingEnemy" },
        new[] { "Birds", "air", "bash", "dashOwlEnemy" },
        new[] { "Elementals", "air", "", "floatTurretEnemyFire", "floatTurretEnemyWood" },
        new[] { "Lasers", "air", "", "floatingRockLaserEnemy" },
        new[] { "Swarms", "air", "bash", "swarmEnemyLarge", "swarmEnemyLargeFire", "swarmEnemyMedium" },
        new[] { "Spiders", "air", "bash", "shootingSpiderEnemy", "spreadshotSpiderEnemy" },
    };

    // bash targets that neither shoot, explode nor stomp, so any bashable kind can stand in for them
    private static readonly string[] PlainBashTargets = {
        "acidSlugEnemy", "fireSlugEnemy", "iceSlugEnemy", "dashOwlEnemy", "swarmEnemyLarge", "swarmEnemyLargeFire", "swarmEnemyMedium", "armouredRammingEnemy"
    };

    private static readonly string[] DamageFields = { "DamageOnTouch", "ProjectileDamage", "ExplosionDamage", "LaserDamage", "GroundStompDamage" };
    private static readonly string[] ExpFields = { "NumberOfExpOrbsToDrop", "TotalExpOrbsToDrop" };

    private static List<Kind> s_kinds;
    private static Dictionary<string, Kind> s_kindOf;
    private static List<Variant> s_variants;
    private static Dictionary<string, string> s_donors;
    private static Dictionary<string, string> s_pinned;
    private static HashSet<string> s_arena;
    private static Dictionary<string, string> s_bash;
    private static Dictionary<string, float[]> s_zones;
    private static Dictionary<string, string> s_placements;

    private static readonly Dictionary<string, Variant> s_rolls = new Dictionary<string, Variant>();
    private static string s_rollKey;
    private static string s_force;

    private static readonly Dictionary<string, RespawningPlaceholder> s_templates = new Dictionary<string, RespawningPlaceholder>();
    private static readonly HashSet<string> s_wanted = new HashSet<string>();
    private static readonly List<string> s_pins = new List<string>();
    private static float s_nextScan;
    private static float s_askedAt = -100f;
    private static bool s_warmed;

    // how far off its surface a turned walker spawns, so its capsule starts clear of the level
    public static float WalkerLift = 1.5f;

    // every unpinned placeholder takes this kind or prefab (any category), for testing; null undoes it
    public static void SetForce(string kindOrPrefab) {
        s_force = kindOrPrefab;
        s_rolls.Clear();
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
        Scale(settings, Factors(target, variant));
        var t = maker.transform;
        var pose = new[] { t.position, t.eulerAngles, t.localScale };
        if (maker != target) {
            t.position = target.transform.position;
            t.rotation = variant.Kind.Clings ? target.transform.rotation : Quaternion.identity;
            t.localScale = new Vector3(target.transform.lossyScale.x < 0f ? -1f : 1f, 1f, 1f);
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
        }

        var walker = entity as GroundEnemy;
        if (walker != null && s_kindOf.TryGetValue(own.name, out var ownKind) && Spot(target, ownKind, out var gravity) != "floor" && gravity != 0f) {
            walker.PlatformMovement.GravityAngle = gravity;
            walker.transform.position -= walker.PlatformMovement.GravityDirection * WalkerLift;
        }

        if (s_arena.Contains(target.MoonGuid.ToString())) {
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
        foreach (var prefab in s_wanted) {
            AskForDonor(prefab);
            break;
        }
    }

    public static void Reset() {
        Active = false;
        s_rolls.Clear();
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
            lines.Add(variant.Index + " " + variant.Prefab + " x" + variant.Weight + " " + variant.Settings);
        }

        return string.Join("\n", lines.ToArray());
    }

    private static Variant VariantFor(RespawningPlaceholder target) {
        Load();
        var key = SeedKey() + "|" + s_force + "|" + PinBash;
        if (key != s_rollKey) {
            s_rolls.Clear();
            s_rollKey = key;
        }

        var guid = target.MoonGuid.ToString();
        if (s_rolls.TryGetValue(guid, out var cached)) {
            return cached;
        }

        var variant = Roll(target, guid);
        s_rolls[guid] = variant;
        return variant;
    }

    private static Variant Roll(RespawningPlaceholder target, string guid) {
        var own = OwnPrefab(target);
        if (own == null || !s_kindOf.TryGetValue(own.name, out var ownKind) || s_pinned.ContainsKey(guid)) {
            return null;
        }

        var bashOnly = PinBash && s_bash.ContainsKey(guid);
        if (bashOnly && Array.IndexOf(PlainBashTargets, own.name) < 0) {
            return null;
        }

        var spot = Spot(target, ownKind, out _);
        var candidates = new List<Kind>();
        foreach (var kind in s_kinds) {
            if (s_force != null ? kind.Name == s_force || kind.Variants.Exists(v => v.Prefab == s_force) : Takes(kind, spot) || (spot == "rhino" && Takes(kind, "floor"))) {
                if (!bashOnly || kind.Bashable) {
                    candidates.Add(kind);
                }
            }
        }

        if (candidates.Count == 0) {
            return null;
        }

        var random = new System.Random(Hash(SeedKey() + "|" + guid));
        var chosen = candidates[random.Next(candidates.Count)];
        var pool = s_force != null && chosen.Name != s_force ? chosen.Variants.FindAll(v => v.Prefab == s_force) : chosen.Variants;
        var total = 0;
        foreach (var variant in pool) {
            total += variant.Weight;
        }

        var pick = random.Next(total);
        foreach (var variant in pool) {
            pick -= variant.Weight;
            if (pick < 0) {
                return variant;
            }
        }

        return null;
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
            return own.Spots == "air" || own.Spots == "rhino" ? own.Spots : "floor";
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

        s_kinds = new List<Kind>();
        s_kindOf = new Dictionary<string, Kind>();
        foreach (var row in Kinds) {
            var flags = row[2].Split(' ');
            var kind = new Kind { Name = row[0], Spots = row[1], Clings = Array.IndexOf(flags, "clings") >= 0, Bashable = Array.IndexOf(flags, "bash") >= 0 };
            s_kinds.Add(kind);
            for (var i = 3; i < row.Length; i++) {
                s_kindOf[row[i]] = kind;
            }
        }

        s_variants = new List<Variant>();
        foreach (var line in RandomizerEnemyData.Variants) {
            var parts = line.Split(new[] { '|' }, 5);
            var variant = new Variant { Index = s_variants.Count, Prefab = parts[0], Class = parts[1], Weight = int.Parse(parts[2]), Home = Floats(parts[3]), Settings = parts[4] };
            s_variants.Add(variant);
            if (s_kindOf.TryGetValue(variant.Prefab, out variant.Kind)) {
                variant.Kind.Variants.Add(variant);
                variant.Kind.Weight += variant.Weight;
            }
        }

        s_donors = Pairs(RandomizerEnemyData.Donors);
        s_pinned = Pairs(RandomizerEnemyData.Pinned);
        s_arena = new HashSet<string>(RandomizerEnemyData.Arena);
        s_bash = Pairs(RandomizerEnemyData.Bash);
        s_placements = Pairs(RandomizerEnemyData.Placements);
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

    // hp, damage and exp multipliers moving a variant from its home zones to the target's zone
    private static float[] Factors(RespawningPlaceholder target, Variant variant) {
        var factors = new[] { 1f, 1f, 1f };
        if (!s_placements.TryGetValue(target.MoonGuid.ToString(), out var zone) || !s_zones.TryGetValue(zone, out var strength)) {
            return factors;
        }

        for (var i = 0; i < factors.Length; i++) {
            factors[i] = Mathf.Clamp(strength[i] / variant.Home[i], 0.25f, 4f);
        }

        return factors;
    }

    private static void Scale(object settings, float[] factors) {
        foreach (var field in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) {
            var factor = field.Name == "Health" ? factors[0] : Array.IndexOf(DamageFields, field.Name) >= 0 ? factors[1] : Array.IndexOf(ExpFields, field.Name) >= 0 ? factors[2] : 0f;
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

    // the seed's name, not its header: co-op partners differ in their sync flags
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

    // the survey's k=v;k={k=v} settings onto a copy; references were never written there
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

        foreach (var name in s_pins) {
            var scene = manager.GetSceneManagerScene(name);
            if (scene != null) {
                scene.PreventUnloading = false;
            }
        }

        s_pins.Clear();
    }
}
