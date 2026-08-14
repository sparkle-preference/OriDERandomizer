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

    // The crush Damage is built with Sein's own detector as its sender, so this is
    // the only handle on what did the crushing.
    public static GameObject LastCrusher;

    public static int LastCrusherFrame = -1;

    // Crushing hazards can also deal Crush damage straight from their own collider,
    // which never reaches this class -- those deaths would otherwise read whatever
    // crushed you last. Trust the stash only on the frame it was set.
    public static GameObject CrusherThisFrame() {
        return LastCrusherFrame == Time.frameCount ? LastCrusher : null;
    }

    public SeinCharacter Sein;
}
