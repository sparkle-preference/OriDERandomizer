using System.Collections;
using Core;
using Game;
using UnityEngine;

public class SeinDamageReciever : CharacterState, IDamageReciever, ISeinReceiver, IProjectileDetonatable {
    public CharacterLeftRightMovement CharacterLeftRightMovement => Sein.PlatformBehaviour.LeftRightMovement;

    public CharacterGravity CharacterGravity => Sein.PlatformBehaviour.Gravity;

    public CharacterInstantStop CharacterInstantStop => Sein.PlatformBehaviour.InstantStop;

    public SeinHealthController HealthController => Sein.Mortality.Health;

    public PlatformMovement PlatformMovement => Sein.PlatformBehaviour.PlatformMovement;

    public Renderer Sprite => Sein.PlatformBehaviour.Visuals.SpriteRenderer;

    public void Start() {
        CharacterGravity.ModifyGravityPlatformMovementSettingsEvent += ModifyGravityPlatformMovementSettings;
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent += ModifyHorizontalPlatformMovementSettings;
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
    }

    public new void OnDestroy() {
        base.OnDestroy();
        CharacterGravity.ModifyGravityPlatformMovementSettingsEvent -= ModifyGravityPlatformMovementSettings;
        CharacterLeftRightMovement.ModifyHorizontalPlatformMovementSettingsEvent -= ModifyHorizontalPlatformMovementSettings;
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
    }

    public override void OnEnter() {
        CharacterInstantStop.Active = false;
    }

    public override void OnExit() {
        CharacterInstantStop.Active = true;
    }

    public void OnRecieveDamage(Damage damage) {
        if (RandomizerBonusSkill.Invincible) {
            return;
        }

        if (damage.Amount < 9000f || damage.Type != DamageType.Water) {
            if (IsImmortal) {
                return;
            }

            if (!Sein.Controller.CanMove) {
                return;
            }

            if (damage.Type == DamageType.SpiritFlameSplatter || damage.Type == DamageType.LevelUp) {
                return;
            }
        }

        damage.SetAmount(Mathf.Round(damage.Amount * Randomizer.DamageModifier));
        var flag = m_invincibleTimeRemaining > 0f;
        var flag2 = m_invincibleToEnemiesTimeRemaining > 0f || (RandomizerBonus.EnhancedChargeJump && Sein.Abilities.ChargeJumpCharging && Sein.Abilities.ChargeJumpCharging.IsCharged);
        if (Sein.Abilities.Stomp && Sein.Abilities.Stomp.Logic.CurrentState == Sein.Abilities.Stomp.State.StompDown) {
            flag = true;
        }

        if (!Sein.gameObject.activeInHierarchy) {
            return;
        }

        if (flag && damage.Amount < 100f && damage.Type != DamageType.Drowning) {
            damage.SetAmount(0f);
        }

        if (flag2 && damage.Amount < 100f && (damage.Type == DamageType.Enemy || damage.Type == DamageType.Projectile || damage.Type == DamageType.SlugSpike)) {
            damage.SetAmount(0f);
        }

        if (damage.Amount < 100f && Sein.Abilities.GrabWall && RandomizerBonus.EnhancedClimb && (damage.Type == DamageType.Spikes || damage.Type == DamageType.Lava)) {
            damage.SetAmount(0f);
        }

        if (damage.Amount == 0f) {
            return;
        }

        if (damage.Amount < 100f) {
            var difficulty = DifficultyController.Instance.Difficulty;
            if (difficulty != DifficultyMode.Easy) {
                if (difficulty == DifficultyMode.Hard) {
                    damage.SetAmount(damage.Amount * 2f);
                    if (damage.Amount < 8f) {
                        damage.SetAmount(8f);
                    }
                }
            } else if (damage.Type != DamageType.Lava && damage.Type != DamageType.Spikes) {
                damage.SetAmount(damage.Amount / 2f);
            } else {
                var num = Mathf.RoundToInt(damage.Amount / 4f);
                if (num > 3) {
                    num = Mathf.FloorToInt((num - 3) * 0.5f) + 3;
                }

                damage.SetAmount(num * 4);
            }
        }

        if (Randomizer.OHKO) {
            damage.SetAmount(damage.Amount * 100f);
        }

        UI.Vignette.SeinHurt.Restart();
        var soundForDamage = (damage.Amount >= BadlyHurtAmount ? SeinBadlyHurtSound : SeinHurtSound).GetSoundForDamage(damage);
        if (soundForDamage != null) {
            var soundPlayer = Sound.Play(soundForDamage, PlatformMovement.Position, null);
            if (soundPlayer) {
                soundPlayer.AttachTo = Sein.PlatformBehaviour.transform;
            }
        }

        var num2 = Mathf.CeilToInt(damage.Amount / 4f);
        damage.SetAmount(num2);
        if (damage.Amount < 1000f && Sein.PlayerAbilities.UltraDefense.HasAbility) {
            damage.SetAmount(Mathf.RoundToInt(num2 * 0.8f));
        }

        Attacking.DamageDisplayText.Create(damage, Sein.transform);
        damage.SetAmount(num2 * 4);
        if (damage.Amount < 1000f && Sein.PlayerAbilities.UltraDefense.HasAbility) {
            damage.SetAmount(Mathf.FloorToInt(num2 * 2 * 0.8f) * 2);
        }

        var num3 = Mathf.RoundToInt(damage.Amount);
        if (num3 >= HealthController.Amount) {
            Sein.Mortality.Health.TakeDamage(num3);
            OnKill(damage);
            return;
        }

        Sein.Mortality.Health.TakeDamage(num3);
        if (damage.Type != DamageType.Drowning) {
            MakeInvincible(1f);
            StartCoroutine(FlashSprite());
            if (HurtEffect) {
                var expr_3BA = (GameObject)InstantiateUtility.Instantiate(HurtEffect);
                expr_3BA.transform.position = transform.position;
                Vector3 vector = PlatformMovement.LocalSpeed.normalized + damage.Force.normalized;
                var z = Mathf.Atan2(vector.y, vector.x) * 57.29578f;
                expr_3BA.transform.rotation = Quaternion.Euler(0f, 0f, z);
            }

            Active = true;
            if (Sein.Abilities.GrabWall) {
                Sein.Abilities.GrabWall.Exit();
            }

            if (Sein.Abilities.Dash) {
                Sein.Abilities.Dash.Exit();
            }

            PlatformMovement.LocalSpeed = damage.Force.x <= 0f ? new Vector2(-HurtSpeed.x, HurtSpeed.y) : HurtSpeed;
            m_hurtTimeRemaining = HurtDuration;
            Sein.PlatformBehaviour.Visuals.Animation.Play(HurtAnimation, 140, ShouldHurtAnimationKeepPlaying);
            return;
        }

        StartCoroutine(FlashSprite());
    }

    public void SetReferenceToSein(SeinCharacter sein) {
        Sein = sein;
    }

    public override void UpdateCharacterState() {
        if (Sein.IsSuspended) {
            return;
        }

        m_hurtTimeRemaining -= Time.deltaTime;
        m_invincibleTimeRemaining -= Time.deltaTime;
        m_invincibleToEnemiesTimeRemaining -= Time.deltaTime;
        if (m_hurtTimeRemaining < 0f) {
            m_hurtTimeRemaining = 0f;
        }

        if (m_invincibleTimeRemaining < 0f) {
            m_invincibleTimeRemaining = 0f;
        }

        if (m_invincibleToEnemiesTimeRemaining < 0f) {
            m_invincibleToEnemiesTimeRemaining = 0f;
        }

        if (Active && m_hurtTimeRemaining == 0f) {
            Active = false;
        }
    }

    public void ModifyHorizontalPlatformMovementSettings(HorizontalPlatformMovementSettings settings) {
        if (Active) {
            settings.Ground.ApplySpeedMultiplier(MoveSpeed);
            settings.Air.ApplySpeedMultiplier(MoveSpeed);
        }
    }

    public void ModifyGravityPlatformMovementSettings(GravityPlatformMovementSettings settings) {
        if (Active) {
            settings.GravityStrength *= GravityMultiplier;
        }
    }

    public void MakeInvincible(float duration) {
        m_invincibleTimeRemaining = Mathf.Max(m_invincibleTimeRemaining, duration);
    }

    public void MakeInvincibleToEnemies(float duration) {
        m_invincibleToEnemiesTimeRemaining = Mathf.Max(m_invincibleToEnemiesTimeRemaining, duration);
    }

    public void ResetInviciblity() {
        m_invincibleTimeRemaining = 0f;
        m_invincibleToEnemiesTimeRemaining = 0f;
    }

    public void OnRestoreCheckpoint() {
        SpriteMaterialTintColor(new Color(0f, 0f, 0f, 0f));
        CameraFrustumOptimizer.ForceUpdate();
        if (m_died) {
            m_died = false;
            Sein.Active = true;
            Sein.GetComponent<GoThroughPlatformHandler>().UpdateColliders();
            Sein.Mortality.Health.OnRespawn();
            if (WorldMapLogic.Instance.MapEnabledArea.FindFaceAtPositionFaster(Characters.Sein.Position) != null) {
                GameController.Instance.SaveGameController.PerformSave();
            }
        }

        RandomizerBonusSkill.UpdateDrain();
    }

    public IEnumerator FlashSprite() {
        yield return new WaitForFixedUpdate();
        for (var i = 0; i < 8; i++) {
            SpriteMaterialTintColor(Color.red);
            yield return new WaitForSeconds(0.05f);
            SpriteMaterialTintColor(new Color(0f, 0f, 0f, 0f));
            yield return new WaitForSeconds(0.05f);
        }

        yield break;
    }

    public void SpriteMaterialTintColor(Color color) {
        if (Sprite) {
            Sprite.sharedMaterial.SetColor(ShaderProperties.TintColor, color);
        }
    }

    public void OnEnable() {
        SpriteMaterialTintColor(new Color(0f, 0f, 0f, 0f));
        m_invincibleTimeRemaining = 0f;
        m_invincibleToEnemiesTimeRemaining = 0f;
    }

    public bool IsInvinsible => m_invincibleTimeRemaining > 0f;

    public bool ShouldHurtAnimationKeepPlaying() {
        return !PlatformMovement.IsOnGround;
    }

    public void OnKill(Damage damage) {
        if (!Sein.Active) {
            return;
        }

        BingoController.OnDeath(damage);
        m_died = true;
        var soundForDamage = SeinDeathSound.GetSoundForDamage(damage);
        if (soundForDamage != null) {
            var soundPlayer = Sound.Play(soundForDamage, PlatformMovement.Position, null);
            if (soundPlayer) {
                soundPlayer.AttachTo = Sein.PlatformBehaviour.transform;
            }
        }

        Utility.DisableLate(Sein);
        SeinDeathCounter.Count++;
        SeinDeathsManager.OnDeath();
        GameController.Instance.ResumeGameplay();
        if (DeathEffectProvider) {
            InstantiateDeathEffect(damage);
        }

        Events.Scheduler.OnPlayerDeath.Call();
        if (DifficultyController.Instance.Difficulty == DifficultyMode.OneLife) {
            SaveSlotsManager.CurrentSaveSlot.WasKilled = true;
            GameController.Instance.SaveGameController.PerformSave();
            SaveSlotBackupsManager.DeleteAllBackups(SaveSlotsManager.CurrentSlotIndex);
        }

        GameController.Instance.StartCoroutine(OnKillRoutine());
    }

    private void InstantiateDeathEffect(Damage damage) {
        var gameObject = (GameObject)InstantiateUtility.Instantiate(DeathEffectProvider.Prefab(new DamageContext(damage)));
        damage.DealToComponents(gameObject);
        var transform = Sein.PlatformBehaviour.Visuals.SpriteMirror.transform;
        gameObject.transform.localPosition = transform.position;
        gameObject.transform.localScale = transform.localScale;
        gameObject.transform.localRotation = transform.localRotation;
    }

    public IEnumerator OnKillRoutine() {
        var deathDuration = DeathDuration;
        for (var t = 0f; t < deathDuration; t += !Sein.IsSuspended ? Time.deltaTime : 0f) {
            if (Characters.Sein == null) {
                yield break;
            }

            yield return new WaitForFixedUpdate();
        }

        if (DifficultyController.Instance.Difficulty == DifficultyMode.OneLife) {
            InstantiateUtility.Instantiate(GameOverScreen, Vector3.zero, Quaternion.identity);
        } else {
            UI.Fader.Fade(DeathFadeInDuration, 0f, DeathFadeOutDuration, OnKillFadeInComplete, null);
        }

        yield return new WaitForSeconds(DeathFadeInDuration);
        SeinDeathCounter.SendTelemetryData();
    }

    public void OnKillFadeInComplete() {
        GameController.Instance.RestoreCheckpoint();
    }

    public bool CanDetonateProjectiles() {
        return m_invincibleToEnemiesTimeRemaining == 0f;
    }

    public override void Serialize(Archive ar) {
        base.Serialize(ar);
        ar.Serialize(ref m_serializationFiller);
    }

    public SeinCharacter Sein;

    public TextureAnimationWithTransitions HurtAnimation;

    public DamageBasedSoundProvider SeinDeathSound;

    public DamageBasedSoundProvider SeinHurtSound;

    public DamageBasedSoundProvider SeinBadlyHurtSound;

    public float BadlyHurtAmount = 4f;

    public bool IsImmortal;

    public float HurtDropPickupSpeed = 20f;

    private float m_invincibleTimeRemaining;

    private float m_invincibleToEnemiesTimeRemaining;

    private bool m_died;

    public GameObject GameOverScreen;

    public float HurtDuration = 0.25f;

    public Vector2 HurtSpeed = new Vector2(6f, 9f);

    public HorizontalPlatformMovementSettings.SpeedMultiplierSet MoveSpeed = new HorizontalPlatformMovementSettings.SpeedMultiplierSet {
        AccelerationMultiplier = 0f,
        DeceelerationMultiplier = 0f,
        MaxSpeedMultiplier = 1f
    };

    public float GravityMultiplier = 2f;

    public GameObject HurtEffect;

    public GameObject HurtDropPickup;

    private float m_hurtTimeRemaining;

    public GameObject KillFader;

    public float DeathDuration;

    public float OneLifeDeathDuration = 2f;

    public float SpawnDuration;

    public float DeathFadeInDuration = 0.05f;

    public float DeathFadeOutDuration = 1f;

    public DamageBasedPrefabProvider DeathEffectProvider;

    private int m_serializationFiller;
}
