using UnityEngine;

public class SpitterEnemyShootingState : SpitterEnemyState {
    public Vector3 ProjectileSpawnerPositionToPlayerPosition {
        get {
            return SpitterEnemy.PlayerPosition - SpitterEnemy.ProjectileSpawner.transform.position;
        }
    }

    public SpitterEnemyShootingState(SpitterEnemy enemy) : base(enemy) {
    }

    public override void UpdateState() {
    }

    public override void OnEnter() {
        ShootProjectile();
    }

    public void ShootProjectile() {
        GroundEnemy.PlayAnimationOnce(SpitterEnemy.Animations.Shooting);
        GroundEnemy.PlatformMovement.LocalSpeedX = 0f;
        var projectileSpeed = SpitterEnemy.Settings.ProjectileSpeed;
        var projectileGravity = SpitterEnemy.Settings.ProjectileGravity;
        var projectileSpawnerPositionToPlayerPosition = ProjectileSpawnerPositionToPlayerPosition;
        // facing is local, so a frog with turned gravity clamps its aim in its own frame
        var turned = GroundEnemy.PlatformMovement.GravityAngle != 0f;
        if (turned) {
            projectileSpawnerPositionToPlayerPosition = GroundEnemy.transform.InverseTransformDirection(projectileSpawnerPositionToPlayerPosition);
        }

        projectileSpawnerPositionToPlayerPosition.x = !GroundEnemy.FaceLeft ? Mathf.Max(1f, projectileSpawnerPositionToPlayerPosition.x) : Mathf.Min(-1f, projectileSpawnerPositionToPlayerPosition.x);
        if (turned) {
            projectileSpawnerPositionToPlayerPosition = GroundEnemy.transform.TransformDirection(projectileSpawnerPositionToPlayerPosition);
        }

        var num = projectileSpawnerPositionToPlayerPosition.magnitude / projectileSpeed;
        var vector = projectileSpawnerPositionToPlayerPosition.normalized * projectileSpeed;
        vector.y += projectileGravity * num * 0.5f;
        SpawnProjectile(vector);
        if (SpitterEnemy.Settings.SpreadShot) {
            var right = turned ? GroundEnemy.transform.right : Vector3.right;
            var down = turned ? -GroundEnemy.transform.up : Vector3.down;
            SpawnProjectile(vector + right * 4f + down * 2f);
            SpawnProjectile(vector - right * 4f + down * 2f);
        }
    }

    public void SpawnProjectile(Vector3 speed) {
        var projectileGravity = SpitterEnemy.Settings.ProjectileGravity;
        var projectileDamage = SpitterEnemy.Settings.ProjectileDamage;
        SpitterEnemy.SpawnPrefab(SpitterEnemy.SpitEffect);
        var gameObject = SpitterEnemy.ProjectileSpawner.Spawn(null);
        if (gameObject != null) {
            var positionToPlayerPosition = GroundEnemy.PositionToPlayerPosition;
            positionToPlayerPosition.x = !GroundEnemy.FaceLeft ? Mathf.Max(1.5f, positionToPlayerPosition.x) : Mathf.Min(-1.5f, positionToPlayerPosition.x);
            var component = gameObject.GetComponent<Projectile>();
            component.Gravity = projectileGravity;
            component.Speed = speed.magnitude;
            component.Direction = speed.normalized;
            component.GetComponentInChildren<DamageDealer>().Damage = projectileDamage;
            component.Owner = GroundEnemy.DamageReciever.gameObject;
        }
    }

    public override void OnExit() {
    }
}
