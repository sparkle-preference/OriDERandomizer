using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Input = Core.Input;

public class CleverMenuItemSelectionManager : MonoBehaviour, ISuspendable {
    public void SetVisible(bool visible) {
        if (visible) {
            gameObject.SetActive(true);
            m_isVisible = true;
            if (FadeAnimator) {
                FadeAnimator.Initialize();
                FadeAnimator.AnimatorDriver.ContinueForward();
            }
        } else {
            m_isVisible = false;
            if (FadeAnimator) {
                FadeAnimator.Initialize();
                FadeAnimator.AnimatorDriver.ContinueBackwards();
                return;
            }

            gameObject.SetActive(false);
        }
    }

    public void SetVisibleImmediate(bool visible) {
        if (visible) {
            gameObject.SetActive(true);
            m_isVisible = true;
            if (FadeAnimator) {
                FadeAnimator.Initialize();
                FadeAnimator.AnimatorDriver.GoToEnd();
                FadeAnimator.AnimatorDriver.Pause();
            }
        } else {
            m_isVisible = false;
            if (FadeAnimator) {
                FadeAnimator.Initialize();
                FadeAnimator.AnimatorDriver.GoToStart();
                FadeAnimator.AnimatorDriver.Pause();
            }

            gameObject.SetActive(false);
        }
    }

    public bool IsVisible => m_isVisible;

    public bool IsHighlightVisible {
        get => m_isHighlightVisible;
        set {
            m_isHighlightVisible = value;
            if (m_isHighlightVisible) {
                if (CurrentMenuItem) {
                    CurrentMenuItem.OnHighlight();
                }
            } else if (CurrentMenuItem) {
                CurrentMenuItem.OnUnhighlight();
            }
        }
    }

    public void RefreshVisible() {
        foreach (var cleverMenuItem in MenuItems) {
            cleverMenuItem.RefreshVisible();
        }
    }

    public void OnEnable() {
        m_isVisible = true;
        if (FadeAnimator) {
            FadeAnimator.Initialize();
            FadeAnimator.AnimatorDriver.ContinueForward();
        }

        RefreshVisible();
    }

    public void OnDisable() {
        m_isVisible = false;
    }

    public bool IsActive {
        get => m_isActive;
        set => m_isActive = value;
    }

    public bool IsLocked { get; set; }

    public CleverMenuItem CurrentMenuItem {
        get {
            if (Index < 0 || Index >= MenuItems.Count) {
                return null;
            }

            return MenuItems[Index];
        }
    }

    public void Awake() {
        SuspensionManager.Register(this);
    }

    public void OnDestroy() {
        SuspensionManager.Unregister(this);
    }

    public void MoveSelection(bool forward) {
        var num = Index;
        var num2 = 0;
        if (forward) {
            do {
                num = (num + 1) % MenuItems.Count;
                if (num2++ > MenuItems.Count) {
                    goto IL_43;
                }
            } while (!MenuItems[num].IsActivated);

            goto IL_93;
            IL_43:
            num = Index;
        } else {
            do {
                num = num - 1 >= 0 ? num - 1 : MenuItems.Count - 1;
                if (num2++ > MenuItems.Count) {
                    goto IL_8C;
                }
            } while (!MenuItems[num].IsActivated);

            goto IL_93;
            IL_8C:
            num = Index;
        }

        IL_93:
        if (num == Index) {
            return;
        }

        if (MenuItems[num].IsActivated) {
            SetCurrentItem(num);
        }
    }

    public void SetCurrentMenuItem(CleverMenuItem menuItem) {
        var currentItem = MenuItems.FindIndex(a => a == menuItem);
        SetCurrentItem(currentItem);
    }

    public void SetCurrentItem(int index) {
        if (CurrentMenuItem) {
            CurrentMenuItem.OnUnhighlight();
        }

        Index = index;
        if (CurrentMenuItem) {
            CurrentMenuItem.OnHighlight();
            OptionChangeCallback();
            if (OptionChangeAction) {
                OptionChangeAction.Perform(null);
            }
        }
    }

    public void Start() {
        m_holdRemainingTime = 0.4f;
        m_delayNavigation = Input.MenuDown.IsPressed || Input.MenuUp.IsPressed;
        if (IsHighlightVisible && CurrentMenuItem) {
            CurrentMenuItem.OnHighlight();
        }

        if (name == "inventoryScreen") {
            m_isPauseScreen = true;
            var cleverMenuItem = MenuItems[0];
            var cleverMenuItem2 = MenuItems[9];
            Navigation.Add(
                new NavigationData {
                    From = cleverMenuItem,
                    To = cleverMenuItem2
                }
            );
            Navigation.Add(
                new NavigationData {
                    From = cleverMenuItem2,
                    To = cleverMenuItem
                }
            );
        }
    }

    public void SetIndexToFirst() {
        for (var i = 0; i < MenuItems.Count; i++) {
            if (MenuItems[i].IsActivated) {
                SetCurrentItem(i);
                return;
            }
        }
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (!GameController.IsFocused) {
            return;
        }

        if (!IsVisible) {
            if (FadeAnimator && FadeAnimator.AnimatorDriver.IsReversed && !FadeAnimator.AnimatorDriver.IsPlaying) {
                gameObject.SetActive(false);
            }

            return;
        }

        if (CurrentMenuItem && CurrentMenuItem.IsPerforming()) {
            return;
        }

        if (IsLocked) {
            return;
        }

        if (Input.LeftClick.OnPressed) {
            var cleverMenuItemUnderCursor = CleverMenuItemUnderCursor;
            if (cleverMenuItemUnderCursor) {
                SetCurrentMenuItem(cleverMenuItemUnderCursor);
                PressCurrentItem();
                return;
            }
        }

        if (Input.CursorMoved && HighlightOnMouseOver) {
            var cleverMenuItemUnderCursor2 = CleverMenuItemUnderCursor;
            if (cleverMenuItemUnderCursor2 && cleverMenuItemUnderCursor2 != CurrentMenuItem) {
                SetCurrentMenuItem(cleverMenuItemUnderCursor2);
            }

            if (UnhighlightOnMouseLeave && cleverMenuItemUnderCursor2 == null && CurrentMenuItem.IsHighlighted) {
                CurrentMenuItem.OnUnhighlight();
            }

            if (HighlightOnMouseOver && cleverMenuItemUnderCursor2 != null && !cleverMenuItemUnderCursor2.IsHighlighted) {
                CurrentMenuItem.OnHighlight();
            }
        }

        if (!IsActive) {
            return;
        }

        switch (ItemDirection) {
            case Direction.LeftToRight:
                if (Input.MenuLeft.OnPressed) {
                    MoveSelection(false);
                    m_holdRemainingTime = 0.4f;
                }

                if (Input.MenuRight.OnPressed) {
                    MoveSelection(true);
                    m_holdRemainingTime = 0.4f;
                }

                if (Input.MenuLeft.Pressed || Input.MenuRight.Pressed) {
                    m_holdRemainingTime -= Time.deltaTime;
                    if (m_holdRemainingTime < 0f) {
                        if (Input.MenuLeft.Pressed) {
                            MoveSelection(false);
                        }

                        if (Input.MenuRight.Pressed) {
                            MoveSelection(true);
                        }
                    }
                }

                break;
            case Direction.TopToBottom:
                if (m_delayNavigation) {
                    if (Input.MenuDown.IsPressed || Input.MenuUp.IsPressed) {
                        break;
                    }

                    m_delayNavigation = false;
                }

                if (Input.MenuUp.OnPressed) {
                    MoveSelection(false);
                    m_holdRemainingTime = 0.4f;
                }

                if (Input.MenuDown.OnPressed) {
                    MoveSelection(true);
                    m_holdRemainingTime = 0.4f;
                }

                if (Input.MenuUp.Pressed || Input.MenuDown.Pressed) {
                    m_holdRemainingTime -= Time.deltaTime;
                    if (m_holdRemainingTime < 0f) {
                        if (Input.MenuUp.Pressed) {
                            m_holdRemainingTime = 0.04f;
                            MoveSelection(false);
                        }

                        if (Input.MenuDown.Pressed) {
                            m_holdRemainingTime = 0.04f;
                            MoveSelection(true);
                        }
                    }
                }

                break;
            case Direction.NavigationCage:
                HandleNavigationCage();
                break;
        }

        if (Input.ActionButtonA.OnPressed && !Input.ActionButtonA.Used) {
            if (m_buttonPressDelay <= 0f) {
                m_buttonPressDelay = ButtonPressDelay;
                Input.ActionButtonA.Used = true;
                Input.Jump.Used = true;
                PressCurrentItem();
            }

            return;
        }

        m_buttonPressDelay = Mathf.Max(0f, m_buttonPressDelay - Time.deltaTime);
        if (Input.Cancel.OnPressed && !Input.Cancel.Used) {
            Input.Cancel.Used = true;
            Input.SoulFlame.Used = true;
            OnBackPressed();
        }
    }

    public void OnDrawGizmosSelected() {
        if (ItemDirection == Direction.NavigationCage) {
            Gizmos.color = Color.yellow;
            foreach (var navigationData in Navigation) {
                if (navigationData.From && navigationData.To) {
                    Gizmos.DrawLine(navigationData.From.transform.position, navigationData.To.transform.position);
                }
            }

            Gizmos.color = Color.white;
        }
    }

    public void HandleNavigationCage() {
        if (Input.Axis.magnitude > 0.5f) {
            if (m_nextPressDelay == 0f) {
                if (ChangeMenuItem()) {
                    m_nextPressDelay = 0.4f;
                    return;
                }

                m_nextPressDelay = 0f;
            } else if (m_nextPressDelay > 0f) {
                m_nextPressDelay -= Time.deltaTime;
                if (m_nextPressDelay < 0f) {
                    m_nextPressDelay = 0f;
                }
            }
        } else {
            m_nextPressDelay = 0f;
        }
    }

    public bool ChangeMenuItem() {
        var normalized = Input.Axis.normalized;
        if (!CurrentMenuItem) {
            return false;
        }

        Vector2 b = CurrentMenuItem.Transform.position;
        var cleverMenuItem = CurrentMenuItem;
        var num = Mathf.Cos(AngleTolerance * 0.0174532924f);
        foreach (var navigationData in Navigation) {
            if ((navigationData.Condition == null || navigationData.Condition(navigationData)) && navigationData.From == CurrentMenuItem && navigationData.To.IsVisible) {
                Vector2 a = navigationData.To.Transform.position;
                if (m_isPauseScreen) {
                    if (cleverMenuItem == MenuItems[0] && navigationData.To == MenuItems[9]) {
                        a = new Vector2(0f, 2f);
                    } else if (cleverMenuItem == MenuItems[9] && navigationData.To == MenuItems[0]) {
                        a = new Vector2(0f, -2f);
                    }
                }

                var normalized2 = (a - b).normalized;
                var num2 = Vector2.Dot(normalized, normalized2);
                if (num2 > num) {
                    num = num2;
                    cleverMenuItem = navigationData.To;
                }
            }
        }

        if (cleverMenuItem != CurrentMenuItem) {
            SetCurrentMenuItem(cleverMenuItem);
            return true;
        }

        return false;
    }

    public void PressCurrentItem() {
        OptionPressedCallback();
        if (CurrentMenuItem) {
            CurrentMenuItem.OnPressed();
        }
    }

    public void OnBackPressed() {
        OnBackPressedCallback();
        if (BackItem) {
            BackItem.OnPressed();
        }

        if (BackAction) {
            BackAction.Perform(null);
        }
    }

    public CleverMenuItem CleverMenuItemUnderCursor {
        get {
            var cursorPositionUI = Input.CursorPositionUI;
            var num = float.PositiveInfinity;
            CleverMenuItem result = null;
            foreach (var cleverMenuItem in MenuItems) {
                if (cleverMenuItem.IsVisible && cleverMenuItem.Bounds.Contains(cursorPositionUI)) {
                    var num2 = Vector3.Distance(cleverMenuItem.Bounds.center, cursorPositionUI);
                    if (num > num2) {
                        num = num2;
                        result = cleverMenuItem;
                    }
                }
            }

            return result;
        }
    }

    [ContextMenu("Create navigation from cage")]
    public void CreateNavigationStructureFromCageTool() {
        var list = FindObjectsOfType(typeof(CleverMenuItem)).Cast<CleverMenuItem>().ToList();
        var dictionary = new Dictionary<CageStructureTool.Vertex, CleverMenuItem>();
        foreach (var vertex in CopyFromCage.Vertices) {
            var a = CopyFromCage.transform.TransformPoint(vertex.Position);
            var num = float.MaxValue;
            CleverMenuItem value = null;
            foreach (var cleverMenuItem in list) {
                var num2 = Vector3.Distance(a, cleverMenuItem.transform.position);
                if (num2 < num) {
                    value = cleverMenuItem;
                    num = num2;
                }
            }

            dictionary[vertex] = value;
        }

        Navigation.Clear();
        foreach (var edge in CopyFromCage.Edges) {
            var key = CopyFromCage.VertexByIndex(edge.VertexA);
            var key2 = CopyFromCage.VertexByIndex(edge.VertexB);
            Navigation.Add(
                new NavigationData {
                    From = dictionary[key],
                    To = dictionary[key2]
                }
            );
            Navigation.Add(
                new NavigationData {
                    From = dictionary[key2],
                    To = dictionary[key]
                }
            );
        }

        MenuItems.Clear();
        foreach (var navigationData in Navigation) {
            if (!MenuItems.Contains(navigationData.From)) {
                MenuItems.Add(navigationData.From);
            }
        }
    }

    public bool IsSuspended { get; set; }

    public void AddMenuItem(string label, Action onPress) {
        AddMenuItem(label, MenuItems.Count - 1, onPress);
    }

    public void AddMenuItem(string label, int index, Action onPress) {
        var component = gameObject.GetComponent<CleverMenuItemLayout>();
        if (component != null) {
            AddMenuItem(label, index, component, onPress);
        }
    }

    public void AddMenuItem(string label, int index, CleverMenuItemLayout layout, Action onPress) {
        var cleverMenuItem = Instantiate(MenuItems[0]);
        cleverMenuItem.gameObject.name = label;
        cleverMenuItem.transform.SetParent(MenuItems[1].transform.parent);
        TransparencyAnimator.Register(cleverMenuItem.transform);
        cleverMenuItem.PressedCallback += onPress;
        cleverMenuItem.gameObject.GetComponentInChildren<MessageBox>().SetMessage(new MessageDescriptor(label));
        cleverMenuItem.ApplyColors();
        MenuItems.Insert(index, cleverMenuItem);
        layout.AddItem(cleverMenuItem, index);
    }

    public const float HOLD_DELAY = 0.4f;

    public const float HOLD_FAST_DELAY = 0.04f;

    public List<NavigationData> Navigation = new List<NavigationData>();

    public CageStructureTool CopyFromCage;

    public List<CleverMenuItem> MenuItems;

    public Direction ItemDirection;

    public ActionMethod OptionChangeAction;

    public Action OptionChangeCallback = delegate { };

    public Action OptionPressedCallback = delegate { };

    public Action OnBackPressedCallback = delegate { };

    public bool HighlightOnMouseOver = true;

    public bool UnhighlightOnMouseLeave;

    public TransparencyAnimator FadeAnimator;

    public int Index;

    private int m_defaultIndex;

    public CleverMenuItem BackItem;

    public ActionMethod BackAction;

    public float ButtonPressDelay = 0.2f;

    public float AngleTolerance = 60f;

    private bool m_isVisible = true;

    private bool m_isActive = true;

    private float m_buttonPressDelay;

    private float m_nextPressDelay;

    private float m_holdDelayDuration;

    private float m_holdRemainingTime;

    private bool m_isHighlightVisible = true;

    private bool m_delayNavigation;

    private bool m_isPauseScreen;

    [Serializable]
    public class NavigationData {
        public CleverMenuItem From;

        public CleverMenuItem To;

        public Func<NavigationData, bool> Condition;
    }

    public enum FocusState {
        None,
        InFocus,
        ChildInFocus
    }

    public enum Direction {
        LeftToRight,
        TopToBottom,
        NavigationCage
    }
}
