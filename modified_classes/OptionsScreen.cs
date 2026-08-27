using System;
using Game;
using UnityEngine;
using Input = Core.Input;

public class OptionsScreen : MenuScreen, ISuspendable {
    public void Awake() {
        Instance = this;
        SuspensionManager.Register(this);
        var navigation = Navigation;
        navigation.OnBackPressedCallback = (Action)Delegate.Combine(navigation.OnBackPressedCallback, new Action(OnBackPressed));
        // Vanilla leaves SETTINGS, CONTROLS, LEADERBOARDS. Dropping leaderboards puts
        // CONTROLS last, so inserting after SETTINGS lands everything in order.
        var root = GetComponent<CleverMenuItemGroup>();
        // subscreens swap here, and a fading exit overlaps the one arriving
        root.HideImmediately = true;
        root.StayLit = true;
        RemoveSubscreen(2);
        AddSubscreen<ControlsSettingsScreen>("CONTROL OPTIONS", 1);
        AddSubscreen<AccessibilitySettingsScreen>("ACCESSIBILITY", 2);
        AddSubscreen<RandoUiScreen>("RANDO UI", 3);
        AddSubscreen<RandoOptionsScreen>("RANDO OPTIONS", 4);
        AddSubscreen<KeybindsScreen>("KEYBINDS", 5);
        AddSubscreen<ControllerBindsScreen>("CONTROLLER BINDS", 6);
    }

    public void OnDestroy() {
        var navigation = Navigation;
        navigation.OnBackPressedCallback = (Action)Delegate.Remove(navigation.OnBackPressedCallback, new Action(OnBackPressed));
    }

    public void FixedUpdate() {
        if (Input.Bash.OnPressed) {
            XboxOne.Help();
        }
    }

    public override void Hide() {
        Navigation.SetVisible(false);
        foreach (var cleverMenuItemGroupItem in GetComponent<CleverMenuItemGroup>().Options) {
            if (cleverMenuItemGroupItem.ItemGroup) {
                cleverMenuItemGroupItem.ItemGroup.IsActive = false;
            }
        }
    }

    public override void ShowImmediate() {
        Navigation.SetVisibleImmediate(true);
        Navigation.SetIndexToFirst();
    }

    public override void HideImmediate() {
        Navigation.SetVisibleImmediate(false);
    }

    public override void Show() {
        Navigation.RefreshVisible();
        Navigation.SetVisible(true);
        Navigation.SetIndexToFirst();
    }

    public void OnBackPressed() {
        if (GameController.Instance.GameInTitleScreen) {
            UI.Menu.HideMenuScreen();
        } else {
            UI.Menu.ShowInventoryOrPauseMenu();
        }
    }

    public bool IsSuspended { get; set; }

    // The screen object stays, so Steam's leaderboard code keeps its references; only the
    // way in goes. Put it to sleep first: the group deactivates its screens in Awake, and
    // one dropped from Options before that runs would otherwise stay awake and polling.
    public void RemoveSubscreen(int index) {
        var item = Navigation.MenuItems[index];
        var group = GetComponent<CleverMenuItemGroup>();
        var option = group.Options.Find(o => o.MenuItem == item);
        if (option != null && option.ItemGroup != null) {
            option.ItemGroup.IsActive = false;
            option.ItemGroup.gameObject.SetActive(false);
        }

        group.Options.Remove(option);
        Navigation.MenuItems.RemoveAt(index);
        var layout = Navigation.transform.FindChild("mainMenuUI").GetComponent<CleverMenuItemLayout>();
        layout.MenuItems.Remove(item);
        Destroy(item.gameObject);
        layout.Sort();
    }

    public void AddSubscreen<TController>(string label, int index) where TController : MonoBehaviour {
        Navigation.AddMenuItem(label, index, Navigation.transform.FindChild("mainMenuUI").GetComponent<CleverMenuItemLayout>(), delegate { });
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
