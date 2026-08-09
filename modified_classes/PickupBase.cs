using System;
using Core;
using Game;
using UnityEngine;

public abstract class PickupBase : SaveSerialize, IFrustumOptimizable, IPooled, IDynamicGraphicHierarchy {
    public void OnValidate() {
        m_onKillRecievers = GetComponentsInChildren(typeof(IKillReciever));
        if (DestroyTarget == null) {
            DestroyTarget = gameObject;
        }

        m_transform = transform;
    }

    public void OnPoolSpawned() {
        OnCollectedEvent = delegate { };
        IsCollected = false;
        m_currentTime = 0f;
    }

    public override void Awake() {
        base.Awake();
        m_bounds = new Bounds(transform.position, Vector3.one * 4f);
    }

    public void FixedUpdate() {
        if (FrustrumOptimized && !m_insideFrustum) {
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

        m_currentTime += Time.deltaTime;
        if (m_currentTime < DelayBeforeCollectable) {
            return;
        }

        if (!IsCollected && Characters.Sein && Vector3.Distance(m_transform.position, Characters.Sein.Position) < Radius) {
            OnCollectorCandidateTouch(Characters.Sein.gameObject);
        }
    }

    public abstract void OnCollectorCandidateTouch(GameObject collector);

    public void SpawnCollectedEffect() {
        if (CollectedEffect) {
            InstantiateUtility.Instantiate(CollectedEffect, m_transform.position, Quaternion.identity);
        }
    }

    public virtual void Collected() {
        IsCollected = true;
        SpawnCollectedEffect();
        if (CollectedSoundProvider != null) {
            Sound.Play(CollectedSoundProvider.GetSound(null), m_transform.position, null);
        }

        for (int i = 0; i < m_onKillRecievers.Length; i++) {
            if (m_onKillRecievers[i]) {
                ((IKillReciever)m_onKillRecievers[i]).OnKill();
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
        ar.Serialize(ref m_currentTime);
        ar.Serialize(ref IsCollected);
        if (ar.Reading) {
            gameObject.SetActive(!IsCollected);
        }
    }

    public Bounds Bounds {
        get {
            m_bounds.center = m_transform.position;
            return m_bounds;
        }
    }

    public void OnFrustumEnter() {
        m_insideFrustum = true;
        if (!IsCollected) {
            gameObject.SetActive(true);
        }
    }

    public void OnFrustumExit() {
        m_insideFrustum = false;
    }

    public bool InsideFrustum {
        get { return m_insideFrustum; }
    }

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

    [HideInInspector] [SerializeField] private Component[] m_onKillRecievers;

    [HideInInspector] [SerializeField] private Transform m_transform;

    private float m_currentTime;

    private Bounds m_bounds;

    private bool m_insideFrustum = true;
}
