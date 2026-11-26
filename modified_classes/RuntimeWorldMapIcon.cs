using System.Linq;
using Game;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RuntimeWorldMapIcon {
    public RuntimeWorldMapIcon(GameWorldArea.WorldMapIcon icon, RuntimeGameWorldArea area) {
        Icon = icon.Icon;
        Guid = icon.Guid;
        Position = icon.Position;
        Area = area;
        IsSecret = icon.IsSecret;
    }

    public bool IsVisible(AreaMapUI areaMap) {
        if (RandomizerMapLayers.Hides(Icon, Position, Guid)) {
            return false;
        }

        if (RandomizerIconType == RandomizerWorldMapIconType.EntranceDoor) {
            return RandomizerEntrances.IconShown(Guid);
        }

        // Sein.
        if (Guid == SeinIcon) {
            RandomizerLocationManager.Location loc;
            RandomizerLocationManager.LocationsByGuid.TryGetValue(FronkeyFight, out loc);
            return !(Characters.Sein.PlayerAbilities.SpiritFlame.HasAbility && loc != null && loc.Collected);
        }

        // show randomizer pickup icons only if they're reachable and not yet collected
        if (RandomizerSettings.CurrentFilter == RandomizerSettings.MapFilterMode.InLogic && RandomizerLocationManager.LocationsByWorldMapGuid.ContainsKey(Guid)) {
            var loc = RandomizerLocationManager.LocationsByWorldMapGuid[Guid];
            return loc.Reachable && !loc.Collected && !RandomizerMapWarp.Hidden(loc);
        }

        // There are two Ginso Trees, with apparently different Guids. This is the second one that doesn't get automatically turned off.
        if (Guid == SecondGinsoTree) {
            return false;
        }

        // This will remove already collected ones from the map.
        if (RandomizerLocationManager.LocationsByWorldMapGuid.ContainsKey(Guid)) {
            var loc = RandomizerLocationManager.LocationsByWorldMapGuid[Guid];
            return !loc.Collected && !RandomizerMapWarp.Hidden(loc);
        }

        return true;
    }

    public void Show() {
        var instance = AreaMapUI.Instance;
        if (Icon == WorldMapIconType.Invisible) {
            return;
        }

        if (!IsVisible(instance)) {
            return;
        }

        if (m_iconGameObject) {
            m_iconGameObject.SetActive(true);
        } else if (RandomizerIconType != RandomizerWorldMapIconType.None) {
            InitRandomizerIcon();
        } else {
            InitStandardIcon(Icon);
        }

        RandomizerMapLayers.StyleIcon(m_iconGameObject, Position, Icon, RandomizerIconType, Guid);
    }

    private void InitStandardIcon(WorldMapIconType iconType) {
        var icon = AreaMapUI.Instance.IconManager.GetIcon(iconType);
        m_iconGameObject = (GameObject)InstantiateUtility.Instantiate(icon);
        var transform = m_iconGameObject.transform;
        transform.parent = AreaMapUI.Instance.Navigation.MapPivot.transform;
        transform.localPosition = Position;
        transform.localRotation = Quaternion.identity;
        transform.localScale = icon.transform.localScale;
        TransparencyAnimator.Register(transform);
    }

    private void InitRandomizerIcon() {
        switch (RandomizerIconType) {
            case RandomizerWorldMapIconType.WaterVein:
                CreateIconFromInventory("ginsoKeyIcon/ginsoKeyGraphic", 4);
                break;
            case RandomizerWorldMapIconType.CleanWater:
                CreateIconFromInventory("waterPurifiedIcon/waterPurifiedGraphics", 20);
                var offset = m_iconGameObject.transform.Find("waterPurifiedGraphic").localPosition;
                foreach (var child in m_iconGameObject.transform) {
                    ((Transform)child).localPosition -= offset;
                }

                break;
            case RandomizerWorldMapIconType.WindRestored:
                CreateIconFromInventory("windRestoredIcon/windRestoredIcon", 10);
                break;
            case RandomizerWorldMapIconType.Sunstone:
                CreateIconFromInventory("mountHoru/sunStoneA", 8);
                break;
            case RandomizerWorldMapIconType.HoruRoom:
                CreateIconFromInventory("warmthReturned/warmthReturnedGraphics", 10);
                break;
            case RandomizerWorldMapIconType.Plant:
                InitStandardIcon(WorldMapIconType.HealthUpgrade);
                m_iconGameObject.name = "plantMapIcon(Clone)";

                m_iconGameObject.transform.localScale *= 1.1f;

                var icon = m_iconGameObject.GetComponent<Renderer>();
                UberShaderAPI.SetMainTexture(icon, PlantTexture, true);
                UberShaderAPI.SetColor(icon, Color.white * 0.5f, true);

                var glow = m_iconGameObject.transform.Find("qq").GetComponent<Renderer>();
                UberShaderAPI.SetColor(glow, new Color(0.29f, 0.51f, 0.89f) * 0.5f, true);
                break;
            case RandomizerWorldMapIconType.SkillTree:
                InitStandardIcon(WorldMapIconType.AbilityPedestal);
                break;
            case RandomizerWorldMapIconType.GumonSeal:
                CreateIconFromInventory("forlornRuins/forlornKeyGraphic", 8f);
                break;
            case RandomizerWorldMapIconType.Keystone:
                InitStandardIcon(WorldMapIconType.Keystone);
                break;
            case RandomizerWorldMapIconType.Experience:
                InitStandardIcon(WorldMapIconType.Experience);
                break;
            case RandomizerWorldMapIconType.EntranceDoor:
                InitEntranceDoorIcon();
                break;
        }
    }

    // the pickup template's square mesh wearing the map altar's static material: the pickup shader
    // pulses and turns its icon, and the texture carries its own glow, so the glow children go too
    private void InitEntranceDoorIcon() {
        InitStandardIcon(WorldMapIconType.HealthUpgrade);
        m_iconGameObject.name = "entranceDoorMapIcon(Clone)";
        // about 0.75 of a pickup icon's height
        m_iconGameObject.transform.localScale *= 1.215f;
        // the icon's bottom edge sits on the door's point
        var height = m_iconGameObject.GetComponent<MeshFilter>().sharedMesh.bounds.size.y * m_iconGameObject.transform.localScale.y;
        m_iconGameObject.transform.localPosition += new Vector3(0f, height / 2f, 0f);
        foreach (Transform child in m_iconGameObject.transform) {
            child.gameObject.SetActive(false);
        }

        var icon = m_iconGameObject.GetComponent<Renderer>();
        var altar = AreaMapUI.Instance.IconManager.GetIcon(WorldMapIconType.Mapstone).GetComponent<Renderer>();
        icon.sharedMaterial = new Material(altar.sharedMaterial);
        UberShaderAPI.SetMainTexture(icon, EntranceDoorTexture, true);
        UberShaderAPI.SetColor(icon, Color.white * 0.5f, true);
    }

    private void CreateIconFromInventory(string name, float scale) {
        if (!inventoryTemplate) {
            // The visible inventory on the pause screen has transparency animations affecting the cloned icons
            // So clone from the permanently disabled inventory that the visible one is cloned from
            inventoryTemplate = SceneManager.GetSceneByName("loadBootstrap").GetRootGameObjects().First(go => go.name == "inventoryScreen").transform;
        }

        var obj = inventoryTemplate.transform.Find("progression").Find(name);
        var clone = GameObject.Instantiate(obj).gameObject;
        clone.SetActive(true);
        clone.transform.SetParent(AreaMapUI.Instance.Navigation.MapPivot.transform);
        clone.transform.localScale = new Vector3(scale, scale, 1);
        clone.transform.localPosition = Position;
        TransparencyAnimator.Register(clone.transform);
        m_iconGameObject = clone;
    }

    public void Hide() {
        if (m_iconGameObject) {
            m_iconGameObject.SetActive(false);
        }
    }

    public void SetIcon(WorldMapIconType icon) {
        if (m_iconGameObject) {
            InstantiateUtility.Destroy(m_iconGameObject);
        }

        Icon = icon;
    }

    public MoonGuid Guid;

    public WorldMapIconType Icon;

    public Vector2 Position;

    private RuntimeGameWorldArea Area;

    public bool IsSecret;

    private GameObject m_iconGameObject;

    public RandomizerWorldMapIconType RandomizerIconType;

    private static Transform inventoryTemplate;

    private static readonly MoonGuid SeinIcon = new MoonGuid(-550456551, 1312223365, -251340902, -293109681);

    private static readonly MoonGuid FronkeyFight = new MoonGuid(686741138, 1236491904, -1735338082, 532353037);

    private static readonly MoonGuid SecondGinsoTree = new MoonGuid(-1906535857, 1336220761, 1768076162, -2078859709);

    private static Texture2D PlantTexture => field ??= RandomizerResources.LoadTexture("plant.png");

    private static Texture2D EntranceDoorTexture => field ??= RandomizerResources.LoadTexture("entrance_door.png");
}
