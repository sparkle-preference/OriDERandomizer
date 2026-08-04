public class MusicVolumeSlider : CleverValueSlider
{
	public override float Value
	{
		get
		{
			if (Setting != null)
			{
				return Setting.Value;
			}
			return GameSettings.Instance.MusicVolume;
		}
		set
		{
			if (Setting != null)
			{
				Setting.Value = value;
				RandomizerSettings.SetDirty();
				return;
			}
			GameSettings.Instance.MusicVolume = value;
			SettingsScreen.Instance.SetDirty();
		}
	}

    // Specifically hijack the music slider because it's the template for our custom sliders
    //  and we don't want to have to manually set all the fields if we made a new subclass of CleverValueSlider
	public RandomizerSettings.FloatSetting Setting;
}
