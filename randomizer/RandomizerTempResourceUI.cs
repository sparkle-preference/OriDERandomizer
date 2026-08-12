using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

// Temporary (over-max) health and energy get their own smaller row directly
// above each HUD bar: every vanilla fill layer is re-capped at the permanent
// max, and each row is a clone of the bar's full layer stack (fill + min +
// glows) driven by only the overflow. Builds lazily from Randomizer.Update;
// the Temp Row settings apply on file save + reload.
public static class RandomizerTempResourceUI {
    private static FloatProviderAnimatorDriver healthFill;

    private static FloatProviderAnimatorDriver energyFill;

    private static GameObject healthRow;

    private static GameObject energyRow;

    private static float builtScale;

    private static bool errorLogged;

    private static float TempRowScale => RandomizerSettings.Customization.TempRowScale;

    public static void EnsureRows() {
        if (Characters.Sein == null) {
            return;
        }

        try {
            if (RandomizerSettings.Customization.DisableTempResourceRows) {
                // installed caps return vanilla values while disabled (see the
                // provider); untouched HUDs stay untouched. Just drop the rows.
                if (healthRow != null) {
                    UnityEngine.Object.Destroy(healthRow);
                    healthRow = null;
                }

                if (energyRow != null) {
                    UnityEngine.Object.Destroy(energyRow);
                    energyRow = null;
                }

                return;
            }

            if (healthFill == null || energyFill == null) {
                FindFills();
            }

            if (builtScale != TempRowScale && (healthRow != null || energyRow != null)) {
                // scale changed: tear down, rebuild next pass with the new
                // size (Destroy lands at end of frame)
                if (healthRow != null) {
                    UnityEngine.Object.Destroy(healthRow);
                }

                if (energyRow != null) {
                    UnityEngine.Object.Destroy(energyRow);
                }

                healthRow = null;
                energyRow = null;
                return;
            }

            if (healthRow == null && healthFill != null) {
                healthRow = BuildRow(healthFill, false);
            }

            if (energyRow == null && energyFill != null) {
                energyRow = BuildRow(energyFill, true);
            }

            builtScale = TempRowScale;
        } catch (Exception e) {
            if (!errorLogged) {
                errorLogged = true;
                Randomizer.log("TempResourceUI: " + e);
            }
        }
    }

    // find each bar's leading fill strip and cap every fill layer at the
    // permanent max; on later passes (fills already ours) just recover refs
    private static void FindFills() {
        foreach (var driver in UnityEngine.Object.FindObjectsOfType<FloatProviderAnimatorDriver>()) {
            if (driver.Value is SeinHealthVisualMaxProvider) {
                healthFill = driver;
                Cap(driver, false, false, ((SeinHealthVisualMaxProvider)driver.Value).DivideBy);
            } else if (driver.Value is SeinHealthVisualMinProvider) {
                Cap(driver, false, true, ((SeinHealthVisualMinProvider)driver.Value).DivideBy);
            } else if (driver.Value is SeinEnergyMaxVisualProvider) {
                energyFill = driver;
                Cap(driver, true, false, ((SeinEnergyMaxVisualProvider)driver.Value).DivideBy);
            } else if (driver.Value is SeinEnergyMinVisualProvider) {
                Cap(driver, true, true, ((SeinEnergyMinVisualProvider)driver.Value).DivideBy);
            } else if (driver.Value is RandomizerTempResourceProvider) {
                var p = (RandomizerTempResourceProvider)driver.Value;
                if (!p.Overflow && !p.UseMinVisual) {
                    if (p.Energy) {
                        energyFill = driver;
                    } else {
                        healthFill = driver;
                    }
                }
            }
        }
    }

    private static void Cap(FloatProviderAnimatorDriver driver, bool energy, bool useMinVisual, float divideBy) {
        var capped = driver.gameObject.AddComponent<RandomizerTempResourceProvider>();
        capped.Energy = energy;
        capped.UseMinVisual = useMinVisual;
        capped.DivideBy = divideBy;
        driver.Value = capped;
    }

    private static GameObject BuildRow(FloatProviderAnimatorDriver fill, bool energy) {
        var ours = fill.Value as RandomizerTempResourceProvider;
        var divideBy = ours != null ? ours.DivideBy : 1f;

        var clone = (GameObject)UnityEngine.Object.Instantiate(fill.gameObject);
        clone.name = energy ? "randomizerTempEnergyRow" : "randomizerTempHealthRow";

        var t = clone.transform;
        var orig = fill.transform;
        t.parent = orig.parent;
        t.localRotation = orig.localRotation;
        t.localScale = orig.localScale * TempRowScale;
        t.localPosition = orig.localPosition;

        var overflow = clone.AddComponent<RandomizerTempResourceProvider>();
        overflow.Energy = energy;
        overflow.Overflow = true;
        overflow.DivideBy = divideBy;
        clone.GetComponent<FloatProviderAnimatorDriver>().Value = overflow;

        // base cells are drawn by fill + min + glow layers stacked; a lone
        // fill clone reads washed out. Clone every driver-bearing sibling
        // layer except the background slots, all on the overflow value.
        var sources = new List<Renderer> { fill.GetComponent<Renderer>() };
        var targets = new List<Renderer> { clone.GetComponent<Renderer>() };
        for (var i = 0; i < orig.parent.childCount; i++) {
            var c = orig.parent.GetChild(i);
            if (c == orig) {
                continue;
            }

            var d = c.GetComponent<FloatProviderAnimatorDriver>();
            var r = c.GetComponent<Renderer>();
            if (d == null || r == null
                || d.Value is SeinMaxHealthValueProvider || d.Value is SeinMaxEnergyValueProvider) {
                continue;
            }

            var layer = (GameObject)UnityEngine.Object.Instantiate(c.gameObject);
            layer.name = clone.name + "_" + c.name;
            var lt = layer.transform;
            lt.parent = t;
            lt.localRotation = Quaternion.identity;
            lt.localScale = Vector3.one;
            var delta = c.localPosition - orig.localPosition;
            var s = t.localScale;
            lt.localPosition = new Vector3(delta.x / s.x, delta.y / s.y, delta.z / s.z);
            layer.GetComponent<FloatProviderAnimatorDriver>().Value = overflow;
            sources.Add(r);
            targets.Add(layer.GetComponent<Renderer>());
        }

        var anchor = clone.AddComponent<RowAnchor>();
        anchor.Strip = orig;
        anchor.StripRenderer = fill.GetComponent<Renderer>();
        anchor.LayerSources = sources.ToArray();
        anchor.LayerTargets = targets.ToArray();
        anchor.BaseLocal = orig.localPosition;
        var mf = fill.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) {
            // the mesh corners on the cell axis; the anchor aligns the
            // wheelward one through both transforms every frame
            var mb = mf.sharedMesh.bounds;
            anchor.EdgeMin = new Vector3(mb.center.x - mb.extents.x, mb.center.y, mb.center.z);
            anchor.EdgeMax = new Vector3(mb.center.x + mb.extents.x, mb.center.y, mb.center.z);
        }

        return clone;
    }

    // keeps a temp row glued above its bar in screen space. The HUD renders
    // through a manually-driven camera, so screen axes come from the HUD
    // itself: the line between the two strips is screen-right, its cross with
    // the strip's plane normal is screen-up — one shared frame for both rows.
    public class RowAnchor : MonoBehaviour {
        public Transform Strip;

        public Renderer StripRenderer;

        public Renderer[] LayerSources;

        public Renderer[] LayerTargets;

        public Vector3 BaseLocal;

        public Vector3 EdgeMin;

        public Vector3 EdgeMax;

        public void LateUpdate() {
            if (Strip == null || StripRenderer == null) {
                return;
            }

            Vector3 right, up;
            ScreenAxes(Strip, out right, out up);
            var absU = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            var mid = PairMidpoint(Strip);
            var side = Mathf.Sign(Vector3.Dot(Strip.position - mid, right));

            // start on the strip, align the wheelward mesh corner through both
            // transforms (sign-proof against each bar's rotation/mirror), then
            // one shared lift for both rows plus the user's outward offset
            transform.localPosition = BaseLocal;
            var shift = 0f;
            if (EdgeMin != EdgeMax) {
                var sMin = Strip.TransformPoint(EdgeMin);
                var sMax = Strip.TransformPoint(EdgeMax);
                var edge = (sMin - mid).sqrMagnitude <= (sMax - mid).sqrMagnitude ? EdgeMin : EdgeMax;
                shift = Vector3.Dot(right, Strip.TransformPoint(edge) - transform.TransformPoint(edge));
            }

            var height = PairHeight(absU, StripRenderer);
            transform.position += right * (shift + side * height * RandomizerSettings.Customization.TempRowHorizontalOffset)
                + up * (height * RandomizerSettings.Customization.TempRowSpacing);

            // the HUD's fades (TransparencyAnimator) only write alpha to
            // renderers cached at startup, never these clones: a row built
            // mid-fade would keep that alpha forever. Mirror each clone's live
            // tint (scaled by the brightness setting) and enabled flag.
            if (LayerSources != null && LayerTargets != null) {
                for (var i = 0; i < LayerSources.Length && i < LayerTargets.Length; i++) {
                    MirrorTint(LayerSources[i], LayerTargets[i]);
                }
            }

            // clones sample their animators cold at build; repair periodically
            if (Time.frameCount % 120 == 0) {
                foreach (var d in GetComponentsInChildren<FloatProviderAnimatorDriver>()) {
                    if (d.Animator != null && d.Value != null) {
                        d.Animator.Initialize();
                        d.Animator.SampleValue(d.Value.GetFloatValue(), true);
                    }
                }
            }
        }

        private static void MirrorTint(Renderer source, Renderer target) {
            if (source == null || target == null) {
                return;
            }

            var sm = source.material;
            if (sm != null && sm.HasProperty("_Color")) {
                var c = sm.GetColor("_Color");
                var b = RandomizerSettings.Customization.TempRowBrightness;
                c.r *= b;
                c.g *= b;
                c.b *= b;
                var tm = target.material;
                if (tm.GetColor("_Color") != c) {
                    tm.SetColor("_Color", c);
                }
            }

            target.enabled = source.enabled;
        }

        public static Vector3 PairMidpoint(Transform fallback) {
            if (healthFill != null && energyFill != null) {
                return (healthFill.transform.position + energyFill.transform.position) * 0.5f;
            }

            return fallback.position;
        }

        // one lift height for both rows: the taller of the two strips
        private static float PairHeight(Vector3 absU, Renderer fallback) {
            var h = 0f;
            if (healthFill != null) {
                var r = healthFill.GetComponent<Renderer>();
                if (r != null) {
                    h = Mathf.Max(h, Vector3.Dot(r.bounds.size, absU));
                }
            }

            if (energyFill != null) {
                var r = energyFill.GetComponent<Renderer>();
                if (r != null) {
                    h = Mathf.Max(h, Vector3.Dot(r.bounds.size, absU));
                }
            }

            if (h <= 0f) {
                h = fallback != null ? Vector3.Dot(fallback.bounds.size, absU) : 0.3f;
            }

            return h;
        }

        private static void ScreenAxes(Transform strip, out Vector3 right, out Vector3 up) {
            if (healthFill != null && energyFill != null && healthFill != energyFill) {
                var r = healthFill.transform.position - energyFill.transform.position;
                if (r.sqrMagnitude > 1e-6f) {
                    right = r.normalized;
                    var u = Vector3.Cross(strip.forward, right);
                    if (u.sqrMagnitude > 1e-6f) {
                        u.Normalize();
                        // the HUD never rolls past horizontal; pick the
                        // perpendicular that points away from the floor
                        up = Vector3.Dot(u, Vector3.up) < 0f ? -u : u;
                        return;
                    }
                }
            }

            right = Vector3.right;
            up = Vector3.up;
        }
    }
}
