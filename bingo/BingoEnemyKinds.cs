using System.Collections.Generic;

// Enemies grouped the way players talk about them. Keys are prefab names, which is what a
// spawned enemy's GameObject is called; every name a placeholder in the game can spawn is
// here. The swarm's small and tiny splits are the only spawned enemies with no placeholder,
// and they stay unmapped so they count no more than they do for KillEnemies.
public static class BingoEnemyKinds {
    // index = the kind's defeat bitfield (BingoController.DefeatBaseId + index), which is
    // in the save file: never reorder, append only
    public static readonly string[] Kinds = {
        "Slimes", "Spitters", "Frogs", "Fronkeys", "Spiders", "Birds", "Fish", "Rhinos", "Swarms",
        "Elementals", "Exploders", "Sharks"
    };

    public static readonly Dictionary<string, string> ByName = new Dictionary<string, string> {
        { "jumperEnemy", "Fronkeys" },
        { "shootingSpiderEnemy", "Spiders" },
        { "spreadshotSpiderEnemy", "Spiders" },
        { "dashOwlEnemy", "Birds" },
        { "spitterEnemy", "Frogs" },
        { "fastSpitterEnemy", "Frogs" },
        { "acidSlugEnemy", "Slimes" },
        { "fireSlugEnemy", "Slimes" },
        { "iceSlugEnemy", "Slimes" },
        { "starSlugEnemy", "Slimes" },
        { "armouredRammingEnemy", "Rhinos" },
        { "jumpShootSharkEnemy", "Sharks" },
        { "jumpShootSharkEnemyFire", "Sharks" },
        { "floatTurretEnemyFire", "Elementals" },
        { "floatTurretEnemyWood", "Elementals" },
        { "floatingRockLaserEnemy", "Elementals" },
        { "fishEnemy", "Fish" },
        { "kamikazeSootEnemy", "Exploders" },
        { "dropSlugEnemy", "Exploders" },
        { "dropSlugFireEnemy", "Exploders" },
        { "swarmEnemyLarge", "Swarms" },
        { "swarmEnemyLargeFire", "Swarms" },
        { "swarmEnemyMedium", "Swarms" },
        { "mortarWormEnemy", "Spitters" },
        { "mortarWormFireEnemy", "Spitters" },
    };

    public static string KindOf(Entity entity) {
        string kind;
        return entity != null && ByName.TryGetValue(entity.name, out kind) ? kind : null;
    }
}
