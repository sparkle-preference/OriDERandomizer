using System;
using System.Collections.Generic;
using System.IO;
using Game;
using UnityEngine;

public static class RandomizerColorManager {
    public static void Initialize() {
        hotColdTarget = new Vector3(0f, 0f);
        var found = false;
        if (File.Exists("Color.txt")) {
            var text = File.ReadAllText("Color.txt").ToLower();
            var lines = text.Split('\n');
            if (lines.Length >= 1 && lines[0].Trim().Equals("customrotation")) {
                colors.Clear();
                var red = 0f;
                var green = 0f;
                var blue = 0f;
                var alpha = 0f;
                var i = 1;
                while (i < lines.Length - 1 && !string.IsNullOrEmpty(lines[i]) && lines[i].Length >= 6) {
                    var components = lines[i].Split(',');
                    if (components.Length >= 4) {
                        float.TryParse(components[0], out red);
                        float.TryParse(components[1], out green);
                        float.TryParse(components[2], out blue);
                        float.TryParse(components[3], out alpha);
                        red /= 511f;
                        green /= 511f;
                        blue /= 511f;
                        alpha /= 511f;
                        colors.Add(new Color(red, green, blue, alpha));
                    }

                    components = lines[i + 1].Split(',');
                    if (components.Length >= 5) {
                        float.TryParse(components[0], out var red2);
                        float.TryParse(components[1], out var green2);
                        float.TryParse(components[2], out var blue2);
                        float.TryParse(components[3], out var alpha2);
                        int.TryParse(components[4], out var frames);
                        frames = Math.Min(frames, 36000);
                        red2 /= 511f;
                        green2 /= 511f;
                        blue2 /= 511f;
                        alpha2 /= 511f;
                        for (var j = 1; j <= frames; j++) {
                            colors.Add(new Color(red + (red2 - red) * j / frames, green + (green2 - green) * j / frames, blue + (blue2 - blue) * j / frames, alpha + (alpha2 - alpha) * j / frames));
                        }
                    }

                    i++;
                }

                customColor = false;
                customRotation = true;
                return;
            }

            colors.Clear();
            customRotation = false;
            var components2 = text.Split(',');
            if ((components2.Length == 3 || components2.Length == 4)) {
                float.TryParse(components2[0], out var red3);
                float.TryParse(components2[1], out var green3);
                float.TryParse(components2[2], out var blue3);
                float alpha3;
                if (components2.Length == 4) {
                    float.TryParse(components2[3], out alpha3);
                } else {
                    alpha3 = 255f;
                }

                colors.Add(new Color(red3 / 511f, green3 / 511f, blue3 / 511f, alpha3 / 511f));
                found = true;
                customColor = true;
            }
        }

        if (!found && (customColor || customRotation)) {
            customColor = false;
            customRotation = false;
        }
    }

    public static void UpdateColors() {
        try {
            if (Randomizer.HotCold || Characters.Sein.PlayerAbilities.Sense.HasAbility) {
                var scale = 64f;
                var distance = 100f;
                if (Characters.Ori.InsideMapstone) {
                    var currentMap = 20 + RandomizerBonus.MapStoneProgression() * 4;
                    using (var enumerator = (RandomizerBonus.SenseFragsActive ? Randomizer.HotColdMapsWithFrags : Randomizer.HotColdMaps).GetEnumerator()) {
                        while (enumerator.MoveNext()) {
                            var map = enumerator.Current;
                            if (map > currentMap) {
                                distance = (map - currentMap - 4) * 2f;
                                break;
                            }
                        }
                    }

                    if (distance < scale && RandomizerBonus.SenseFragsEnabled && !RandomizerBonus.SenseFragsActive) {
                        RandomizerBonus.SenseFragsActive = true;
                    }
                } else {
                    distance = Vector3.Distance(hotColdTarget, Characters.Sein.Position);
                }

                if (distance < scale) {
                    if (colorBeforeSense.Count == 0) {
                        colorBeforeSense.Add(Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color);
                    }

                    if (!(customRotation && RandomizerSettings.Customization.DiscoSense)) {
                        Color hotColor = RandomizerSettings.Customization.HotColor;
                        Color coldColor = RandomizerSettings.Customization.ColdColor;
                        var scaleFactor = distance / scale;
                        Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = new Color(Mathf.Lerp(hotColor.r, coldColor.r, scaleFactor), Mathf.Lerp(hotColor.g, coldColor.g, scaleFactor), Mathf.Lerp(hotColor.b, coldColor.b, scaleFactor), Mathf.Lerp(hotColor.a, coldColor.a, scaleFactor));
                    } else {
                        colorIndex += (int)(20f * (1f - distance / scale));
                        Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = colors[colorIndex];
                    }

                    return;
                }

                if (!(customRotation || customColor) && colorBeforeSense.Count > 0) {
                    Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = colorBeforeSense[0];
                    colorBeforeSense.Clear();
                }
            }

            if (customRotation) {
                colorIndex = (colorIndex + 1) % colors.Count;
                Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = colors[colorIndex];
                return;
            }

            if (customColor) {
                Characters.Sein.PlatformBehaviour.Visuals.SpriteRenderer.material.color = colors[0];
            }
        } catch (Exception e) {
            Randomizer.LogError("ColorTick: " + colorIndex + " out of " + colors.Count + ": " + e.Message);
        }
    }

    public static void UpdateHotColdTarget() {
        var minimum = float.MaxValue;
        hotColdTarget = new Vector3(5000f, 5000f);
        foreach (var target in Randomizer.HotColdItems.Values) {
            if (Characters.Sein.Inventory.GetRandomizerItem(target.Id) == 0) {
                var distance = Vector3.Distance(target.Position, Characters.Sein.Position);
                if (distance < minimum) {
                    minimum = distance;
                    hotColdTarget = target.Position;
                }
            }
        }

        if (RandomizerBonus.SenseFragsActive) {
            foreach (var target in Randomizer.HotColdFrags.Values) {
                if (Characters.Sein.Inventory.GetRandomizerItem(target.Id) == 0) {
                    var distance = Vector3.Distance(target.Position, Characters.Sein.Position);
                    if (distance < minimum) {
                        minimum = distance;
                        hotColdTarget = target.Position;
                    }
                }
            }
        }
    }

    private static bool customColor;

    private static bool customRotation;

    private static List<Color> colors = new List<Color>();

    private static List<Color> colorBeforeSense = new List<Color>(); // this is an Optional actually

    private static int colorIndex;

    private static Vector3 hotColdTarget;
}
