using BaseModLib;
using UnityEngine;

namespace OriBFRandomizer.settings
{
    public static class Keybinds
    {
        public static readonly KeybindSetting OPEN_TELEPORTER_MENU = new KeybindSetting("key.teleport", "Open Teleporter Menu", "", new[]{KeyCode.LeftAlt, KeyCode.R});
        public static readonly KeybindSetting REPLAY_MESSAGE = new KeybindSetting("key.replay_message", "Replay Last Message", "", new[]{KeyCode.LeftAlt, KeyCode.T});
        public static readonly KeybindSetting RELOAD_SEED = new KeybindSetting("key.reload_seed", "Reload Seed", "", new[]{KeyCode.LeftAlt, KeyCode.L});
        public static readonly KeybindSetting SHOW_PROGRESS = new KeybindSetting("key.show_progress", "Show Progress", "", new[]{KeyCode.LeftAlt, KeyCode.P});
        public static readonly KeybindSetting DOUBLE_BASH = new KeybindSetting("key.dbash", "Double Bash", "", new[]{KeyCode.R});
        public static readonly KeybindSetting GJUMP = new KeybindSetting("key.gjump", "Grenade Jump", "", new[]{KeyCode.T});
        public static readonly KeybindSetting LIST_TREES = new KeybindSetting("key.list_trees", "List Trees", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha1});
        public static readonly KeybindSetting LIST_MAP_ALTARS = new KeybindSetting("key.list_maps", "List Map Altars", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha2});
        public static readonly KeybindSetting LIST_TELEPORTERS = new KeybindSetting("key.list_tps", "List Teleporters", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha3});
        public static readonly KeybindSetting LIST_RELICS = new KeybindSetting("key.list_relics", "List Relics", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha4});
        public static readonly KeybindSetting SHOW_STATS = new KeybindSetting("key.show_stats", "Show Stats", "", new[]{KeyCode.LeftAlt, KeyCode.Alpha5});
    }
}