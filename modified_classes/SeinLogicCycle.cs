using Game;
using UnityEngine;

public class SeinLogicCycle : MonoBehaviour {
    public void Start() {
        Sein = Characters.Sein;
    }

    public SeinMortality Mortality => Sein.Mortality;

    public SeinAbilities Abilities => Sein.Abilities;

    public PlatformBehaviour PlatformBehaviour => Sein.PlatformBehaviour;

    public void FixedUpdate() {
        if (Sein.IsSuspended) {
            return;
        }

        var abilities = Abilities;
        PlatformBehaviour.Gravity.SetStateActive(AllowGravity);
        PlatformBehaviour.GravityToGround.SetStateActive(AllowGravityToGround);
        PlatformBehaviour.InstantStop.SetStateActive(AllowInstantStop);
        PlatformBehaviour.LeftRightMovement.SetStateActive(AllowLeftRightMovement);
        PlatformBehaviour.AirNoDeceleration.SetStateActive(AllowAirNoDeceleration);
        PlatformBehaviour.ApplyFrictionToSpeed.SetStateActive(ApplyFrictionToSpeed);
        abilities.StandardSpiritFlame.SetStateActive(AllowStandardSpiritFlame);
        abilities.Bash.SetStateActive(AllowBash);
        abilities.LookUp.SetStateActive(AllowLooking);
        abilities.Lever.SetStateActive(AllowLever);
        abilities.Footsteps.SetStateActive(AllowFootsteps);
        abilities.SpiritFlameTargetting.SetStateActive(AllowSpiritFlameTargetting);
        abilities.ChargeFlame.SetStateActive(AllowChargeFlame);
        abilities.WallSlide.SetStateActive(AllowWallSlide);
        abilities.Stomp.SetStateActive(AllowStomp);
        abilities.Carry.SetStateActive(AllowCarry);
        abilities.Fall.SetStateActive(AllowFall);
        abilities.GrabBlock.SetStateActive(AllowGrabBlock);
        abilities.Idle.SetStateActive(AllowIdle);
        abilities.Run.SetStateActive(AllowRun);
        abilities.Crouch.SetStateActive(AllowCrouching);
        abilities.GrabWall.SetStateActive(AllowWallGrabbing);
        abilities.Jump.SetStateActive(AllowJumping);
        abilities.DoubleJump.SetStateActive(AllowDoubleJump);
        abilities.Glide.SetStateActive(AllowGliding);
        abilities.WallJump.SetStateActive(AllowWallJump);
        abilities.ChargeJumpCharging.SetStateActive(AllowChargeJumpCharging);
        abilities.ChargeJump.SetStateActive(AllowChargeJump);
        abilities.WallChargeJump.SetStateActive(AllowWallChargeJump);
        abilities.StandingOnEdge.SetStateActive(AllowStandingOnEdge);
        abilities.PushAgainstWall.SetStateActive(AllowPushAgainstWall);
        abilities.EdgeClamber.SetStateActive(AllowEdgeClamber);
        Mortality.CrushDetector.SetStateActive(AllowCrushDetector);
        PlatformBehaviour.Visuals.SpriteRotater.SetStateActive(AllowSpriteRotater);
        Mortality.DamageReciever.SetStateActive(AllowDamageReciever);
        abilities.Invincibility.SetStateActive(AllowInvincibility);
        PlatformBehaviour.JumpSustain.SetStateActive(AllowJumpSustain);
        PlatformBehaviour.UpwardsDeceleration.SetStateActive(AllowUpwardsDeceleration);
        Sein.ForceController.SetStateActive(AllowForceController);
        abilities.Swimming.SetStateActive(AllowSwimming);
        abilities.Dash.SetStateActive(AllowDash);
        abilities.Grenade.SetStateActive(AllowGrenade);
        Sein.SoulFlame.SetStateActive(true);
        CharacterState.UpdateCharacterState(Mortality.CrushDetector);
        CharacterState.UpdateCharacterState(Mortality.DamageReciever);
        CharacterState.UpdateCharacterState(PlatformBehaviour.Gravity);
        CharacterState.UpdateCharacterState(PlatformBehaviour.GravityToGround);
        CharacterState.UpdateCharacterState(PlatformBehaviour.InstantStop);
        CharacterState.UpdateCharacterState(Abilities.Carry);
        CharacterState.UpdateCharacterState(Abilities.GrabBlock);
        CharacterState.UpdateCharacterState(Abilities.SpiritFlameTargetting);
        CharacterState.UpdateCharacterState(Abilities.SpiritFlame);
        CharacterState.UpdateCharacterState(Abilities.ChargeFlame);
        CharacterState.UpdateCharacterState(Abilities.StandardSpiritFlame);
        CharacterState.UpdateCharacterState(Abilities.IceSpiritFlame);
        CharacterState.UpdateCharacterState(Abilities.StandingOnEdge);
        CharacterState.UpdateCharacterState(Abilities.Glide);
        CharacterState.UpdateCharacterState(Abilities.Bash);
        CharacterState.UpdateCharacterState(Abilities.WallJump);
        CharacterState.UpdateCharacterState(Abilities.EdgeClamber);
        CharacterState.UpdateCharacterState(Abilities.DoubleJump);
        CharacterState.UpdateCharacterState(Abilities.ChargeJumpCharging);
        CharacterState.UpdateCharacterState(Abilities.ChargeJump);
        CharacterState.UpdateCharacterState(Abilities.WallChargeJump);
        CharacterState.UpdateCharacterState(Abilities.Jump);
        CharacterState.UpdateCharacterState(Abilities.Fall);
        CharacterState.UpdateCharacterState(Abilities.PushAgainstWall);
        CharacterState.UpdateCharacterState(PlatformBehaviour.AirNoDeceleration);
        CharacterState.UpdateCharacterState(PlatformBehaviour.ApplyFrictionToSpeed);
        CharacterState.UpdateCharacterState(Abilities.Crouch);
        CharacterState.UpdateCharacterState(Abilities.Invincibility);
        CharacterState.UpdateCharacterState(Abilities.Run);
        CharacterState.UpdateCharacterState(Abilities.Idle);
        CharacterState.UpdateCharacterState(Abilities.LookUp);
        CharacterState.UpdateCharacterState(Abilities.GrabWall);
        CharacterState.UpdateCharacterState(Abilities.Footsteps);
        CharacterState.UpdateCharacterState(Sein.Abilities.Lever);
        CharacterState.UpdateCharacterState(PlatformBehaviour.JumpSustain);
        CharacterState.UpdateCharacterState(PlatformBehaviour.UpwardsDeceleration);
        CharacterState.UpdateCharacterState(Sein.ForceController);
        CharacterState.UpdateCharacterState(Abilities.WallSlide);
        CharacterState.UpdateCharacterState(Abilities.Stomp);
        CharacterState.UpdateCharacterState(Abilities.Swimming);
        CharacterState.UpdateCharacterState(PlatformBehaviour.Visuals.SpriteRotater);
        CharacterState.UpdateCharacterState(Sein.SoulFlame);
        CharacterState.UpdateCharacterState(Abilities.Dash);
        CharacterState.UpdateCharacterState(Abilities.Grenade);
        Sein.Controller.HandleOffscreenIssue();
    }

    public bool AllowInvincibility => true;

    public bool AllowAirNoDeceleration => true;

    public bool ApplyFrictionToSpeed => true;

    public bool AllowSpiritFlameTargetting => Sein.PlayerAbilities.SpiritFlame.HasAbility && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsBashing;

    public bool AllowCrushDetector => !Sein.Controller.IsPlayingAnimation;

    public bool AllowSpriteRotater => true;

    public bool AllowDamageReciever => !Sein.Controller.IsPlayingAnimation;

    public bool AllowJumpSustain => !Sein.Controller.IsPlayingAnimation;

    public bool AllowUpwardsDeceleration => !Sein.Controller.IsPlayingAnimation;

    public bool AllowForceController => !Sein.Controller.IsPlayingAnimation;

    public bool AllowGravity => !Sein.Controller.IsPlayingAnimation;

    public bool AllowGravityToGround => !Sein.Controller.IsSwimming && !Sein.Controller.IsPlayingAnimation;

    public bool AllowSwimming => true;

    public bool AllowDash => !RandomizerBonus.Swimming() && !Sein.Controller.IsGrabbingLever && !Sein.Controller.IsCarrying && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsPushPulling && !Sein.Controller.IsAimingGrenade && !Sein.Controller.IsStomping && !Sein.Controller.IsBashing && !SeinAbilityRestrictZone.IsInside() && !SeinAbilityRestrictZone.IsInside(SeinAbilityRestrictZoneMode.Dash) && Sein.Controller.CanMove;

    public bool AllowGrenade => !RandomizerBonus.Swimming() && !Sein.Controller.IsGrabbingLever && !Sein.Controller.IsCarrying && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsPushPulling && !SeinAbilityRestrictZone.IsInside() && Sein.Controller.CanMove && !Sein.Controller.IsBashing && !Sein.Controller.IsStandingOnEdge && !Sein.Controller.IsDashing;

    public bool AllowInstantStop => !Sein.Controller.IsSwimming && !Sein.Controller.IsPlayingAnimation;

    public bool AllowLeftRightMovement => !Sein.Controller.IsPlayingAnimation && (!Sein.Controller.IsSwimming || !Sein.Abilities.Swimming.IsUnderwater);

    public bool AllowBash => Sein.PlayerAbilities.Bash.HasAbility && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsPushPulling && !Sein.Controller.IsGrabbingLever && !Sein.Controller.IsAimingGrenade;

    public bool AllowLooking => !Sein.Controller.IsSwimming && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsAimingGrenade;

    public bool AllowLever => !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsPushPulling && !Sein.Controller.IsSwimming && !Sein.Controller.IsBashing && !Sein.Controller.IsStomping && !Sein.Controller.IsAimingGrenade;

    public bool AllowFootsteps => !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsSwimming;

    public bool AllowStandardSpiritFlame => Sein.PlayerAbilities.SpiritFlame.HasAbility && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsBashing;

    public bool AllowChargeFlame => Sein.PlayerAbilities.ChargeFlame.HasAbility && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsBashing;

    public bool AllowWallSlide => !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsCarrying && !Sein.Controller.IsSwimming && !Sein.Controller.IsBashing && !Sein.Controller.IsGliding && !Sein.Controller.IsStomping;

    public bool AllowStomp => Sein.PlayerAbilities.Stomp.HasAbility && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsCarrying && !Sein.Controller.IsBashing && !Sein.Controller.IsGrabbingWall && !Sein.Controller.IsAimingGrenade;

    public bool AllowCarry => !Sein.Controller.IsSwimming && !Sein.Controller.IsBashing && !Sein.Controller.IsStomping && !Sein.Controller.IsAimingGrenade;

    public bool AllowFall => !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsSwimming && !Sein.Controller.IsBashing;

    public bool AllowGrabBlock => !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsBashing && !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsStomping && !Sein.Controller.IsAimingGrenade;

    public bool AllowIdle => !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsCarrying && !Sein.Controller.IsSwimming && !Sein.Controller.IsBashing && !Sein.Controller.IsPushPulling;

    public bool AllowRun => !Sein.Controller.IsCarrying && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsSwimming && !Sein.Controller.IsBashing && !Sein.Controller.IsPushPulling;

    public bool AllowCrouching => !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsAimingGrenade && !Sein.Controller.IsDashing;

    public bool AllowWallGrabbing => (Sein.PlayerAbilities.Climb.HasAbility || (RandomizerBonus.EnhancedWallJump && Sein.PlayerAbilities.WallJump.HasAbility)) && !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsBashing && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation;

    public bool AllowJumping => !Sein.Controller.IsSwimming && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation;

    public bool AllowDoubleJump => Sein.PlayerAbilities.DoubleJump.HasAbility && !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation;

    public bool AllowGliding => Sein.PlayerAbilities.Glide.HasAbility && !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsGrabbingWall && !Sein.Controller.IsBashing && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsDashing;

    public bool AllowWallJump => (Sein.PlayerAbilities.WallJump.HasAbility || (RandomizerBonus.EnhancedClimb && Sein.PlayerAbilities.Climb.HasAbility)) && !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsGliding && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation;

    public bool AllowChargeJumpCharging => AllowChargeJump || AllowDash;

    public bool AllowChargeJump => Sein.PlayerAbilities.ChargeJump.HasAbility && !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsAimingGrenade;

    public bool AllowWallChargeJump => !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsAimingGrenade;

    public bool AllowStandingOnEdge => !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsStomping && !Sein.Controller.IsPlayingAnimation && !Sein.Controller.IsAimingGrenade;

    public bool AllowPushAgainstWall => !Sein.Controller.IsSwimming && !Sein.Controller.IsCarrying && !Sein.Controller.IsPlayingAnimation;

    public bool AllowEdgeClamber => !Sein.Controller.IsCarrying && !Sein.Controller.IsSwimming && !Sein.Controller.IsPlayingAnimation;

    public SeinCharacter Sein;
}
