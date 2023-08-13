using BaseModLib;
using UnityEngine;

namespace OriBFRandomizer.settings
{
    public static class Keybinds
    {
        public static readonly KeybindSetting OPEN_TELEPORTER_MENU 
            = new KeybindSetting("key.teleport", "Open Teleporter Menu", "", new[]{KeyCode.LeftAlt, KeyCode.R}, Randomizer.TeleportAnywhere);
        public static readonly KeybindSetting REPLAY_MESSAGE 
            = new KeybindSetting("key.replay_message", "Replay Last Message", "", new[]{KeyCode.LeftAlt, KeyCode.T}, Randomizer.playLastMessage);
        public static readonly KeybindSetting RELOAD_SEED 
            = new KeybindSetting("key.reload_seed", "Reload Seed", "", new[]{KeyCode.LeftAlt, KeyCode.L}, Randomizer.reloadSeed);
        public static readonly KeybindSetting SHOW_PROGRESS 
            = new KeybindSetting("key.show_progress", "Show Progress", "", new[]{KeyCode.LeftAlt, KeyCode.P}, Randomizer.showProgress);
        public static readonly KeybindSetting DOUBLE_BASH = new KeybindSetting("key.dbash", "Double Bash", "", new[]{KeyCode.R});
        public static readonly KeybindSetting GJUMP = new KeybindSetting("key.gjump", "Grenade Jump", "", new[]{KeyCode.T});
        public static readonly KeybindSetting LIST_TREES 
            = new KeybindSetting("key.list_trees", "List Trees", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha1}, RandomizerTrackedDataManager.ListTrees);
        public static readonly KeybindSetting LIST_MAP_ALTARS 
            = new KeybindSetting("key.list_maps", "List Map Altars", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha2}, RandomizerTrackedDataManager.ListMapstones);
        public static readonly KeybindSetting LIST_TELEPORTERS 
            = new KeybindSetting("key.list_tps", "List Teleporters", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha3}, RandomizerTrackedDataManager.ListTeleporters);
        public static readonly KeybindSetting LIST_RELICS 
            = new KeybindSetting("key.list_relics", "List Relics", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha4}, RandomizerTrackedDataManager.ListRelics);
        public static readonly KeybindSetting SHOW_STATS 
            = new KeybindSetting("key.show_stats", "Show Stats", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha5}, () => RandomizerStatsManager.ShowStats(10));
    }
}