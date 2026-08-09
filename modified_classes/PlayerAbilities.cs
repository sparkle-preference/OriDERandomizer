using System;
using Game;
using UnityEngine;

public class PlayerAbilities : SaveSerialize, ISeinReceiver {
    public CharacterAbility[] Abilities { get; private set; }

    public int OriStrength {
        get {
            if (UltraSplitFlame.HasAbility) {
                return 3;
            }

            if (CinderFlame.HasAbility) {
                return 2;
            }

            if (SparkFlame.HasAbility) {
                return 1;
            }

            return 0;
        }
    }

    public int SplitFlameTargets {
        get {
            if (UltraSplitFlame.HasAbility) {
                return 4 + RandomizerBonus.SpiritFlameLevel();
            }

            if (SplitFlameUpgrade.HasAbility) {
                return 2 + RandomizerBonus.SpiritFlameLevel();
            }

            return 1 + RandomizerBonus.SpiritFlameLevel();
        }
    }

    public float AttractionDistance {
        get {
            if (Characters.Sein.PlayerAbilities.UltraMagnet.HasAbility) {
                return 200f;
            }

            if (Characters.Sein.PlayerAbilities.Magnet.HasAbility) {
                return 8f;
            }

            return 0f;
        }
    }

    public new void Awake() {
        base.Awake();
        Abilities = new[] {
            Bash,
            ChargeFlame,
            WallJump,
            Stomp,
            DoubleJump,
            ChargeJump,
            Magnet,
            UltraMagnet,
            Climb,
            Glide,
            SpiritFlame,
            RapidFire,
            SoulEfficiency,
            WaterBreath,
            ChargeFlameBlast,
            ChargeFlameBurn,
            DoubleJumpUpgrade,
            BashBuff,
            UltraDefense,
            HealthEfficiency,
            Sense,
            StompUpgrade,
            QuickFlame,
            MapMarkers,
            EnergyEfficiency,
            HealthMarkers,
            EnergyMarkers,
            AbilityMarkers,
            Rekindle,
            Regroup,
            ChargeFlameEfficiency,
            UltraSoulFlame,
            SoulFlameEfficiency,
            SplitFlameUpgrade,
            SparkFlame,
            CinderFlame,
            UltraSplitFlame,
            Dash,
            Grenade,
            GrenadeUpgrade,
            ChargeDash,
            AirDash,
            GrenadeEfficiency
        };
    }

    public void SetAllAbilitys(bool abilityEnabled) {
        foreach (CharacterAbility characterAbility in Abilities) {
            characterAbility.HasAbility = abilityEnabled;
        }

        m_sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
    }

    public override void Serialize(Archive ar) {
        try {
            foreach (CharacterAbility characterAbility in Abilities) {
                ar.Serialize(ref characterAbility.HasAbility);
            }
        } catch (Exception exception) {
            Debug.LogException(exception);
        }

        if (ar.Reading) {
            m_sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
        }
    }

    public void SetAbility(AbilityType ability, bool value) {
        switch (ability) {
            case AbilityType.Bash:
                Bash.HasAbility = value;
                break;
            case AbilityType.ChargeFlame:
                ChargeFlame.HasAbility = value;
                break;
            case AbilityType.WallJump:
                WallJump.HasAbility = value;
                break;
            case AbilityType.Stomp:
                Stomp.HasAbility = value;
                break;
            case AbilityType.DoubleJump:
                DoubleJump.HasAbility = value;
                break;
            case AbilityType.ChargeJump:
                ChargeJump.HasAbility = value;
                break;
            case AbilityType.Magnet:
                Magnet.HasAbility = value;
                break;
            case AbilityType.UltraMagnet:
                UltraMagnet.HasAbility = value;
                break;
            case AbilityType.Climb:
                Climb.HasAbility = value;
                break;
            case AbilityType.Glide:
                Glide.HasAbility = value;
                break;
            case AbilityType.SpiritFlame:
                SpiritFlame.HasAbility = value;
                Characters.Ori.MoveOriToPlayer();
                break;
            case AbilityType.RapidFlame:
                RapidFire.HasAbility = value;
                break;
            case AbilityType.SplitFlameUpgrade:
                SplitFlameUpgrade.HasAbility = value;
                break;
            case AbilityType.SoulEfficiency:
                SoulEfficiency.HasAbility = value;
                break;
            case AbilityType.WaterBreath:
                WaterBreath.HasAbility = value;
                break;
            case AbilityType.ChargeFlameBlast:
                ChargeFlameBlast.HasAbility = value;
                break;
            case AbilityType.ChargeFlameBurn:
                ChargeFlameBurn.HasAbility = value;
                break;
            case AbilityType.DoubleJumpUpgrade:
                DoubleJumpUpgrade.HasAbility = value;
                break;
            case AbilityType.BashBuff:
                BashBuff.HasAbility = value;
                break;
            case AbilityType.UltraDefense:
                UltraDefense.HasAbility = value;
                break;
            case AbilityType.HealthEfficiency:
                HealthEfficiency.HasAbility = value;
                break;
            case AbilityType.Sense:
                Sense.HasAbility = value;
                break;
            case AbilityType.UltraStomp:
                StompUpgrade.HasAbility = value;
                break;
            case AbilityType.SparkFlame:
                SparkFlame.HasAbility = value;
                break;
            case AbilityType.QuickFlame:
                QuickFlame.HasAbility = value;
                break;
            case AbilityType.MapMarkers:
                MapMarkers.HasAbility = value;
                if (value)
                    foreach (var area in GameWorld.Instance.RuntimeAreas)
                        area.DiscoverAllAreas();
                break;
            case AbilityType.EnergyEfficiency:
                EnergyEfficiency.HasAbility = value;
                break;
            case AbilityType.HealthMarkers:
                HealthMarkers.HasAbility = value;
                break;
            case AbilityType.EnergyMarkers:
                EnergyMarkers.HasAbility = value;
                break;
            case AbilityType.AbilityMarkers:
                AbilityMarkers.HasAbility = value;
                break;
            case AbilityType.Rekindle:
                Rekindle.HasAbility = value;
                break;
            case AbilityType.Regroup:
                Regroup.HasAbility = value;
                break;
            case AbilityType.ChargeFlameEfficiency:
                ChargeFlameEfficiency.HasAbility = value;
                break;
            case AbilityType.UltraSoulFlame:
                UltraSoulFlame.HasAbility = value;
                break;
            case AbilityType.SoulFlameEfficiency:
                SoulFlameEfficiency.HasAbility = value;
                break;
            case AbilityType.CinderFlame:
                CinderFlame.HasAbility = value;
                break;
            case AbilityType.UltraSplitFlame:
                UltraSplitFlame.HasAbility = value;
                break;
            case AbilityType.Dash:
                Dash.HasAbility = value;
                break;
            case AbilityType.Grenade:
                Grenade.HasAbility = value;
                break;
            case AbilityType.GrenadeUpgrade:
                GrenadeUpgrade.HasAbility = value;
                break;
            case AbilityType.ChargeDash:
                ChargeDash.HasAbility = value;
                break;
            case AbilityType.AirDash:
                AirDash.HasAbility = value;
                break;
            case AbilityType.GrenadeEfficiency:
                GrenadeEfficiency.HasAbility = value;
                break;
        }

        m_sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
    }

    public bool HasAbility(AbilityType ability) {
        switch (ability) {
            case AbilityType.Bash:
                return Bash.HasAbility;
            case AbilityType.ChargeFlame:
                return ChargeFlame.HasAbility;
            case AbilityType.WallJump:
                return WallJump.HasAbility;
            case AbilityType.Stomp:
                return Stomp.HasAbility;
            case AbilityType.DoubleJump:
                return DoubleJump.HasAbility;
            case AbilityType.ChargeJump:
                return ChargeJump.HasAbility;
            case AbilityType.Magnet:
                return Magnet.HasAbility;
            case AbilityType.UltraMagnet:
                return UltraMagnet.HasAbility;
            case AbilityType.Climb:
                return Climb.HasAbility;
            case AbilityType.Glide:
                return Glide.HasAbility;
            case AbilityType.SpiritFlame:
                return SpiritFlame.HasAbility;
            case AbilityType.RapidFlame:
                return RapidFire.HasAbility;
            case AbilityType.SplitFlameUpgrade:
                return SplitFlameUpgrade.HasAbility;
            case AbilityType.SoulEfficiency:
                return SoulEfficiency.HasAbility;
            case AbilityType.WaterBreath:
                return WaterBreath.HasAbility;
            case AbilityType.ChargeFlameBlast:
                return ChargeFlameBlast.HasAbility;
            case AbilityType.ChargeFlameBurn:
                return ChargeFlameBurn.HasAbility;
            case AbilityType.DoubleJumpUpgrade:
                return DoubleJumpUpgrade.HasAbility;
            case AbilityType.BashBuff:
                return BashBuff.HasAbility;
            case AbilityType.UltraDefense:
                return UltraDefense.HasAbility;
            case AbilityType.HealthEfficiency:
                return HealthEfficiency.HasAbility;
            case AbilityType.Sense:
                return Sense.HasAbility;
            case AbilityType.UltraStomp:
                return StompUpgrade.HasAbility;
            case AbilityType.SparkFlame:
                return SparkFlame.HasAbility;
            case AbilityType.QuickFlame:
                return QuickFlame.HasAbility;
            case AbilityType.MapMarkers:
                return MapMarkers.HasAbility;
            case AbilityType.EnergyEfficiency:
                return EnergyEfficiency.HasAbility;
            case AbilityType.HealthMarkers:
                return HealthMarkers.HasAbility;
            case AbilityType.EnergyMarkers:
                return EnergyMarkers.HasAbility;
            case AbilityType.AbilityMarkers:
                return AbilityMarkers.HasAbility;
            case AbilityType.Rekindle:
                return Rekindle.HasAbility;
            case AbilityType.Regroup:
                return Regroup.HasAbility;
            case AbilityType.ChargeFlameEfficiency:
                return ChargeFlameEfficiency.HasAbility;
            case AbilityType.UltraSoulFlame:
                return UltraSoulFlame.HasAbility;
            case AbilityType.SoulFlameEfficiency:
                return SoulFlameEfficiency.HasAbility;
            case AbilityType.CinderFlame:
                return CinderFlame.HasAbility;
            case AbilityType.UltraSplitFlame:
                return UltraSplitFlame.HasAbility;
            case AbilityType.Dash:
                return Dash.HasAbility;
            case AbilityType.Grenade:
                return Grenade.HasAbility;
            case AbilityType.GrenadeUpgrade:
                return GrenadeUpgrade.HasAbility;
            case AbilityType.ChargeDash:
                return ChargeDash.HasAbility;
            case AbilityType.AirDash:
                return AirDash.HasAbility;
            case AbilityType.GrenadeEfficiency:
                return GrenadeEfficiency.HasAbility;
        }

        return false;
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
        m_sein.PlayerAbilities = this;
    }

    public CharacterAbility Bash;

    public CharacterAbility ChargeFlame;

    public CharacterAbility WallJump;

    public CharacterAbility Stomp;

    public CharacterAbility DoubleJump;

    public CharacterAbility ChargeJump;

    public CharacterAbility Magnet;

    public CharacterAbility UltraMagnet;

    public CharacterAbility Climb;

    public CharacterAbility Glide;

    public CharacterAbility SpiritFlame;

    public CharacterAbility RapidFire;

    public CharacterAbility SoulEfficiency;

    public CharacterAbility WaterBreath;

    public CharacterAbility ChargeFlameBlast;

    public CharacterAbility ChargeFlameBurn;

    public CharacterAbility DoubleJumpUpgrade;

    public CharacterAbility BashBuff;

    public CharacterAbility UltraDefense;

    public CharacterAbility HealthEfficiency;

    public CharacterAbility Sense;

    public CharacterAbility StompUpgrade;

    public CharacterAbility QuickFlame;

    public CharacterAbility MapMarkers;

    public CharacterAbility EnergyEfficiency;

    public CharacterAbility HealthMarkers;

    public CharacterAbility EnergyMarkers;

    public CharacterAbility AbilityMarkers;

    public CharacterAbility Rekindle;

    public CharacterAbility Regroup;

    public CharacterAbility ChargeFlameEfficiency;

    public CharacterAbility UltraSoulFlame;

    public CharacterAbility SoulFlameEfficiency;

    public CharacterAbility SplitFlameUpgrade;

    public CharacterAbility SparkFlame;

    public CharacterAbility CinderFlame;

    public CharacterAbility UltraSplitFlame;

    public CharacterAbility Grenade;

    public CharacterAbility Dash;

    public CharacterAbility GrenadeUpgrade;

    public CharacterAbility ChargeDash;

    public CharacterAbility AirDash;

    public CharacterAbility GrenadeEfficiency;

    public ActionMethod GainAbilityAction;

    private SeinCharacter m_sein;
}
