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
        get { return m_totalRequiredHardPoints; }
        set { m_totalRequiredHardPoints = value; }
    }

    public int TotalRequiredSkillPoints {
        get { return m_totalRequiredPoints; }
        set { m_totalRequiredPoints = value; }
    }

    public Color LargeIconColor { get; set; }

    public bool Visible {
        get { return true; }
    }

    public bool RequiresAbilitiesOrItems {
        get { return RequiredAbilities.Count != 0 || RequiredItems.Count != 0; }
    }

    public bool SoulRequirementMet {
        get { return ActualRequiredSkillPoints <= Characters.Sein.Level.SkillPoints; }
    }

    public bool AbilitiesRequirementMet {
        get {
            using (List<SkillItem>.Enumerator enumerator = RequiredItems.GetEnumerator()) {
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
        m_animator = Icon.GetComponent<TransparencyAnimator>();
    }

    public bool CanEarnSkill {
        get { return SoulRequirementMet && AbilitiesRequirementMet; }
    }

    public void FixedUpdate() {
        UpdateItem();
    }

    public void UpdateItem() {
        LearntSkillGlow.SetActive(HasSkillItem && Visible);
        Icon.gameObject.SetActive(Visible);
        if (HasSkillItem == m_animator.AnimatorDriver.IsReversed) {
            m_animator.Initialize();
            if (HasSkillItem) {
                m_animator.AnimatorDriver.ContinueForward();
            } else {
                m_animator.AnimatorDriver.ContinueBackwards();
            }
        }
    }

    public void OnEnable() {
        HasSkillItem = Characters.Sein.PlayerAbilities.HasAbility(Ability);
        UpdateItem();
        m_animator.Initialize();
        if (HasSkillItem) {
            m_animator.AnimatorDriver.GoToEnd();
        } else {
            m_animator.AnimatorDriver.GoToStart();
        }
    }

    public MessageProvider Name {
        get { return RandomizerText.GetAbilityName(Ability) ?? NameMessageProvider; }
    }

    public MessageProvider Description {
        get { return RandomizerText.GetAbilityDescription(Ability) ?? DescriptionMessageProvider; }
    }

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

    private TransparencyAnimator m_animator;

    public GameObject LearntSkillGlow;

    public bool HasSkillItem;

    private int m_totalRequiredPoints;

    private int m_totalRequiredHardPoints;
}
