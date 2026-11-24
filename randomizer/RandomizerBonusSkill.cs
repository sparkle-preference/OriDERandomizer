using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Game;
using UnityEngine;

public static class RandomizerBonusSkill {
    public static void SwitchBonusSkill() {
        var unlocked = new Dictionary<int, int>(UnlockedBonusSkills);
        if (unlocked.Count == 0) {
            Randomizer.Print("No bonus skills unlocked!", 3, false, false, false, true);
            return;
        }

        var slot_0 = unlocked.Keys.Min();
        var slot_n = unlocked.Keys.Max();
        if (ActiveBonus == 0) {
            ActiveBonus = unlocked[slot_0];
        }

        var active_slot = get(ActiveBonus) >> 2;
        var cur_slot = active_slot;
        if (unlocked.Count == 1) {
            Randomizer.Print("Bonus Skill (" + (cur_slot + 1) + "): " + Name(unlocked[cur_slot]), 3, false, false, false, true);
            return;
        }

        while (cur_slot < slot_n) {
            cur_slot++;
            if (unlocked.ContainsKey(cur_slot) && get(unlocked[cur_slot]) > 0) {
                ActiveBonus = unlocked[cur_slot];
                Randomizer.Print("Bonus Skill (" + (cur_slot + 1) + "): " + Name(unlocked[cur_slot]), 3, false, false, false, true);
                return;
            }
        }

        cur_slot = slot_0;
        while (cur_slot < active_slot) {
            if (unlocked.ContainsKey(cur_slot) && get(unlocked[cur_slot]) > 0) {
                ActiveBonus = unlocked[cur_slot];
                Randomizer.Print("Bonus Skill (" + (cur_slot + 1) + "): " + Name(unlocked[cur_slot]), 3, false, false, false, true);
                return;
            }

            cur_slot++;
        }
    }

    public static void BonusSkillSlot(int slot) {
        if (!UnlockedBonusSkills.TryGetValue(slot, out var bonus)) {
            Randomizer.Print("No bonus skill in slot " + (1 + slot), 3, false, false, false, true);
            return;
        }

        ActivateBonusSkill(bonus);
    }

    public static void ActivateBonusSkill() {
        var ab = ActiveBonus;
        ActivateBonusSkill(ab);
    }

    public static void ActivateBonusSkill(int ab) {
        if (!Characters.Sein || Characters.Sein.IsSuspended || ab == 0 || get(ab) == 0) {
            return;
        }

        switch (ab) {
            case 101:
                if (Characters.Sein.Energy.Current > 0f) {
                    var amount = Characters.Sein.Energy.Current * 4f;
                    Characters.Sein.Energy.SetCurrent(Characters.Sein.Mortality.Health.Amount / 4f);
                    Characters.Sein.Mortality.Health.SetAmount(amount);
                    return;
                }

                UI.SeinUI.ShakeEnergyOrbBar();
                Characters.Sein.Energy.NotifyOutOfEnergy();
                return;
            case 102:
            case 103:
            case 109:
                if (IsActive(ab)) {
                    Deactivate(ab);
                    BonusSkillText(RandomizerItems.BonusSkillNames[ab] + " off");
                    EnergyDrainRate -= DrainRates[ab];
                } else if (Characters.Sein.Energy.Current > 5 * DrainRates[ab]) {
                    Activate(ab);
                    BonusSkillText(RandomizerItems.BonusSkillNames[ab] + " on");
                    EnergyDrainRate += DrainRates[ab];
                } else {
                    UI.SeinUI.ShakeEnergyOrbBar();
                    Characters.Sein.Energy.NotifyOutOfEnergy();
                    return;
                }

                if (ab == 102) {
                    if (IsActive(ab) || Characters.Sein.Abilities.Carry.IsCarrying) {
                        Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle += 180f;
                    } else {
                        Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle = 0f;
                    }

                    Characters.Sein.PlatformBehaviour.LeftRightMovement.PlatformMovement.LocalSpeedX *= -1;
                }

                break;
            case 104:
                if (!CanWarpTo(LastAltR)) {
                    return;
                }

                if (Characters.Sein.Energy.Current >= 0.5f) {
                    Characters.Sein.Energy.Spend(0.5f);
                    Randomizer.WarpTo(LastAltR, 0);
                    return;
                }

                UI.SeinUI.ShakeEnergyOrbBar();
                Characters.Sein.Energy.NotifyOutOfEnergy();
                return;
            case 105:
                if (!CanWarpTo(LastSoulLink)) {
                    return;
                }

                if (Characters.Sein.Energy.Current >= 0.5f) {
                    Characters.Sein.Energy.Spend(0.5f);
                    Randomizer.WarpTo(LastSoulLink, 0);
                    return;
                }

                UI.SeinUI.ShakeEnergyOrbBar();
                Characters.Sein.Energy.NotifyOutOfEnergy();
                return;
            case 106: {
                if (!Characters.Sein.SoulFlame.InsideCheckpointMarker) {
                    BonusSkillText("You can only Respec on a Soul Link!");
                    return;
                }

                var apToGain = RandomizerBonus.ResetAP();
                if (apToGain == 0) {
                    BonusSkillText("No AP to refund");
                    return;
                }

                BonusSkillText("Respec successful. " + apToGain + " AP refunded!");
                var abilities = Characters.Sein.PlayerAbilities.Abilities;
                var actuallySkills = new List<CharacterAbility> {
                    Characters.Sein.PlayerAbilities.WallJump,
                    Characters.Sein.PlayerAbilities.ChargeFlame,
                    Characters.Sein.PlayerAbilities.DoubleJump,
                    Characters.Sein.PlayerAbilities.Bash,
                    Characters.Sein.PlayerAbilities.Stomp,
                    Characters.Sein.PlayerAbilities.Climb,
                    Characters.Sein.PlayerAbilities.Glide,
                    Characters.Sein.PlayerAbilities.ChargeJump,
                    Characters.Sein.PlayerAbilities.Dash,
                    Characters.Sein.PlayerAbilities.Grenade,
                    Characters.Sein.PlayerAbilities.SpiritFlame
                };
                for (var i = 0; i < abilities.Length; i++) {
                    if (!actuallySkills.Contains(abilities[i])) {
                        abilities[i].HasAbility = false;
                    }
                }

                Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
                Characters.Sein.Inventory.SkillPointsCollected += apToGain;
                Characters.Sein.Level.SkillPoints += apToGain;
                return;
            }
            case 107:
                if (LevelExplosionCooldown > 0) {
                    return;
                }

                if (Characters.Sein.Energy.Current >= 1f || Characters.Sein.Inventory.GetRandomizerItem(1102) == 1) {
                    if (Characters.Sein.Inventory.GetRandomizerItem(1102) == 0) {
                        Characters.Sein.Energy.Spend(1f);
                    }

                    OldHealth = Characters.Sein.Mortality.Health.Amount;
                    OldEnergy = Characters.Sein.Energy.Current;
                    Characters.Sein.Level.AttemptInstantiateLevelUp();
                    LevelExplosionCooldown = 15;
                    return;
                }

                UI.SeinUI.ShakeEnergyOrbBar();
                Characters.Sein.Energy.NotifyOutOfEnergy();
                return;
            case 108:
                if (IsActive(ab)) {
                    Deactivate(ab);
                    BonusSkillText("Movement Bonuses on");
                } else {
                    Activate(ab);
                    BonusSkillText("Movement Bonuses off");
                }

                return;
            case 110:
                if (IsActive(ab)) {
                    Deactivate(ab);
                    BonusSkillText(RandomizerItems.BonusSkillNames[ab] + " off");
                    EnergyDrainRate -= DrainRates[ab];
                } else if (Characters.Sein.Energy.Current > 1f) {
                    Activate(ab);
                    Characters.Sein.Energy.Spend(0.5f);
                    BonusSkillText(RandomizerItems.BonusSkillNames[ab] + " on");
                    EnergyDrainRate += DrainRates[ab];
                } else {
                    UI.SeinUI.ShakeEnergyOrbBar();
                    Characters.Sein.Energy.NotifyOutOfEnergy();
                }

                break;
            case 111:
                Characters.Sein.Mortality.DamageReciever.OnRecieveDamage(new Damage(9000f, new Vector2(0, 0), Characters.Sein.Position, DamageType.Lava, Characters.Sein.GameObject));
                break;
            case 112: {
                // contents this build can't read count as empty
                var held = RandomizerEnemies.CatchName(Pokeball - 1) == null ? -1 : Pokeball - 1;
                if (held >= 0) {
                    if (RandomizerEnemies.Release(held, new Vector2(get(89) / 100f, get(90) / 100f)) == null) {
                        BonusSkillText("The Pokeball isn't ready yet");
                        return;
                    }

                    Pokeball = 0;
                    return;
                }

                bool arena;
                Vector2 offset;
                var caught = RandomizerEnemies.Catch(out arena, out offset);
                if (caught >= 0) {
                    Pokeball = caught + 1;
                    set(89, Mathf.RoundToInt(offset.x * 100f));
                    set(90, Mathf.RoundToInt(offset.y * 100f));
                    BonusSkillText(Name(ab));
                } else if (arena) {
                    BonusSkillText("Arena enemies can't be caught");
                }

                break;
            }
            case 114:
                if (IsActive(ab)) {
                    Deactivate(ab);
                    BonusSkillText("Ori");
                } else {
                    Activate(ab);
                    BonusSkillText("Naru");
                }

                break;
            case 115:
                if (IsActive(ab)) {
                    Deactivate(ab);
                    BonusSkillText("Enhanced Effects on");
                } else {
                    Activate(ab);
                    BonusSkillText("Enhanced Effects off");
                }

                Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
                break;
            // the id moved; a stored ActiveBonus can still be the old one
            case LegacyCreditWarp:
            case CreditWarp:
                if (!Characters.Sein.Controller.CanMove || !Characters.Sein.Active) {
                    return;
                }

                Randomizer.WarpTo(new Vector3(-2478, -593, 0), 0);
                GameController.Instance.RemoveGameplayObjects();
                RandomizerStatsManager.Finish();
                RandomizerSyncManager.SendGameComplete();
                RandomizerCreditsManager.Initialize();
                break;
            case 113:
                if (IsActive(ab)) {
                    Deactivate(ab);
                    BonusSkillText("Bash/Stomp Damage on");
                } else {
                    Activate(ab);
                    BonusSkillText("Bash/Stomp Damage off");
                }

                break;
            case 116: {
                var scene = Scenes.Manager.CurrentScene != null ? Scenes.Manager.CurrentScene.Scene : "?";
                var zone = RandomizerStatsManager.CurrentZone();
                var pos = Characters.Sein.Position.ToString("F2");
                BonusSkillText(pos + "\n" + scene + "\n" + zone);
                Randomizer.log("MARK | scene=" + scene + " | zone=" + zone + " | pos=" + pos);
                break;
            }
            case 117:
                if (IsActive(ab)) {
                    Deactivate(ab);
                    BonusSkillText("Warp to Grenade disabled");
                } else {
                    Activate(ab);
                    BonusSkillText("Warp to Grenade enabled");
                }

                break;
            default:
                return;
        }
    }

    public static bool CanWarpTo(Vector3 target) {
        return !((Characters.Sein.Abilities.Carry.IsCarrying && !Randomizer.AllowOrbWarps) || !Characters.Sein.Controller.CanMove || !Characters.Sein.Active || (target.x == 0f && target.y == 0f));
    }

    static RandomizerBonusSkill() {
        Reset();
    }

    public static void Update() {
        if (!Characters.Sein.IsSuspended && Characters.Sein.Controller.CanMove && Characters.Sein.Active) {
            if (DrainNextUpdate > 0) {
                UpdateDrain();
            }

            if (EnergyDrainRate > Characters.Sein.Energy.Current) {
                foreach (var ds in ActiveDrainSkills) {
                    Deactivate(ds);
                    if (ds == 102) {
                        if (Characters.Sein.Abilities.Carry.IsCarrying) {
                            Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle += 180f;
                        } else {
                            Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle = 0f;
                        }

                        Characters.Sein.PlatformBehaviour.LeftRightMovement.PlatformMovement.LocalSpeedX *= -1;
                    }
                }

                BonusSkillText("Out of energy! Bonus skills disabled.");
                UpdateDrain();
                return;
            }

            // rates are per 60 Hz frame, paid in real time whatever the refresh rate
            if (EnergyDrainRate > 0f) {
                Characters.Sein.Energy.Spend(EnergyDrainRate * Mathf.Min(Time.unscaledDeltaTime, MaxDrainStep) * 60f);
            }
        }
    }

    public static void OnSave() {
        UpdateDrain();
    }

    public static void OnDeath() {
        UpdateDrain();
    }

    // Seeds and the server's pickup table say 1587; the save keeps Credit Warp at 4087.
    public const int LegacyCreditWarp = 1587;

    public const int CreditWarp = 4087;

    public static void FoundBonusSkill(int ID) {
        if (ID == LegacyCreditWarp) {
            ID = CreditWarp;
        }

        var psuedo = ID == 108 || ID == 115 || ID == CreditWarp;
        if (get(ID) > 0) {
            if (!psuedo) {
                RandomizerSwitch.PickupMessage(RandomizerItems.BonusSkillNames[ID] + " (duplicate)");
            }

            return;
        }

        if (!psuedo) {
            RandomizerSwitch.PickupMessage("Unlocked Bonus Skill: " + RandomizerItems.BonusSkillNames[ID]);
        }

        var offset = 0;
        var ubs = new Dictionary<int, int>(UnlockedBonusSkills);
        if (ubs.Count > 0) {
            offset = (1 + ubs.Keys.Max()) << 2;
        }

        set(ID, offset + 1);
        if (ActiveBonus == 0) {
            ActiveBonus = ID;
        }
    }

    public static void LoseBonusSkill(int ID) {
        if (ID == LegacyCreditWarp) {
            ID = CreditWarp;
        }

        if (get(ID) == 0) {
            return;
        }

        var gravitySwapped = ID == 102 && IsActive(ID);
        set(ID, 0);
        if (ActiveBonus == ID) {
            ActiveBonus = 0;
        }

        // every other toggle reads IsActive as it goes; gravity swap flipped once and must flip back
        if (gravitySwapped) {
            if (Characters.Sein.Abilities.Carry.IsCarrying) {
                Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle += 180f;
            } else {
                Characters.Sein.PlatformBehaviour.Gravity.BaseSettings.GravityAngle = 0f;
            }

            Characters.Sein.PlatformBehaviour.LeftRightMovement.PlatformMovement.LocalSpeedX *= -1;
        }

        if (ID == 115) {
            Characters.Sein.Prefabs.EnsureRightPrefabsAreThereForAbilities();
        }

        UpdateDrain();
        if (ID != 108 && ID != 115 && ID != CreditWarp) {
            RandomizerSwitch.PickupMessage("@" + RandomizerItems.BonusSkillNames[ID] + " Lost!!@", 240);
        }
    }

    public static void Reset() {
        UpdateDrain();
    }

    public static void UpdateDrain() {
        try {
            if (!Characters.Sein || !Characters.Sein.Inventory || !Characters.Sein.PlatformBehaviour) {
                return;
            }

            var ads = new HashSet<int>(ActiveDrainSkills);

            EnergyDrainRate = 0f;
            foreach (var ds in ads) {
                EnergyDrainRate += DrainRates[ds];
            }

            if (ads.Contains(103)) {
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed = 35f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Acceleration = 90f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Decceleration = 45f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.MaxSpeed = 35f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Acceleration = 39f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Decceleration = 39f;
            } else {
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Acceleration = 60f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.Decceleration = 30f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Ground.MaxSpeed = 11.6666f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Acceleration = 26f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.Decceleration = 26f;
                Characters.Sein.PlatformBehaviour.LeftRightMovement.Settings.Air.MaxSpeed = 11.6666f;
            }

            DrainNextUpdate -= 1;
        } catch (Exception e) {
            Randomizer.LogError("Update Drain: " + e.Message);
        }
    }

    public static int ActiveBonus {
        get => get(4089);
        set => set(4089, value);
    }

    // the Pokeball's enemy as its variant + 1 (0 when empty), rolled back with the save;
    // 89 and 90 hold where it stood from Ori, in hundredths along his facing and up
    public static int Pokeball {
        get => get(88);
        set => set(88, value);
    }

    public static int LevelExplosionCooldown;

    public static Vector3 LastAltR {
        get =>
            new Vector3(
                get(84) / 100f,
                get(85) / 100f
            );
        set {
            set(84, (int)(value.x * 100));
            set(85, (int)(value.y * 100));
        }
    }

    public static Vector3 LastSoulLink {
        get =>
            new Vector3(
                get(86) / 100f,
                get(87) / 100f
            );
        set {
            set(86, (int)(value.x * 100));
            set(87, (int)(value.y * 100));
        }
    }

    public static float OldHealth;
    public static float OldEnergy;

    public static Dictionary<int, int> UnlockedBonusSkills {
        get {
            var ubs = new Dictionary<int, int>();
            foreach (var id in RandomizerItems.BonusSkillNames.Keys) {
                // a migrated save holds Credit Warp at both ids; 4087 is the one that counts
                if (id == LegacyCreditWarp) {
                    continue;
                }

                var val = get(id);
                if (val % 2 == 1) {
                    if (ubs.ContainsKey(val >> 2)) {
                        Randomizer.LogError("Duplicate keys: " + ubs[val >> 2] + " and " + id);
                    }

                    ubs[val >> 2] = id;
                }
            }

            return ubs;
        }
    }

    public static float ExtremeSpeed => IsActive(103) ? 3.0f : 1.0f;

    public static bool Invincible => IsActive(110);

    // also gates BingoController's death dump
    public static bool HasMark {
        get {
            if (!Characters.Sein || !Characters.Sein.Inventory) {
                return false;
            }

            return get(116) > 0;
        }
    }

    public static float EnergyDrainRate;

    // seconds; a load hitch drains no more than this
    private const float MaxDrainStep = 0.1f;

    public static bool IsActive(int id) {
        try {
            if (!Characters.Sein) {
                return false;
            }

            return (get(id) >> 1) % 2 == 1;
        } catch (Exception e) {
            Randomizer.LogError("IsActive: " + e.Message);
            return false;
        }
    }

    public static void Deactivate(int id) {
        if (IsActive(id)) {
            set(id, get(id) - 2);
        } else if (RandomizerSettings.Dev) {
            Randomizer.log("ignoring deactivation of " + id + " since skill was not active");
        }

        UpdateDrain();
    }

    public static void Activate(int id) {
        if (!IsActive(id)) {
            set(id, get(id) + 2);
        } else if (RandomizerSettings.Dev) {
            Randomizer.log("ignoring activation of " + id + " since skill was already active");
        }

        UpdateDrain();
    }

    public static HashSet<int> ActiveDrainSkills {
        get {
            var ads = new HashSet<int>();
            foreach (var id in DrainRates.Keys) {
                if (IsActive(id)) {
                    ads.Add(id);
                }
            }

            return ads;
        }
    }

    private static int get(int item) {
        return Characters.Sein.Inventory.GetRandomizerItem(item);
    }

    private static int set(int item, int value) {
        return Characters.Sein.Inventory.SetRandomizerItem(item, value);
    }

    public static Dictionary<int, float> DrainRates = new Dictionary<int, float> {
        { 102, 0.00112f },
        { 103, 0.00112f },
        { 109, 0.00112f },
        { 110, 0.01667f },
        { 114, 0f },
    };

    public static float AbilityDamage(float orig) {
        if (IsActive(113)) {
            return 0;
        }

        return orig;
    }

    public static float TimewarpFactor = 0.5f;

    public static float TimeScale(float orig) {
        return IsActive(109) ? orig * TimewarpFactor : orig;
    }

    public static Vector3 TimeScale(Vector3 orig) {
        return orig * TimeScale(1.0f);
    }

    public static Vector2 TimeScale(Vector2 orig) {
        return orig * TimeScale(1.0f);
    }

    public static void TimeScale(Animator animator) {
        animator.speed = TimeScale(1.0f);
    }

    public static void BonusSkillText(string text) {
        Randomizer.Print(text, 3, false, false, false, true);
    }

    // the Pokeball's name says what it holds
    public static string Name(int id) {
        var name = RandomizerItems.BonusSkillNames[id];
        return id != 112 ? name : name + " (" + (RandomizerEnemies.CatchName(Pokeball - 1) ?? "empty") + ")";
    }

    public static bool UnlockCreditWarp(string message) {
        if (get(CreditWarp) > 0) {
            return false;
        }

        FoundBonusSkill(CreditWarp);
        ActiveBonus = CreditWarp;
        if (!RandomizerSwitch.SilentMode) {
            message += "\nPress " + RandomizerRebinding.BonusToggle.FirstBindName() + " to warp to credits";
            Randomizer.QueueWinMessage(message);
        }

        RandomizerStatsManager.WriteStatsFile();
        return true;
    }

    public static void DelayDrainUpdate() {
        DrainNextUpdate = 5;
    }

    private static int DrainNextUpdate;
}
