using UnityEngine;

public class WormMortarShootingState : WormState {
    public WormMortarShootingState(WormEnemy worm, MortarWormDirectionalAnimations shoot, PrefabSpawner shootEffect, SoundSource shootSound, ProjectileSpawner projectileSpawner, float shootDelay, float projectileDamage) : base(worm) {
        this.shoot = shoot;
        this.shootEffect = shootEffect;
        this.shootSound = shootSound;
        this.projectileSpawner = projectileSpawner;
        this.shootDelay = shootDelay;
        this.projectileDamage = projectileDamage;
    }

    public override void OnEnter() {
        var mortarWormEnemy = (MortarWormEnemy)Worm;
        var direction = (projectileSpawner.Speed * projectileSpawner.Direction + 0.5f * projectileSpawner.Gravity * shootDelay * shootDelay * Vector3.down).normalized;
        direction = mortarWormEnemy.transform.InverseTransformDirection(direction);
        if (mortarWormEnemy.FaceLeft) {
            direction.x *= -1f;
        }

        Worm.Animation.Play(shoot.PickWithDirection(direction));
        projectileAnimationPosition = mortarWormEnemy.Spawn.FindPosition(direction);
    }

    public override void OnExit() {
        hasShot = false;
        base.OnExit();
    }

    public override void UpdateState() {
        if (CurrentStateTime >= shootDelay && !hasShot) {
            hasShot = true;
            if (shootEffect) {
                shootEffect.Spawn(null);
            }

            if (shootSound) {
                shootSound.Play();
            }

            var projectile = projectileSpawner.SpawnProjectile();
            var b = RandomizerBonusSkill.TimeScale(projectile.Direction * projectile.Speed * shootDelay + Vector3.down * projectile.Gravity * shootDelay * shootDelay * 0.5f);
            projectile.Position += b;
            projectile.SpeedVector += Vector3.down * projectile.Gravity * shootDelay;
            projectile.GetComponent<DamageDealer>().Damage = projectileDamage;
            var vector = projectileAnimationPosition - projectile.Position;
            vector.z = 0f;
            projectile.Position += vector;
            projectile.Displacement = vector;
        }

        base.UpdateState();
    }

    private readonly MortarWormDirectionalAnimations shoot;

    private readonly PrefabSpawner shootEffect;

    private readonly SoundSource shootSound;

    private readonly ProjectileSpawner projectileSpawner;

    private readonly float shootDelay;

    private readonly float projectileDamage;

    private Vector3 projectileAnimationPosition;

    private bool hasShot;
}
