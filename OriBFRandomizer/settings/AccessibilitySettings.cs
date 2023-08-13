using BaseModLib;

namespace OriBFRandomizer.settings
{
    public static class AccessibilitySettings
    {
        public static readonly FloatSetting CAMERA_SHAKE =
            new FloatSetting("accessibility.camera_shake", 0, 1, 0.1f, "Camera Shake Factor", "", 1.0f);

        public static readonly BoolSetting SOUND_COMPRESSION =
            new BoolSetting("accesibility.sound_compression", "Apply Sound Compression", "", false);

        public static readonly FloatSetting SOUND_COMPRESSION_FACTOR =
            new FloatSetting("accsibility.sound_compression_factor", 0, 1, 0.1f, "Sound Compression Factor", "", 0.7f);
        
        public static readonly FloatSetting SA_OPACITY_FACTOR  =
            new FloatSetting("accsibility.ability_menu_opacity", 0, 1, 0.1f, "Ability Menu Posacity", "", 0.2f);
        
        public static readonly BoolSetting CURSOR_LOCK =
            new BoolSetting("accesibility.cursor_lock", "Cursor Lock", "", true);
    }
}