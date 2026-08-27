using System.Collections.Generic;
using UnityEngine;

public class CleverMenuItemLayout : MonoBehaviour {
    public void OnEnable() {
        Sort();
    }

    [ContextMenu("Apply")]
    public void Sort() {
        ApplyWindow();
        var num = 0f;
        foreach (var cleverMenuItem in MenuItems) {
            if (cleverMenuItem.IsVisible) {
                cleverMenuItem.transform.localPosition = Vector3.down * num;
                num += cleverMenuItem.Space;
            }
        }

        foreach (var cleverMenuItem2 in MenuItems) {
            if (cleverMenuItem2.IsVisible) {
                if (VerticalAlignment == Alignment.Center) {
                    cleverMenuItem2.transform.localPosition += Vector3.up * num * 0.5f;
                }

                if (VerticalAlignment == Alignment.Bottom) {
                    cleverMenuItem2.transform.localPosition += Vector3.up * num;
                }
            }
        }
    }

    // Rows outside the window are hidden, not moved: this menu has no mask anywhere, so a
    // row pushed past the panel edge still draws over whatever is behind it.
    private void ApplyWindow() {
        if (MaxVisible <= 0 || MenuItems.Count <= MaxVisible) {
            for (var i = 0; i < MenuItems.Count; i++) {
                Show(i, true);
            }

            ScrollTop = 0;
            return;
        }

        if (Selection == null) {
            Selection = GetComponent<CleverMenuItemSelectionManager>();
        }

        // navigation walks hidden rows (MoveSelection tests IsActivated, not IsVisible),
        // so the window follows the selection rather than gating it
        var focus = Selection != null ? Mathf.Max(0, Selection.Index) : ScrollTop;
        ScrollTop = Mathf.Clamp(ScrollTop, focus - MaxVisible + 1, focus);
        ScrollTop = Mathf.Clamp(ScrollTop, 0, MenuItems.Count - MaxVisible);
        var last = ScrollTop + MaxVisible - 1;
        for (var i = 0; i < MenuItems.Count; i++) {
            Show(i, i >= ScrollTop && i < ScrollTop + MaxVisible);
        }

        // a half-lit row at either edge says there is more past it, unless it is the one
        // being pointed at -- the row you are on is always fully lit
        if (EdgeFade <= 0f) {
            return;
        }

        if (ScrollTop > 0 && ScrollTop != focus) {
            MenuItems[ScrollTop].SetOpacity(EdgeFade);
        }

        if (last < MenuItems.Count - 1 && last != focus) {
            MenuItems[last].SetOpacity(EdgeFade);
        }
    }

    private void Show(int index, bool visible) {
        var item = MenuItems[index];
        var hide = item.GetComponent<RandomizerScrollHide>();
        if (hide == null) {
            if (visible) {
                item.SetOpacity(1f);
                return;
            }

            hide = item.gameObject.AddComponent<RandomizerScrollHide>();
            hide.Inner = item.Visible;
            item.Visible = hide;
        }

        hide.Hidden = !visible;
        item.RefreshVisible();
        if (visible) {
            item.SetOpacity(1f);
        }
    }

    public void ScrollBy(int rows) {
        ScrollTo(ScrollTop + rows);
    }

    // The window normally follows the selection, so a mouse-driven scroll carries the
    // selection with it rather than leaving the two disagreeing.
    public void ScrollTo(int top) {
        if (MaxVisible <= 0 || MenuItems.Count <= MaxVisible) {
            return;
        }

        var wanted = Mathf.Clamp(top, 0, MenuItems.Count - MaxVisible);
        if (wanted == ScrollTop) {
            return;
        }

        ScrollTop = wanted;
        if (Selection != null) {
            var inside = Mathf.Clamp(Selection.Index, ScrollTop, ScrollTop + MaxVisible - 1);
            if (inside != Selection.Index) {
                Selection.SetCurrentItem(inside);
                return;
            }
        }

        Sort();
    }

    public void AddItem(CleverMenuItem item) {
        MenuItems.Add(item);
        Sort();
    }

    public void AddItem(CleverMenuItem item, int index) {
        MenuItems.Insert(index, item);
        Sort();
    }

    public List<CleverMenuItem> MenuItems = new List<CleverMenuItem>();

    public Alignment VerticalAlignment;

    // 0 lays out every row, which is what every layout that has not opted in wants
    public int MaxVisible;

    public CleverMenuItemSelectionManager Selection;

    public int ScrollTop;

    // 0 = no fade; otherwise the opacity of a window edge that has more rows past it
    public float EdgeFade;

    public enum Alignment {
        Top,
        Center,
        Bottom
    }
}
