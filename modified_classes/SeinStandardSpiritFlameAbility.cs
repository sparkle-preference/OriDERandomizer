using System;
using System.Collections.Generic;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinStandardSpiritFlameAbility : CharacterState, ISeinReceiver {
    public SpiritFlame CurrentSpiritFlame {
        get { return GetStandardSpiritFlame(OriLevel); }
    }

    public int OriLevel {
        get { return m_sein.PlayerAbilities.OriStrength; }
    }

    public bool LockShootingSpiritFlame {
        get { return m_sein.Abilities.SpiritFlame.LockShootingSpiritFlame; }
    }

    public int MaxTargets {
        get { return m_sein.PlayerAbilities.SplitFlameTargets; }
    }

    private bool ProcessAutofire(bool pressed, bool held, bool released) {
        switch (RandomizerSettings.Controls.Autofire.Value) {
            case RandomizerSettings.AutofireMode.Hold:
                if (pressed) {
                    m_lastAutofire = Mathf.Round(Time.time * 120f);
                    m_autofireSuppressed = RandomizerRebinding.SuppressAutofire.Pressed;
                }

                m_isAutofiring = held && !m_autofireSuppressed;
                break;
            case RandomizerSettings.AutofireMode.Toggle:
                if (pressed) {
                    m_lastAutofire = Mathf.Round(Time.time * 120f);
                    m_isAutofiring = m_isAutofiring || RandomizerRebinding.SuppressAutofire.Pressed ? false : true;

                    if (m_isAutofiring) {
                        m_autofireBegan = m_lastAutofire;
                    }
                }

                if (held && Mathf.Round(Time.time * 120f) - m_autofireBegan >= 24f) {
                    m_isAutofiring = false;
                }

                break;
        }

        if (m_isAutofiring) {
            float scaledTime = Mathf.Round(Time.time * 120f);
            if (scaledTime - m_lastAutofire >= 6f) {
                m_lastAutofire = scaledTime;
                return true;
            }

            UpdateTargetting();
        }

        return false;
    }

    public override void UpdateCharacterState() {
        if (m_sein.Controller.InputLocked) {
            return;
        }

        if (SeinAbilityRestrictZone.IsInside()) {
            return;
        }

        bool pressed = Input.SpiritFlame.OnPressed && !Input.SpiritFlame.Used;
        bool held = Input.SpiritFlame.Pressed && Input.SpiritFlame.WasPressed;
        bool released = Input.SpiritFlame.Released;

        if (ProcessAutofire(pressed, held, released)) {
            pressed = true;
        }

        if (pressed) {
            if (Characters.Ori == null) {
                return;
            }

            m_timeOfBeforeLastShot = m_timeOfLastShot;
            m_timeOfLastShot = Mathf.Round(Time.time * 120f);
        }

        if (released) {
            UpdateTargetting();
        }

        if (m_sein.PlayerAbilities.RapidFire.HasAbility) {
            ProcessRapidFire(pressed);
        } else {
            ProcessBaseSpiritFlame(pressed);
        }
    }

    private void ProcessRapidFire(bool pressed) {
        float scaledTime = Mathf.Round(Time.time * 120f);

        if (m_isSpamming) {
            if (scaledTime - m_timeOfLastSpam >= 18f) {
                m_timeOfLastSpam = scaledTime;
                pressed = true;
            } else {
                pressed = false;
            }

            if (scaledTime - m_timeOfLastShot > 24f) {
                m_isSpamming = false;
            }
        } else if (pressed && scaledTime - m_timeOfBeforeLastShot <= 24f) {
            m_timeOfLastSpam = scaledTime;
            m_isSpamming = true;
        }

        if (pressed) {
            Characters.Ori.ShootAnimation.Restart();
            if (!LockShootingSpiritFlame) {
                SpiritFlame currentSpiritFlame = CurrentSpiritFlame;
                m_sein.Abilities.SpiritFlame.ThrowSpiritFlames(currentSpiritFlame);
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
                StandardSpiritFlameShotCombo.NumberOfShotsPerCombo = !m_sein.PlayerAbilities.QuickFlame.HasAbility ? 2 : 3;
                SpiritFlame currentSpiritFlame = CurrentSpiritFlame;
                m_sein.Abilities.SpiritFlame.ThrowSpiritFlames(currentSpiritFlame);
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

    public List<ISpiritFlameAttackable> ClosestAttackables {
        get { return m_sein.Abilities.SpiritFlameTargetting.ClosestAttackables; }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
        m_sein.Abilities.StandardSpiritFlame = this;
    }

    public void UpdateTargetting() {
        m_sein.Abilities.SpiritFlameTargetting.MaxNumberOfTargets = MaxTargets;
        m_sein.Abilities.SpiritFlameTargetting.Range = SpiritFlameRange;
    }

    public ShotCombo StandardSpiritFlameShotCombo = new ShotCombo();

    public PoisonSettings Poison = new PoisonSettings();

    public SpiritFlame[] StandardSpiritFlames;

    public float SpiritFlameRange = 8f;

    public bool CanDamageOverTime;

    private SeinCharacter m_sein;

    private float m_timeOfLastShot;

    private float m_timeOfBeforeLastShot;

    private bool m_isSpamming;

    private float m_timeOfLastSpam;

    public float SpamShotSpeed = 10f;

    private float m_lastAutofire;

    private bool m_isAutofiring;

    private float m_autofireBegan;

    private bool m_autofireSuppressed;

    [Serializable]
    public class PoisonSettings {
        public float DamageAmount = 4f;

        public int DamageDuration = 4;

        public GameObject PoisonEffect;
    }
}
