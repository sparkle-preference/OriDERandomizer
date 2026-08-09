using System.Collections.Generic;
using UnityEngine;

public class CleverMenuItemLayout : MonoBehaviour {
    public void OnEnable() {
        Sort();
    }

    [ContextMenu("Apply")]
    public void Sort() {
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

    public enum Alignment {
        Top,
        Center,
        Bottom
    }
}
