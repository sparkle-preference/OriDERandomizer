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
        if (!InstantiateUtility.IsDestroyed(instance)) {
            InstantiateUtility.Destroy(instance);
            instance = null;
        }

        Instantiate();
    }

    public void OnCollect() {
        collected = true;
        remainingRespawnTime = RespawnTime;
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        if (!collected && RandomizerLocationManager.IsPickupCollected(MoonGuid))
            // only do anything if the pickup isn't spawned; if it's spawned, PickupBase will mark itself collected
        {
            if (instance == null) {
                OnCollect();
            }
        }

        if (remainingRespawnTime > 0f) {
            remainingRespawnTime -= Time.deltaTime;
            collected = false;
        }

        if (instance == null && !collected && UI.Cameras.Current.IsOnScreenPadded(transform.position, 5f)) {
            Instantiate();
        }
    }

    public void Instantiate() {
        instance = InstantiateUtility.Instantiate(Prefab, transform.position, transform.rotation) as GameObject;
        UberPoolManager.Instance.AddOnDestroyed(instance, delegate { instance = null; });

        var pickupBase = instance.GetComponentInChildren<PickupBase>();
        pickupBase.MoonGuid = MoonGuid;
        pickupBase.OnCollectedEvent = (Action)Delegate.Combine(pickupBase.OnCollectedEvent, new Action(OnCollect));

        if (instance.GetComponent<DestroyOnRestoreCheckpoint>() == null) {
            instance.AddComponent<DestroyOnRestoreCheckpoint>();
        }

        if (GetComponent<VisibleOnWorldMap>() && instance.GetComponent<VisibleOnWorldMap>()) {
            instance.GetComponent<VisibleOnWorldMap>().MoonGuid = GetComponent<VisibleOnWorldMap>().MoonGuid;
        }

        instance.transform.parent = transform.parent;
        instance.name = Prefab.name;
    }

    public override void Serialize(Archive ar) {
        ar.Serialize(ref collected);
        ar.Serialize(ref remainingRespawnTime);
    }

    public bool Collected => collected;

    public bool IsSuspended { get; set; }

    public float RespawnTime;

    public GameObject Prefab;

    public static AllContainer<CollectablePlaceholder> All = new AllContainer<CollectablePlaceholder>();

    public bool UseDebug;

    private float remainingRespawnTime;

    private GameObject instance;

    private bool collected;
}
