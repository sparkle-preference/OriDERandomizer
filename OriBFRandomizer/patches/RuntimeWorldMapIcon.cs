using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OriBFRandomizer.patches
{
    public static class RuntimeWorldMapIconExt
    {
        public static Dictionary<RuntimeWorldMapIcon, RandomizerWorldMapIconType> RandomizerWorldMapIconTypeMap =
            new Dictionary<RuntimeWorldMapIcon, RandomizerWorldMapIconType>();

        public static void InitStandardIcon(this RuntimeWorldMapIcon _this, ref GameObject m_iconGameObject,
            WorldMapIconType iconType)
        {
            GameObject icon = AreaMapUI.Instance.IconManager.GetIcon(iconType);
            m_iconGameObject = (GameObject) InstantiateUtility.Instantiate(icon);
            Transform transform = m_iconGameObject.transform;
            transform.parent = AreaMapUI.Instance.Navigation.MapPivot.transform;
            transform.localPosition = _this.Position;
            transform.localRotation = Quaternion.identity;
            transform.localScale = icon.transform.localScale;
            TransparencyAnimator.Register(transform);
        }

        public static void InitRandomizerIcon(this RuntimeWorldMapIcon _this, ref GameObject m_iconGameObject)
        {
            switch (RandomizerWorldMapIconTypeMap[_this])
            {
                case RandomizerWorldMapIconType.Plant:
                {
                    _this.InitStandardIcon(ref m_iconGameObject, WorldMapIconType.HealthUpgrade);
                    m_iconGameObject.name = "plantMapIcon(Clone)";
                    Renderer[] componentsInChildren = m_iconGameObject.GetComponentsInChildren<Renderer>();
                    for (int i = 0; i < componentsInChildren.Length; i++)
                    {
                        componentsInChildren[i].material.color = new Color(0.1792157f, 0.2364706f, 0.8656863f);
                    }

                    m_iconGameObject.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                    return;
                }
                case RandomizerWorldMapIconType.WaterVein:
                    _this.CreateIconFromInventory(ref m_iconGameObject, "ginsoKeyIcon/ginsoKeyGraphic", 4f);
                    return;
                case RandomizerWorldMapIconType.Sunstone:
                    _this.CreateIconFromInventory(ref m_iconGameObject, "mountHoru/sunStoneA", 8f);
                    return;
                case RandomizerWorldMapIconType.CleanWater:
                {
                    _this.CreateIconFromInventory(ref m_iconGameObject, "waterPurifiedIcon/waterPurifiedGraphics", 20f);
                    Vector3 localPosition = m_iconGameObject.transform.Find("waterPurifiedGraphic").localPosition;
                    var enumerator = m_iconGameObject.transform.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        object obj = enumerator.Current;
                        ((Transform) obj).localPosition -= localPosition;
                    }

                    return;
                    break;
                }
                case RandomizerWorldMapIconType.WindRestored:
                    break;
                case RandomizerWorldMapIconType.HoruRoom:
                    _this.CreateIconFromInventory(ref m_iconGameObject, "warmthReturned/warmthReturnedGraphics", 10f);
                    return;
                case RandomizerWorldMapIconType.SkillTree:
                    _this.InitStandardIcon(ref m_iconGameObject, WorldMapIconType.AbilityPedestal);
                    return;
                default:
                    return;
            }

            _this.CreateIconFromInventory(ref m_iconGameObject, "windRestoredIcon/windRestoredIcon", 10f);
        }

        private static Transform inventoryTemplate;

        public static void CreateIconFromInventory(this RuntimeWorldMapIcon _this, ref GameObject m_iconGameObject,
            string name, float scale)
        {
            if (!inventoryTemplate)
            {
                inventoryTemplate = SceneManager.GetSceneByName("loadBootstrap").GetRootGameObjects()
                    .First((GameObject go) => go.name == "inventoryScreen").transform;
            }

            GameObject gameObject = Object
                .Instantiate<Transform>(inventoryTemplate.transform.Find("progression").Find(name)).gameObject;
            gameObject.SetActive(true);
            gameObject.transform.SetParent(AreaMapUI.Instance.Navigation.MapPivot.transform);
            gameObject.transform.localScale = new Vector3(scale, scale, 1f);
            gameObject.transform.localPosition = _this.Position;
            TransparencyAnimator.Register(gameObject.transform);
            m_iconGameObject = gameObject;
        }
    }

    [HarmonyPatch(typeof(RuntimeWorldMapIcon))]
    public static class RuntimeWorldMapIconPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch("IsVisible")]
        public static bool MapFilterPatch(RuntimeWorldMapIcon __instance, ref bool __result)
        {
            if (RandomizerSettings.CurrentFilter == RandomizerSettings.MapFilterMode.InLogic &&
                RandomizerLocationManager.LocationsByWorldMapGuid.ContainsKey(__instance.Guid))
            {
                RandomizerLocationManager.Location location =
                    RandomizerLocationManager.LocationsByWorldMapGuid[__instance.Guid];
                __result = location.Reachable && !location.Collected;
            }
            else
                __result = true;

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Show")]
        public static bool InitIconsPatch(RuntimeWorldMapIcon __instance, ref GameObject ___m_iconGameObject)
        {
            AreaMapUI instance = AreaMapUI.Instance;
            if (__instance.Icon == WorldMapIconType.Invisible)
            {
            }
            else if (!__instance.IsVisible(instance))
            {
            }
            else if (___m_iconGameObject)
            {
                ___m_iconGameObject.SetActive(true);
            }
            else if (RuntimeWorldMapIconExt.RandomizerWorldMapIconTypeMap.TryGetValue(__instance, out var value) &&
                     value != RandomizerWorldMapIconType.None)
            {
                __instance.InitRandomizerIcon(ref ___m_iconGameObject);
            }
            else
                __instance.InitStandardIcon(ref ___m_iconGameObject, __instance.Icon);

            return false;
        }
    }
}