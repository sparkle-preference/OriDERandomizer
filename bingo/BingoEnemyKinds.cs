using System.Collections.Generic;

// Enemies grouped the way players talk about them, keyed by prefab name (a spawned enemy's
// GameObject name). The swarm's small/tiny splits stay unmapped, as they are for KillEnemies.
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
