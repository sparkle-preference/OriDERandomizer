using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;

namespace OriBFRandomizer.patches
{
    public static class GameMapTeleporterExt
    {
        public static GameMapTeleporter GameMapTeleporter(string name, float x, float y)
        {
            return GameMapTeleporter(name, new Vector3(x, y, 0f), false);
        }

        public static GameMapTeleporter GameMapTeleporter(string name, Vector3 position, bool activated)
        {
            var tp = FormatterServices.GetUninitializedObject(typeof(GameMapTeleporter)) as GameMapTeleporter;

            tp.Identifier = name;
            tp.WorldPosition = position;
            tp.Activated = activated;
            RandomizerMessageProvider randomizerMessageProvider =
                (RandomizerMessageProvider) ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
            randomizerMessageProvider.SetMessage(name);
            tp.Name = randomizerMessageProvider;
            return tp;
        }

        public static void SetInfo(this GameMapTeleporter _this, string name, Vector3 position, bool activated)
        {
            if (_this.Identifier != name)
            {
                _this.Identifier = name;
                RandomizerMessageProvider randomizerMessageProvider =
                    (RandomizerMessageProvider) ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
                randomizerMessageProvider.SetMessage(name);
                _this.Name = randomizerMessageProvider;
            }

            _this.WorldPosition = position;
            _this.Activated = activated;
        }
    }
    
    [HarmonyPatch(typeof(GameMapTeleporter))]
    public static class GameMapTeleporterPatches{

        [HarmonyPatch("Show")]
        [HarmonyPostfix]
        public static void FancyRandoTps(GameMapTeleporter __instance, GameObject ___m_worldMapIconGameObject)
        {
            if (__instance.Name.GetType() == typeof(RandomizerMessageProvider))
            {
                Renderer[] componentsInChildren = ___m_worldMapIconGameObject.GetComponentsInChildren<Renderer>();
                int[] array = new int[]
                {
                    0,
                    10,
                    11,
                    12
                };
                int[] array2 = new int[]
                {
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    7,
                    8,
                    9
                };
                foreach (int num in array)
                {
                    Color color = componentsInChildren[num].material.color;
                    Color color2 = new Color(RandomizerSettings.Customization.WarpTeleporterColor.Value.r * color.r, RandomizerSettings.Customization.WarpTeleporterColor.Value.g * color.g, RandomizerSettings.Customization.WarpTeleporterColor.Value.b * color.b, color.a);
                    componentsInChildren[num].material.color = color2;
                }
                foreach (int num2 in array2)
                {
                    Color color3 = componentsInChildren[num2].material.color;
                    Color color4 = new Color(RandomizerSettings.Customization.WarpTeleporterColor.Value.r, RandomizerSettings.Customization.WarpTeleporterColor.Value.g, RandomizerSettings.Customization.WarpTeleporterColor.Value.b, color3.a);
                    componentsInChildren[num2].material.color = color4;
                }
            }
        }
    }
}