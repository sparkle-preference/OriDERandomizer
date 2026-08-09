using System;
using System.Collections.Generic;
using Core;
using fsm;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinChargeFlameAbility : CharacterState, ISeinReceiver {
    public float ChargeDuration => ChargeFlameSettings.ChargeDuration;

    public bool HasEnoughEnergy => m_sein.Energy.CanAfford(m_sein.PlayerAbilities.ChargeFlameEfficiency.HasAbility ? 0f : 0.5f);

    public void SpendEnergy() {
        m_sein.Energy.Spend(m_sein.PlayerAbilities.ChargeFlameEfficiency.HasAbility ? 0f : 0.5f);
    }

    public void RestoreEnergy() {
        m_sein.Energy.Gain(m_sein.PlayerAbilities.ChargeFlameEfficiency.HasAbility ? 0f : 0.5f);
    }

    public override void Awake() {
        base.Awake();
        State.Start = new State {
            UpdateStateEvent = UpdateStartState,
            OnEnterEvent = OnEnterStartState
        };
        State.Precharging = new State {
            UpdateStateEvent = UpdatePrechargingState
        };
        State.Charging = new State {
            UpdateStateEvent = UpdateChargingState
        };
        State.Charged = new State {
            UpdateStateEvent = UpdateChargedState,
            OnEnterEvent = OnEnterChargedState
        };
        Logic.RegisterStates(
            State.Start,
            State.Precharging,
            State.Charging,
            State.Charged
        );
        Logic.ChangeState(State.Start);
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public void OnRestoreCheckpoint() {
        if (m_chargeFlameChargeEffect) {
            InstantiateUtility.Destroy(m_chargeFlameChargeEffect);
        }

        if (CurrentChargingSound()) {
            CurrentChargingSound().StopAndFadeOut(0.5f);
        }

        Logic.ChangeState(State.Start);
    }

    public void OnEnterStartState() {
        if (m_chargeFlameChargeEffect) {
            InstantiateUtility.Destroy(m_chargeFlameChargeEffect);
        }
    }

    public void UpdateStartState() {
        if (m_chargeFlameChargeEffect) {
            InstantiateUtility.Destroy(m_chargeFlameChargeEffect);
        }

        if (m_sein.Controller.IsBashing) {
            return;
        }

        bool pressed = ChargeFlameButton.OnPressed && !ChargeFlameButton.Used;

        if (RandomizerSettings.Controls.Autofire == RandomizerSettings.AutofireMode.Hold && !RandomizerRebinding.SuppressAutofire.Pressed) {
            pressed = false;
        }

        if (pressed && m_sein.PlayerAbilities.ChargeFlame.HasAbility && !m_sein.Controller.InputLocked && !m_sein.Abilities.SpiritFlame.LockShootingSpiritFlame) {
            Logic.ChangeState(State.Precharging);
        }
    }

    public void UpdatePrechargingState() {
        if (Logic.CurrentStateTime > 0.3f) {
            m_chargeFlameChargeEffect = (GameObject)InstantiateUtility.Instantiate(ChargeFlameSettings.ChargeFlameChargeEffectPrefab);
            m_chargeFlameChargeEffect.transform.position = Characters.Ori.transform.position;
            m_chargeFlameChargeEffect.transform.parent = Characters.Ori.transform;
            m_chargeFlameChargeEffect.GetComponentsInChildren(s_legacyAnimatorList);
            for (int i = 0; i < s_legacyAnimatorList.Count; i++) {
                s_legacyAnimatorList[i].Speed = 1f / ChargeDuration;
            }

            s_legacyAnimatorList.Clear();
            if (CurrentChargingSound()) {
                CurrentChargingSound().Play();
            }

            Logic.ChangeState(State.Charging);
            return;
        }

        if (ChargeFlameButton.Released) {
            Logic.ChangeState(State.Start);
            return;
        }

        if (m_sein.Abilities.SpiritFlame.LockShootingSpiritFlame) {
            Logic.ChangeState(State.Start);
            return;
        }

        if (m_sein.Controller.InputLocked) {
            Logic.ChangeState(State.Start);
        }
    }

    public void UpdateChargingState() {
        if (ChargeFlameButton.Released || m_sein.Controller.InputLocked || m_sein.Abilities.SpiritFlame.LockShootingSpiritFlame) {
            if (CurrentChargingSound()) {
                CurrentChargingSound().StopAndFadeOut(0.5f);
            }

            Logic.ChangeState(State.Start);
            return;
        }

        if (Logic.CurrentStateTime >= ChargeDuration) {
            if (HasEnoughEnergy) {
                Logic.ChangeState(State.Charged);
                SpendEnergy();
                return;
            }

            if (CurrentChargingSound()) {
                CurrentChargingSound().StopAndFadeOut(0.5f);
            }

            Logic.ChangeState(State.Start);
            UI.SeinUI.ShakeEnergyOrbBar();
            if (NotEnoughEnergySound) {
                Sound.Play(NotEnoughEnergySound.GetSound(null), transform.position, null);
            }
        }
    }

    public void ReleaseChargeBurst() {
        if (CurrentChargingSound()) {
            CurrentChargingSound().StopAndFadeOut(0.5f);
        }

        if (m_sein.PlayerAbilities.ChargeFlameBlast.HasAbility) {
            InstantiateUtility.Instantiate(ChargeFlameSettings.ChargeFlameBurstC, Characters.Ori.Position, Quaternion.identity);
        } else if (m_sein.PlayerAbilities.ChargeFlameBurn.HasAbility) {
            InstantiateUtility.Instantiate(ChargeFlameSettings.ChargeFlameBurstB, Characters.Ori.Position, Quaternion.identity);
        } else {
            InstantiateUtility.Instantiate(ChargeFlameSettings.ChargeFlameBurstA, Characters.Ori.Position, Quaternion.identity);
        }

        Logic.ChangeState(State.Start);
    }

    public void UpdateChargedState() {
        if (ChargeFlameButton.Released) {
            ReleaseChargeBurst();
            return;
        }

        if (Input.SoulFlame.OnPressed) {
            Input.SoulFlame.Used = true;
            if (CurrentChargingSound()) {
                CurrentChargingSound().StopAndFadeOut(0.5f);
            }

            foreach (var item in m_capturedProjectiles) {
                if (!InstantiateUtility.IsDestroyed(item.Key as Component)) {
                    (item.Key as Component).GetComponent<Collider>().enabled = true;
                }
            }

            Logic.ChangeState(State.Start);
            RestoreEnergy();
            UI.SeinUI.ShakeEnergyOrbBar();
            return;
        }

        if (RandomizerBonus.EnhancedChargeFlame) {
            for (int i = 0; i < Targets.Attackables.Count; i++) {
                IAttackable attackable = Targets.Attackables[i];
                if (InstantiateUtility.IsDestroyed(attackable as Component)) {
                    continue;
                }

                if (attackable.CanBeChargeFlamed() && attackable is Projectile) {
                    Vector3 distance = attackable.Position - Characters.Ori.transform.position;
                    if (distance.magnitude <= m_captureRadius) {
                        CapturedProjectile capturedProjectile = null;

                        if (!m_capturedProjectiles.ContainsKey(attackable)) {
                            capturedProjectile = new CapturedProjectile();
                            m_capturedProjectiles[attackable] = capturedProjectile;
                        } else {
                            capturedProjectile = m_capturedProjectiles[attackable];
                            if (!capturedProjectile.IsDestroyed) {
                                continue;
                            }
                        }

                        capturedProjectile.Direction = distance.normalized;
                        capturedProjectile.CapturedVelocity = (attackable as Projectile).Speed;
                        capturedProjectile.IsDestroyed = false;
                        (attackable as Component).GetComponent<Collider>().enabled = false;
                    }
                }
            }

            foreach (var item in m_capturedProjectiles) {
                if (InstantiateUtility.IsDestroyed(item.Key as Component)) {
                    item.Value.IsDestroyed = true;
                    continue;
                }

                Projectile projectile = item.Key as Projectile;
                Vector3 targetPosition = Characters.Ori.transform.position;
                Vector3 direction = targetPosition - projectile.Position;

                if (direction.magnitude > 0.2f) {
                    projectile.Direction = (projectile.Direction + direction).normalized;
                    projectile.Speed = Mathf.Lerp(1f, 30f, direction.magnitude / m_captureRadius);
                } else {
                    projectile.SpeedVector = Vector3.zero;
                    projectile.Position = targetPosition;
                }
            }
        }
    }

    public Input.InputButtonProcessor ChargeFlameButton => Input.SpiritFlame;

    public bool IsCharging => Logic.CurrentState != State.Start;

    public override void UpdateCharacterState() {
        Logic.UpdateState(Time.deltaTime);
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
        m_sein.Abilities.ChargeFlame = this;
    }

    public override void OnExit() {
        if (Logic.CurrentState == State.Precharging) {
            Logic.ChangeState(State.Start);
        }

        if (Logic.CurrentState == State.Charging) {
            if (CurrentChargingSound()) {
                CurrentChargingSound().StopAndFadeOut(0.5f);
            }

            Logic.ChangeState(State.Start);
        }

        if (Logic.CurrentState == State.Charged) {
            ReleaseChargeBurst();
        }

        base.OnExit();
    }

    private SoundSource CurrentChargingSound() {
        if (m_sein.PlayerAbilities.ChargeFlameBlast.HasAbility) {
            return ChargingSoundLevelC;
        }

        if (m_sein.PlayerAbilities.ChargeFlameBurn.HasAbility) {
            return ChargingSoundLevelB;
        }

        return ChargingSoundLevelA;
    }

    public Dictionary<IAttackable, CapturedProjectile> CapturedProjectiles => m_capturedProjectiles;

    public void OnEnterChargedState() {
        m_capturedProjectiles.Clear();
    }

    public SoundSource ChargingSoundLevelA;

    public SoundSource ChargingSoundLevelB;

    public SoundSource ChargingSoundLevelC;

    public AchievementAsset KillEnemiesSimultaneouslyAchievement;

    public SoundProvider NotEnoughEnergySound;

    public ChargeFlameDefinitions ChargeFlameSettings;

    public States State = new States();

    private StateMachine Logic = new StateMachine();

    private GameObject m_chargeFlameChargeEffect;

    public float EnergyCost = 1f;

    private static readonly List<LegacyAnimator> s_legacyAnimatorList = new List<LegacyAnimator>();

    private SeinCharacter m_sein;

    private float m_captureRadius = 9f;

    private Dictionary<IAttackable, CapturedProjectile> m_capturedProjectiles = new Dictionary<IAttackable, CapturedProjectile>();

    [Serializable]
    public class ChargeFlameDefinitions {
        public float ChargeDuration = 1f;

        public GameObject ChargeFlameBurstA;

        public GameObject ChargeFlameBurstB;

        public GameObject ChargeFlameBurstC;

        public GameObject ChargeFlameChargeEffectPrefab;
    }

    public class States {
        public State Start;

        public State Precharging;

        public State Charging;

        public State Charged;
    }

    public class CapturedProjectile {
        public Vector3 Direction;

        public float CapturedVelocity;

        public bool IsDestroyed;
    }
}
