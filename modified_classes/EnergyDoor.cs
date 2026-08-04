using System.Collections.Generic;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class EnergyDoor : SaveSerialize
{
	public void OnValidate()
	{
		m_transform = transform;
	}

	public override void Awake()
	{
		base.Awake();
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
			Characters.Sein.Abilities.SpiritFlame.AddLock("energyDoor");
		}
		Characters.Ori.GetComponent<Rigidbody>().velocity = Vector3.zero;
		Characters.Ori.EnableHoverWobbling = false;
		if (m_hint == null)
		{
			m_hint = UI.Hints.Show(HintMessage, HintLayer.HintZone);
		}
		if (OnOriEnterSoundProvider)
		{
			Sound.Play(OnOriEnterSoundProvider.GetSound(null), m_transform.position, null);
		}
	}

	public void Unhighlight()
	{
		Characters.Ori.ChangeState(Ori.State.Hovering);
		Characters.Ori.EnableHoverWobbling = true;
		if (Characters.Sein.Abilities.SpiritFlame)
		{
			Characters.Sein.Abilities.SpiritFlame.RemoveLock("energyDoor");
		}
		if (m_hint)
		{
			m_hint.HideMessageScreen();
		}
		if (OnOriExitSoundProvider)
		{
			Sound.Play(OnOriExitSoundProvider.GetSound(null), m_transform.position, null);
		}
	}

	public void RestoreOrbs()
	{
		if (AmountOfEnergyUsed > 0 && RestoreSoundProvider)
		{
			Sound.Play(RestoreSoundProvider.GetSound(null), m_transform.position, null);
		}
		if (Characters.Sein)
		{
			Characters.Sein.Energy.Gain(AmountOfEnergyUsed);
		}
		AmountOfEnergyUsed = 0;
	}

	public void OnDisable()
	{
		if (CurrentState == State.Highlighted)
		{
			RestoreOrbs();
			Unhighlight();
		}
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref m_slotsPending);
		ar.Serialize(ref AmountOfEnergyUsed);
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
			return !OriHasTargets && DistanceToSein <= Radius;
		}
	}

	public void RegisterSlot(EnergyDoorSlot slot)
	{
		m_slots.Add(slot);
	}

	public void UpdateSlots()
	{
		foreach (EnergyDoorSlot energyDoorSlot in m_slots)
		{
			energyDoorSlot.Refresh();
		}
	}

	public void FixedUpdate()
	{
		if (!Characters.Sein)
		{
			return;
		}
		State currentState = CurrentState;
		if (currentState != State.Normal)
		{
			if (currentState == State.Highlighted)
			{
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
					if (Characters.Sein.Energy.Current < 1f && AmountOfEnergyRequired > AmountOfEnergyUsed)
					{
						OnFailAction.Perform(null);
						Characters.Sein.Energy.NotifyOutOfEnergy();
					}
					if (Characters.Sein.Energy.Current >= 1f && AmountOfEnergyUsed < AmountOfEnergyRequired)
					{
						AmountOfEnergyUsed++;
						Characters.Sein.Energy.Spend(1f);
						UpdateSlots();
						if (PlaceSlotSoundProvider)
						{
							Sound.Play(PlaceSlotSoundProvider.GetSound(null), m_transform.position, null);
						}
					}
					if (AmountOfEnergyUsed == AmountOfEnergyRequired)
					{
						BingoController.OnEnergyDoor(MoonGuid);
						OnOpenedAction.Perform(null);
						Unhighlight();
						CurrentState = State.Opened;
						if (ActivateSoundProvider)
						{
							Sound.Play(ActivateSoundProvider.GetSound(null), m_transform.position, null);
						}
					}
				}
			}
		}
		else if (SeinInRange && !OriHasTargets && Characters.Sein.Controller.CanMove)
		{
			Highlight();
			CurrentState = State.Highlighted;
		}
	}

	public Transform OriTarget;

	[SerializeField]
	[HideInInspector]
	private Transform m_transform;

	private int m_slotsPending;

	private int m_slotsFilled;

	public ActionMethod OnOpenedAction;

	public ActionMethod OnFailAction;

	public int AmountOfEnergyRequired;

	public int AmountOfEnergyUsed;

	public SoundProvider PlaceSlotSoundProvider;

	public SoundProvider ActivateSoundProvider;

	public SoundProvider RestoreSoundProvider;

	public SoundProvider OnOriEnterSoundProvider;

	public SoundProvider OnOriExitSoundProvider;

	public float OriDuration = 1f;

	public float Radius = 10f;

	public Texture2D HintTexture;

	public MessageProvider HintMessage;

	private MessageBox m_hint;

	public State CurrentState;

	private List<EnergyDoorSlot> m_slots = new List<EnergyDoorSlot>();

	public enum State
	{
		Normal,
		Highlighted,
		Opened
	}
}
