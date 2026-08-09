using System;
using System.Collections.Generic;
using Core;

public class CleverMenuItemGroup : CleverMenuItemGroupBase {
    public override bool IsVisible {
        get { return SelectionManager.IsVisible; }
        set {
            if (SelectionManager.FadeAnimator && SelectionManager.FadeAnimator.FinalOpacity < 0.05f && !value) {
                SelectionManager.SetVisibleImmediate(false);
            } else {
                SelectionManager.SetVisible(value);
            }
        }
    }

    public override bool CanBeEntered {
        get { return !CanBeEnteredCondition || CanBeEnteredCondition.Validate(null); }
    }

    public override bool IsActive {
        get { return SelectionManager.IsActive; }
        set {
            SelectionManager.IsActive = value;
            UpdateHighlight();
            if (SuspendOnActivated) {
                if (value) {
                    if (!m_isFrozen) {
                        m_isFrozen = true;
                        m_suspendablesIgnore.Clear();
                        SuspensionManager.GetSuspendables(m_suspendablesIgnore, gameObject);
                        SuspensionManager.SuspendExcluding(m_suspendablesIgnore);
                    }
                } else if (m_isFrozen) {
                    m_isFrozen = false;
                    SuspensionManager.ResumeExcluding(m_suspendablesIgnore);
                    m_suspendablesIgnore.Clear();
                }
            }
        }
    }

    public void OnDisable() {
        if (SuspendOnActivated && m_isFrozen) {
            m_isFrozen = false;
            SuspensionManager.ResumeExcluding(m_suspendablesIgnore);
            m_suspendablesIgnore.Clear();
        }
    }

    public override bool IsHighlightVisible {
        get { return SelectionManager.IsHighlightVisible; }
        set { SelectionManager.IsHighlightVisible = value; }
    }

    public void OnSelectionManagerBackPressed() {
        OnBackPressed();
    }

    public new void Awake() {
        base.Awake();
        CleverMenuItemSelectionManager selectionManager = SelectionManager;
        selectionManager.OptionChangeCallback = (Action)Delegate.Combine(selectionManager.OptionChangeCallback, new Action(OnMenuItemChange));
        CleverMenuItemSelectionManager selectionManager2 = SelectionManager;
        selectionManager2.OptionPressedCallback = (Action)Delegate.Combine(selectionManager2.OptionPressedCallback, new Action(OnMenuItemPressed));
        CleverMenuItemSelectionManager selectionManager3 = SelectionManager;
        selectionManager3.OnBackPressedCallback = (Action)Delegate.Combine(selectionManager3.OnBackPressedCallback, new Action(OnSelectionManagerBackPressed));
        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
            cleverMenuItemGroupItem.ItemGroup.IsActive = false;
            CleverMenuItemGroupBase itemGroup = cleverMenuItemGroupItem.ItemGroup;
            itemGroup.OnBackPressed = (Action)Delegate.Combine(itemGroup.OnBackPressed, new Action(OnOptionBackPressed));
        }
    }

    public new void OnDestroy() {
        base.OnDestroy();
        CleverMenuItemSelectionManager selectionManager = SelectionManager;
        selectionManager.OptionChangeCallback = (Action)Delegate.Remove(selectionManager.OptionChangeCallback, new Action(OnMenuItemChange));
        CleverMenuItemSelectionManager selectionManager2 = SelectionManager;
        selectionManager2.OptionPressedCallback = (Action)Delegate.Remove(selectionManager2.OptionPressedCallback, new Action(OnMenuItemPressed));
        CleverMenuItemSelectionManager selectionManager3 = SelectionManager;
        selectionManager3.OnBackPressedCallback = (Action)Delegate.Remove(selectionManager3.OnBackPressedCallback, new Action(OnSelectionManagerBackPressed));
        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
            CleverMenuItemGroupBase itemGroup = cleverMenuItemGroupItem.ItemGroup;
            itemGroup.OnBackPressed = (Action)Delegate.Remove(itemGroup.OnBackPressed, new Action(OnOptionBackPressed));
        }
    }

    public void Start() {
        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
            bool isVisible = SelectionManager.CurrentMenuItem == cleverMenuItemGroupItem.MenuItem && ExpandOnHighlight;
            cleverMenuItemGroupItem.ItemGroup.IsVisible = isVisible;
        }
    }

    public void OnOptionBackPressed() {
        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
            if (!ExpandOnHighlight) {
                cleverMenuItemGroupItem.ItemGroup.IsVisible = false;
            }

            cleverMenuItemGroupItem.ItemGroup.IsActive = false;
            cleverMenuItemGroupItem.ItemGroup.IsHighlightVisible = false;
        }

        if (!IsActive && OnCollapseSound) {
            Sound.Play(OnCollapseSound.GetSound(null), transform.position, null);
        }

        IsActive = true;
    }

    public void OnMenuItemChange() {
        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
            if (SelectionManager.CurrentMenuItem == cleverMenuItemGroupItem.MenuItem && ExpandOnHighlight) {
                cleverMenuItemGroupItem.ItemGroup.IsVisible = true;
            } else {
                cleverMenuItemGroupItem.ItemGroup.IsVisible = false;
            }
        }

        if (OnChangeSelectionSound && IsActive && m_playChangeSound) {
            Sound.Play(OnChangeSelectionSound.GetSound(null), transform.position, null);
        }

        IsActive = true;
        Root.OnMenuItemChangedInGroup(this);
    }

    public override bool OnMenuItemChangedInGroup(CleverMenuItemGroup group) {
        bool flag = false;
        if (group == this) {
            flag = true;
        } else {
            IsActive = false;
        }

        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
            if (cleverMenuItemGroupItem.ItemGroup.OnMenuItemChangedInGroup(group)) {
                flag = true;
            }
        }

        IsHighlightVisible = flag;
        return flag;
    }

    public void OnMenuItemPressed() {
        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
            cleverMenuItemGroupItem.ItemGroup.IsVisible = (SelectionManager.CurrentMenuItem == cleverMenuItemGroupItem.MenuItem);
        }

        foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem2 in Options) {
            if (SelectionManager.CurrentMenuItem == cleverMenuItemGroupItem2.MenuItem && cleverMenuItemGroupItem2.ItemGroup.CanBeEntered) {
                cleverMenuItemGroupItem2.ItemGroup.EnterInGroup();
                OnEnteredChildGroup();
                if (SelectionManager.CurrentMenuItem == cleverMenuItemGroupItem2.MenuItem && OnExpandSound) {
                    Sound.Play(OnExpandSound.GetSound(null), transform.position, null);
                }
            }
        }
    }

    public void UpdateHighlight() {
        if (HighlightAnimator == null) {
            return;
        }

        if (IsActive) {
            HighlightAnimator.Initialize();
            HighlightAnimator.AnimatorDriver.ContinueForward();
        } else {
            HighlightAnimator.Initialize();
            HighlightAnimator.AnimatorDriver.ContinueBackwards();
        }
    }

    public void OnEnteredChildGroup() {
        IsActive = false;
    }

    public override void EnterInGroup() {
        m_playChangeSound = false;
        SelectionManager.SetIndexToFirst();
        m_playChangeSound = true;
        IsActive = true;
        IsHighlightVisible = true;
        if (!ExpandOnHighlight) {
            foreach (CleverMenuItemGroupItem cleverMenuItemGroupItem in Options) {
                cleverMenuItemGroupItem.ItemGroup.IsVisible = false;
            }
        }
    }

    public void AddItem(CleverMenuItem item, CleverMenuItemGroupBase itemGroup) {
        CleverMenuItemGroupItem cleverMenuItemGroupItem = new CleverMenuItemGroupItem {
            ItemGroup = itemGroup,
            MenuItem = item
        };
        cleverMenuItemGroupItem.ItemGroup.IsActive = false;
        itemGroup.OnBackPressed = (Action)Delegate.Combine(itemGroup.OnBackPressed, new Action(OnOptionBackPressed));
        Options.Add(cleverMenuItemGroupItem);
    }

    public CleverMenuItemGroup Root;

    public List<CleverMenuItemGroupItem> Options;

    public CleverMenuItemSelectionManager SelectionManager;

    public SoundProvider OnExpandSound;

    public SoundProvider OnCollapseSound;

    public SoundProvider OnChangeSelectionSound;

    public bool ExpandOnHighlight;

    public Condition CanBeEnteredCondition;

    public TransparencyAnimator HighlightAnimator;

    public bool SuspendOnActivated;

    private bool m_playChangeSound = true;

    private bool m_isFrozen;

    private HashSet<ISuspendable> m_suspendablesIgnore = new HashSet<ISuspendable>();

    [Serializable]
    public class CleverMenuItemGroupItem {
        public CleverMenuItem MenuItem;

        public CleverMenuItemGroupBase ItemGroup;
    }
}
