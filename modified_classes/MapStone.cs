using Game;
using UnityEngine;
using Input = Core.Input;

public class MapStone : SaveSerialize
{
	public override void Awake()
	{
		base.Awake();
		m_transform = transform;
	}

	public void FindWorldArea()
	{
		if (GameWorld.Instance)
		{
			WorldArea = GameWorld.Instance.WorldAreaAtPosition(m_transform.position);
		}
		if (WorldArea == null)
		{
		}
	}

	public void Start()
	{
		if (WorldArea == null)
		{
			FindWorldArea();
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

	public void Highlight()
	{
		if (OriTarget)
		{
			Characters.Ori.MoveOriToPosition(OriTarget.position, OriDuration);
		}
		if (Characters.Sein.Abilities.SpiritFlame)
		{
			Characters.Sein.Abilities.SpiritFlame.AddLock("mapStone");
		}
		Characters.Ori.GetComponent<Rigidbody>().velocity = Vector3.zero;
		Characters.Ori.EnableHoverWobbling = false;
		Characters.Ori.InsideMapstone = true;
		BingoController.OnTouchMapstone();
		if (m_hint == null)
		{
			m_hint = UI.Hints.Show(HintMessage, HintLayer.HintZone);
		}
		if (OriEnterAction)
		{
			OriEnterAction.Perform(null);
		}
	}

	public void Unhighlight()
	{
		Characters.Ori.ChangeState(Ori.State.Hovering);
		Characters.Ori.EnableHoverWobbling = true;
		Characters.Ori.InsideMapstone = false;
		if (Characters.Sein.Abilities.SpiritFlame)
		{
			Characters.Sein.Abilities.SpiritFlame.RemoveLock("mapStone");
		}
		if (OriExitAction)
		{
			OriExitAction.Perform(null);
		}
		if (m_hint)
		{
			m_hint.HideMessageScreen();
		}
	}

	public void OnDisable()
	{
		if (CurrentState == State.Highlighted)
		{
			CurrentState = State.Normal;
			Unhighlight();
		}
	}

	public bool Activated => CurrentState == State.Activated;

	public override void Serialize(Archive ar)
	{
		CurrentState = (State)ar.Serialize((int)CurrentState);
	}

	public float DistanceToSein => Vector3.Distance(m_transform.position, Characters.Sein.Position);

	public void FixedUpdate()
	{
		State currentState = CurrentState;
		if (currentState != State.Activated && RandomizerLocationManager.IsPickupCollected(MoonGuid))
		{
			if (currentState == State.Highlighted)
			{
				Unhighlight();
			}

			if (OnOpenedAction)
			{
				OnOpenedAction.PerformInstantly(null);
			}

			CurrentState = State.Activated;
			return;
		}

		if (currentState != State.Normal)
		{
			if (currentState == State.Highlighted)
			{
				if (DistanceToSein > Radius || OriHasTargets || !Characters.Sein.IsOnGround)
				{
					Unhighlight();
					CurrentState = State.Normal;
				}
				if (Characters.Sein.Controller.CanMove && !Characters.Sein.IsSuspended && Input.SpiritFlame.OnPressed)
				{
					if (Characters.Sein.Inventory.MapStones > 0)
					{
						Characters.Sein.Inventory.MapStones--;
						if (OnOpenedAction)
						{
							OnOpenedAction.Perform(null);
						}
						AchievementsLogic.Instance.OnMapStoneActivated();
						CurrentState = State.Activated;
						RandomizerLocationManager.GivePickup(MoonGuid);
						GameWorld.Instance.CurrentArea.DirtyCompletionAmount();
						return;
					}
					UI.SeinUI.ShakeMapstones();
					if (OnFailAction)
					{
						OnFailAction.Perform(null);
					}
				}
			}
		}
		else if (DistanceToSein < Radius && !OriHasTargets && Characters.Sein.IsOnGround)
		{
			Highlight();
			CurrentState = State.Highlighted;
		}
	}

	public Transform OriTarget;

	public Color OriHoverColor;

	public float Radius = 2f;

	private Transform m_transform;

	public GameWorldArea WorldArea;

	public Texture2D HintTexture;

	public MessageProvider HintMessage;

	private MessageBox m_hint;

	public ActionMethod OriEnterAction;

	public ActionMethod OriExitAction;

	public ActionMethod OnOpenedAction;

	public ActionMethod OnFailAction;

	public float OriDuration = 1f;

	public State CurrentState;

	public enum State
	{
		Normal,
		Highlighted,
		Activated
	}
}
