using System;
using System.Collections.Generic;
using Game;
using Sein.World;
using UnityEngine;

public static class RandomizerBonus
{
    // this instead of a default argument bc dnspy hates us and there is no light nor love
    public static void UpgradeID(int ID) => UpgradeID(ID, -1);

    public static void UpgradeID(int ID, int coords)
    {
        bool flag = ID < 0;
        if (flag)
        {
            ID = -ID;
        }
        if (RandomizerBonusSkill.BonusSkillNames.ContainsKey(ID))
        {
            RandomizerBonusSkill.FoundBonusSkill(ID);
            return;
        }

        // keysanity
        if (ID >= 300 && ID < 312) {
            if(flag)
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            else
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            Randomizer.Keysanity.ShowPickupHint(ID, coords);
            return;
        }

        // keysanity door hints: 313-324 act like touching door 300-311
        // (unlock + show that door's zone hints). Not revocable.
        if (ID >= 313 && ID < 325) {
            if(flag)
                return;
            Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            Randomizer.Keysanity.UnlockDoorHint(ID - 13);
            return;
        }

        if(ID >= 200 && ID < 260)
        {
            int abilityId = (ID - 200) % 30;
            Ability ability = abilities[abilityId];
            if (ID < 230)
                ability.Found();
            else
                ability.Lost();
            return;
        }
        switch (ID)
        {
        case 0:
            if (!flag)
            {
                Characters.Sein.Mortality.Health.SetAmount(Characters.Sein.Mortality.Health.MaxHealth + 20);
                RandomizerSwitch.PickupMessage("Mega Health");
            }
            break;
        case 1:
            if (!flag)
            {
                Characters.Sein.Energy.SetCurrent(Characters.Sein.Energy.Max + 5f);
                RandomizerSwitch.PickupMessage("Mega Energy");
            }
            break;
        case 2:
            Randomizer.returnToStart();
            RandomizerSwitch.PickupMessage("Go Home!");
            return;
        case 20:
            break;
        case 6:
            if (!flag)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("Attack Upgrade (" + SpiritFlameLevel() + ")");
                return;
            }
            if (SpiritFlameLevel() > 0)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                RandomizerSwitch.PickupMessage("Attack Upgrade (" + SpiritFlameLevel() + ")");
            }
            break;
        case 8:
            RandomizerSwitch.PickupMessage("Explosion Power Upgrade");
            if (!ExplosionPower())
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            }
            break;
        case 9:
            if (!flag)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            }
            else if (Characters.Sein.Inventory.GetRandomizerItem(ID) > 0)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            }
            if (Characters.Sein.Inventory.GetRandomizerItem(ID) == 1)
                RandomizerSwitch.PickupMessage("Spirit Light Efficiency");
            else
                RandomizerSwitch.PickupMessage("Spirit Light Efficiency (" + Characters.Sein.Inventory.GetRandomizerItem(ID) + ")");
            break;
        case 10:
            RandomizerSwitch.PickupMessage("Extra Air Dash");
            if (!DoubleAirDash())
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
            }
            break;
        case 11:
            RandomizerSwitch.PickupMessage("Charge Dash Efficiency");
            if (!ChargeDashEfficiency())
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
            }
            break;
        case 12:
            if (!flag)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                if (DoubleJumpUpgrades() == 1)
                {
                    RandomizerSwitch.PickupMessage("Extra Double Jump");
                    return;
                }
                RandomizerSwitch.PickupMessage("Extra Double Jump (" + DoubleJumpUpgrades() + ")");
            }
            else if (DoubleJumpUpgrades() > 0)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                if (DoubleJumpUpgrades() == 1)
                {
                    RandomizerSwitch.PickupMessage("Extra Double Jump");
                    return;
                }
                RandomizerSwitch.PickupMessage("Extra Double Jump (" + DoubleJumpUpgrades() + ")");
            }
            break;
        case 13:
            if (!flag)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("Health Regeneration (" + HealthRegeneration() + ")");
                return;
            }
            if (HealthRegeneration() > 0)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                RandomizerSwitch.PickupMessage("Health Regeneration (" + HealthRegeneration() + ")");
            }
            break;
        case 15:
            if (!flag)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("Energy Regeneration (" + EnergyRegeneration() + ")");
                return;
            }
            if (EnergyRegeneration() > 0)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                RandomizerSwitch.PickupMessage("Energy Regeneration (" + EnergyRegeneration() + ")");
            }
            break;
        case 17:
            if (flag)
            {
                if (WaterVeinShards() > 0)
                {
                    Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                    RandomizerSwitch.PickupMessage("*Water Vein Shard (" + WaterVeinShards() + "/3)*");
                }
            }
            else {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("*Water Vein Shard (" + WaterVeinShards() + "/3)*", 300);
            }
            Keys.GinsoTree = WaterVeinShards() >= 3;
            if(Keys.GinsoTree) 
                RandomizerStatsManager.FoundEvent(0);
            return;
        case 19:
            if (flag)
            {
                if (GumonSealShards() > 0)
                {
                    Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                    RandomizerSwitch.PickupMessage("#Gumon Seal Shard (" + GumonSealShards() + "/3)#");
                }
            }
            else {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("#Gumon Seal Shard (" + GumonSealShards() + "/3)#", 300);
            }
            Keys.ForlornRuins = GumonSealShards() >= 3;
            if(Keys.ForlornRuins) 
                RandomizerStatsManager.FoundEvent(2);
            return;
        case 21:
            if (flag)
            {
                if (SunstoneShards() > 0)
                {
                    Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                    RandomizerSwitch.PickupMessage("@Sunstone Shard (" + SunstoneShards() + "/3)@");
                }
            }
            else {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("@Sunstone Shard (" + SunstoneShards() + "/3)@", 300);
            }
            Keys.MountHoru = SunstoneShards() >= 3;
            if(Keys.MountHoru) 
                RandomizerStatsManager.FoundEvent(4);
            return;
        case 28:
            if (!flag)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            }else if (WarmthFrags() > 0)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            }
            if(Randomizer.fragKeyFinish < WarmthFrags())
            {
                RandomizerSwitch.PickupMessage("@Warmth Fragment (extra)@", 300);
                return;
            }
            RandomizerSwitch.PickupMessage(string.Concat("@Warmth Fragment (", WarmthFrags().ToString(), "/", Randomizer.fragKeyFinish, ")@"), 300);
            break;
        case 29:
            return;
        case 30:
            if (!flag)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("Bleeding x" + Bleeding());
                return;
            }
            if (Bleeding() > 0)
            {
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
                RandomizerSwitch.PickupMessage("Bleeding x" + Bleeding());
            }
            break;
        case 31:
            if (!flag)
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            else if (Lifesteal() > 0)
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            if(Lifesteal() == 1)
                RandomizerSwitch.PickupMessage("Health Leech");
            else
                RandomizerSwitch.PickupMessage("Health Leech x" + Lifesteal());
            break;
        case 32:
            if (!flag)
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            else if (Manavamp() > 0)
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            if(Manavamp() == 1)
                RandomizerSwitch.PickupMessage("Energy Leech");
            else
                RandomizerSwitch.PickupMessage("Energy Leech x" + Manavamp());
            break;
        case 33:
            int v;
            if (!flag)
            {
                v = Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("Skill Velocity Upgrade x" + v);
                if(Characters.Sein.Inventory.GetRandomizerItem(108) == 0) 
                    RandomizerBonusSkill.FoundBonusSkill(108);
                return;
            }
            v = Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            RandomizerSwitch.PickupMessage("Skill Velocity Upgrade x" + v);
            break;
        case 34:
            Characters.Sein.Inventory.SetRandomizerItem(34, 1);
            RandomizerSwitch.PickupMessage("Return to start disabled!");
        break;
        case 35:
            Characters.Sein.Inventory.SetRandomizerItem(34, 0);
            RandomizerSwitch.PickupMessage("Return to start enabled!");
        break;
        case 36:
            RandomizerSwitch.PickupMessage("Underwater Skill Usage");
            Characters.Sein.Inventory.SetRandomizerItem(36, 1);
            break;
        case 37:
            int j;
            if (!flag)
            {
                j = Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("Jump Upgrade x" + j);
                if(Characters.Sein.Inventory.GetRandomizerItem(108) == 0) 
                    RandomizerBonusSkill.FoundBonusSkill(108);
                return;
            }
            j = Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            RandomizerSwitch.PickupMessage("Jump Upgrade x" + j);
            break;
        case 40:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Wall Jump Lost!!@", 240);
            Characters.Sein.PlayerAbilities.WallJump.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 41:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@ChargeFlame Lost!!@", 240);
            Characters.Sein.PlayerAbilities.ChargeFlame.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 42:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@DoubleJump Lost!!@", 240);
            Characters.Sein.PlayerAbilities.DoubleJump.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 43:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Bash Lost!!@", 240);
            Characters.Sein.PlayerAbilities.Bash.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 44:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Stomp Lost!!@", 240);
            Characters.Sein.PlayerAbilities.Stomp.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 45:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Glide Lost!!@", 240);
            Characters.Sein.PlayerAbilities.Glide.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 46:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Climb Lost!!@", 240);
            Characters.Sein.PlayerAbilities.Climb.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 47:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Charge Jump Lost!!@", 240);
            Characters.Sein.PlayerAbilities.ChargeJump.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 48:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Dash Lost!!@", 240);
            Characters.Sein.PlayerAbilities.Dash.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 49:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Grenade Lost!!@", 240);
            Characters.Sein.PlayerAbilities.Grenade.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 50:
            if (!Characters.Sein || flag)
                return;
            RandomizerSwitch.PickupMessage("@Sein Lost!!@", 240);
            Characters.Sein.PlayerAbilities.SpiritFlame.HasAbility = false;
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
            return;
        case 81:
            if(Characters.Sein.Inventory.GetRandomizerItem(ID) > 0)
                return;
            Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            string s_color = "";
            string g_color = "";
            if(Characters.Sein.PlayerAbilities.HasAbility(AbilityType.Stomp))
                s_color = "$";
            if(Characters.Sein.PlayerAbilities.HasAbility(AbilityType.Grenade))
                g_color = "$";
            RandomizerSwitch.PickupMessage(s_color + "Stomp: " + Randomizer.StompZone + s_color + g_color+ "    Grenade: "+ Randomizer.GrenadeZone + g_color, 480);
            break;
        case 410:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $S$*p*#i#$r$*i*#t# $F$*l*#a#$m$*e*\nSein's voice has been restored!", 300);
            }
            break;
        case 411:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $W$*a*#l#$l$ *J*#u#$m$*p*\nNow comes with bonus Climb! ...yeah, sorry, that's it for this one.", 300);
            }
            break;
        case 412:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $C$*h*#a#$r$*g*#e# $F$*l*#a#$m$*e*\nPowerful enough to capture nearby projectiles in its gravity!", 300);
            }
            break;
        case 413:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $D$*o*#u#$b$*l*#e# $J$*u*#m#$p$\nLook, we were running out of ideas, so just...have infinite jumps, I guess.", 300);
            }
            break;
        case 414:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $B$*a*#s#$h$\nMuch more balanced now that it doesn't require a target.", 300);
            }
            break;
        case 415:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $S$*t*#o#$m$*p*\nWhy should we be limited to only stomping downwards?", 300);
            }
            break;
        case 416:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $G$*l*#i#$d$*e*\nYou are the wind beneath your wings. Er...feather.", 300);
            }
            break;
        case 417:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $C$*l*#i#$m$*b*\nNow comes with protective gear for hazardous surfaces!", 300);
            }
            break;
        case 418:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $C$*h*#a#$r$*g*#e# $J$*u*#m#$p$\nYour very aura is now enough to protect from enemies. Or kill them.", 300);
            }
            break;
        case 419:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $D$*a*#s#$h$\nBe freed from the limitations of the horizontal axis!", 300);
            }
            break;
        case 420:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $G$*r*#e#$n$*a*#d#$e$\nI guess it can break floors and walls now? For some reason?", 300);
            }
            break;
        case 422:
            if (!flag)
            {
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                RandomizerBonusSkill.FoundBonusSkill(115);
                RandomizerSwitch.PickupMessage("*E*#n#$h$*a*#n#$c$*e*#d# $C$*l*#e#$a$*n* #W#$a$*t*#e#$r$\nIt isn't just clean - it's been cleaned out!", 300);
            }
            break;
        case 1102:
            if (!flag)
                Characters.Sein.Inventory.SetRandomizerItem(ID, 1);
                return;
            Characters.Sein.Inventory.SetRandomizerItem(ID, 0);
            return;
        default:
            if(flag)
                Characters.Sein.Inventory.IncRandomizerItem(ID, -1);
            else
                Characters.Sein.Inventory.IncRandomizerItem(ID, 1);
            return;
        }
    }

    public static bool SenseFragsEnabled => Characters.Sein.Inventory.GetRandomizerItem(1100) > 0;
    public static bool SenseFragsActive {
        get => SenseFragsEnabled && Characters.Sein.Inventory.GetRandomizerItem(1101) > 0;
        set => Characters.Sein.Inventory.SetRandomizerItem(1101, value ? 1 : 0);
    }

    public static bool ForlornEscapeHint()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(81) > 0;
    }

    public static bool DoubleAirDash()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(10) > 0;
    }

    public static bool ChargeDashEfficiency()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(11) > 0;
    }

    public static bool DoubleJumpUpgrade()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(12) > 0;
    }

    public static int HealthRegeneration()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(13);
    }

    public static int EnergyRegeneration()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(15);
    }

    public static int WaterVeinShards()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(17);
    }

    public static int SunstoneShards()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(21);
    }

    public static int GumonSealShards()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(19);
    }

    public static int SpiritFlameLevel()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(6);
    }

    public static int MapStoneProgression()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(23);
    }

    public static int SkillTreeProgression()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(27);
    }

    public static bool ExplosionPower()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(8) > 0;
    }

    public static int ExpWithBonuses(int baseExp, bool doTrack)
    {
        float mult = 1.0f + Characters.Sein.Inventory.GetRandomizerItem(9);
        if(Characters.Sein.PlayerAbilities.AbilityMarkers.HasAbility) 
            mult += .5f;
        if(Characters.Sein.PlayerAbilities.SoulEfficiency.HasAbility)
            mult += .5f;
        int total = (int)(baseExp*mult);
        if(doTrack)
        {
            RandomizerStatsManager.OnExp(baseExp, total-baseExp);
            BingoController.OnExp(total);
        }
        return total;
    }

    public static int GetPickupCount()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(0);
    }

    public static int DoubleJumpUpgrades()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(12);
    }

    public static int UpgradeCount(int ID)
    {
        if(ID >= 200 && ID <= 230) {
            int abilityId = (ID - 200) % 30;
            Ability ability = abilities[abilityId];
            return ability.Has() ? 1 : 0;            
        }
        if(ID == 17 || ID == 19 || ID == 21)
            return Math.Min(Characters.Sein.Inventory.GetRandomizerItem(ID), 3); 
        return Characters.Sein.Inventory.GetRandomizerItem(ID);
    }

    public static void CollectMapstone()
    {
        Characters.Sein.Inventory.IncRandomizerItem(23, 1);
    }

    public static void Update()
    {
        int healthRegenLevel = HealthRegeneration() + (Characters.Sein.PlayerAbilities.HealthMarkers.HasAbility ? 2 : 0) - Bleeding();
        int energyRegenLevel = EnergyRegeneration() + (Characters.Sein.PlayerAbilities.EnergyMarkers.HasAbility ? 2 : 0);

        if (healthRegenLevel > 0)
        {
            Characters.Sein.Mortality.Health.GainHealth(healthRegenLevel * HealthRegenAmount * Time.deltaTime / HealthRegenTimeSeconds);
        }
        else if (healthRegenLevel < 0)
        {
            Characters.Sein.Mortality.Health.LoseHealth(-healthRegenLevel * HealthRegenAmount * Time.deltaTime / HealthRegenTimeSeconds);
        }
        if (Bleeding() > 0 && Characters.Sein.Mortality.Health.Amount <= 0f)
        {
            Characters.Sein.Mortality.DamageReciever.OnRecieveDamage(new Damage(1f, default(Vector2), default(Vector3), DamageType.Water, null));
        }
        Characters.Sein.Energy.Gain(energyRegenLevel * EnergyRegenAmount * Time.deltaTime / EnergyRegenTimeSeconds);
        RandomizerBonusSkill.Update();
    }

    public static void DamageDealt(float damage)
    {
        if (Characters.Sein)
        {
            if (damage > 20f)
            {
                damage = 20f;
            }
            Characters.Sein.Mortality.Health.GainHealth(Lifesteal() * 0.2f * damage);
            Characters.Sein.Energy.Gain(Manavamp() * 0.05f * damage);
        }
    }

    public static int Bleeding()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(30);
    }

    public static bool ExpEfficiency()
    {
        return false;
    }

    public static int Lifesteal()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(31);
    }

    public static int Manavamp()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(32);
    }

    public static int Velocity()
    {
        try {
            if(RandomizerBonusSkill.IsActive(108))
                return 0;
            return Characters.Sein.Inventory.GetRandomizerItem(33);            
        }
        catch(Exception) {
            return 0;
        }
    }
    public static int Jumpgrades()
    {
        try {
            if(RandomizerBonusSkill.IsActive(108))
                return 0;
            return Characters.Sein.Inventory.GetRandomizerItem(37);            
        }
        catch(Exception) {
            return 0;
        }
    }
    public static float Jumpscale => 1f + .25f * Jumpgrades();
    public static float DoubleJumpscale => 1f + .10f * Jumpgrades();
    public static float Veloscale => 1f + .20f * Velocity();

    public static bool GravitySuit()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(36) > 0;
    }

    public static bool Swimming()
    {
        return Characters.Sein.Controller.IsSwimming && !GravitySuit();
    }

    public static void SpentAP(int numSpent)
    {
        Characters.Sein.Inventory.IncRandomizerItem(80, numSpent);
    }

    public static int ResetAP() {
        int refund = Characters.Sein.Inventory.GetRandomizerItem(80);
        Characters.Sein.Inventory.SetRandomizerItem(80, 0);
        BingoController.OnResetAP();
        return refund;
    }

    public static void ListBonuses() {
        List<string> bonuses = new List<string>();
        foreach(var kv in BonusNames) {
            var amnt = Characters.Sein.Inventory.GetRandomizerItem(kv.Key);
            if(amnt == 0) continue;
            if(amnt == 1)
                bonuses.Add(kv.Value);
            else
                bonuses.Add($"{kv.Value} ({amnt})");
        }
        if(bonuses.Count > 0) {
            var msg = $"ALIGNRIGHTANCHORTOPPARAMS_12_14_1_{string.Join("\n", bonuses.ToArray())}";
            Randomizer.printInfo(msg);
        } else Randomizer.printInfo("No bonus passives");
    }

    public static bool EnhancedSpiritFlame => Randomizer.Inventory.GetRandomizerItem(410) > 0;
    public static bool EnhancedWallJump => Randomizer.Inventory.GetRandomizerItem(411) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedChargeFlame => Randomizer.Inventory.GetRandomizerItem(412) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedDoubleJump => Randomizer.Inventory.GetRandomizerItem(413) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedBash => Randomizer.Inventory.GetRandomizerItem(414) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedStomp => Randomizer.Inventory.GetRandomizerItem(415) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedGlide => Randomizer.Inventory.GetRandomizerItem(416) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedClimb => Randomizer.Inventory.GetRandomizerItem(417) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedChargeJump => Randomizer.Inventory.GetRandomizerItem(418) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedDash => Randomizer.Inventory.GetRandomizerItem(419) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedGrenade => Randomizer.Inventory.GetRandomizerItem(420) > 0 && !RandomizerBonusSkill.IsActive(115);
    public static bool EnhancedCleanWater => Randomizer.Inventory.GetRandomizerItem(422) > 0 && !RandomizerBonusSkill.IsActive(115);

    private static Dictionary<int, String> BonusNames = new Dictionary<int, String> {
        {6, "Attack Upgrade"},
        {13, "Health Regeneration"},
        {15, "Energy Regeneration"},
        {12, "Extra Double Jump"},
        {33, "Skill Velocity Upgrade"},
        {37, "Jumpgrade"},
        {31, "Health Leech"},
        {32, "Energy Leech"},
        {36, "Underwater Skill Usage"},
        {10, "Extra Air Dash"},
        {11, "Charge Dash Efficiency"},
        {9, "Spirit Light Efficiency"},
    };

    public static int WarmthFrags()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(28);
    }

    public static bool AltRDisabled()
    {
        return Characters.Sein.Inventory.GetRandomizerItem(34) == 1;
    }

    public static bool DoubleAirDashUsed;

    private static Ability[] abilities = {
        new Ability("Quick Flame", p => p.QuickFlame),
        new Ability("Spark Flame", p => p.SparkFlame),
        new Ability("Charge Flame Burn", p => p.ChargeFlameBurn),
        new Ability("Split Flame", p => p.SplitFlameUpgrade),
        new Ability("Ultra Light Burst", p => p.GrenadeUpgrade),
        new Ability("Cinder Flame", p => p.CinderFlame),
        new Ability("Ultra Stomp", p => p.StompUpgrade),
        new Ability("Rapid Flame", p => p.RapidFire),
        new Ability("Charge Flame Blast", p => p.ChargeFlameBlast),
        new Ability("Ultra Split Flame", p => p.UltraSplitFlame),
        new Ability("Spirit Magnet", p => p.Magnet),
        new Ability("Map Markers", p => p.MapMarkers),
        new Ability("Life Efficiency", p => p.HealthEfficiency),
        new Ability("Ultra Spirit Magnet", p => p.UltraMagnet),
        new Ability("Energy Efficiency", p => p.EnergyEfficiency),
        new Ability("Ability Markers", p => p.AbilityMarkers),
        new Ability("Spirit Efficiency", p => p.SoulEfficiency),
        new Ability("Life Markers", p => p.HealthMarkers),
        new Ability("Energy Markers", p => p.EnergyMarkers),
        new Ability("Sense", p => p.Sense),
        new Ability("Rekindle", p => p.Rekindle),
        new Ability("Regroup", p => p.Regroup),
        new Ability("Charge Flame Efficiency", p => p.ChargeFlameEfficiency),
        new Ability("Air Dash", p => p.AirDash),
        new Ability("Ultra Soul Link", p => p.UltraSoulFlame),
        new Ability("Charge Dash", p => p.ChargeDash),
        new Ability("Water Breath", p => p.WaterBreath),
        new Ability("Soul Link Efficiency", p => p.SoulFlameEfficiency),
        new Ability("Triple Jump", p => p.DoubleJumpUpgrade),
        new Ability("Ultra Defense", p => p.UltraDefense)
    };

    public static float HealthRegenAmount = 4f;

    public static float HealthRegenTimeSeconds = 60f;

    public static float EnergyRegenAmount = 1f;

    public static float EnergyRegenTimeSeconds = 60f;

    public static bool SuppressEnhancedSpiritFlame = false;

    private class Ability
    {
        public Ability(string name, Func<PlayerAbilities, CharacterAbility> selector)
        {
            this.name = name;
            this.selector = selector;
        }
        public void Found()
        {
            if (!Characters.Sein)
            {
                return;
            }
            RandomizerSwitch.PickupMessage("$" + name + "$", 240);
            selector(Characters.Sein.PlayerAbilities).HasAbility = true;
        }
        public void Lost()
        {
            if (!Characters.Sein)
            {
                return;
            }
            RandomizerSwitch.PickupMessage("@" + name + " Lost!!@", 240);
            selector(Characters.Sein.PlayerAbilities).HasAbility = false;
        }
        public bool Has() => Characters.Sein && selector(Characters.Sein.PlayerAbilities).HasAbility;
        private string name;
        private Func<PlayerAbilities, CharacterAbility> selector;
    }
}
