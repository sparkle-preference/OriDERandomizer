using System;
using System.Collections.Generic;
using UnityEngine;

public static class CleverMenuItemLayoutExt
{
	public static void AddItem(this CleverMenuItemLayout _this, CleverMenuItem item)
	{
		_this.MenuItems.Add(item);
		_this.Sort();
	}

	public static void AddItem(this CleverMenuItemLayout _this, CleverMenuItem item, int index)
	{
		_this.MenuItems.Insert(index, item);
		_this.Sort();
	}
}
