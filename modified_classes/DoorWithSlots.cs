using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class DoorWithSlots : SaveSerialize
{
	public void OnValidate()
	{
		m_transform = transform;
	}

	public override void Awake()
	{
		base.Awake();
		m_opensOnLeftSide = m_transform.TransformPoint(Vector3.right).x < m_transform.position.x;
	}

	public void Highlight()
	{
		if (OriTarget)
		{
			Characters.Ori.MoveOriToPosition(OriTarget.position, OriDuration);
		}
		else
		{
			Characters.Ori.MoveOriToPosition(m_transform.position, OriDuration);
		}
		if (Characters.Sein.Abilities.SpiritFlame)
		{
			Characters.Sein.Abilities.SpiritFlame.AddLock("doorWithSlots");
		}
		Characters.Ori.GetComponent<Rigidbody>().velocity = Vector3.zero;
		Characters.Ori.EnableHoverWobbling = false;
		Characters.Ori.InsideDoor = true;
		if (m_hint == null)
		{
			m_hint = UI.Hints.Show(HintMessage, HintLayer.HintZone, 600f);
		}
		if (OnOriEnterSoundProvider)
		{
			Sound.Play(OnOriEnterSoundProvider.GetSound(null), m_transform.position, null);
		}
		Randomizer.Keysanity.ApplyKeystoneCount(MoonGuid, NumberOfOrbsUsed);
	}

	public void Unhighlight()
	{
		Characters.Ori.ChangeState(Ori.State.Hovering);
		Characters.Ori.EnableHoverWobbling = true;
		Characters.Ori.InsideDoor = false;
		if (Characters.Sein.Abilities.SpiritFlame)
		{
			Characters.Sein.Abilities.SpiritFlame.RemoveLock("doorWithSlots");
		}
		if (m_hint)
		{
			m_hint.HideMessageScreen();
		}
		if (OnOriExitSoundProvider)
		{
			Sound.Play(OnOriExitSoundProvider.GetSound(null), m_transform.position, null);
		}
		Randomizer.Keysanity.ResetKeystoneCount();
	}

	public void RestoreOrbs()
	{
		if (NumberOfOrbsUsed > 0 && RestoreLeafsSoundProvider)
		{
			Sound.Play(RestoreLeafsSoundProvider.GetSound(null), m_transform.position, null);
		}
		Characters.Sein.Inventory.CollectKeystones(NumberOfOrbsUsed);
		NumberOfOrbsUsed = 0;
		Randomizer.Keysanity.ResetKeystoneCount();
	}

	public void OnDisable()
	{
		if (!Characters.Sein)
		{
			return;
		}
		if (CurrentState == State.Highlighted)
		{
			RestoreOrbs();
			Unhighlight();
		}
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref m_slotsPending);
		ar.Serialize(ref NumberOfOrbsUsed);
		ar.Serialize(ref m_slotsFilled);
		if (ar.Reading && CurrentState == State.Highlighted)
		{
			Unhighlight();
			CurrentState = State.Normal;
		}
		CurrentState = (State)ar.Serialize((int)CurrentState);
		if (ar.Reading && CurrentState == State.Highlighted)
		{
			RestoreOrbs();
			CurrentState = State.Normal;
		}
		if (m_openDoorSound)
		{
			m_openDoorSound.FadeOut(0.5f, true);
			UberPoolManager.Instance.RemoveOnDestroyed(m_openDoorSound.gameObject);
			m_openDoorSound = null;
		}
		if (ar.Reading && CurrentState == State.Opened)
		{
			m_checkItOpened = true;
		}
	}

	public float DistanceToSein
	{
		get
		{
			return Vector3.Distance(m_transform.position, Characters.Sein.Position);
		}
	}

	public bool OriHasTargets
	{
		get
		{
			SeinSpiritFlameTargetting spiritFlameTargetting = Characters.Sein.Abilities.SpiritFlameTargetting;
			return spiritFlameTargetting && spiritFlameTargetting.ClosestAttackables.Count > 0;
		}
	}

	public bool SeinInRange
	{
		get
		{
			return !OriHasTargets && DistanceToSein <= Radius && (Randomizer.OpenMode || ((!m_opensOnLeftSide || m_transform.position.x >= Characters.Sein.Position.x) && (m_opensOnLeftSide || m_transform.position.x <= Characters.Sein.Position.x)));
		}
	}

	public void FixedUpdate()
	{
		switch (CurrentState)
		{
		case State.Normal:
			if (SeinInRange && !OriHasTargets && Characters.Sein.Controller.CanMove)
			{
				Highlight();
				CurrentState = State.Highlighted;
			}
			break;
		case State.Highlighted:
			if (!SeinInRange)
			{
				RestoreOrbs();
				Unhighlight();
				CurrentState = State.Normal;
			}
			if (!Characters.Sein.Controller.CanMove)
			{
				RestoreOrbs();
				Unhighlight();
				CurrentState = State.Normal;
				return;
			}
			if (Characters.Sein.Controller.CanMove && !Characters.Sein.IsSuspended && Input.SpiritFlame.OnPressed)
			{
				if (Characters.Sein.Inventory.Keystones == 0 && NumberOfOrbsRequired > NumberOfOrbsUsed)
				{
					OnFailAction.Perform(null);
					UI.SeinUI.ShakeKeystones();
					if (NotEnoughLeafsSoundProvider)
					{
						Sound.Play(NotEnoughLeafsSoundProvider.GetSound(null), m_transform.position, null);
					}
				}
				if (Characters.Sein.Inventory.Keystones > 0 && NumberOfOrbsUsed < NumberOfOrbsRequired)
				{
					NumberOfOrbsUsed++;
					Characters.Sein.Inventory.SpendKeystones(1);
					if (PlaceLeafSoundSoundProvider)
					{
						Sound.Play(PlaceLeafSoundSoundProvider.GetSound(null), m_transform.position, null);
					}
				}
				if (NumberOfOrbsUsed == NumberOfOrbsRequired)
				{
					OnOpenedAction.Perform(null);
					Unhighlight();
					BingoController.OnKSDoor(MoonGuid);
					RandomizerLocationManager.OpenDoorByGuid(MoonGuid);
					CurrentState = State.Opened;
					if (OpenDoorSoundProvider)
					{
						m_openDoorSound = Sound.Play(OpenDoorSoundProvider.GetSound(null), m_transform.position, delegate
						{
							m_openDoorSound = null;
						});
						m_openDoorSound.PauseOnSuspend = true;
					}
				}
			}
			break;
		case State.Opened:
			if (m_checkItOpened)
			{
				m_checkItOpened = false;
				MakeSureItsAtEnd(transform.FindChild("doorPieces/doorLeft"));
				MakeSureItsAtEnd(transform.FindChild("doorPieces/doorRight"));
			}
			break;
		default:
			return;
		}
	}

	private void MakeSureItsAtEnd(Transform c)
	{
		if (c == null)
		{
			return;
		}
		LegacyTranslateAnimator component = c.GetComponent<LegacyTranslateAnimator>();
		if (component.CurrentTime <= 0f && component.Stopped)
		{
			component.StopAndSampleAtEnd();
		}
	}

	public Transform OriTarget;

	public Color OriHoverColor;

	[SerializeField]
	[HideInInspector]
	private Transform m_transform;

	private int m_slotsPending;

	private int m_slotsFilled;

	public ActionMethod OnOpenedAction;

	public ActionMethod OnFailAction;

	public int NumberOfOrbsRequired;

	public int NumberOfOrbsUsed;

	public SoundProvider PlaceLeafSoundSoundProvider;

	public SoundProvider NotEnoughLeafsSoundProvider;

	public SoundProvider OpenDoorSoundProvider;

	public SoundProvider RestoreLeafsSoundProvider;

	public SoundProvider OnOriEnterSoundProvider;

	public SoundProvider OnOriExitSoundProvider;

	public float OriDuration = 1f;

	public float Radius = 10f;

	public MessageProvider HintMessage;

	public CameraShakeAsset DoorKeyInsertShake;

	public ControllerShakeAsset DoorKeyInsertControllerShake;

	private MessageBox m_hint;

	private bool m_opensOnLeftSide;

	public State CurrentState;

	private bool m_checkItOpened;

	private SoundPlayer m_openDoorSound;

	public enum State
	{
		Normal,
		Highlighted,
		Opened
	}
}
