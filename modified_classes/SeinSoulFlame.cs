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

    public bool SoulFlameExists => checkpointMarkerGameObject;

    public Vector3 SoulFlamePosition => checkpointMarkerGameObject.transform.position;

    public new void Awake() {
        base.Awake();
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
        Events.Scheduler.OnGameReset.Add(OnGameReset);
    }

    public void OnGameReset() {
        numberOfSoulFlamesCast = 0;
    }

    public void OnRestoreCheckpoint() {
        if (CanAffordSoulFlame) {
            cooldownRemaining = 0f;
        }

        LockSoulFlame = false;
        nagTimer = NagDuration;
    }

    public override void OnDestroy() {
        base.OnDestroy();
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
        Events.Scheduler.OnGameReset.Remove(OnGameReset);
        if (checkpointMarkerGameObject) {
            InstantiateUtility.Destroy(checkpointMarkerGameObject);
            soulFlame = null;
            checkpointMarkerGameObject = null;
        }
    }

    public void FillSoulFlameBar() {
        cooldownRemaining = 0f;
        nagTimer = 0f;
    }

    public bool InsideCheckpointMarker => soulFlame && soulFlame.IsInside;

    public SoulFlamePlacementSafety IsSafeToCastSoulFlame {
        get {
            var position = sein.Position;
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

            for (var k = 0; k < sein.Abilities.SpiritFlameTargetting.ClosestAttackables.Count; k++) {
                var entityTargetting = sein.Abilities.SpiritFlameTargetting.ClosestAttackables[k] as EntityTargetting;
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

            if (sein.Mortality.DamageReciever.IsInvinsible) {
                return SoulFlamePlacementSafety.UnsafeZone;
            }

            var groundCollider = sein.PlatformBehaviour.PlatformMovementListOfColliders.GroundCollider;
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

    public float BarValue => (1f - CooldownRemaining) * (1f - holdDownTime);

    public float CooldownRemaining => cooldownRemaining;

    public bool ShowFlameOnUI => Mathf.Approximately(BarValue, 1f);

    public float SoulFlameCost {
        get {
            if (sein.PlayerAbilities.SoulFlameEfficiency.HasAbility) {
                return 0.5f;
            }

            return 1f;
        }
    }

    public bool CanAffordSoulFlame => sein.Energy.CanAfford(SoulFlameCost);

    public bool AllowedToAccessSkillTree => sein.Level.Current > 0 && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe;

    public bool PlayerCouldSoulFlame => Characters.Sein.Controller.CanMove && !sein.Controller.IsSwimming && !UI.Fader.IsFadingInOrStay() && !SeinAbilityRestrictZone.IsInside() && !LockSoulFlame;

    public void HandleNagging() {
        if (readyForReadySequence && PlayerCouldSoulFlame && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe && CanAffordSoulFlame) {
            readyForReadySequence = false;
            InstantiateUtility.Instantiate(SoulFlameReadyText, Characters.Ori.transform.position, Quaternion.identity);
            UI.SeinUI.OnSoulFlameReady();
            var gameObject = Instantiate(SoulFlameReadyEffect);
            gameObject.transform.parent = Characters.Ori.transform;
            gameObject.transform.localPosition = Vector3.zero;
            Sound.Play(SoulFlameReadySoundProvider.GetSound(null), Characters.Sein.Position, null);
            nagTimer = NagDuration;
        }

        if (nagTimer > 0f) {
            nagTimer -= Time.deltaTime;
            if (nagTimer <= 0f) {
                if (PlayerCouldSoulFlame && CanAffordSoulFlame && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe) {
                    nagTimer = 0f;
                    InstantiateUtility.Instantiate(SoulFlameReadyText, Characters.Ori.transform.position, Quaternion.identity);
                    UI.SeinUI.OnSoulFlameReady();
                    Sound.Play(SoulFlameReadySoundProvider.GetSound(null), Characters.Sein.Position, null);
                    nagTimer = NagDuration;
                    return;
                }

                nagTimer = 2f;
            }
        }
    }

    private void HandleDelayOnGround() {
        if (!sein.IsOnGround) {
            delayOnGround = 0.1f;
            return;
        }

        delayOnGround = Mathf.Max(0f, delayOnGround - Time.deltaTime);
    }

    public override void UpdateCharacterState() {
        if (sein.Controller.IsBashing) {
            return;
        }

        HandleDelayOnGround();
        HandleCooldown();
        HandleCheckpointMarkerVisibility();
        HandleNagging();
        HandleSkillTreeHint();
        HandleCharging();
        if (sein.Energy.Max == 0f) {
            cooldownRemaining = 1f;
        }

        if (!UI.Fader.IsFadingInOrStay()) {
            if (Input.SoulFlame.OnPressed && !Input.SoulFlame.Used && !Input.Cancel.Used) {
                isCasting = true;
                if (InsideCheckpointMarker) {
                    tapRemainingTime = 0.3f;
                } else if (!CanAffordSoulFlame) {
                    HideOtherMessages();
                    UI.SeinUI.ShakeEnergyOrbBar();
                    sein.Energy.NotifyOutOfEnergy();
                } else if (cooldownRemaining != 0f) {
                    HideOtherMessages();
                    notReadyHint = UI.Hints.Show(NotReadyMessage, HintLayer.SoulFlame, 1f);
                    Sound.Play(NotReadySound.GetSound(null), transform.position, null);
                } else if (IsSafeToCastSoulFlame != SoulFlamePlacementSafety.Safe) {
                    HideOtherMessages();
                    switch (IsSafeToCastSoulFlame) {
                        case SoulFlamePlacementSafety.UnsafeEnemies:
                            notSafeHint = UI.Hints.Show(NotSafeEnemiesMessage, HintLayer.SoulFlame, 1f);
                            break;
                        case SoulFlamePlacementSafety.UnsafeGround:
                            notSafeHint = UI.Hints.Show(NotSafeGroundMessage, HintLayer.SoulFlame, 1f);
                            break;
                        case SoulFlamePlacementSafety.UnsafeZone:
                            notSafeHint = UI.Hints.Show(NotSafeZoneMessage, HintLayer.SoulFlame, 1f);
                            break;
                        case SoulFlamePlacementSafety.SavePedestal:
                            notSafeHint = UI.Hints.Show(SavePedestalZoneMessage, HintLayer.SoulFlame, 1f);
                            break;
                    }

                    Sound.Play(NotSafeSound.GetSound(null), transform.position, null);
                }
            }

            if (isCasting && sein.IsOnGround && delayOnGround == 0f && tapRemainingTime > 0f) {
                tapRemainingTime -= Time.deltaTime;
                if (tapRemainingTime < 0f && InsideCheckpointMarker && Characters.Sein.PlayerAbilities.Rekindle.HasAbility && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe) {
                    OnSoulFlameCast();
                    var position = Characters.Sein.Position;
                    Characters.Sein.Position = soulFlame.Position;
                    SaveSlotBackupsManager.CreateCurrentBackup();
                    GameController.Instance.CreateCheckpoint();
                    Characters.Sein.Position = position;
                    GameController.Instance.SaveGameController.PerformSave();
                    soulFlame.OnRekindle();
                    GameController.Instance.PerformSaveGameSequence();
                }
            }

            if (Input.SoulFlame.Released) {
                isCasting = false;
                if (tapRemainingTime > 0f) {
                    tapRemainingTime = 0f;
                    if (AllowedToAccessSkillTree && InsideCheckpointMarker) {
                        if (skillTreeHint) {
                            skillTreeHint.Visibility.HideImmediately();
                        }

                        Input.Start.Used = true;
                        UI.Menu.ShowSkillTree();
                    }
                }
            }
        } else {
            tapRemainingTime = 0f;
        }

        if (holdDownTime == 1f && sein.IsOnGround && delayOnGround == 0f) {
            CastSoulFlame();
        }
    }

    private void CastSoulFlame() {
        if (ChargingSound) {
            ChargingSound.StopAndFadeOut(0.1f);
        }

        sein.Energy.Spend(SoulFlameCost);
        cooldownRemaining = 1f;
        holdDownTime = 0f;
        if (sein.PlayerAbilities.Regroup.HasAbility) {
            sein.Mortality.Health.GainHealth(4);
        }

        if (sein.PlayerAbilities.UltraSoulFlame.HasAbility) {
            sein.Mortality.Health.GainHealth(4);
        }

        sceneCheckpoint = new MoonGuid(Scenes.Manager.CurrentScene.SceneMoonGuid);
        if (checkpointMarkerGameObject) {
            checkpointMarkerGameObject.GetComponent<SoulFlame>().Disappear();
        }

        SpawnSoulFlame(Characters.Sein.Position);
        RandomizerBonusSkill.LastSoulLink = Characters.Sein.Position;
        RandomizerStatsManager.OnSave();
        OnSoulFlameCast();
        SaveSlotBackupsManager.CreateCurrentBackup();
        GameController.Instance.CreateCheckpoint();
        GameController.Instance.SaveGameController.PerformSave();
        numberOfSoulFlamesCast++;
        if (numberOfSoulFlamesCast == 50) {
            AchievementsController.AwardAchievement(AchievementsLogic.Instance.SoulLinkManyTimesAchievementAsset);
        }

        if (CheckpointSequence) {
            CheckpointSequence.Perform(null);
        }
    }

    private void HandleCharging() {
        if (isCasting && CanAffordSoulFlame && IsSafeToCastSoulFlame == SoulFlamePlacementSafety.Safe && cooldownRemaining == 0f && !InsideCheckpointMarker && PlayerCouldSoulFlame) {
            if (holdDownTime == 0f && ChargingSound) {
                ChargingSound.Play();
            }

            holdDownTime += Time.deltaTime / HoldDownDuration;
            if (holdDownTime > 1f) {
                holdDownTime = 1f;
            }

            ChargeEffectAnimator.AnimatorDriver.ContinueForward();
            return;
        }

        ChargeEffectAnimator.AnimatorDriver.ContinueBackwards();
        if (ChargingSound && ChargingSound.IsPlaying) {
            ChargingSound.StopAndFadeOut(0.1f);
        }

        if (holdDownTime > 0f) {
            if (AbortChargingSound && !AbortChargingSound.IsPlaying) {
                AbortChargingSound.Play();
            }

            holdDownTime -= Time.deltaTime / HoldDownDuration;
            if (holdDownTime <= 0f) {
                holdDownTime = 0f;
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
        if (cooldownRemaining > 0f) {
            nagTimer = 0f;
            if (sein.PlayerAbilities.Rekindle.HasAbility) {
                cooldownRemaining -= Time.deltaTime / RekindleCooldownDuration;
            } else {
                cooldownRemaining -= Time.deltaTime / CooldownDuration;
            }

            if (cooldownRemaining <= 0f) {
                cooldownRemaining = 0f;
                readyForReadySequence = true;
            }
        }
    }

    private void HandleCheckpointMarkerVisibility() {
        if (checkpointMarkerGameObject) {
            var flag = Scenes.Manager.SceneIsEnabled(sceneCheckpoint);
            var flag2 = UI.Cameras.Current.IsOnScreenPadded(soulFlame.Position, 5f);
            if (checkpointMarkerGameObject.activeSelf) {
                if (!flag && !flag2) {
                    checkpointMarkerGameObject.SetActive(false);
                }
            } else if (flag) {
                checkpointMarkerGameObject.SetActive(true);
            }
        }
    }

    private void HandleSkillTreeHint() {
        if (AllowedToAccessSkillTree) {
            if (InsideCheckpointMarker && SkillTreeMessage && SkillTreeRekindleMessage && PlayerCouldSoulFlame) {
                if (skillTreeHint == null) {
                    var messageProvider = !Characters.Sein.PlayerAbilities.Rekindle.HasAbility || IsSafeToCastSoulFlame != SoulFlamePlacementSafety.Safe ? SkillTreeMessage : SkillTreeRekindleMessage;
                    skillTreeHint = UI.Hints.Show(messageProvider, HintLayer.SoulFlame, float.PositiveInfinity);
                }
            } else if (skillTreeHint) {
                skillTreeHint.HideMessageScreen();
            }
        }
    }

    public void HideOtherMessages() {
        if (notReadyHint) {
            notReadyHint.HideMessageScreen();
        }

        if (notSafeHint) {
            notSafeHint.HideMessageScreen();
        }
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        this.sein = sein;
        this.sein.SoulFlame = this;
    }

    public override void Serialize(Archive ar) {
        base.Serialize(ar);
        ar.Serialize(ref cooldownRemaining);
        ar.Serialize(ref readyForReadySequence);
        ar.Serialize(ref nagTimer);
        sceneCheckpoint.Serialize(ar);
        ar.Serialize(ref numberOfSoulFlamesCast);
        if (ar.Writing) {
            ar.Serialize(soulFlame != null);
            if (soulFlame) {
                ar.Serialize(soulFlame.Position);
            }
        } else {
            var flag = false;
            ar.Serialize(ref flag);
            if (flag) {
                var zero = Vector3.zero;
                ar.Serialize(ref zero);
                if (soulFlame) {
                    soulFlame.Position = zero;
                    return;
                }

                SpawnSoulFlame(zero);
            } else {
                DestroySoulFlame();
            }
        }
    }

    public void SpawnSoulFlame(Vector3 position) {
        checkpointMarkerGameObject = (GameObject)InstantiateUtility.Instantiate(CheckpointMarker, position, Quaternion.identity);
        soulFlame = checkpointMarkerGameObject.GetComponent<SoulFlame>();
    }

    public void DestroySoulFlame() {
        if (soulFlame) {
            InstantiateUtility.Destroy(soulFlame.gameObject);
            soulFlame = null;
            checkpointMarkerGameObject = null;
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

    private MessageBox notSafeHint;

    private MessageBox notReadyHint;

    private MessageBox skillTreeHint;

    private GameObject checkpointMarkerGameObject;

    private SoulFlame soulFlame;

    private SeinCharacter sein;

    private int numberOfSoulFlamesCast;

    private float holdDownTime;

    public float HoldDownDuration = 0.7f;

    private float nagTimer;

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

    private float cooldownRemaining;

    private bool readyForReadySequence;

    private float tapRemainingTime;

    private MoonGuid sceneCheckpoint = new MoonGuid(0, 0, 0, 0);

    private bool isCasting;

    private float delayOnGround;

    public enum SoulFlamePlacementSafety {
        Safe,
        UnsafeEnemies,
        UnsafeGround,
        UnsafeZone,
        SavePedestal,
    }
}
