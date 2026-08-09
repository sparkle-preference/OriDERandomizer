using System;
using UnityEngine;

public class SeinPrefabFactory : SaveSerialize, ISeinReceiver {
    public new void Awake() {
        base.Awake();
        Bash = new SeinNestedPrefab(Sein, SeinPrefabSet.Bash);
        Carry = new SeinNestedPrefab(Sein, SeinPrefabSet.Carry);
        ChargeJump = new SeinNestedPrefab(Sein, SeinPrefabSet.ChargeJump);
        Crouch = new SeinNestedPrefab(Sein, SeinPrefabSet.Crouch);
        DoubleJump = new SeinNestedPrefab(Sein, SeinPrefabSet.DoubleJump);
        Fall = new SeinNestedPrefab(Sein, SeinPrefabSet.Fall);
        Glide = new SeinNestedPrefab(Sein, SeinPrefabSet.Glide);
        GrabPushPull = new SeinNestedPrefab(Sein, SeinPrefabSet.GrabPushPull);
        GrabWall = new SeinNestedPrefab(Sein, SeinPrefabSet.GrabWall);
        Jump = new SeinNestedPrefab(Sein, SeinPrefabSet.Jump);
        PushAgainstWall = new SeinNestedPrefab(Sein, SeinPrefabSet.PushAgainstWall);
        Run = new SeinNestedPrefab(Sein, SeinPrefabSet.Run);
        Idle = new SeinNestedPrefab(Sein, SeinPrefabSet.Idle);
        SpiritFlame = new SeinNestedPrefab(Sein, SeinPrefabSet.SpiritFlame);
        StandingOnEdge = new SeinNestedPrefab(Sein, SeinPrefabSet.StandingOnEdge);
        Stomp = new SeinNestedPrefab(Sein, SeinPrefabSet.Stomp);
        WallJump = new SeinNestedPrefab(Sein, SeinPrefabSet.WallJump);
        WallSlide = new SeinNestedPrefab(Sein, SeinPrefabSet.WallSlide);
        Swimming = new SeinNestedPrefab(Sein, SeinPrefabSet.Swimming);
        SoulFlame = new SeinNestedPrefab(Sein, SeinPrefabSet.SoulFlame);
        PickupProcessor = new SeinNestedPrefab(Sein, SeinPrefabSet.PickupProcessor);
        Dash = new SeinNestedPrefab(Sein, SeinPrefabSet.Dash);
        Grenade = new SeinNestedPrefab(Sein, SeinPrefabSet.Grenade);
        Carry.IsInstantiated = true;
        Crouch.IsInstantiated = true;
        Fall.IsInstantiated = true;
        Jump.IsInstantiated = true;
        PushAgainstWall.IsInstantiated = true;
        Run.IsInstantiated = true;
        Idle.IsInstantiated = true;
        StandingOnEdge.IsInstantiated = true;
        Swimming.IsInstantiated = true;
        SoulFlame.IsInstantiated = true;
        GrabPushPull.IsInstantiated = true;
        SpiritFlame.IsInstantiated = true;
        PickupProcessor.IsInstantiated = true;
        m_prefabs = new[] {
            Bash,
            Carry,
            ChargeJump,
            Crouch,
            DoubleJump,
            Fall,
            Glide,
            GrabPushPull,
            GrabWall,
            Jump,
            PushAgainstWall,
            Run,
            SpiritFlame,
            StandingOnEdge,
            Stomp,
            WallJump,
            WallSlide,
            Swimming,
            SoulFlame,
            PickupProcessor,
            Dash,
            Grenade
        };
    }

    public void Start() {
    }

    public void EnsureRightPrefabsAreThereForAbilities() {
        WallJump.IsInstantiated = Sein.PlayerAbilities.WallJump.HasAbility;
        WallSlide.IsInstantiated = true;
        Stomp.IsInstantiated = Sein.PlayerAbilities.Stomp.HasAbility;
        DoubleJump.IsInstantiated = Sein.PlayerAbilities.DoubleJump.HasAbility;
        ChargeJump.IsInstantiated = Sein.PlayerAbilities.ChargeJump.HasAbility;
        GrabWall.IsInstantiated = Sein.PlayerAbilities.Climb.HasAbility || (RandomizerBonus.EnhancedWallJump && Sein.PlayerAbilities.WallJump.HasAbility);
        Bash.IsInstantiated = Sein.PlayerAbilities.Bash.HasAbility;
        Glide.IsInstantiated = Sein.PlayerAbilities.Glide.HasAbility;
        Dash.IsInstantiated = Sein.PlayerAbilities.Dash.HasAbility;
        Grenade.IsInstantiated = Sein.PlayerAbilities.Grenade.HasAbility;
    }

    public void PushState() {
    }

    public void PopState() {
    }

    public override void Serialize(Archive ar) {
        try {
            foreach (SeinNestedPrefab seinNestedPrefab in m_prefabs) {
                seinNestedPrefab.IsInstantiated = ar.Serialize(seinNestedPrefab.IsInstantiated);
            }
        } catch (Exception exception) {
            Debug.LogException(exception);
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
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
