using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Input = Core.Input;

public class CleverMenuItemSelectionManager : MonoBehaviour, ISuspendable {
    public void SetVisible(bool visible) {
        if (visible) {
            gameObject.SetActive(true);
            isVisible = true;
            if (FadeAnimator) {
                FadeAnimator.Initialize();
                FadeAnimator.AnimatorDriver.ContinueForward();
            }
        } else {
            isVisible = false;
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
            isVisible = true;
            if (FadeAnimator) {
                FadeAnimator.Initialize();
                FadeAnimator.AnimatorDriver.GoToEnd();
                FadeAnimator.AnimatorDriver.Pause();
            }
        } else {
            isVisible = false;
            if (FadeAnimator) {
                FadeAnimator.Initialize();
                FadeAnimator.AnimatorDriver.GoToStart();
                FadeAnimator.AnimatorDriver.Pause();
            }

            gameObject.SetActive(false);
        }
    }

    public bool IsVisible => isVisible;

    public bool IsHighlightVisible {
        get => isHighlightVisible;
        set {
            isHighlightVisible = value;
            if (isHighlightVisible) {
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
        isVisible = true;
        if (FadeAnimator) {
            FadeAnimator.Initialize();
            FadeAnimator.AnimatorDriver.ContinueForward();
        }

        RefreshVisible();
    }

    public void OnDisable() {
        isVisible = false;
    }

    public bool IsActive {
        get => isActive;
        set => isActive = value;
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
        holdRemainingTime = 0.4f;
        delayNavigation = Input.MenuDown.IsPressed || Input.MenuUp.IsPressed;
        if (IsHighlightVisible && CurrentMenuItem) {
            CurrentMenuItem.OnHighlight();
        }

        if (name == "inventoryScreen") {
            isPauseScreen = true;
            var cleverMenuItem = MenuItems[0];
            var cleverMenuItem2 = MenuItems[9];
            Navigation.Add(
                new NavigationData {
                    From = cleverMenuItem,
                    To = cleverMenuItem2,
                }
            );
            Navigation.Add(
                new NavigationData {
                    From = cleverMenuItem2,
                    To = cleverMenuItem,
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
                    holdRemainingTime = 0.4f;
                }

                if (Input.MenuRight.OnPressed) {
                    MoveSelection(true);
                    holdRemainingTime = 0.4f;
                }

                if (Input.MenuLeft.Pressed || Input.MenuRight.Pressed) {
                    holdRemainingTime -= Time.deltaTime;
                    if (holdRemainingTime < 0f) {
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
                if (delayNavigation) {
                    if (Input.MenuDown.IsPressed || Input.MenuUp.IsPressed) {
                        break;
                    }

                    delayNavigation = false;
                }

                if (Input.MenuUp.OnPressed) {
                    MoveSelection(false);
                    holdRemainingTime = 0.4f;
                }

                if (Input.MenuDown.OnPressed) {
                    MoveSelection(true);
                    holdRemainingTime = 0.4f;
                }

                if (Input.MenuUp.Pressed || Input.MenuDown.Pressed) {
                    holdRemainingTime -= Time.deltaTime;
                    if (holdRemainingTime < 0f) {
                        if (Input.MenuUp.Pressed) {
                            holdRemainingTime = 0.04f;
                            MoveSelection(false);
                        }

                        if (Input.MenuDown.Pressed) {
                            holdRemainingTime = 0.04f;
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
            if (buttonPressDelay <= 0f) {
                buttonPressDelay = ButtonPressDelay;
                Input.ActionButtonA.Used = true;
                Input.Jump.Used = true;
                PressCurrentItem();
            }

            return;
        }

        buttonPressDelay = Mathf.Max(0f, buttonPressDelay - Time.deltaTime);
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
            if (nextPressDelay == 0f) {
                if (ChangeMenuItem()) {
                    nextPressDelay = 0.4f;
                    return;
                }

                nextPressDelay = 0f;
            } else if (nextPressDelay > 0f) {
                nextPressDelay -= Time.deltaTime;
                if (nextPressDelay < 0f) {
                    nextPressDelay = 0f;
                }
            }
        } else {
            nextPressDelay = 0f;
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
                if (isPauseScreen) {
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
                    To = dictionary[key2],
                }
            );
            Navigation.Add(
                new NavigationData {
                    From = dictionary[key2],
                    To = dictionary[key],
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

    public CleverMenuItem BackItem;

    public ActionMethod BackAction;

    public float ButtonPressDelay = 0.2f;

    public float AngleTolerance = 60f;

    private bool isVisible = true;

    private bool isActive = true;

    private float buttonPressDelay;

    private float nextPressDelay;

    private float holdRemainingTime;

    private bool isHighlightVisible = true;

    private bool delayNavigation;

    private bool isPauseScreen;

    [Serializable]
    public class NavigationData {
        public CleverMenuItem From;

        public CleverMenuItem To;

        public Func<NavigationData, bool> Condition;
    }

    public enum Direction {
        LeftToRight,
        TopToBottom,
        NavigationCage,
    }
}
