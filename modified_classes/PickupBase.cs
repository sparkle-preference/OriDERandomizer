using System;
using Core;
using Game;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class PickupBase : SaveSerialize, IFrustumOptimizable, IPooled, IDynamicGraphicHierarchy {
    public void OnValidate() {
        OnKillReceivers = GetComponentsInChildren(typeof(IKillReciever));
        if (DestroyTarget == null) {
            DestroyTarget = gameObject;
        }

        Transform = transform;
    }

    public void OnPoolSpawned() {
        OnCollectedEvent = delegate { };
        IsCollected = false;
        currentTime = 0f;
    }

    public override void Awake() {
        base.Awake();
        bounds = new Bounds(transform.position, Vector3.one * 4f);
    }

    public void FixedUpdate() {
        if (FrustrumOptimized && !insideFrustum) {
            gameObject.SetActive(false);
            return;
        }

        if (!IsCollected && RandomizerLocationManager.IsPickupCollected(MoonGuid)) {
            IsCollected = true;

            if (OnCollectedAction != null) {
                OnCollectedAction.PerformInstantly(null);
            }

            OnCollectedEvent();

            if (DestroyOnCollect) {
                InstantiateUtility.Destroy(DestroyTarget);
            } else {
                gameObject.SetActive(false);
            }
        }

        currentTime += Time.deltaTime;
        if (currentTime < DelayBeforeCollectable) {
            return;
        }

        if (!IsCollected && Characters.Sein && Vector3.Distance(Transform.position, Characters.Sein.Position) < Radius) {
            OnCollectorCandidateTouch(Characters.Sein.gameObject);
        }
    }

    public abstract void OnCollectorCandidateTouch(GameObject collector);

    public void SpawnCollectedEffect() {
        if (CollectedEffect) {
            InstantiateUtility.Instantiate(CollectedEffect, Transform.position, Quaternion.identity);
        }
    }

    public virtual void Collected() {
        IsCollected = true;
        SpawnCollectedEffect();
        if (CollectedSoundProvider != null) {
            Sound.Play(CollectedSoundProvider.GetSound(null), Transform.position, null);
        }

        for (var i = 0; i < OnKillReceivers.Length; i++) {
            if (OnKillReceivers[i]) {
                ((IKillReciever)OnKillReceivers[i]).OnKill();
            }
        }

        if (OnCollectedAction != null) {
            OnCollectedAction.Perform(null);
        }

        OnCollectedEvent();
        if (DestroyOnCollect) {
            InstantiateUtility.Destroy(DestroyTarget);
        } else {
            gameObject.SetActive(false);
        }
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref currentTime);
        ar.Serialize(ref IsCollected);
        if (ar.Reading) {
            gameObject.SetActive(!IsCollected);
        }
    }

    public Bounds Bounds {
        get {
            bounds.center = Transform.position;
            return bounds;
        }
    }

    public void OnFrustumEnter() {
        insideFrustum = true;
        if (!IsCollected) {
            gameObject.SetActive(true);
        }
    }

    public void OnFrustumExit() {
        insideFrustum = false;
    }

    public bool InsideFrustum => insideFrustum;

    public bool IsCollected;

    public SoundProvider CollectedSoundProvider;

    public Action OnCollectedEvent = delegate { };

    public ActionMethod OnCollectedAction;

    public float DelayBeforeCollectable;

    public bool DestroyOnCollect;

    public GameObject DestroyTarget;

    public GameObject CollectedEffect;

    public float Radius = 2f;

    public bool FrustrumOptimized;

    [FormerlySerializedAs("m_onKillRecievers")] [HideInInspector] [SerializeField] private Component[] OnKillReceivers;

    [FormerlySerializedAs("m_transform")] [HideInInspector] [SerializeField] private Transform Transform;

    private float currentTime;

    private Bounds bounds;

    private bool insideFrustum = true;
}
