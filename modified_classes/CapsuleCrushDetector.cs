using UnityEngine;

public class CapsuleCrushDetector : CharacterState, ISeinReceiver {
    public PlatformBehaviour PlatformBehaviour => Sein.PlatformBehaviour;

    public void OnTriggerEnter(Collider collider) {
        OnTrigger(collider);
    }

    public void OnTriggerStay(Collider collider) {
        OnTrigger(collider);
    }

    private void OnTrigger(Collider collider) {
        if (collider.GetComponent<CrushPlayer>()) {
            LastCrusher = collider.gameObject;
            LastCrusherFrame = Time.frameCount;
            var damage = new Damage(10000f, Vector2.zero, Sein.Position, DamageType.Crush, gameObject);
            damage.DealToComponents(Sein.gameObject);
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
        Sein.Mortality.CrushDetector = this;
    }

    // The crush Damage's sender is Sein's own detector, so this is the only handle on the crusher.
    public static GameObject LastCrusher;

    public static int LastCrusherFrame = -1;

    // Null unless this frame's crush came through here; hazards dealing Crush directly never do.
    public static GameObject CrusherThisFrame() {
        return LastCrusherFrame == Time.frameCount ? LastCrusher : null;
    }

    public SeinCharacter Sein;
}
