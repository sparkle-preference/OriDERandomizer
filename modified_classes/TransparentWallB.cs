using Core;
using Game;
using UnityEngine;

public class TransparentWallB : SaveSerialize, ISuspendable {
    public TransparentWallB() {
        IsSuspended = false;
    }

    public new void Awake() {
        SuspensionManager.Register(this);
    }

    public new void OnDestroy() {
        SuspensionManager.Unregister(this);
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref hasBeenShown);
    }

    public float SenseTime => Animator.Duration / 2f;

    public void Start() {
        var animatorDriver = Animator.AnimatorDriver;
        if (WallVisible) {
            Animator.Initialize();
            animatorDriver.GoToEnd();
        } else if (HasSense) {
            Animator.Initialize();
            animatorDriver.CurrentTime = SenseTime;
            animatorDriver.Pause();
            animatorDriver.Sample();
        } else {
            Animator.Initialize();
            animatorDriver.GoToStart();
        }
    }

    public void OnTriggerEnter(Collider other) {
        OnEnterTrigger(other);
        OnTrigger(other);
    }

    public void OnTriggerStay(Collider other) {
        OnTrigger(other);
    }

    private void OnEnterTrigger(Collider other) {
        if (other.gameObject.CompareTag("Player")) {
            if (!hasBeenShown) {
                if (SeinTransparentWallHandler.Instance) {
                    Sound.Play(SeinTransparentWallHandler.Instance.EnterTransparentWallFirstTimeSoundProvider.GetSound(null), transform.position, null);
                }
            } else if (SeinTransparentWallHandler.Instance) {
                Sound.Play(SeinTransparentWallHandler.Instance.EnterTransparentWallSoundProvider.GetSound(null), transform.position, null);
            }
        }
    }

    public void OnTrigger(Collider other) {
        if (other.gameObject.CompareTag("Player")) {
            beingTriggered = true;
            if (!hasBeenShown) {
                hasBeenShown = true;
                AchievementsLogic.Instance.RevealTransparentWall();
            }
        }
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        var animatorDriver = Animator.AnimatorDriver;
        if (WallVisible) {
            if (animatorDriver.IsReversed || !animatorDriver.IsPlaying) {
                animatorDriver.SetForward();
                animatorDriver.Resume();
            }
        } else if (lastVisible) {
            animatorDriver.SetBackwards();
            animatorDriver.Resume();
            if (SeinTransparentWallHandler.Instance) {
                Sound.Play(SeinTransparentWallHandler.Instance.LeaveTransparentWallSoundProvider.GetSound(null), transform.position, null);
            }
        }

        lastVisible = WallVisible;
        if (animatorDriver.CurrentTime < SenseTime && HasSense) {
            animatorDriver.Pause();
            animatorDriver.CurrentTime = SenseTime;
            animatorDriver.Sample();
        }

        beingTriggered = false;
    }

    public bool HasSense => !(Characters.Sein == null);

    public bool WallVisible => beingTriggered;

    public bool IsSuspended { get; set; }

    private bool hasBeenShown;

    private bool lastVisible;

    private bool beingTriggered;

    public BaseAnimator Animator;
}
