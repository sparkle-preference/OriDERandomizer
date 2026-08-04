using Game;
using UnityEngine;

public class Naru : MonoBehaviour, ICharacter {
    public void Awake() {
        Characters.Naru = this;
        Characters.Current = this;
    }

    public void OnDestroy() {
        Randomizer.onNaruDestroyed();
        if (Characters.Naru == this) {
            Characters.Naru = null;
        }

        if (Characters.Current == this) {
            Characters.Current = null;
        }
    }

    public Vector3 Position {
        get => transform.position;
        set => transform.position = value;
    }

    public void Activate(bool active) {
        gameObject.SetActive(active);
    }

    public GameObject GameObject => gameObject;

    public bool FaceLeft {
        get => Animation.SpriteMirror.FaceLeft;
        set => Animation.SpriteMirror.FaceLeft = value;
    }

    public Vector3 Speed {
        get => PlatformBehaviour.PlatformMovement.LocalSpeed;
        set => PlatformBehaviour.PlatformMovement.LocalSpeed = value;
    }

    public Transform Transform => transform;

    public bool IsOnGround => PlatformBehaviour.PlatformMovement.IsOnGround;

    public void PlaceOnGround() {
        PlatformBehaviour.PlatformMovement.PlaceOnGround(0.5f, 0f);
    }

    public CharacterAnimationSystem Animation;

    public NaruController Controller;

    public PlatformBehaviour PlatformBehaviour;

    public bool SeinNaruComboEnabled;

    public NaruSounds Sounds;
}
