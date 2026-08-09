using System;
using System.Collections.Generic;
using System.Text;
using Game;
using UnityEngine;

public class SkillTreeManager : MenuScreen {
    public bool AllLanesFull => EnergyLane.HasAllSkills && UtilityLane.HasAllSkills && CombatLane.HasAllSkills;

    public void Awake() {
        Instance = this;
        var navigationManager = NavigationManager;
        navigationManager.OptionChangeCallback = (Action)Delegate.Combine(navigationManager.OptionChangeCallback, new Action(OnMenuItemChange));
        navigationManager.OptionPressedCallback = (Action)Delegate.Combine(navigationManager.OptionPressedCallback, new Action(OnMenuItemPressed));
        navigationManager.OnBackPressedCallback = (Action)Delegate.Combine(navigationManager.OnBackPressedCallback, new Action(OnBackPressed));
        OnMenuItemChange();
        foreach (var navigationData in NavigationManager.Navigation) {
            navigationData.Condition = Condition;
        }

        UpdateRequirementsText();
    }

    public void OnBackPressed() {
        UI.Menu.HideMenuScreen();
    }

    public override void Hide() {
        NavigationManager.SetVisible(false);
    }

    public override void ShowImmediate() {
        NavigationManager.SetVisibleImmediate(true);
        OnMenuItemChange();
    }

    public override void HideImmediate() {
        NavigationManager.SetVisibleImmediate(false);
    }

    public override void Show() {
        NavigationManager.SetVisible(true);
        OnMenuItemChange();
    }

    public static bool Condition(CleverMenuItemSelectionManager.NavigationData navigationData) {
        var component = navigationData.To.GetComponent<SkillItem>();
        return !component || component.Visible;
    }

    public void OnDestroy() {
        var navigationManager = NavigationManager;
        navigationManager.OptionChangeCallback = (Action)Delegate.Remove(navigationManager.OptionChangeCallback, new Action(OnMenuItemChange));
        navigationManager.OptionPressedCallback = (Action)Delegate.Remove(navigationManager.OptionPressedCallback, new Action(OnMenuItemPressed));
        navigationManager.OnBackPressedCallback = (Action)Delegate.Remove(navigationManager.OnBackPressedCallback, new Action(OnBackPressed));
        Instance = null;
    }

    public void OnMenuItemPressed() {
        if (CurrentSkillItem == null) {
            if (Characters.Sein && !Characters.Sein.IsSuspended) {
                NavigationManager.Index = -1;
            }

            return;
        }

        if (CurrentSkillItem.HasSkillItem) {
            if (OnAlreadyEarnedAbility) {
                RequirementsLineAShake.Restart();
                OnAlreadyEarnedAbility.Perform(null);
            }

            return;
        }

        if (CurrentSkillItem.CanEarnSkill) {
            CurrentSkillItem.HasSkillItem = true;
            Characters.Sein.PlayerAbilities.SetAbility(CurrentSkillItem.Ability, true);
            Characters.Sein.PlayerAbilities.GainAbilityAction = CurrentSkillItem.GainSkillSequence;
            InstantiateUtility.Instantiate(GainSkillEffect, CurrentSkillItem.transform.position, Quaternion.identity);
            RandomizerBonus.SpentAP(CurrentSkillItem.ActualRequiredSkillPoints);
            BingoController.OnGainAbility(CurrentSkillItem.Ability);
            if (CurrentSkillItem.Ability == AbilityType.Sense) RandomizerHints.TryShowSenseHint();
            Characters.Sein.Level.SkillPoints -= CurrentSkillItem.ActualRequiredSkillPoints;
            if (OnGainAbility) {
                OnGainAbility.Perform(null);
            }

            SeinLevel.HasSpentSkillPoint = true;
            AchievementsController.AwardAchievement(SpentFirstSkillPointAchievement);
            GameController.Instance.CreateCheckpoint();
            RandomizerStatsManager.OnSave(false);
            GameController.Instance.SaveGameController.PerformSave();
            UpdateRequirementsText();
            return;
        }

        if (!CurrentSkillItem.SoulRequirementMet) {
            if (CurrentSkillItem.RequiresAbilitiesOrItems) {
                RequirementsLineAShake.Restart();
            } else {
                RequirementsLineAShake.Restart();
            }
        }

        if (!CurrentSkillItem.AbilitiesRequirementMet) {
            RequirementsLineAShake.Restart();
        }

        if (OnCantEarnSkill) {
            OnCantEarnSkill.Perform(null);
        }
    }

    public MessageDescriptor AbilityMastered => new MessageDescriptor("$" + AbilityMasteredMessageProvider + "$");

    public MessageProvider AbilityName(AbilityType ability) {
        foreach (var abilityMessageProvider in AbilityMessages) {
            if (abilityMessageProvider.AbilityType == ability) {
                return abilityMessageProvider.MessageProvider;
            }
        }

        return null;
    }

    public string RequiredAbilitiesText(SkillItem skillItem) {
        var abilitiesRequirementMet = skillItem.AbilitiesRequirementMet;
        var stringBuilder = new StringBuilder(30);
        stringBuilder.Append(" ");
        for (var j = 0; j < skillItem.RequiredItems.Count; j++) {
            var skillItem2 = skillItem.RequiredItems[j];
            if (abilitiesRequirementMet) {
                stringBuilder.Append("$" + skillItem2.Name + "$");
            } else {
                stringBuilder.Append("#" + skillItem2.Name + "#");
            }

            if (j != skillItem.RequiredItems.Count - 1) {
                stringBuilder.Append(!abilitiesRequirementMet ? "@,@ " : "$,$ ");
            }
        }

        if (abilitiesRequirementMet) {
            return "$" + RequiresMessageProvider.ToString().Replace("[Requirements]", "$" + stringBuilder + "$") + "$";
        }

        return "@" + RequiresMessageProvider.ToString().Replace("[Requirements]", "@" + stringBuilder + "@") + "@";
    }

    public void UpdateRequirementsText() {
        CurrentSkillItem = NavigationManager.CurrentMenuItem.GetComponent<SkillItem>();
        if (CurrentSkillItem) {
            AbilityTitle.SetMessageProvider(CurrentSkillItem.Name);
            AbilityDescription.SetMessageProvider(CurrentSkillItem.Description);
            if (CurrentSkillItem.HasSkillItem) {
                RequirementsLineA.SetMessage(AbilityMastered);
                return;
            }

            if (CurrentSkillItem.RequiresAbilitiesOrItems) {
                RequirementsLineA.SetMessage(new MessageDescriptor(RequiredAbilitiesText(CurrentSkillItem) + "\n" + RequiredSoulsText(CurrentSkillItem)));
                return;
            }

            RequirementsLineA.SetMessage(new MessageDescriptor(RequiredSoulsText(CurrentSkillItem)));
        }
    }

    public string NameText(SkillItem skillItem) {
        if (skillItem.HasSkillItem) {
            return "$" + skillItem.Name + "$";
        }

        if (skillItem.CanEarnSkill) {
            return "#" + skillItem.Name + "#";
        }

        return "@" + skillItem.Name + "@";
    }

    public string RequiredSoulsText(SkillItem skillItem) {
        if (skillItem.HasSkillItem) {
            return string.Empty;
        }

        var requiredPoints = skillItem.ActualRequiredSkillPoints;
        var totalRequiredPoints = skillItem.ActualTotalRequiredSkillPoints;
        var costMessage = requiredPoints != 1 ? RandomizerText.CostsAbilityPoints : RandomizerText.CostsAbilityPoint;
        if (totalRequiredPoints <= Characters.Sein.Level.SkillPoints) {
            return "$" + costMessage.Replace("[Amount]", requiredPoints.ToString()).Replace("[Total]", totalRequiredPoints.ToString()) + "$";
        }

        return "@" + costMessage.Replace("[Amount]", requiredPoints.ToString()).Replace("[Total]", totalRequiredPoints.ToString()) + "@";
    }

    public void OnMenuItemChange() {
        CurrentSkillItem = NavigationManager.CurrentMenuItem.GetComponent<SkillItem>();
        if (CurrentSkillItem == null) {
            Cursor.gameObject.SetActive(false);
            InfoPanel.SetActive(false);
            AbilityDiskInfoPanel.SetActive(true);
            AbilityDiskInfoPanelDescription.RefreshText();
            return;
        }

        Cursor.gameObject.SetActive(true);
        Cursor.position = CurrentSkillItem.transform.position;
        foreach (var obj in LargeIcon.transform) {
            var transform = (Transform)obj;
            transform.gameObject.SetActive(transform.name == CurrentSkillItem.LargeIcon.name);
        }

        InfoPanel.SetActive(true);
        AbilityDiskInfoPanel.SetActive(false);
        UpdateRequirementsText();
    }

    public void FixedUpdate() {
        if (NavigationManager.Index == -1) {
            NavigationManager.Index = 0;
        }
    }

    public static SkillTreeManager Instance;

    public CleverMenuItemSelectionManager NavigationManager;

    public SkillItem CurrentSkillItem;

    public Transform Cursor;

    public SoundProvider OpenSound;

    public SoundProvider CloseSound;

    public GameObject LargeIcon;

    public Renderer LargeIconGlow;

    public MessageBox RequirementsLineA;

    public MessageBox AbilityTitle;

    public MessageBox AbilityDescription;

    public GameObject InfoPanel;

    public MessageBox AbilityDiskInfoPanelDescription;

    public GameObject AbilityDiskInfoPanel;

    public SkillTreeLaneLogic EnergyLane;

    public SkillTreeLaneLogic UtilityLane;

    public SkillTreeLaneLogic CombatLane;

    public GameObject GainSkillEffect;

    public LegacyAnimator RequirementsLineAShake;

    public ActionMethod OnGainAbility;

    public ActionMethod OnAlreadyEarnedAbility;

    public ActionMethod OnCantEarnSkill;

    public MessageProvider AbilityPointMessageProvider;

    public MessageProvider AbilityPointsMessageProvider;

    public MessageProvider RequiresMessageProvider;

    public MessageProvider AbilityMasteredMessageProvider;

    public AchievementAsset SpentFirstSkillPointAchievement;

    public List<AbilityMessageProvider> AbilityMessages;

    [Serializable]
    public class AbilityMessageProvider {
        public AbilityType AbilityType;

        public MessageProvider MessageProvider;
    }
}
