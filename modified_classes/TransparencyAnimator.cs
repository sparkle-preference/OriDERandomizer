using System.Collections.Generic;
using CatlikeCoding.TextBox;
using UnityEngine;

public class TransparencyAnimator : BaseAnimator {
    static TransparencyAnimator() {
        var array = new bool[3];
        array[0] = true;
        array[1] = true;
        s_disableRenderer = array;
    }

    [ContextMenu("Print out renderer data")]
    public void PrintOutRendererData() {
        foreach (var rendererData in m_rendererData) {
            if (rendererData.Renderer != null) {
            }
        }
    }

    private int PropertyId {
        get {
            if (s_propIds == null) {
                s_propIds = new int[s_propNames.Length];
                for (var i = 0; i < s_propNames.Length; i++) {
                    s_propIds[i] = Shader.PropertyToID(s_propNames[i]);
                }
            }

            return s_propIds[(int)Mode];
        }
    }

    private bool UseSharedMaterial => (IsInScene && !m_forceUseRendererMaterial) || !Application.isPlaying;

    public new void Awake() {
        m_forceUseRendererMaterial = GetComponentInChildren<TextBox>() != null;
        base.Awake();
    }

    private bool CanBeAnimated(Renderer r) {
        return !(r.sharedMaterial == null) && r.sharedMaterial.HasProperty("_Color") && r.GetComponent<UberGhostTrail>() == null;
    }

    public override void CacheOriginals() {
        m_rendererData.Clear();
        m_renderers.Clear();
        AddChild(transform);
        if (AnimateChildren) {
            AddChildren(transform);
        }
    }

    private void AddChild(Transform child) {
        var component = child.GetComponent<Renderer>();
        if (component && CanBeAnimated(component) && !m_renderers.Contains(component)) {
            m_rendererData.Add(new RendererData(component, PropertyId));
            m_renderers.Add(component);
        }
    }

    private void AddChildren(Transform childTransform) {
        var childCount = childTransform.childCount;
        for (var i = 0; i < childCount; i++) {
            var child = childTransform.GetChild(i);
            var component = child.GetComponent<TransparencyAnimator>();
            if (component != null) {
                m_childTransparencyAnimators.Add(component);
            } else {
                var component2 = child.GetComponent<CleverMenuItem>();
                if (component2 != null && component2.AnimateColors) {
                    if (m_cleverMenuItems == null) {
                        m_cleverMenuItems = new List<CleverMenuItem>();
                    }

                    m_cleverMenuItems.Add(component2);
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
            m_childTransparencyAnimators.Add(component);
            return;
        }

        var component2 = child.GetComponent<CleverMenuItem>();
        if (component2 != null && component2.AnimateColors) {
            if (m_cleverMenuItems == null) {
                m_cleverMenuItems = new List<CleverMenuItem>();
            }

            m_cleverMenuItems.Add(component2);
            return;
        }

        AddChild(child);
        AddChildren(child);
        ApplyTransparency();
    }

    public override void SampleValue(float value, bool forceSample) {
        value = TimeToAnimationCurveTime(value);
        m_opacity = AnimationCurve.Evaluate(value);
        ApplyTransparency(false);
    }

    public void ApplyTransparency(bool force = true) {
        var finalOpacity = FinalOpacity;
        if (!Mathf.Approximately(m_lastFinalOpacity, finalOpacity) || force) {
            m_lastFinalOpacity = finalOpacity;
            for (var i = 0; i < m_rendererData.Count; i++) {
                m_rendererData[i].SetRendererAlpha((int)Mode, PropertyId, UseSharedMaterial, finalOpacity);
            }

            for (var j = 0; j < m_childTransparencyAnimators.Count; j++) {
                m_childTransparencyAnimators[j].SetParentOpacity(finalOpacity);
            }

            if (m_cleverMenuItems != null) {
                for (var k = 0; k < m_cleverMenuItems.Count; k++) {
                    m_cleverMenuItems[k].SetParentOpacity(finalOpacity);
                }
            }
        }
    }

    public void SetParentOpacity(float opacity) {
        if (!Mathf.Approximately(opacity, m_parentOpacity)) {
            m_parentOpacity = opacity;
            if (IsInitialized) {
                ApplyTransparency();
            }
        }
    }

    public float FinalOpacity => m_opacity * m_parentOpacity;

    public override float Duration => AnimationCurveTimeToTime(AnimationCurve.CurveDuration());

    public override void RestoreToOriginalState() {
        m_parentOpacity = 1f;
        m_opacity = 1f;
        for (var i = 0; i < m_childTransparencyAnimators.Count; i++) {
            m_childTransparencyAnimators[i].RestoreToOriginalState();
        }

        for (var j = 0; j < m_rendererData.Count; j++) {
            m_rendererData[j].SetRendererAlpha((int)Mode, PropertyId, UseSharedMaterial, 1f);
        }
    }

    public override bool IsLooping => AnimationCurve.postWrapMode != WrapMode.ClampForever;

    public void Reset() {
        if (m_childTransparencyAnimators != null) {
            m_childTransparencyAnimators.Clear();
        }

        if (m_cleverMenuItems != null) {
            m_cleverMenuItems.Clear();
        }

        if (m_rendererData != null) {
            m_rendererData.Clear();
        }

        if (m_renderers != null) {
            m_renderers.Clear();
        }
    }

    private static string[] s_propNames = {
        "_Color",
        "_MaskDissolveColor",
        "_AdditiveLayerColor",
    };

    private static bool[] s_disableRenderer;

    private static int[] s_propIds;

    public AnimationCurve AnimationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public bool AnimateChildren;

    public AnimateMode Mode;

    [PooledSafe] private readonly List<RendererData> m_rendererData = new List<RendererData>(4);

    [PooledSafe] private readonly List<TransparencyAnimator> m_childTransparencyAnimators = new List<TransparencyAnimator>(4);

    [PooledSafe] private List<CleverMenuItem> m_cleverMenuItems;

    private bool m_forceUseRendererMaterial;

    private float m_parentOpacity = 1f;

    private float m_opacity = 1f;

    [PooledSafe] private readonly HashSet<Renderer> m_renderers = new HashSet<Renderer>();

    private float m_lastFinalOpacity = 123456792f;

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

            if (s_disableRenderer[mode]) {
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
