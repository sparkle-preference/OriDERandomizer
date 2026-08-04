using Core;
using UnityEngine;

public class ToggleCustomSettingsAction : MonoBehaviour
{
	public void Awake()
	{
		var componentInChildren = GetComponentInChildren<ToggleSettingsAction>();
		OnSound = componentInChildren.OnSound;
		OffSound = componentInChildren.OffSound;
		Destroy(componentInChildren);
	}

	private void PlaySound(bool on)
	{
		if (on && OnSound)
		{
			Sound.Play(OnSound.GetSound(null), transform.position, null);
			return;
		}
		if (OffSound && !on)
		{
			Sound.Play(OffSound.GetSound(null), transform.position, null);
		}
	}

	public void Toggle()
	{
		SetSetting(!IsEnabled);
		PlaySound(IsEnabled);
		Setting.Value = IsEnabled;
		RandomizerSettings.SetDirty();
	}

	public void SetSetting(bool enabled)
	{
		MessageBox.SetMessage(new MessageDescriptor(enabled ? "ON" : "OFF"));
		IsEnabled = enabled;
	}

	public void Init()
	{
		MessageBox = transform.FindChild("text/stateText").GetComponent<MessageBox>();
		SetSetting(Setting.Value);
	}

	public SoundProvider OnSound;

	public SoundProvider OffSound;

	public MessageBox MessageBox;

	public bool IsEnabled;

	public RandomizerSettings.BoolSetting Setting;
}
