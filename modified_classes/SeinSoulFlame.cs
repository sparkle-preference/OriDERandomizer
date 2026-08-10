using System;
using Core;
using Game;
using UnityEngine;
using Input = Core.Input;

public class SeinSoulFlame : CharacterState, ISeinReceiver {
    static SeinSoulFlame() {
        OnSoulFlameCast = delegate { };
    }

    public static event Action OnSoulFlameCast;

    public bool SoulFlameExists => m_checkpointMarkerGameObject;

    public Vector3 SoulFlamePosition => m_checkpointMarkerGameObject.transform.position;

    public new void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
        Events.Scheduler.OnGameReset.Add(OnGameReset);
    }

    public void OnGameReset() {
        m_numberOfSoulFlamesCast = 0;
    }

    public void OnRestoreCheckpoint() {
        if (CanAffordSoulFlame) {
            m_cooldownRemaining = 0f;
        }

        LockSoulFlame = false;
        m_nagTimer = NagDuration;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
        Events.Scheduler.OnGameReset.Remove(OnGameReset);
        if (m_checkpointMarkerGameObject) {
            InstantiateUtility.Destroy(m_checkpointMarkerGameObject);
            m_soulFlame = null;
            m_checkpointMarkerGameObject = null;
        }
    }

    public void FillSoulFlameBar() {
        m_cooldownRemaining = 0f;
        m_nagTimer = 0f;
    }

    public bool InsideCheckpointMarker => m_soulFlame && m_soulFlame.IsInside;

    public SoulFlamePlacementSafety IsSafeToCastSoulFlame {
        get {
            var position = m_sein.Position;
            for (var i = 0; i < NoSoulFlameZone.All.Count; i++) {
                if (NoSoulFlameZone.All[i].BoundingRect.Contains(position)) {
                    return SoulFlamePlacementSafety.UnsafeZone;
                }
            }

            if (!Sein.World.Events.DarknessLifted && SpiritLightDarknessZone.IsInsideDarknessZone(position) && !SaveInTheDarkZone.IsInside(position) && !LightSource.TestPosition(position, 0f)) {
                return SoulFlamePlacementSafety.UnsafeZone;
            }

            for (var j = 0; j < SavePedestal.All.Count; j++) {
                if (SavePedestal.All[j].IsInside) {
                    return SoulFlamePlacementSafety.SavePedestal;
                }
            }

            for (var k = 0; k < m_sein.Abilities.SpiritFlameTargetting.ClosestAttackables.Count; k++) {
                var entityTargetting = m_sein.Abilities.SpiritFlameTargetting.ClosestAttackables[k] as EntityTargetting;
                if (entityTargetting && entityTargetting.Entity is Enemy) {
                    return SoulFlamePlacementSafety.UnsafeEnemies;
                }
            }

            for (var l = 0; l < RespawningPlaceholder.All.Count; l++) {
                var respawningPlaceholder = RespawningPlaceholder.All[l];
                if (!respawningPlaceholder.EntityIsDead && Vector3.Distance(position, respawningPlaceholder.Position) < 10f) {
                    return SoulFlamePlacementSafety.UnsafeEnemies;
                }
            }

            if (m_sein.Mortality.DamageReciever.IsInvinsible) {
                return SoulFlamePlacementSafety.UnsafeZone;
            }

            var groundCollider = m_sein.PlatformBehaviour.PlatformMovementListOfColliders.GroundCollider;
            if (groundCollider) {
                if (groundCollider.attachedRigidbody) {
                    return SoulFlamePlacementSafety.UnsafeGround;
                }

                if (groundCollider.GetComponent<HeatUpPlatform>()) {
                    return SoulFlamePlacementSafety.UnsafeGround;
                }
            }

            if (Physics.SphereCast(new Ray(position, Vector3.right), 0.5f, 0.7f, UnsafeMask) | Physics.SphereCast(new Ray(position, -Vector3.right), 0.5f, 0.7f, UnsafeMask)) {
                return SoulFlamePlacementSafety.UnsafeGround;
            }

            return SoulFlamePlacementSafety.Safe;
        }
    }

    public float BarValue => (1f - CooldownRemaining) * (1f - m_holdDownTime);

    public float CooldownRemaining => m_cooldownRemaining;

    public bool ShowFlameOnUI => Mathf.Approximately(BarValue, 1f);

    public float SoulFlameCost {
        get {
            if (m_sein.PlayerAbilities.SoulFlameEfficiency.HasAbility) {
                return 0.5f;
            }

            return 1f;
        }
    }

    public bool CanAffordSoulFlame => m_sein.Energy.CanAfford(SoulFlameCost);

    public bool AllowedToAccessSkillTree => m_sein.Level.Current > 0 && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe;

    public bool PlayerCouldSoulFlame => Characters.Sein.Controller.CanMove && !m_sein.Controller.IsSwimming && !UI.Fader.IsFadingInOrStay() && !SeinAbilityRestrictZone.IsInside() && !LockSoulFlame;

    public void HandleNagging() {
        if (m_readyForReadySequence && PlayerCouldSoulFlame && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe && CanAffordSoulFlame) {
            m_readyForReadySequence = false;
            InstantiateUtility.Instantiate(SoulFlameReadyText, Characters.Ori.transform.position, Quaternion.identity);
            UI.SeinUI.OnSoulFlameReady();
            var gameObject = Instantiate(SoulFlameReadyEffect);
            gameObject.transform.parent = Characters.Ori.transform;
            gameObject.transform.localPosition = Vector3.zero;
            Sound.Play(SoulFlameReadySoundProvider.GetSound(null), Characters.Sein.Position, null);
            m_nagTimer = NagDuration;
        }

        if (m_nagTimer > 0f) {
            m_nagTimer -= Time.deltaTime;
            if (m_nagTimer <= 0f) {
                if (PlayerCouldSoulFlame && CanAffordSoulFlame && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe) {
                    m_nagTimer = 0f;
                    InstantiateUtility.Instantiate(SoulFlameReadyText, Characters.Ori.transform.position, Quaternion.identity);
                    UI.SeinUI.OnSoulFlameReady();
                    Sound.Play(SoulFlameReadySoundProvider.GetSound(null), Characters.Sein.Position, null);
                    m_nagTimer = NagDuration;
                    return;
                }

                m_nagTimer = 2f;
            }
        }
    }

    private void HandleDelayOnGround() {
        if (!m_sein.IsOnGround) {
            m_delayOnGround = 0.1f;
            return;
        }

        m_delayOnGround = Mathf.Max(0f, m_delayOnGround - Time.deltaTime);
    }

    public override void UpdateCharacterState() {
        if (m_sein.Controller.IsBashing) {
            return;
        }

        HandleDelayOnGround();
        HandleCooldown();
        HandleCheckpointMarkerVisibility();
        HandleNagging();
        HandleSkillTreeHint();
        HandleCharging();
        if (m_sein.Energy.Max == 0f) {
            m_cooldownRemaining = 1f;
        }

        if (!UI.Fader.IsFadingInOrStay()) {
            if (Input.SoulFlame.OnPressed && !Input.SoulFlame.Used && !Input.Cancel.Used) {
                m_isCasting = true;
                if (InsideCheckpointMarker) {
                    m_tapRemainingTime = 0.3f;
                } else if (!CanAffordSoulFlame) {
                    HideOtherMessages();
                    UI.SeinUI.ShakeEnergyOrbBar();
                    m_sein.Energy.NotifyOutOfEnergy();
                } else if (m_cooldownRemaining != 0f) {
                    HideOtherMessages();
                    m_notReadyHint = UI.Hints.Show(NotReadyMessage, HintLayer.SoulFlame, 1f);
                    Sound.Play(NotReadySound.GetSound(null), transform.position, null);
                } else if (IsSafeToCastSoulFlame != SoulFlamePlacementSafety.Safe) {
                    HideOtherMessages();
                    switch (IsSafeToCastSoulFlame) {
                        case SoulFlamePlacementSafety.UnsafeEnemies:
                            m_notSafeHint = UI.Hints.Show(NotSafeEnemiesMessage, HintLayer.SoulFlame, 1f);
                            break;
                        case SoulFlamePlacementSafety.UnsafeGround:
                            m_notSafeHint = UI.Hints.Show(NotSafeGroundMessage, HintLayer.SoulFlame, 1f);
                            break;
                        case SoulFlamePlacementSafety.UnsafeZone:
                            m_notSafeHint = UI.Hints.Show(NotSafeZoneMessage, HintLayer.SoulFlame, 1f);
                            break;
                        case SoulFlamePlacementSafety.SavePedestal:
                            m_notSafeHint = UI.Hints.Show(SavePedestalZoneMessage, HintLayer.SoulFlame, 1f);
                            break;
                    }

                    Sound.Play(NotSafeSound.GetSound(null), transform.position, null);
                }
            }

            if (m_isCasting && m_sein.IsOnGround && m_delayOnGround == 0f && m_tapRemainingTime > 0f) {
                m_tapRemainingTime -= Time.deltaTime;
                if (m_tapRemainingTime < 0f && InsideCheckpointMarker && Characters.Sein.PlayerAbilities.Rekindle.HasAbility && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe) {
                    OnSoulFlameCast();
                    var position = Characters.Sein.Position;
                    Characters.Sein.Position = m_soulFlame.Position;
                    SaveSlotBackupsManager.CreateCurrentBackup();
                    GameController.Instance.CreateCheckpoint();
                    Characters.Sein.Position = position;
                    GameController.Instance.SaveGameController.PerformSave();
                    m_soulFlame.OnRekindle();
                    GameController.Instance.PerformSaveGameSequence();
                }
            }

            if (Input.SoulFlame.Released) {
                m_isCasting = false;
                if (m_tapRemainingTime > 0f) {
                    m_tapRemainingTime = 0f;
                    if (AllowedToAccessSkillTree && InsideCheckpointMarker) {
                        if (m_skillTreeHint) {
                            m_skillTreeHint.Visibility.HideImmediately();
                        }

                        Input.Start.Used = true;
                        UI.Menu.ShowSkillTree();
                    }
                }
            }
        } else {
            m_tapRemainingTime = 0f;
        }

        if (m_holdDownTime == 1f && m_sein.IsOnGround && m_delayOnGround == 0f) {
            CastSoulFlame();
        }
    }

    private void CastSoulFlame() {
        if (ChargingSound) {
            ChargingSound.StopAndFadeOut(0.1f);
        }

        m_sein.Energy.Spend(SoulFlameCost);
        m_cooldownRemaining = 1f;
        m_holdDownTime = 0f;
        if (m_sein.PlayerAbilities.Regroup.HasAbility) {
            m_sein.Mortality.Health.GainHealth(4);
        }

        if (m_sein.PlayerAbilities.UltraSoulFlame.HasAbility) {
            m_sein.Mortality.Health.GainHealth(4);
        }

        m_sceneCheckpoint = new MoonGuid(Scenes.Manager.CurrentScene.SceneMoonGuid);
        if (m_checkpointMarkerGameObject) {
            m_checkpointMarkerGameObject.GetComponent<SoulFlame>().Disappear();
        }

        SpawnSoulFlame(Characters.Sein.Position);
        RandomizerBonusSkill.LastSoulLink = Characters.Sein.Position;
        RandomizerStatsManager.OnSave();
        OnSoulFlameCast();
        SaveSlotBackupsManager.CreateCurrentBackup();
        GameController.Instance.CreateCheckpoint();
        GameController.Instance.SaveGameController.PerformSave();
        m_numberOfSoulFlamesCast++;
        if (m_numberOfSoulFlamesCast == 50) {
            AchievementsController.AwardAchievement(AchievementsLogic.Instance.SoulLinkManyTimesAchievementAsset);
        }

        if (CheckpointSequence) {
            CheckpointSequence.Perform(null);
        }
    }

    private void HandleCharging() {
        if (m_isCasting && CanAffordSoulFlame && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe && m_cooldownRemaining == 0f && !InsideCheckpointMarker && PlayerCouldSoulFlame) {
            if (m_holdDownTime == 0f && ChargingSound) {
                ChargingSound.Play();
            }

            m_holdDownTime += Time.deltaTime / HoldDownDuration;
            if (m_holdDownTime > 1f) {
                m_holdDownTime = 1f;
            }

            ChargeEffectAnimator.AnimatorDriver.ContinueForward();
            return;
        }

        ChargeEffectAnimator.AnimatorDriver.ContinueBackwards();
        if (ChargingSound && ChargingSound.IsPlaying) {
            ChargingSound.StopAndFadeOut(0.1f);
        }

        if (m_holdDownTime > 0f) {
            if (AbortChargingSound && !AbortChargingSound.IsPlaying) {
                AbortChargingSound.Play();
            }

            m_holdDownTime -= Time.deltaTime / HoldDownDuration;
            if (m_holdDownTime <= 0f) {
                m_holdDownTime = 0f;
                if (AbortChargingSound) {
                    AbortChargingSound.StopAndFadeOut(0.1f);
                }

                if (FullyAbortedSound) {
                    Sound.Play(FullyAbortedSound.GetSound(null), transform.position, null);
                }
            }
        }
    }

    private void HandleCooldown() {
        if (m_cooldownRemaining > 0f) {
            m_nagTimer = 0f;
            if (m_sein.PlayerAbilities.Rekindle.HasAbility) {
                m_cooldownRemaining -= Time.deltaTime / RekindleCooldownDuration;
            } else {
                m_cooldownRemaining -= Time.deltaTime / CooldownDuration;
            }

            if (m_cooldownRemaining <= 0f) {
                m_cooldownRemaining = 0f;
                m_readyForReadySequence = true;
            }
        }
    }

    private void HandleCheckpointMarkerVisibility() {
        if (m_checkpointMarkerGameObject) {
            var flag = Scenes.Manager.SceneIsEnabled(m_sceneCheckpoint);
            var flag2 = UI.Cameras.Current.IsOnScreenPadded(m_soulFlame.Position, 5f);
            if (m_checkpointMarkerGameObject.activeSelf) {
                if (!flag && !flag2) {
                    m_checkpointMarkerGameObject.SetActive(false);
                }
            } else if (flag) {
                m_checkpointMarkerGameObject.SetActive(true);
            }
        }
    }

    private void HandleSkillTreeHint() {
        if (AllowedToAccessSkillTree) {
            if (InsideCheckpointMarker && SkillTreeMessage && SkillTreeRekindleMessage && PlayerCouldSoulFlame) {
                if (m_skillTreeHint == null) {
                    var messageProvider = !Characters.Sein.PlayerAbilities.Rekindle.HasAbility || IsSafeToCastSoulFlame != SoulFlamePlacementSafety.Safe ? SkillTreeMessage : SkillTreeRekindleMessage;
                    m_skillTreeHint = UI.Hints.Show(messageProvider, HintLayer.SoulFlame, float.PositiveInfinity);
                }
            } else if (m_skillTreeHint) {
                m_skillTreeHint.HideMessageScreen();
            }
        }
    }

    public void HideOtherMessages() {
        if (m_notReadyHint) {
            m_notReadyHint.HideMessageScreen();
        }

        if (m_notSafeHint) {
            m_notSafeHint.HideMessageScreen();
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        m_sein = sein;
        m_sein.SoulFlame = this;
    }

    public override void Serialize(Archive ar) {
        base.Serialize(ar);
        ar.Serialize(ref m_cooldownRemaining);
        ar.Serialize(ref m_readyForReadySequence);
        ar.Serialize(ref m_nagTimer);
        m_sceneCheckpoint.Serialize(ar);
        ar.Serialize(ref m_numberOfSoulFlamesCast);
        if (ar.Writing) {
            ar.Serialize(m_soulFlame != null);
            if (m_soulFlame) {
                ar.Serialize(m_soulFlame.Position);
            }
        } else {
            var flag = false;
            ar.Serialize(ref flag);
            if (flag) {
                var zero = Vector3.zero;
                ar.Serialize(ref zero);
                if (m_soulFlame) {
                    m_soulFlame.Position = zero;
                    return;
                }

                SpawnSoulFlame(zero);
            } else {
                DestroySoulFlame();
            }
        }
    }

    public void SpawnSoulFlame(Vector3 position) {
        m_checkpointMarkerGameObject = (GameObject)InstantiateUtility.Instantiate(CheckpointMarker, position, Quaternion.identity);
        m_soulFlame = m_checkpointMarkerGameObject.GetComponent<SoulFlame>();
    }

    public void DestroySoulFlame() {
        if (m_soulFlame) {
            InstantiateUtility.Destroy(m_soulFlame.gameObject);
            m_soulFlame = null;
            m_checkpointMarkerGameObject = null;
        }
    }

    public BaseAnimator ChargeEffectAnimator;

    public GameObject CheckpointMarker;

    public ActionMethod CheckpointSequence;

    public AnimationCurve ParticleRateOverSpeed;

    public AchievementAsset CreateManySoulLinkAchievement;

    public MessageProvider SkillTreeRekindleMessage;

    public MessageProvider SkillTreeMessage;

    public MessageProvider NotSafeZoneMessage;

    public MessageProvider NotSafeEnemiesMessage;

    public MessageProvider NotSafeGroundMessage;

    public MessageProvider SavePedestalZoneMessage;

    public MessageProvider NotReadyMessage;

    public LayerMask UnsafeMask;

    private MessageBox m_notSafeHint;

    private MessageBox m_notReadyHint;

    private MessageBox m_skillTreeHint;

    private GameObject m_checkpointMarkerGameObject;

    private SoulFlame m_soulFlame;

    private SeinCharacter m_sein;

    private int m_numberOfSoulFlamesCast;

    private float m_holdDownTime;

    public float HoldDownDuration = 0.7f;

    private float m_nagTimer;

    public float NagDuration = 120f;

    public bool LockSoulFlame;

    public SoundProvider NotSafeSound;

    public SoundProvider NotReadySound;

    public SoundSource ChargingSound;

    public SoundSource AbortChargingSound;

    public SoundProvider FullyAbortedSound;

    public SoundProvider SoulFlameReadySoundProvider;

    public GameObject SoulFlameReadyEffect;

    public GameObject SoulFlameReadyText;

    public float CooldownDuration = 60f;

    public float RekindleCooldownDuration = 10f;

    private float m_cooldownRemaining;

    private bool m_readyForReadySequence;

    private float m_tapRemainingTime;

    private MoonGuid m_sceneCheckpoint = new MoonGuid(0, 0, 0, 0);

    private bool m_isCasting;

    private float m_delayOnGround;

    public enum SoulFlamePlacementSafety {
        Safe,
        UnsafeEnemies,
        UnsafeGround,
        UnsafeZone,
        SavePedestal
    }
}
