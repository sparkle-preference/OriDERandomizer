using System;
using System.Collections.Generic;
using Core;

public static class CleverMenuItemGroupExt {
	public static void AddItem(this CleverMenuItemGroup group, CleverMenuItem item, CleverMenuItemGroupBase itemGroup)
	{
		CleverMenuItemGroup.CleverMenuItemGroupItem cleverMenuItemGroupItem = new CleverMenuItemGroup.CleverMenuItemGroupItem
		{
			ItemGroup = itemGroup,
			MenuItem = item
		};
		cleverMenuItemGroupItem.ItemGroup.IsActive = false;
		itemGroup.OnBackPressed = (Action)Delegate.Combine(itemGroup.OnBackPressed, new Action(group.OnOptionBackPressed));
		group.Options.Add(cleverMenuItemGroupItem);
	}
}
