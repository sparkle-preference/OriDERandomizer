using System;
using System.Collections.Generic;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinStandardSpiritFlameAbility : CharacterState, ISeinReceiver {
    public SpiritFlame CurrentSpiritFlame => GetStandardSpiritFlame(OriLevel);

    public int OriLevel => sein.PlayerAbilities.OriStrength;

    public bool LockShootingSpiritFlame => sein.Abilities.SpiritFlame.LockShootingSpiritFlame;

    public int MaxTargets => sein.PlayerAbilities.SplitFlameTargets;

    private bool ProcessAutofire(bool pressed, bool held, bool released) {
        switch (RandomizerSettings.Controls.Autofire.Value) {
            case RandomizerSettings.AutofireMode.Hold:
                if (pressed) {
                    lastAutofire = Mathf.Round(Time.time * 120f);
                    autofireSuppressed = RandomizerRebinding.SuppressAutofire.Pressed;
                }

                isAutofiring = held && !autofireSuppressed;
                break;
            case RandomizerSettings.AutofireMode.Toggle:
                if (pressed) {
                    lastAutofire = Mathf.Round(Time.time * 120f);
                    isAutofiring = isAutofiring || RandomizerRebinding.SuppressAutofire.Pressed ? false : true;

                    if (isAutofiring) {
                        autofireBegan = lastAutofire;
                    }
                }

                if (held && Mathf.Round(Time.time * 120f) - autofireBegan >= 24f) {
                    isAutofiring = false;
                }

                break;
        }

        if (isAutofiring) {
            var scaledTime = Mathf.Round(Time.time * 120f);
            if (scaledTime - lastAutofire >= 6f) {
                lastAutofire = scaledTime;
                return true;
            }

            UpdateTargetting();
        }

        return false;
    }

    public override void UpdateCharacterState() {
        if (sein.Controller.InputLocked) {
            return;
        }

        if (SeinAbilityRestrictZone.IsInside()) {
            return;
        }

        var pressed = Input.SpiritFlame.OnPressed && !Input.SpiritFlame.Used;
        var held = Input.SpiritFlame.Pressed && Input.SpiritFlame.WasPressed;
        var released = Input.SpiritFlame.Released;

        if (ProcessAutofire(pressed, held, released)) {
            pressed = true;
        }

        if (pressed) {
            if (Characters.Ori == null) {
                return;
            }

            timeOfBeforeLastShot = timeOfLastShot;
            timeOfLastShot = Mathf.Round(Time.time * 120f);
        }

        if (released) {
            UpdateTargetting();
        }

        if (sein.PlayerAbilities.RapidFire.HasAbility) {
            ProcessRapidFire(pressed);
        } else {
            ProcessBaseSpiritFlame(pressed);
        }
    }

    private void ProcessRapidFire(bool pressed) {
        var scaledTime = Mathf.Round(Time.time * 120f);

        if (isSpamming) {
            if (scaledTime - timeOfLastSpam >= 18f) {
                timeOfLastSpam = scaledTime;
                pressed = true;
            } else {
                pressed = false;
            }

            if (scaledTime - timeOfLastShot > 24f) {
                isSpamming = false;
            }
        } else if (pressed && scaledTime - timeOfBeforeLastShot <= 24f) {
            timeOfLastSpam = scaledTime;
            isSpamming = true;
        }

        if (pressed) {
            Characters.Ori.ShootAnimation.Restart();
            if (!LockShootingSpiritFlame) {
                var currentSpiritFlame = CurrentSpiritFlame;
                sein.Abilities.SpiritFlame.ThrowSpiritFlames(currentSpiritFlame);
                Input.SpiritFlame.Used = true;
            }
        }
    }

    private void ProcessBaseSpiritFlame(bool pressed) {
        StandardSpiritFlameShotCombo.UseShotDelay = false;
        StandardSpiritFlameShotCombo.Update(Time.deltaTime);

        if (pressed) {
            Characters.Ori.ShootAnimation.Restart();
            if (StandardSpiritFlameShotCombo.CanShoot && !LockShootingSpiritFlame) {
                StandardSpiritFlameShotCombo.NumberOfShotsPerCombo = !sein.PlayerAbilities.QuickFlame.HasAbility ? 2 : 3;
                var currentSpiritFlame = CurrentSpiritFlame;
                sein.Abilities.SpiritFlame.ThrowSpiritFlames(currentSpiritFlame);
                StandardSpiritFlameShotCombo.Shoot();
                Input.SpiritFlame.Used = true;
            }
        }
    }

    public SpiritFlame GetStandardSpiritFlame(int index) {
        if (index < 0) {
            index = 0;
        }

        if (index >= StandardSpiritFlames.Length) {
            index = StandardSpiritFlames.Length - 1;
        }

        return StandardSpiritFlames[index];
    }

    public List<ISpiritFlameAttackable> ClosestAttackables => sein.Abilities.SpiritFlameTargetting.ClosestAttackables;

    public void SetReferenceToSein(SeinCharacter sein) {
        this.sein = sein;
        this.sein.Abilities.StandardSpiritFlame = this;
    }

    public void UpdateTargetting() {
        sein.Abilities.SpiritFlameTargetting.MaxNumberOfTargets = MaxTargets;
        sein.Abilities.SpiritFlameTargetting.Range = SpiritFlameRange;
    }

    public ShotCombo StandardSpiritFlameShotCombo = new ShotCombo();

    public PoisonSettings Poison = new PoisonSettings();

    public SpiritFlame[] StandardSpiritFlames;

    public float SpiritFlameRange = 8f;

    public bool CanDamageOverTime;

    private SeinCharacter sein;

    private float timeOfLastShot;

    private float timeOfBeforeLastShot;

    private bool isSpamming;

    private float timeOfLastSpam;

    public float SpamShotSpeed = 10f;

    private float lastAutofire;

    private bool isAutofiring;

    private float autofireBegan;

    private bool autofireSuppressed;

    [Serializable]
    public class PoisonSettings {
        public float DamageAmount = 4f;

        public int DamageDuration = 4;

        public GameObject PoisonEffect;
    }
}
