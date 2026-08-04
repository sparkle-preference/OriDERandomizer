using Core;
using Game;
using UnityEngine;

public class StompPost : SaveSerialize, IDamageReciever, IAttackable, IStompAttackable, ISuspendable, IDynamicGraphicHierarchy {
    public new void Awake() {
        base.Awake();
        SuspensionManager.Register(this);
        transform = base.transform;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        SuspensionManager.Unregister(this);
    }

    public void Start() {
        distanceStompedIntoGround = 0f;
        startLocalPosition = base.transform.localPosition;
    }

    public void OnRecieveDamage(Damage damage) {
        if (damage.Type == DamageType.Stomp && Vector3.Dot(base.transform.rotation * Vector3.down, Characters.Sein.PlatformBehaviour.PlatformMovement.GravityDirection) > Mathf.Cos(0.17453292f) && !activated) {
            distanceStompedIntoGround = Mathf.Min(StompIntoGroundAmount, distanceStompedIntoGround + StompIntoGroundAmount / NumberOfStomps);
            remainingRiseDelayTime = RisingDelay;
            if (Mathf.Approximately(distanceStompedIntoGround, StompIntoGroundAmount)) {
                BingoController.OnStompPost(MoonGuid);
                activated = true;
                if (AllTheWayInAction) {
                    AllTheWayInAction.Perform(null);
                }

                if (AllTheWayInSound) {
                    Sound.Play(AllTheWayInSound.GetSound(null), transform.position, null);
                }
            } else if (StompSound) {
                Sound.Play(StompSound.GetSound(null), transform.position, null);
            }
        }
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (remainingRiseDelayTime > 0f) {
            remainingRiseDelayTime -= Time.deltaTime;
            if (remainingRiseDelayTime < 0f) {
                remainingRiseDelayTime = 0f;
            }
        }

        if (!activated && remainingRiseDelayTime < 0f) {
            distanceStompedIntoGround -= Time.deltaTime * RiseSpeed;
            if (distanceStompedIntoGround < 0f) {
                distanceStompedIntoGround = 0f;
            }
        }

        base.transform.localPosition = Vector3.Lerp(base.transform.localPosition, startLocalPosition + Vector3.down * distanceStompedIntoGround, 0.3f);
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref activated);
        ar.Serialize(ref distanceStompedIntoGround);
        ar.Serialize(ref remainingRiseDelayTime);
    }

    public bool IsSuspended { get; set; }

    public Vector3 Position => transform.position;

    public bool CanBeChargeFlamed() {
        return false;
    }

    public bool CanBeChargeDashed() {
        return false;
    }

    public bool CanBeGrenaded() {
        return false;
    }

    public bool CanBeStomped() {
        return true;
    }

    public bool CanBeBashed() {
        return false;
    }

    public bool CanBeSpiritFlamed() {
        return false;
    }

    public bool IsStompBouncable() {
        return false;
    }

    public bool CanBeLevelUpBlasted() {
        return false;
    }

    public bool CountsTowardsSuperJumpAchievement() {
        return false;
    }

    public bool IsDead() {
        return false;
    }

    public void ForceActivate() {
        activated = true;
        base.transform.localPosition += Vector3.down * StompIntoGroundAmount;
    }

    public int NumberOfStomps = 3;

    public float StompIntoGroundAmount = 0.1f;

    public float RisingDelay = 8f;

    public float RiseSpeed = 1f;

    public SoundProvider StompSound;

    public SoundProvider AllTheWayInSound;

    public ActionMethod AllTheWayInAction;

    private Vector3 startLocalPosition;

    private new Transform transform;

    private float distanceStompedIntoGround;

    private float remainingRiseDelayTime;

    private bool activated;
}
