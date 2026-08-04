using System;
using Game;
using UnityEngine;
using Input = Core.Input;

public class OptionsScreen : MenuScreen, ISuspendable
{
	public void Awake()
	{
		Instance = this;
		SuspensionManager.Register(this);
		var navigation = Navigation;
		navigation.OnBackPressedCallback = (Action)Delegate.Combine(navigation.OnBackPressedCallback, new Action(OnBackPressed));
		AddSubscreen<ControlsSettingsScreen>("CONTROL OPTIONS", 2);
		AddSubscreen<AccessibilitySettingsScreen>("ACCESSIBILITY", 3);
		AddSubscreen<KeybindsScreen>("KEYBINDS", 4);
		AddSubscreen<MenuKeybindsScreen>("MENU KEYBINDS", 5);
		AddSubscreen<ControllerBindsScreen>("CONTROLLER BINDS", 6);
		AddSubscreen<ControllerMenuBindsScreen>("CONTROLLER MENU BINDS", 7);
	}

	public void OnDestroy()
	{
		var navigation = Navigation;
		navigation.OnBackPressedCallback = (Action)Delegate.Remove(navigation.OnBackPressedCallback, new Action(OnBackPressed));
	}

	public void FixedUpdate()
	{
		if (Input.Bash.OnPressed)
		{
			XboxOne.Help();
		}
	}

	public override void Hide()
	{
		Navigation.SetVisible(false);
		foreach (var cleverMenuItemGroupItem in GetComponent<CleverMenuItemGroup>().Options)
		{
			if (cleverMenuItemGroupItem.ItemGroup)
			{
				cleverMenuItemGroupItem.ItemGroup.IsActive = false;
			}
		}
	}

	public override void ShowImmediate()
	{
		Navigation.SetVisibleImmediate(true);
		Navigation.SetIndexToFirst();
	}

	public override void HideImmediate()
	{
		Navigation.SetVisibleImmediate(false);
	}

	public override void Show()
	{
		Navigation.RefreshVisible();
		Navigation.SetVisible(true);
		Navigation.SetIndexToFirst();
	}

	public void OnBackPressed()
	{
		if (GameController.Instance.GameInTitleScreen)
		{
			UI.Menu.HideMenuScreen();
		}
		else
		{
			UI.Menu.ShowInventoryOrPauseMenu();
		}
	}

	public bool IsSuspended { get; set; }

	public void AddSubscreen<TController>(string label, int index) where TController : MonoBehaviour
	{
		Navigation.AddMenuItem(label, index, Navigation.transform.FindChild("mainMenuUI").GetComponent<CleverMenuItemLayout>(), delegate
		{
		});
		var gameObject = Instantiate(transform.FindChild("*settings").gameObject);
		gameObject.name = "*" + label.ToLower();
		gameObject.transform.SetParent(transform);
		Destroy(gameObject.GetComponent<SettingsScreen>());
		gameObject.AddComponent<TController>();
		gameObject.SetActive(false);
		GetComponent<CleverMenuItemGroup>().AddItem(Navigation.MenuItems[index], gameObject.GetComponent<CleverMenuItemGroupBase>());
	}

	public static OptionsScreen Instance;

	public SoundProvider OpenSound;

	public SoundProvider CloseSound;

	public CleverMenuItemSelectionManager Navigation;
}
