using System;
using Game;
using UnityEngine;

public class CollectablePlaceholder : SaveSerialize, ISuspendable, IDynamicGraphic {
    public override void Awake() {
        All.Add(this);
        if (Prefab == null) {
            InstantiateUtility.Destroy(gameObject);
            return;
        }

        base.Awake();
        GetComponent<Renderer>().enabled = false;
        SuspensionManager.Register(this);
    }

    public override void OnDestroy() {
        SuspensionManager.Unregister(this);
        base.OnDestroy();
        All.Remove(this);
    }

    public void Spawn() {
        if (!InstantiateUtility.IsDestroyed(m_instance)) {
            InstantiateUtility.Destroy(m_instance);
            m_instance = null;
        }

        Instantiate();
    }

    public void OnCollect() {
        m_collected = true;
        m_remainingRespawnTime = RespawnTime;
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (!m_collected && RandomizerLocationManager.IsPickupCollected(MoonGuid)) {
            // only do anything if the pickup isn't spawned; if it's spawned, PickupBase will mark itself collected
            if (m_instance == null) {
                OnCollect();
            }
        }

        if (m_remainingRespawnTime > 0f) {
            m_remainingRespawnTime -= Time.deltaTime;
            m_collected = false;
        }

        if (m_instance == null && !m_collected && UI.Cameras.Current.IsOnScreenPadded(transform.position, 5f)) {
            Instantiate();
        }
    }

    public void Instantiate() {
        m_instance = InstantiateUtility.Instantiate(Prefab, transform.position, transform.rotation) as GameObject;
        UberPoolManager.Instance.AddOnDestroyed(m_instance, delegate { m_instance = null; });

        var pickupBase = m_instance.GetComponentInChildren<PickupBase>();
        pickupBase.MoonGuid = MoonGuid;
        pickupBase.OnCollectedEvent = (Action)Delegate.Combine(pickupBase.OnCollectedEvent, new Action(OnCollect));

        if (m_instance.GetComponent<DestroyOnRestoreCheckpoint>() == null) {
            m_instance.AddComponent<DestroyOnRestoreCheckpoint>();
        }

        if (GetComponent<VisibleOnWorldMap>() && m_instance.GetComponent<VisibleOnWorldMap>()) {
            m_instance.GetComponent<VisibleOnWorldMap>().MoonGuid = GetComponent<VisibleOnWorldMap>().MoonGuid;
        }

        m_instance.transform.parent = transform.parent;
        m_instance.name = Prefab.name;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_collected);
        ar.Serialize(ref m_remainingRespawnTime);
    }

    public bool Collected => m_collected;

    public bool IsSuspended { get; set; }

    public float RespawnTime;

    public GameObject Prefab;

    public static AllContainer<CollectablePlaceholder> All = new AllContainer<CollectablePlaceholder>();

    public bool UseDebug;

    private float m_remainingRespawnTime;

    private GameObject m_instance;

    private bool m_collected;
}
