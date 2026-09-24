public class MusicVolumeSlider : CleverValueSlider {
    public override float Value {
        get {
            if (Setting != null) {
                return Setting.Value;
            }

            return GameSettings.Instance.MusicVolume;
        }
        set {
            if (Setting != null) {
                Setting.Value = value;
                RandomizerSettings.SetDirty();
                return;
            }

            GameSettings.Instance.MusicVolume = value;
            SettingsScreen.Instance.SetDirty();
        }
    }

    // set on AddSlider's clones; null keeps this the vanilla music volume slider
    public RandomizerSettings.FloatSetting Setting;
}
