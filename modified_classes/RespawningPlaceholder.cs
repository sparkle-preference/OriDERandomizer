using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

public abstract class RespawningPlaceholder : SaveSerialize, ISuspendable, IDynamicGraphic {
    protected void SetCurrentEntity(Entity entity) {
        this.m_currentEntity = entity;
    }

    // enemy rando: an entity a template built, taken over by the placeholder it stands for
    public void Adopt(Entity entity) {
        this.SetCurrentEntity(entity);
    }

    public virtual bool CheckForOverlap {
        get {
            return true;
        }
    }

    public bool EntityIsDead {
        get {
            return this.m_isKilled;
        }
    }

    public virtual bool NeedsToRespawn {
        get {
            return this.m_needsToRespawn;
        }
    }

    public bool IsOnScreen {
        get {
            return UI.Cameras.Current.IsOnScreenPadded(this.m_transform.position, 8f);
        }
    }

    public Entity CurrentEntity {
        get {
            return this.m_currentEntity;
        }
    }

    public void DestroyCurrentInstance() {
        if (this.m_currentEntity) {
            InstantiateUtility.Destroy(this.m_currentEntity.gameObject);
            this.m_currentEntity = null;
        }
    }

    public void Spawn() {
        if (RandomizerEnemies.Waiting(this)) {
            return;
        }
        this.DestroyCurrentInstance();
        this.m_isKilled = false;
        this.m_currentEntity = RandomizerEnemies.Instantiate(this) ?? this.Instantiate();
        RandomizerEnemies.VaryExp(this, this.m_currentEntity);
        GameObject gameObject = this.m_currentEntity.gameObject;
        if (gameObject.GetComponent<DestroyWhenOutsideActiveBoundaries>() == null) {
            gameObject.AddComponent<DestroyWhenOutsideActiveBoundaries>();
            Debug.LogError("The enemy type of " + gameObject.name + " didn't have the DestroyOnOutOfBoundary component on it!");
        }
        this.m_currentEntity.SetSceneRoot(this.m_sceneRootGUID);
        this.m_currentEntity.DamageReciever.OnDeathEvent.Add(new Action<Damage>(this.OnDeath));
        if (this.m_currentEntity && this.WasTimedRespawn) {
            gameObject.SendMessage("OnTimedRespawn", SendMessageOptions.DontRequireReceiver);
        }
        this.WasTimedRespawn = false;
    }

    public override void Awake() {
        SuspensionManager.Register(this);
        Events.Scheduler.OnSceneRootDisabled.Add(new Action<SceneRoot>(this.OnSceneRootDisabled));
        Events.Scheduler.OnGameSerializeLoad.Add(new Action(this.OnGameSerializeLoad));
        base.Awake();
        base.GetComponent<Renderer>().enabled = false;
        this.m_transform = base.transform;
        this.m_sceneRootGUID = SceneRoot.FindFromTransform(base.transform).MetaData.SceneMoonGuid;
        RandomizerEnemies.Offer(this);
    }

    public void OnEnable() {
        RespawningPlaceholder.All.Add(this);
        this.m_enableTime = Time.time;
        if (this.m_currentEntity) {
            this.m_currentEntity.ReclaimOwernship(this);
        }
    }

    public void OnDisable() {
        RespawningPlaceholder.All.Remove(this);
    }

    public override void OnDestroy() {
        SuspensionManager.Unregister(this);
        Events.Scheduler.OnSceneRootDisabled.Remove(new Action<SceneRoot>(this.OnSceneRootDisabled));
        Events.Scheduler.OnGameSerializeLoad.Remove(new Action(this.OnGameSerializeLoad));
        base.OnDestroy();
    }

    public void OnGameSerializeLoad() {
        this.DestroyCurrentInstance();
    }

    public void OnSceneRootDisabled(SceneRoot sceneRoot) {
        if (this.m_sceneRootGUID == sceneRoot.MetaData.SceneMoonGuid && this.m_currentEntity) {
            if (this.m_currentEntity.IsOnScreen()) {
                this.m_currentEntity.FreeOwnership(this);
                return;
            }
            this.DestroyCurrentInstance();
        }
    }

    public void OnDeath(Damage damage) {
        if (this.RespawnOnTimeout && this.RespawnTime != 0f) {
            this.m_respawnTime = GameController.Instance.GameTime + this.RespawnTime;
            this.m_needsToRespawn = true;
        }
        this.m_isKilled = true;
        this.OnCurrentInstanceDeath(damage);
        this.DestroyCurrentInstance();
    }

    public void FixedUpdate() {
        this.UpdateSpawnState();
    }

    public void UpdateSpawnState() {
        if (this.IsSuspended) {
            return;
        }
        if (this.RespawnOnTimeout && this.NeedsToRespawn && this.m_respawnTime < GameController.Instance.GameTime) {
            if (this.IsOnScreen && this.m_enableTime + 0.1f < Time.time) {
                if (this.RespawnOnScreen) {
                    float num = Vector3.Distance(Characters.Sein.PlatformBehaviour.PlatformMovement.Position, this.m_transform.position);
                    if (num > this.MinDistanceFromPlayer) {
                        this.PerformTimedRespawn();
                    }
                }
            } else {
                this.PerformRespawn();
            }
        }
        if (!this.m_isKilled && this.m_currentEntity == null && this.IsOnScreen) {
            this.Spawn();
        }
    }

    public void PerformRespawn() {
        this.m_needsToRespawn = false;
        this.m_isKilled = false;
    }

    public void PerformTimedRespawn() {
        this.WasTimedRespawn = true;
        this.PerformRespawn();
    }

    public abstract Entity Instantiate();

    public override void Serialize(Archive ar) {
        ar.Serialize(ref this.m_isKilled);
        ar.Serialize(ref this.m_needsToRespawn);
        ar.Serialize(ref this.m_respawnTime);
    }

    public bool IsSuspended { get; set; }

    public Vector3 Position {
        get {
            return base.transform.position;
        }
    }

    public static List<RespawningPlaceholder> All = new List<RespawningPlaceholder>();

    public bool RespawnOnTimeout = true;

    public bool RespawnOnScreen;

    public float RespawnTime = 3f;

    public float MinDistanceFromPlayer = 20f;

    private bool m_isKilled;

    private float m_respawnTime;

    private bool m_needsToRespawn;

    private Transform m_transform;

    private MoonGuid m_sceneRootGUID;

    public Action<Damage> OnCurrentInstanceDeath = delegate { };

    protected bool WasTimedRespawn;

    private float m_enableTime;

    private Entity m_currentEntity;
}
