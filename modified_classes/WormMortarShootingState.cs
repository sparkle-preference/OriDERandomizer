using UnityEngine;

public class WormMortarShootingState : WormState {
    public WormMortarShootingState(WormEnemy worm, MortarWormDirectionalAnimations shoot, PrefabSpawner shootEffect, SoundSource shootSound, ProjectileSpawner projectileSpawner, float shootDelay, float projectileDamage) : base(worm) {
        m_shoot = shoot;
        m_shootEffect = shootEffect;
        m_shootSound = shootSound;
        m_projectileSpawner = projectileSpawner;
        m_shootDelay = shootDelay;
        m_projectileDamage = projectileDamage;
    }

    public override void OnEnter() {
        var mortarWormEnemy = (MortarWormEnemy)Worm;
        var direction = (m_projectileSpawner.Speed * m_projectileSpawner.Direction + 0.5f * m_projectileSpawner.Gravity * m_shootDelay * m_shootDelay * Vector3.down).normalized;
        direction = mortarWormEnemy.transform.InverseTransformDirection(direction);
        if (mortarWormEnemy.FaceLeft) {
            direction.x *= -1f;
        }

        Worm.Animation.Play(m_shoot.PickWithDirection(direction));
        m_projectileAnimationPosition = mortarWormEnemy.Spawn.FindPosition(direction);
    }

    public override void OnExit() {
        m_hasShot = false;
        base.OnExit();
    }

    public override void UpdateState() {
        if (CurrentStateTime >= m_shootDelay && !m_hasShot) {
            m_hasShot = true;
            if (m_shootEffect) {
                m_shootEffect.Spawn(null);
            }

            if (m_shootSound) {
                m_shootSound.Play();
            }

            var projectile = m_projectileSpawner.SpawnProjectile();
            var b = RandomizerBonusSkill.TimeScale(projectile.Direction * projectile.Speed * m_shootDelay + Vector3.down * projectile.Gravity * m_shootDelay * m_shootDelay * 0.5f);
            projectile.Position += b;
            projectile.SpeedVector += Vector3.down * projectile.Gravity * m_shootDelay;
            projectile.GetComponent<DamageDealer>().Damage = m_projectileDamage;
            var vector = m_projectileAnimationPosition - projectile.Position;
            vector.z = 0f;
            projectile.Position += vector;
            projectile.Displacement = vector;
        }

        base.UpdateState();
    }

    private readonly MortarWormDirectionalAnimations m_shoot;

    private readonly PrefabSpawner m_shootEffect;

    private readonly SoundSource m_shootSound;

    private readonly ProjectileSpawner m_projectileSpawner;

    private readonly float m_shootDelay;

    private readonly float m_projectileDamage;

    private Vector3 m_projectileAnimationPosition;

    private bool m_hasShot;
}
