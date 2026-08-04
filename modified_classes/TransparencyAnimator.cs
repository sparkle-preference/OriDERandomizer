using System.Collections.Generic;
using CatlikeCoding.TextBox;
using UnityEngine;

public class TransparencyAnimator : BaseAnimator {
    static TransparencyAnimator() {
        var array = new bool[3];
        array[0] = true;
        array[1] = true;
        disableRenderer = array;
    }

    [ContextMenu("Print out renderer data")]
    public void PrintOutRendererData() {
        foreach (var rendererData in rendererData) {
            if (rendererData.Renderer != null) {
            }
        }
    }

    private int PropertyId {
        get {
            if (propIds == null) {
                propIds = new int[propNames.Length];
                for (var i = 0; i < propNames.Length; i++) {
                    propIds[i] = Shader.PropertyToID(propNames[i]);
                }
            }

            return propIds[(int)Mode];
        }
    }

    private bool UseSharedMaterial => (IsInScene && !forceUseRendererMaterial) || !Application.isPlaying;

    public new void Awake() {
        forceUseRendererMaterial = GetComponentInChildren<TextBox>() != null;
        base.Awake();
    }

    private bool CanBeAnimated(Renderer r) {
        return !(r.sharedMaterial == null) && r.sharedMaterial.HasProperty("_Color") && r.GetComponent<UberGhostTrail>() == null;
    }

    public override void CacheOriginals() {
        rendererData.Clear();
        renderers.Clear();
        AddChild(transform);
        if (AnimateChildren) {
            AddChildren(transform);
        }
    }

    private void AddChild(Transform child) {
        var component = child.GetComponent<Renderer>();
        if (component && CanBeAnimated(component) && !renderers.Contains(component)) {
            rendererData.Add(new RendererData(component, PropertyId));
            renderers.Add(component);
        }
    }

    private void AddChildren(Transform childTransform) {
        var childCount = childTransform.childCount;
        for (var i = 0; i < childCount; i++) {
            var child = childTransform.GetChild(i);
            var component = child.GetComponent<TransparencyAnimator>();
            if (component != null) {
                childTransparencyAnimators.Add(component);
            } else {
                var component2 = child.GetComponent<CleverMenuItem>();
                if (component2 != null && component2.AnimateColors) {
                    if (cleverMenuItems == null) {
                        cleverMenuItems = new List<CleverMenuItem>();
                    }

                    cleverMenuItems.Add(component2);
                }

                AddChild(child);
                AddChildren(child);
            }
        }
    }

    public static void Register(Transform child) {
        var parent = child.parent;
        while (parent) {
            var component = parent.GetComponent<TransparencyAnimator>();
            if (component && component.AnimateChildren) {
                component.ManuallyRegister(child);
                break;
            }

            parent = parent.parent;
        }
    }

    private void ManuallyRegister(Transform child) {
        if (!IsInitialized) {
            return;
        }

        var component = child.GetComponent<TransparencyAnimator>();
        if (component) {
            childTransparencyAnimators.Add(component);
            return;
        }

        var component2 = child.GetComponent<CleverMenuItem>();
        if (component2 != null && component2.AnimateColors) {
            if (cleverMenuItems == null) {
                cleverMenuItems = new List<CleverMenuItem>();
            }

            cleverMenuItems.Add(component2);
            return;
        }

        AddChild(child);
        AddChildren(child);
        ApplyTransparency();
    }

    public override void SampleValue(float value, bool forceSample) {
        value = TimeToAnimationCurveTime(value);
        opacity = AnimationCurve.Evaluate(value);
        ApplyTransparency(false);
    }

    public void ApplyTransparency(bool force = true) {
        var finalOpacity = FinalOpacity;
        if (!Mathf.Approximately(lastFinalOpacity, finalOpacity) || force) {
            lastFinalOpacity = finalOpacity;
            for (var i = 0; i < rendererData.Count; i++) {
                rendererData[i].SetRendererAlpha((int)Mode, PropertyId, UseSharedMaterial, finalOpacity);
            }

            for (var j = 0; j < childTransparencyAnimators.Count; j++) {
                childTransparencyAnimators[j].SetParentOpacity(finalOpacity);
            }

            if (cleverMenuItems != null) {
                for (var k = 0; k < cleverMenuItems.Count; k++) {
                    cleverMenuItems[k].SetParentOpacity(finalOpacity);
                }
            }
        }
    }

    public void SetParentOpacity(float opacity) {
        if (!Mathf.Approximately(opacity, parentOpacity)) {
            parentOpacity = opacity;
            if (IsInitialized) {
                ApplyTransparency();
            }
        }
    }

    public float FinalOpacity => opacity * parentOpacity;

    public override float Duration => AnimationCurveTimeToTime(AnimationCurve.CurveDuration());

    public override void RestoreToOriginalState() {
        parentOpacity = 1f;
        opacity = 1f;
        for (var i = 0; i < childTransparencyAnimators.Count; i++) {
            childTransparencyAnimators[i].RestoreToOriginalState();
        }

        for (var j = 0; j < rendererData.Count; j++) {
            rendererData[j].SetRendererAlpha((int)Mode, PropertyId, UseSharedMaterial, 1f);
        }
    }

    public override bool IsLooping => AnimationCurve.postWrapMode != WrapMode.ClampForever;

    public void Reset() {
        if (childTransparencyAnimators != null) {
            childTransparencyAnimators.Clear();
        }

        if (cleverMenuItems != null) {
            cleverMenuItems.Clear();
        }

        if (rendererData != null) {
            rendererData.Clear();
        }

        if (renderers != null) {
            renderers.Clear();
        }
    }

    private static string[] propNames = {
        "_Color",
        "_MaskDissolveColor",
        "_AdditiveLayerColor",
    };

    private static bool[] disableRenderer;

    private static int[] propIds;

    public AnimationCurve AnimationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public bool AnimateChildren;

    public AnimateMode Mode;

    [PooledSafe] private readonly List<RendererData> rendererData = new List<RendererData>(4);

    [PooledSafe] private readonly List<TransparencyAnimator> childTransparencyAnimators = new List<TransparencyAnimator>(4);

    [PooledSafe] private List<CleverMenuItem> cleverMenuItems;

    private bool forceUseRendererMaterial;

    private float parentOpacity = 1f;

    private float opacity = 1f;

    [PooledSafe] private readonly HashSet<Renderer> renderers = new HashSet<Renderer>();

    private float lastFinalOpacity = 123456792f;

    public enum AnimateMode {
        Color,
        Dissolve,
        Additive,
    }

    private struct RendererData {
        public RendererData(Renderer renderer, int id) {
            Renderer = renderer;
            OriginalAlpha = renderer.sharedMaterial.GetColor(id).a;
        }

        public void SetRendererAlpha(int mode, int propertyID, bool useSharedMaterial, float value) {
            if (Renderer == null || Renderer.sharedMaterial == null) {
                return;
            }

            if (disableRenderer[mode]) {
                Renderer.enabled = value > 0.01f;
            }

            var a = value * OriginalAlpha;
            var material = !useSharedMaterial ? Renderer.material : Renderer.sharedMaterial;
            var color = material.GetColor(propertyID);
            color.a = a;
            material.SetColor(propertyID, color);
        }

        public readonly float OriginalAlpha;

        public readonly Renderer Renderer;
    }
}
