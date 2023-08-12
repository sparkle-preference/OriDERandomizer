using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
	
	[HarmonyPatch(typeof(AreaMapIconManager))]
	[HarmonyPatch("Increment")]
	public static class AreaMapIconManagerExt
	{

		public static bool ShowAreaIcons()
		{
			for (int i = 0; i < GameWorld.Instance.RuntimeAreas.Count; i++)
			{
				RuntimeGameWorldArea runtimeGameWorldArea = GameWorld.Instance.RuntimeAreas[i];
				foreach (var icon in RandomizerWorldMapIconManager.Icons)
				{
					if (!runtimeGameWorldArea.Area.InsideFace(icon.Position))
						continue;
				
					RuntimeWorldMapIcon runtimeWorldMapIcon = null;
					for (int j = 0; j < runtimeGameWorldArea.Icons.Count; j++)
					{
						if (runtimeGameWorldArea.Icons[j].Guid == icon.Guid)
						{
							runtimeWorldMapIcon = runtimeGameWorldArea.Icons[j];
							break;
						}
					}

					bool collected = RandomizerLocationManager.IsPickupCollected(icon.Guid);
					if (runtimeWorldMapIcon == null && !collected)
					{
						GameWorldArea.WorldMapIcon worldMapIcon = new GameWorldArea.WorldMapIcon( new SceneMetaData.WorldMapIcon(new VisibleOnWorldMap
						{
							MoonGuid = icon.Guid,
							Icon = WorldMapIconType.HealthUpgrade,
							IsSecret = false,
							Offset = icon.Position
						}));
						var foo = new RuntimeWorldMapIcon(worldMapIcon, runtimeGameWorldArea);
						RuntimeWorldMapIconExt.RandomizerWorldMapIconTypeMap[foo] = icon.Type;
						runtimeGameWorldArea.Icons.Add(foo);
					}
					else if (runtimeWorldMapIcon != null)
					{
						runtimeWorldMapIcon.Icon = collected ? WorldMapIconType.Invisible : WorldMapIconType.HealthUpgrade;
					}
				}

				for (int k = 0; k < runtimeGameWorldArea.Icons.Count; k++)
				{
					runtimeGameWorldArea.Icons[k].Hide();
				}
				if (!runtimeGameWorldArea.Area.VisitableCondition || runtimeGameWorldArea.Area.VisitableCondition.Validate(null))
				{
					for (int l = 0; l < runtimeGameWorldArea.Icons.Count; l++)
					{
						RuntimeWorldMapIcon runtimeWorldMapIcon2 = runtimeGameWorldArea.Icons[l];
						if (!GameMapUI.Instance.ShowingTeleporters || runtimeWorldMapIcon2.Icon != WorldMapIconType.SavePedestal)
						{
							runtimeWorldMapIcon2.Show();
						}
					}
				}
			}

			return false;
		}
	}
}
