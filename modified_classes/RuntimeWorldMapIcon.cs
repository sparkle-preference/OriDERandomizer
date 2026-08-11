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
        // Sein.
        if (Guid == new MoonGuid(-550456551, 1312223365, -251340902, -293109681)) {
            var fronkeyFight = new MoonGuid(686741138, 1236491904, -1735338082, 532353037);
            var loc = RandomizerLocationManager.LocationsByGuid[fronkeyFight];
            return !(Characters.Sein.PlayerAbilities.SpiritFlame.HasAbility && loc.Collected);
        }

        // show randomizer pickup icons only if they're reachable and not yet collected
        if (RandomizerSettings.CurrentFilter == RandomizerSettings.MapFilterMode.InLogic && RandomizerLocationManager.LocationsByWorldMapGuid.ContainsKey(Guid)) {
            var loc = RandomizerLocationManager.LocationsByWorldMapGuid[Guid];
            return loc.Reachable && !loc.Collected;
        }

        // There are two Ginso Trees, with apparently different Guids. This is the second one that doesn't get automatically turned off.
        if (Guid == new MoonGuid(-1906535857, 1336220761, 1768076162, -2078859709)) {
            return false;
        }

        // This will remove already collected ones from the map.
        if (RandomizerLocationManager.LocationsByWorldMapGuid.ContainsKey(Guid)) {
            var loc = RandomizerLocationManager.LocationsByWorldMapGuid[Guid];
            return !loc.Collected;
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
            return;
        }

        if (RandomizerIconType != RandomizerWorldMapIconType.None) {
            InitRandomizerIcon();
        } else {
            InitStandardIcon(Icon);
        }
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
                foreach (var child in m_iconGameObject.transform)
                    ((Transform)child).localPosition -= offset;
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
        }
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

    private static Texture2D PlantTexture {
        get {
            if (_plantTexture == null) {
                _plantTexture = new Texture2D(0, 0);
                _plantTexture.LoadImage(RandomizerResources.ReadResource("plant.png"));
            }

            return _plantTexture;
        }
    }

    private static Texture2D _plantTexture;
}
