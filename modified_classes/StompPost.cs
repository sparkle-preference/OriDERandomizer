using Core;
using Game;
using UnityEngine;

public class StompPost : SaveSerialize, IDamageReciever, IAttackable, IStompAttackable, ISuspendable, IDynamicGraphicHierarchy {
    public new void Awake() {
        base.Awake();
        SuspensionManager.Register(this);
        m_transform = transform;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        SuspensionManager.Unregister(this);
    }

    public void Start() {
        m_distanceStompedIntoGround = 0f;
        m_startLocalPosition = transform.localPosition;
    }

    public void OnRecieveDamage(Damage damage) {
        if (damage.Type == DamageType.Stomp && Vector3.Dot(transform.rotation * Vector3.down, Characters.Sein.PlatformBehaviour.PlatformMovement.GravityDirection) > Mathf.Cos(0.17453292f) && !m_activated) {
            m_distanceStompedIntoGround = Mathf.Min(StompIntoGroundAmount, m_distanceStompedIntoGround + StompIntoGroundAmount / NumberOfStomps);
            m_remainingRiseDelayTime = RisingDelay;
            if (Mathf.Approximately(m_distanceStompedIntoGround, StompIntoGroundAmount)) {
                BingoController.OnStompPost(MoonGuid);
                m_activated = true;
                if (AllTheWayInAction) {
                    AllTheWayInAction.Perform(null);
                }

                if (AllTheWayInSound) {
                    Sound.Play(AllTheWayInSound.GetSound(null), m_transform.position, null);
                }
            } else if (StompSound) {
                Sound.Play(StompSound.GetSound(null), m_transform.position, null);
            }
        }
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (m_remainingRiseDelayTime > 0f) {
            m_remainingRiseDelayTime -= Time.deltaTime;
            if (m_remainingRiseDelayTime < 0f) {
                m_remainingRiseDelayTime = 0f;
            }
        }

        if (!m_activated && m_remainingRiseDelayTime < 0f) {
            m_distanceStompedIntoGround -= Time.deltaTime * RiseSpeed;
            if (m_distanceStompedIntoGround < 0f) {
                m_distanceStompedIntoGround = 0f;
            }
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, m_startLocalPosition + Vector3.down * m_distanceStompedIntoGround, 0.3f);
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_activated);
        ar.Serialize(ref m_distanceStompedIntoGround);
        ar.Serialize(ref m_remainingRiseDelayTime);
    }

    public bool IsSuspended { get; set; }

    public Vector3 Position => m_transform.position;

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
        m_activated = true;
        transform.localPosition += Vector3.down * StompIntoGroundAmount;
    }

    public int NumberOfStomps = 3;

    public float StompIntoGroundAmount = 0.1f;

    public float RisingDelay = 8f;

    public float RiseSpeed = 1f;

    public SoundProvider StompSound;

    public SoundProvider AllTheWayInSound;

    public ActionMethod AllTheWayInAction;

    private Vector3 m_startLocalPosition;

    private Transform m_transform;

    private float m_distanceStompedIntoGround;

    private float m_remainingRiseDelayTime;

    private bool m_activated;
}
