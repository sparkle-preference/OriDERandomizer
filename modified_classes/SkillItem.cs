using System.Collections.Generic;
using Game;
using UnityEngine;

public class SkillItem : MonoBehaviour {
    public int ActualRequiredSkillPoints {
        get {
            if (DifficultyController.Instance.Difficulty == DifficultyMode.Hard) {
                return RequiredHardSkillPoints;
            }

            return RequiredSkillPoints;
        }
    }

    public int ActualTotalRequiredSkillPoints {
        get {
            if (DifficultyController.Instance.Difficulty == DifficultyMode.Hard) {
                return TotalRequiredHardSkillPoints;
            }

            return TotalRequiredSkillPoints;
        }
    }

    public int TotalRequiredHardSkillPoints {
        get => totalRequiredHardPoints;
        set => totalRequiredHardPoints = value;
    }

    public int TotalRequiredSkillPoints {
        get => totalRequiredPoints;
        set => totalRequiredPoints = value;
    }

    public Color LargeIconColor { get; set; }

    public bool Visible => true;

    public bool RequiresAbilitiesOrItems => RequiredAbilities.Count != 0 || RequiredItems.Count != 0;

    public bool SoulRequirementMet => ActualRequiredSkillPoints <= Characters.Sein.Level.SkillPoints;

    public bool AbilitiesRequirementMet {
        get {
            using (var enumerator = RequiredItems.GetEnumerator()) {
                while (enumerator.MoveNext()) {
                    if (!enumerator.Current.HasSkillItem) {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public void Awake() {
        animator = Icon.GetComponent<TransparencyAnimator>();
    }

    public bool CanEarnSkill => SoulRequirementMet && AbilitiesRequirementMet;

    public void FixedUpdate() {
        UpdateItem();
    }

    public void UpdateItem() {
        LearntSkillGlow.SetActive(HasSkillItem && Visible);
        Icon.gameObject.SetActive(Visible);
        if (HasSkillItem == animator.AnimatorDriver.IsReversed) {
            animator.Initialize();
            if (HasSkillItem) {
                animator.AnimatorDriver.ContinueForward();
            } else {
                animator.AnimatorDriver.ContinueBackwards();
            }
        }
    }

    public void OnEnable() {
        HasSkillItem = Characters.Sein.PlayerAbilities.HasAbility(Ability);
        UpdateItem();
        animator.Initialize();
        if (HasSkillItem) {
            animator.AnimatorDriver.GoToEnd();
        } else {
            animator.AnimatorDriver.GoToStart();
        }
    }

    public MessageProvider Name => RandomizerText.GetAbilityName(Ability) ?? NameMessageProvider;

    public MessageProvider Description => RandomizerText.GetAbilityDescription(Ability) ?? DescriptionMessageProvider;

    public int RequiredSkillPoints = 1;

    public int RequiredHardSkillPoints = 1;

    public List<AbilityType> RequiredAbilities = new List<AbilityType>();

    public List<SkillItem> RequiredItems = new List<SkillItem>();

    public AbilityType Ability;

    public Texture LargeIcon;

    public MessageProvider NameMessageProvider;

    public MessageProvider DescriptionMessageProvider;

    public Renderer Icon;

    public ActionMethod GainSkillSequence;

    private TransparencyAnimator animator;

    public GameObject LearntSkillGlow;

    public bool HasSkillItem;

    private int totalRequiredPoints;

    private int totalRequiredHardPoints;
}
