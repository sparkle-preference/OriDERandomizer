using System;
using Core;
using Game;
using UnityEngine;

public class Entity : SaveSerialize, IRespawnReciever, IFrustumOptimizable, ISuspendable {
    public Entity() {
        IsSuspended = false;
    }

    public void OnSceneUnloaded(SceneRoot sceneRoot) {
        if (!Scenes.Manager.IsInsideActiveSceneBoundary(transform.position)) {
            InstantiateUtility.Destroy(gameObject);
        }
    }

    public void ReclaimOwernship(RespawningPlaceholder placeholder) {
        transform.parent = placeholder.transform.parent;
        Events.Scheduler.OnSceneRootDisabled.Remove(OnSceneUnloaded);
        m_registeredToSceneRootDisabled = false;
    }

    public void FreeOwnership(RespawningPlaceholder placeholder) {
        transform.parent = null;
        Events.Scheduler.OnSceneRootDisabled.Add(OnSceneUnloaded);
        m_registeredToSceneRootDisabled = true;
    }

    public virtual bool CanBeOptimized() {
        return true;
    }

    public bool IsInWater => WaterZone.PositionInWater(Position);

    public void Drown() {
        Damage damage = new Damage(1000f, Vector3.zero, Position, DamageType.Water, gameObject);
        DamageReciever.OnRecieveDamage(damage);
    }

    public bool IsOnScreen() {
        return UI.Cameras.Current == null || UI.Cameras.Current.IsOnScreen(transform.position);
    }

    public override void Awake() {
        SuspensionManager.Register(this);
        if (FrustrumOptimized) {
            CameraFrustumOptimizer.Register(this);
        }

        SceneRoot sceneRoot = SceneRoot.FindFromTransform(transform);
        if (sceneRoot != null) {
            SceneRootGUID = sceneRoot.MetaData.SceneMoonGuid;
        }

        base.Awake();
    }

    public void SetSceneRoot(MoonGuid sceneRoot) {
        SceneRootGUID = sceneRoot;
    }

    public override void OnDestroy() {
        SuspensionManager.Unregister(this);
        if (FrustrumOptimized) {
            CameraFrustumOptimizer.Unregister(this);
        }

        if (m_registeredToSceneRootDisabled) {
            Events.Scheduler.OnSceneRootDisabled.Remove(OnSceneUnloaded);
        }

        base.OnDestroy();
    }

    public override void Serialize(Archive ar) {
        Position = ar.Serialize(Position);
        Rotation = ar.Serialize(Rotation);
    }

    public void Start() {
        StartPosition = transform.position;
    }

    public void FixedUpdate() {
        if (this is Enemy)
            (this as Enemy).Animation.Animator.TextureAnimator.SpeedMultiplier = RandomizerBonusSkill.TimeScale(1f);
        if (FrustrumOptimized && !m_insideFrustum && CanBeOptimized()) {
            gameObject.SetActive(false);
        }
    }

    public bool PlayerIsToLeft => PositionToPlayerPosition.x < 0f;

    public Vector3 PlayerPosition => Characters.Sein.PlatformBehaviour.PlatformMovement.Position;

    public Vector3 Position {
        get => transform.position;
        set => transform.position = value;
    }

    public Quaternion Rotation {
        get => transform.rotation;
        set => transform.rotation = value;
    }

    public Vector3 PositionToPlayerPosition => transform.InverseTransformDirection(PlayerPosition - Position);

    public Vector3 StartPositionToPlayerPosition => PlayerPosition - StartPosition;

    public bool LeftOfStartPosition => StartPositionToPlayerPosition.x < 0f;

    public Vector3 PositionToStartPosition => StartPosition - Position;

    public Vector3 StartPosition { get; set; }

    public bool AfterTime(float duration) {
        return Controller.StateMachine.CurrentStateTime > duration;
    }

    public bool IsSuspended { get; set; }

    public void OnTimedRespawn() {
    }

    public void RegisterRespawnDelegate(Action onRespawn) {
        DamageReciever.OnDeathEvent.Add(delegate(Damage a) { onRespawn(); });
    }

    public void PlaySound(SoundSource sound) {
        if (sound != null) {
            sound.Play();
        }
    }

    public void StopSound(SoundSource sound) {
        if (sound != null) {
            sound.Stop();
        }
    }

    public void PlaySound(SoundProvider sound) {
        if (sound != null) {
            Sound.Play(sound.GetSound(null), Position, null);
        }
    }

    public void SpawnPrefab(PrefabSpawner prefabSpawner) {
        if (prefabSpawner != null) {
            prefabSpawner.Spawn(null);
        }
    }

    public void SpawnPrefab(GameObject prefab) {
        if (prefab != null) {
            InstantiateUtility.Instantiate(prefab, Position, transform.rotation);
        }
    }

    public void DestroyPrefab(PrefabSpawner prefabSpawner) {
        if (prefabSpawner != null) {
            prefabSpawner.DestroyInstance();
        }
    }

    public void ActivateDamageDealer() {
        DamageDealer.Activated = true;
    }

    public void DeactivateDamageDealer() {
        DamageDealer.Activated = false;
    }

    public void ActivateTargetting() {
        Targetting.Activated = true;
    }

    public void DeactivateTargetting() {
        Targetting.Activated = false;
    }

    public void OnFrustumEnter() {
        m_insideFrustum = true;
        if (!DamageReciever || !DamageReciever.NoHealthLeft) {
            gameObject.SetActive(true);
        }
    }

    public void OnFrustumExit() {
        m_insideFrustum = false;
    }

    public bool InsideFrustum => m_insideFrustum;

    public Bounds Bounds {
        get {
            Vector3 size = new Vector3(BoundingBox.width, BoundingBox.height, 0f);
            Vector3 vector = transform.position;
            vector += new Vector3(BoundingBox.center.x, BoundingBox.center.y, 0f);
            return new Bounds(vector, size);
        }
    }

    public bool PlayerInsideSameScene() {
        RuntimeSceneMetaData currentScene = Scenes.Manager.CurrentScene;
        return currentScene != null && currentScene.SceneMoonGuid == SceneRootGUID;
    }

    public EntityController Controller;

    public EntityDamageReciever DamageReciever;

    public EntityDamageDealer DamageDealer;

    public EntityTargetting Targetting;

    protected MoonGuid SceneRootGUID;

    public Rect BoundingBox = new Rect {
        width = 4f,
        height = 4f,
        center = Vector2.zero
    };

    public bool FrustrumOptimized;

    private bool m_registeredToSceneRootDisabled;

    private bool m_insideFrustum = true;
}
