using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class IgnitableSpiritTorch : SaveSerialize {
    static IgnitableSpiritTorch() {
        OnLightTorchWithGrenadeEvent = delegate { };
    }

    public static event Action OnLightTorchWithGrenadeEvent;

    public override void Awake() {
        base.Awake();
        transform = base.transform;
        UpdateLightSettings();
        all.Add(this);
    }

    public void UpdateLightSettings() {
        if (isLit) {
            LightSource.GetComponent<SpiritLightRadialVisualAffector>().Radius = LitRadius;
        } else {
            LightSource.GetComponent<SpiritLightRadialVisualAffector>().Radius = UnlitRadius;
        }
    }

    public override void OnDestroy() {
        base.OnDestroy();
        all.Remove(this);
    }

    public static IgnitableSpiritTorch IgniteAnyTorchesNearPosition(Vector3 position) {
        foreach (var ignitableSpiritTorch in all) {
            if (!ignitableSpiritTorch.isLit && Vector3.Distance(ignitableSpiritTorch.Position, position) < 2f) {
                ignitableSpiritTorch.Light(true);
                return ignitableSpiritTorch;
            }
        }

        return null;
    }

    public void Light(bool byGrenade) {
        BingoController.OnLanternLit(MoonGuid, byGrenade);
        isLit = true;
        if (OnLitAction) {
            OnLitAction.Perform(null);
        }

        UpdateLightSettings();
        if (byGrenade) {
            OnLightTorchWithGrenadeEvent();
        }
    }

    public Vector3 Position => transform.position;

    public override void Serialize(Archive ar) {
        ar.Serialize(ref isLit);
        if (ar.Reading) {
            UpdateLightSettings();
        }
    }

    public void FixedUpdate() {
        if (!isLit && Items.LightTorch && Vector3.Distance(Items.LightTorch.Position, Position) < TouchRadius) {
            Light(false);
        }
    }

    private const int GRENADE_IGNITE_RADIUS = 2;

    private static List<IgnitableSpiritTorch> all = new List<IgnitableSpiritTorch>();

    public ActionSequence OnLitAction;

    public GameObject LightSource;

    public float TouchRadius = 2f;

    private new Transform transform;

    private bool isLit;

    public float LitRadius = 5f;

    public float UnlitRadius = 2f;

    public BaseAnimator IgniteAnimator;
}
