using Game;
using UnityEngine;

public class SeinCharacter : MonoBehaviour, ICharacter
{
	public Vector2 PhysicsSpeed
	{
		get
		{
			var platformMovement = PlatformBehaviour.PlatformMovement;
			return !platformMovement.IsOnGround ? platformMovement.WorldSpeed : platformMovement.GroundNormal * platformMovement.LocalSpeedY + platformMovement.GroundBinormal * platformMovement.LocalSpeedX;
		}
	}

	public CharacterAnimationSystem Animation => PlatformBehaviour.Visuals.Animation;

	public bool IsSuspended => PlatformBehaviour.PlatformMovement.IsSuspended;

	public Vector3 Position
	{
		get => PlatformBehaviour.PlatformMovement.Position;
		set => PlatformBehaviour.PlatformMovement.Position = value;
	}

	public bool Active
	{
		get => gameObject.activeSelf;
		set => gameObject.SetActive(value);
	}

	public void Awake()
	{
		Characters.Sein = this;
		Characters.Current = this;
		Input = new SeinInput(this);
		MakeBelongToSein(gameObject);
	}

	public void OnDestroy()
	{
		if (Characters.Sein == this)
		{
			Characters.Sein = null;
		}
		if (ReferenceEquals(Characters.Current, this))
		{
			Characters.Current = null;
		}
	}

	public void MakeBelongToSein(GameObject go)
	{
		go.BroadcastMessage("SetReferenceToSein", this, SendMessageOptions.DontRequireReceiver);
	}

	public void FixedUpdate()
	{
		Input.Update();
	}

	public void Activate(bool active)
	{
		gameObject.SetActive(active);
		if (active)
		{
			gameObject.BroadcastMessage("SetReferenceToSein", this, SendMessageOptions.DontRequireReceiver);
		}
	}

	public GameObject GameObject => gameObject;

	public bool FaceLeft
	{
		get => Animation.SpriteMirror.FaceLeft;
		set => Animation.SpriteMirror.FaceLeft = value;
	}

	public Vector3 Speed
	{
		get => PlatformBehaviour.PlatformMovement.LocalSpeed;
		set => PlatformBehaviour.PlatformMovement.LocalSpeed = value;
	}

	public Transform Transform => transform;

	public bool IsOnGround => PlatformBehaviour.PlatformMovement.IsOnGround;

	public void PlaceOnGround()
	{
		PlatformBehaviour.PlatformMovement.PlaceOnGround(0.5f, 0f);
	}

	public void ResetAirLimits()
	{
		if (Abilities.DoubleJump)
		{
			Abilities.DoubleJump.ResetDoubleJump();
		}
		if (Abilities.Dash)
		{
			Abilities.Dash.ResetDashLimit();
		}
	}

	public SeinAbilities Abilities;

	public CloneOfSeinForPortals CloneOfSeinForPortals;

	public SeinController Controller;

	public SeinCutsceneBlocked CutsceneBlocked;

	public SeinCutsceneMovement CutsceneMovement;

	public SeinDoorHandler DoorHandler;

	public SeinSoulFlame SoulFlame;

	public SeinInventory Inventory;

	public SeinEnvironmentForceController ForceController;

	public SeinInput Input;

	public SeinLevel Level;

	public SeinEnergy Energy;

	public SeinMortality Mortality;

	public SeinPickupProcessor PickupHandler;

	public PlatformBehaviour PlatformBehaviour;

	public PlayerAbilities PlayerAbilities;

	public SeinPrefabFactory Prefabs;
}
