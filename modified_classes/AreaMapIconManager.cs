using System;
using UnityEngine;

public class AreaMapIconManager : MonoBehaviour {
    public void Awake() {
    }

    public void ShowAreaIcons() {
        for (int i = 0; i < GameWorld.Instance.RuntimeAreas.Count; i++) {
            RuntimeGameWorldArea runtimeGameWorldArea = GameWorld.Instance.RuntimeAreas[i];
            foreach (var icon in RandomizerWorldMapIconManager.Icons) {
                if (!runtimeGameWorldArea.Area.InsideFace(icon.Position))
                    continue;

                RuntimeWorldMapIcon runtimeWorldMapIcon = null;
                for (int j = 0; j < runtimeGameWorldArea.Icons.Count; j++) {
                    if (runtimeGameWorldArea.Icons[j].Guid == icon.Guid) {
                        runtimeWorldMapIcon = runtimeGameWorldArea.Icons[j];
                        break;
                    }
                }

                bool collected = RandomizerLocationManager.IsPickupCollected(icon.Guid);
                if (runtimeWorldMapIcon == null && !collected) {
                    GameWorldArea.WorldMapIcon worldMapIcon = new GameWorldArea.WorldMapIcon {
                        Guid = icon.Guid,
                        Icon = WorldMapIconType.HealthUpgrade,
                        IsSecret = false,
                        Position = icon.Position
                    };
                    runtimeGameWorldArea.Icons.Add(
                        new RuntimeWorldMapIcon(worldMapIcon, runtimeGameWorldArea) {
                            RandomizerIconType = icon.Type
                        }
                    );
                } else if (runtimeWorldMapIcon != null) {
                    runtimeWorldMapIcon.Icon = collected ? WorldMapIconType.Invisible : WorldMapIconType.HealthUpgrade;
                }
            }

            for (int k = 0; k < runtimeGameWorldArea.Icons.Count; k++) {
                runtimeGameWorldArea.Icons[k].Hide();
            }

            for (int l = 0; l < runtimeGameWorldArea.Icons.Count; l++) {
                RuntimeWorldMapIcon runtimeWorldMapIcon2 = runtimeGameWorldArea.Icons[l];
                if (!GameMapUI.Instance.ShowingTeleporters || runtimeWorldMapIcon2.Icon != WorldMapIconType.SavePedestal) {
                    runtimeWorldMapIcon2.Show();
                }
            }
        }
    }

    public GameObject GetIcon(WorldMapIconType iconType) {
        switch (iconType) {
            case WorldMapIconType.Keystone:
                return Icons.Keystone;
            case WorldMapIconType.Mapstone:
                return Icons.Mapstone;
            case WorldMapIconType.BreakableWall:
                return Icons.BreakableWall;
            case WorldMapIconType.BreakableWallBroken:
                return Icons.BreakableWallBroken;
            case WorldMapIconType.StompableFloor:
                return Icons.StompableFloor;
            case WorldMapIconType.StompableFloorBroken:
                return Icons.StompableFloorBroken;
            case WorldMapIconType.EnergyGateTwo:
                return Icons.EnergyGateTwo;
            case WorldMapIconType.EnergyGateOpen:
                return Icons.EnergyGateOpen;
            case WorldMapIconType.KeystoneDoorFour:
                return Icons.KeystoneDoorFour;
            case WorldMapIconType.KeystoneDoorOpen:
                return Icons.KeystoneDoorOpen;
            case WorldMapIconType.AbilityPedestal:
                return Icons.AbilityPedestal;
            case WorldMapIconType.HealthUpgrade:
                return Icons.HealthUpgrade;
            case WorldMapIconType.EnergyUpgrade:
                return Icons.EnergyUpgrade;
            case WorldMapIconType.SavePedestal:
                return Icons.SavePedestal;
            case WorldMapIconType.AbilityPoint:
                return Icons.AbilityPoint;
            case WorldMapIconType.KeystoneDoorTwo:
                return Icons.KeystoneDoorTwo;
            case WorldMapIconType.Experience:
                return Icons.Experience;
            case WorldMapIconType.MapstonePickup:
                return Icons.MapstonePickup;
            case WorldMapIconType.EnergyGateTwelve:
                return Icons.EnergyGateTwelve;
            case WorldMapIconType.EnergyGateTen:
                return Icons.EnergyGateTen;
            case WorldMapIconType.EnergyGateEight:
                return Icons.EnergyGateEight;
            case WorldMapIconType.EnergyGateSix:
                return Icons.EnergyGateSix;
            case WorldMapIconType.EnergyGateFour:
                return Icons.EnergyGateFour;
        }

        return null;
    }

    public IconGameObjects Icons;

    [Serializable]
    public class IconGameObjects {
        public GameObject Keystone;

        public GameObject Mapstone;

        public GameObject BreakableWall;

        public GameObject BreakableWallBroken;

        public GameObject StompableFloor;

        public GameObject StompableFloorBroken;

        public GameObject EnergyGateOpen;

        public GameObject KeystoneDoorTwo;

        public GameObject KeystoneDoorFour;

        public GameObject KeystoneDoorOpen;

        public GameObject AbilityPedestal;

        public GameObject HealthUpgrade;

        public GameObject EnergyUpgrade;

        public GameObject SavePedestal;

        public GameObject AbilityPoint;

        public GameObject Experience;

        public GameObject MapstonePickup;

        public GameObject EnergyGateTwelve;

        public GameObject EnergyGateTen;

        public GameObject EnergyGateEight;

        public GameObject EnergyGateSix;

        public GameObject EnergyGateFour;

        public GameObject EnergyGateTwo;
    }
}
