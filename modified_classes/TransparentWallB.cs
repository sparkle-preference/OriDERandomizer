using Core;
using Game;
using UnityEngine;

public class TransparentWallB : SaveSerialize, ISuspendable
{
	public TransparentWallB()
	{
		IsSuspended = false;
	}

	public new void Awake()
	{
		SuspensionManager.Register(this);
	}

	public new void OnDestroy()
	{
		SuspensionManager.Unregister(this);
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref m_hasBeenShown);
	}

	public float SenseTime => Animator.Duration / 2f;

	public void Start()
	{
		var animatorDriver = Animator.AnimatorDriver;
		if (WallVisible)
		{
			Animator.Initialize();
			animatorDriver.GoToEnd();
		}
		else if (HasSense)
		{
			Animator.Initialize();
			animatorDriver.CurrentTime = SenseTime;
			animatorDriver.Pause();
			animatorDriver.Sample();
		}
		else
		{
			Animator.Initialize();
			animatorDriver.GoToStart();
		}
	}

	public void OnTriggerEnter(Collider other)
	{
		OnEnterTrigger(other);
		OnTrigger(other);
	}

	public void OnTriggerStay(Collider other)
	{
		OnTrigger(other);
	}

	private void OnEnterTrigger(Collider other)
	{
		if (other.gameObject.CompareTag("Player"))
		{
			if (!m_hasBeenShown)
			{
				if (SeinTransparentWallHandler.Instance)
				{
					Sound.Play(SeinTransparentWallHandler.Instance.EnterTransparentWallFirstTimeSoundProvider.GetSound(null), transform.position, null);
				}
			}
			else if (SeinTransparentWallHandler.Instance)
			{
				Sound.Play(SeinTransparentWallHandler.Instance.EnterTransparentWallSoundProvider.GetSound(null), transform.position, null);
			}
		}
	}

	public void OnTrigger(Collider other)
	{
		if (other.gameObject.CompareTag("Player"))
		{
			m_beingTriggered = true;
			if (!m_hasBeenShown)
			{
				m_hasBeenShown = true;
				AchievementsLogic.Instance.RevealTransparentWall();
			}
		}
	}

	public void FixedUpdate()
	{
		if (IsSuspended)
		{
			return;
		}
		var animatorDriver = Animator.AnimatorDriver;
		if (WallVisible)
		{
			if (animatorDriver.IsReversed || !animatorDriver.IsPlaying)
			{
				animatorDriver.SetForward();
				animatorDriver.Resume();
			}
		}
		else if (m_lastVisiable)
		{
			animatorDriver.SetBackwards();
			animatorDriver.Resume();
			if (SeinTransparentWallHandler.Instance)
			{
				Sound.Play(SeinTransparentWallHandler.Instance.LeaveTransparentWallSoundProvider.GetSound(null), transform.position, null);
			}
		}
		m_lastVisiable = WallVisible;
		if (animatorDriver.CurrentTime < SenseTime && HasSense)
		{
			animatorDriver.Pause();
			animatorDriver.CurrentTime = SenseTime;
			animatorDriver.Sample();
		}
		m_beingTriggered = false;
	}

	public bool HasSense => !(Characters.Sein == null);

	public bool WallVisible => m_beingTriggered;

	public bool IsSuspended { get; set; }

	private bool m_hasBeenShown;

	private bool m_lastVisiable;

	private bool m_beingTriggered;

	public BaseAnimator Animator;
}
