using System;
using Core;
using Game;
using Sein.World;
using UnityEngine;

public class InventoryManager : MenuScreen {
    public override void Show() {
        PracticeMenu.OnPauseShown(this);
        NavigationManager.SetVisible(true);
        NavigationManager.SetIndexToFirst();
    }

    public override void Hide() {
        NavigationManager.SetVisible(false);
    }

    public override void ShowImmediate() {
        PracticeMenu.OnPauseShown(this);
        NavigationManager.SetVisibleImmediate(true);
        NavigationManager.SetIndexToFirst();
    }

    public override void HideImmediate() {
        NavigationManager.SetVisibleImmediate(false);
    }

    public void Awake() {
        Instance = this;

        var navigationManager = NavigationManager;
        navigationManager.OptionChangeCallback = (Action)Delegate.Combine(navigationManager.OptionChangeCallback, new Action(OnMenuItemChange));
        navigationManager.OptionPressedCallback = (Action)Delegate.Combine(navigationManager.OptionPressedCallback, new Action(OnMenuItemPressed));
        navigationManager.OnBackPressedCallback = (Action)Delegate.Combine(navigationManager.OnBackPressedCallback, new Action(OnBackPressed));

        var instance = DifficultyController.Instance;
        instance.OnDifficultyChanged = (Action)Delegate.Combine(instance.OnDifficultyChanged, new Action(OnDifficultyChanged));

        if (Difficulty) {
            var difficultyProvider = (DifficultyModeMessageProvider)Difficulty.MessageProvider;
            difficultyProvider.Easy = RandomizerText.DifficultyOverrides.Easy.NameOverrideUpper;
            difficultyProvider.Normal = RandomizerText.DifficultyOverrides.Normal.NameOverrideUpper;
            difficultyProvider.Hard = RandomizerText.DifficultyOverrides.Hard.NameOverrideUpper;
            difficultyProvider.OneLife = RandomizerText.DifficultyOverrides.OneLife.NameOverrideUpper;

            var difficultySequence = (ActionSequence)Difficulty.transform.parent.GetComponent<RunActionCondition>().Action;
            var difficultyAction = (InstantiateAction)difficultySequence.Actions[0];
            var difficultyScreen = difficultyAction.Prefab.GetComponent<ChangeDifficultyScreen>();
            difficultyScreen.Easy = RandomizerText.DifficultyOverrides.Easy.NameOverride;
            difficultyScreen.Normal = RandomizerText.DifficultyOverrides.Normal.NameOverride;
            difficultyScreen.Hard = RandomizerText.DifficultyOverrides.Hard.NameOverride;
            difficultyScreen.OneLife = RandomizerText.DifficultyOverrides.OneLife.NameOverride;

            var changeDifficultyManager = difficultyAction.Prefab.GetComponent<CleverMenuItemSelectionManager>();
            changeDifficultyManager.MenuItems[0].GetComponentInChildren<MessageBox>().SetMessageProvider(RandomizerText.DifficultyOverrides.Easy.NameOverrideUpper);
            changeDifficultyManager.MenuItems[1].GetComponentInChildren<MessageBox>().SetMessageProvider(RandomizerText.DifficultyOverrides.Normal.NameOverrideUpper);
            changeDifficultyManager.MenuItems[2].GetComponentInChildren<MessageBox>().SetMessageProvider(RandomizerText.DifficultyOverrides.Hard.NameOverrideUpper);
        }

        waterVeinClueText = Instantiate(EnergyUpgradesText);
        waterVeinClueText.transform.position = GinsoTreeKey.transform.position + Vector3.down * 0.55f;
        waterVeinClueText.transform.SetParent(GinsoTreeKey.transform);
        gumonSealClueText = Instantiate(EnergyUpgradesText);
        gumonSealClueText.transform.position = ForlornRuinsKey.transform.position + Vector3.down * 0.55f;
        gumonSealClueText.transform.SetParent(ForlornRuinsKey.transform);
        sunstoneClueText = Instantiate(EnergyUpgradesText);
        sunstoneClueText.transform.position = MountHoruKey.transform.position + Vector3.down * 0.55f;
        sunstoneClueText.transform.SetParent(MountHoruKey.transform);
    }

    public void OnBackPressed() {
        UI.Menu.HideMenuScreen();
    }

    public void OnMenuItemChange() {
    }

    public void OnMenuItemPressed() {
        var component = NavigationManager.CurrentMenuItem.GetComponent<InventoryAbilityItem>();
        if (component && !component.HasAbility) {
            if (PressUngainedAbilityOptionSound) {
                Sound.Play(PressUngainedAbilityOptionSound.GetSound(null), transform.position, null);
            }

            return;
        }

        var component2 = NavigationManager.CurrentMenuItem.GetComponent<InventoryItemHelpText>();
        if (component2) {
            SuspensionManager.SuspendAll();
            var messageBox = UI.MessageController.ShowMessageBoxB(HelpMessageBox, component2.HelpMessage, Vector3.zero, float.PositiveInfinity);
            if (messageBox) {
                messageBox.SetAvatar(component2.Avatar);
                messageBox.OnMessageScreenHide += OnMessageScreenHide;
            } else {
                SuspensionManager.ResumeAll();
            }

            m_currentCloseMessageSound = !component ? CloseStatisticsMessageSound : CloseAbilityMessageSound;
            if (component && PressAbilityOptionSound) {
                Sound.Play(PressAbilityOptionSound.GetSound(null), transform.position, null);
            }
        }
    }

    public void OnMessageScreenHide() {
        SuspensionManager.ResumeAll();
        if (m_currentCloseMessageSound && transform) {
            Sound.Play(m_currentCloseMessageSound.GetSound(null), transform.position, null);
        }
    }

    public void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }

        var navigationManager = NavigationManager;
        navigationManager.OptionChangeCallback = (Action)Delegate.Remove(navigationManager.OptionChangeCallback, new Action(OnMenuItemChange));
        navigationManager.OptionPressedCallback = (Action)Delegate.Remove(navigationManager.OptionPressedCallback, new Action(OnMenuItemPressed));
        navigationManager.OnBackPressedCallback = (Action)Delegate.Remove(navigationManager.OnBackPressedCallback, new Action(OnBackPressed));

        var instance = DifficultyController.Instance;
        instance.OnDifficultyChanged = (Action)Delegate.Remove(instance.OnDifficultyChanged, new Action(OnDifficultyChanged));
    }

    public void OnDifficultyChanged() {
        if (Difficulty) {
            Difficulty.RefreshText();
        }
    }

    public void UpdateItems() {
        SeinCharacter sein = Characters.Sein;
        if (sein == null) {
            return;
        }

        CompletionText.SetMessage(new MessageDescriptor(GameWorld.Instance.CompletionPercentage + "%"));
        DeathText.SetMessage(new MessageDescriptor(SeinDeathCounter.Count.ToString()));
        HealthUpgradesText.SetMessage(new MessageDescriptor(sein.Mortality.Health.HealthUpgradesCollected + " / " + 12));
        EnergyUpgradesText.SetMessage(new MessageDescriptor(sein.Energy.EnergyUpgradesCollected + " / " + 15));
        SkillPointUniquesText.SetMessage(new MessageDescriptor(sein.Inventory.SkillPointsCollected + " / " + 33));
        waterVeinClueText.SetMessage(new MessageDescriptor(GetKeyLabel(Keys.GinsoTree, RandomizerBonus.WaterVeinShards(), 0)));
        gumonSealClueText.SetMessage(new MessageDescriptor(GetKeyLabel(Keys.ForlornRuins, RandomizerBonus.GumonSealShards(), 1)));
        sunstoneClueText.SetMessage(new MessageDescriptor(GetKeyLabel(Keys.MountHoru, RandomizerBonus.SunstoneShards(), 2)));
        var timer = GameController.Instance.Timer;
        TimeText.SetMessage(new MessageDescriptor(string.Format("{0:D2}:{1:D2}:{2:D2}", timer.Hours, timer.Minutes, timer.Seconds)));
        var component = NavigationManager.CurrentMenuItem.GetComponent<InventoryAbilityItem>();
        if (component) {
            AbilityNameText.gameObject.SetActive(true);
            AbilityItemHighlight.SetActive(true);
            AbilityItemHighlight.transform.position = component.transform.position;
            if (component.HasAbility) {
                AbilityNameText.SetMessageProvider(component.AbilityName);
            } else {
                AbilityNameText.SetMessageProvider(LockedMessageProvider);
            }
        } else {
            AbilityNameText.gameObject.SetActive(false);
            AbilityItemHighlight.SetActive(false);
        }

        if (Difficulty) {
            Difficulty.RefreshText();
        }
    }

    public void FixedUpdate() {
        UpdateItems();
    }

    public void OnEnable() {
        UpdateItems();
    }

    public string GetKeyLabel(bool hasKey, int shards, int keyIndex) {
        if (hasKey) {
            return "";
        }

        if (Randomizer.Shards) {
            return string.Format("{0}/3", shards);
        }

        if (!Randomizer.CluesMode) {
            return "";
        }

        if (RandomizerBonus.SkillTreeProgression() >= RandomizerClues.RevealOrder[keyIndex] * 3) {
            return RandomizerClues.ClueFor(keyIndex);
        }

        return "";
    }

    public const int TotalHealthUpgrades = 12;

    public const int TotalEnergyUpgrades = 15;

    public const int TotalSkillPoints = 33;

    public const int MaxLevel = 20;

    public static InventoryManager Instance;

    public CleverMenuItemSelectionManager NavigationManager;

    public SoundProvider OpenSound;

    public SoundProvider CloseSound;

    public SoundProvider PressAbilityOptionSound;

    public SoundProvider PressUngainedAbilityOptionSound;

    public SoundProvider CloseAbilityMessageSound;

    public SoundProvider CloseStatisticsMessageSound;

    private SoundProvider m_currentCloseMessageSound;

    public GameObject AbilityItemHighlight;

    public MessageBox AbilityNameText;

    public MessageBox TimeText;

    public MessageBox CompletionText;

    public MessageBox DeathText;

    public MessageBox HealthUpgradesText;

    public MessageBox EnergyUpgradesText;

    public MessageBox SkillPointUniquesText;

    public GameObject GinsoTreeKey;

    public GameObject ForlornRuinsKey;

    public GameObject MountHoruKey;

    public GameObject WorldEventsGroup;

    public MessageBox Difficulty;

    public MessageProvider LockedMessageProvider;

    public MessageProvider NotAvailableYetMessageProvider;

    public MessageProvider DiedZeroTimesMessageProvider;

    public MessageProvider DiedOneTimeMessagProvider;

    public MessageProvider DiedMultipleTimesMessageProvider;

    public GameObject HelpMessageBox;

    private MessageBox gumonSealClueText;

    private MessageBox waterVeinClueText;

    private MessageBox sunstoneClueText;
}
