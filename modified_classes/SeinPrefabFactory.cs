using System;
using UnityEngine;

public class SeinPrefabFactory : SaveSerialize, ISeinReceiver
{
	public new void Awake()
	{
		base.Awake();
		this.Bash = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Bash);
		this.Carry = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Carry);
		this.ChargeJump = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.ChargeJump);
		this.Crouch = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Crouch);
		this.DoubleJump = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.DoubleJump);
		this.Fall = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Fall);
		this.Glide = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Glide);
		this.GrabPushPull = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.GrabPushPull);
		this.GrabWall = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.GrabWall);
		this.Jump = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Jump);
		this.PushAgainstWall = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.PushAgainstWall);
		this.Run = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Run);
		this.Idle = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Idle);
		this.SpiritFlame = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.SpiritFlame);
		this.StandingOnEdge = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.StandingOnEdge);
		this.Stomp = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Stomp);
		this.WallJump = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.WallJump);
		this.WallSlide = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.WallSlide);
		this.Swimming = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Swimming);
		this.SoulFlame = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.SoulFlame);
		this.PickupProcessor = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.PickupProcessor);
		this.Dash = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Dash);
		this.Grenade = new SeinNestedPrefab(this.Sein, this.SeinPrefabSet.Grenade);
		this.Carry.IsInstantiated = true;
		this.Crouch.IsInstantiated = true;
		this.Fall.IsInstantiated = true;
		this.Jump.IsInstantiated = true;
		this.PushAgainstWall.IsInstantiated = true;
		this.Run.IsInstantiated = true;
		this.Idle.IsInstantiated = true;
		this.StandingOnEdge.IsInstantiated = true;
		this.Swimming.IsInstantiated = true;
		this.SoulFlame.IsInstantiated = true;
		this.GrabPushPull.IsInstantiated = true;
		this.SpiritFlame.IsInstantiated = true;
		this.PickupProcessor.IsInstantiated = true;
		this.m_prefabs = new SeinNestedPrefab[]
		{
			this.Bash,
			this.Carry,
			this.ChargeJump,
			this.Crouch,
			this.DoubleJump,
			this.Fall,
			this.Glide,
			this.GrabPushPull,
			this.GrabWall,
			this.Jump,
			this.PushAgainstWall,
			this.Run,
			this.SpiritFlame,
			this.StandingOnEdge,
			this.Stomp,
			this.WallJump,
			this.WallSlide,
			this.Swimming,
			this.SoulFlame,
			this.PickupProcessor,
			this.Dash,
			this.Grenade
		};
	}

	public void Start()
	{
	}

	public void EnsureRightPrefabsAreThereForAbilities()
	{
		this.WallJump.IsInstantiated = this.Sein.PlayerAbilities.WallJump.HasAbility || (RandomizerBonus.EnhancedClimb && this.Sein.PlayerAbilities.Climb.HasAbility);
		this.WallSlide.IsInstantiated = true;
		this.Stomp.IsInstantiated = this.Sein.PlayerAbilities.Stomp.HasAbility;
		this.DoubleJump.IsInstantiated = this.Sein.PlayerAbilities.DoubleJump.HasAbility;
		this.ChargeJump.IsInstantiated = this.Sein.PlayerAbilities.ChargeJump.HasAbility;
		this.GrabWall.IsInstantiated = this.Sein.PlayerAbilities.Climb.HasAbility || (RandomizerBonus.EnhancedWallJump && this.Sein.PlayerAbilities.WallJump.HasAbility);
		this.Bash.IsInstantiated = this.Sein.PlayerAbilities.Bash.HasAbility;
		this.Glide.IsInstantiated = this.Sein.PlayerAbilities.Glide.HasAbility;
		this.Dash.IsInstantiated = this.Sein.PlayerAbilities.Dash.HasAbility;
		this.Grenade.IsInstantiated = this.Sein.PlayerAbilities.Grenade.HasAbility;
	}

	public void PushState()
	{
	}

	public void PopState()
	{
	}

	public override void Serialize(Archive ar)
	{
		try
		{
			foreach (SeinNestedPrefab seinNestedPrefab in this.m_prefabs)
			{
				seinNestedPrefab.IsInstantiated = ar.Serialize(seinNestedPrefab.IsInstantiated);
			}
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
		}
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		this.Sein = sein;
	}

	public SeinCharacter Sein;

	public SeinPrefabSet SeinPrefabSet;

	private SeinNestedPrefab[] m_prefabs = new SeinNestedPrefab[0];

	public SeinNestedPrefab Bash;

	public SeinNestedPrefab Carry;

	public SeinNestedPrefab ChargeJump;

	public SeinNestedPrefab Crouch;

	public SeinNestedPrefab DoubleJump;

	public SeinNestedPrefab Fall;

	public SeinNestedPrefab Glide;

	public SeinNestedPrefab GrabPushPull;

	public SeinNestedPrefab GrabWall;

	public SeinNestedPrefab Jump;

	public SeinNestedPrefab PushAgainstWall;

	public SeinNestedPrefab Run;

	public SeinNestedPrefab Idle;

	public SeinNestedPrefab SpiritFlame;

	public SeinNestedPrefab StandingOnEdge;

	public SeinNestedPrefab Stomp;

	public SeinNestedPrefab WallJump;

	public SeinNestedPrefab WallSlide;

	public SeinNestedPrefab Swimming;

	public SeinNestedPrefab SoulFlame;

	public SeinNestedPrefab Dash;

	public SeinNestedPrefab Grenade;

	public SeinNestedPrefab PickupProcessor;
}
