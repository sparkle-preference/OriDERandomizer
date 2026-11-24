using System.Collections.Generic;
using UnityEngine;

public class WorldEventsManager {
    public void OnGameReset() {
        m_worldEvents.Clear();
        All.Clear();
    }

    public void Serialize(Archive ar) {
        var num = ar.Serialize(m_worldEvents.Count);
        if (ar.Writing) {
            foreach (var keyValuePair in m_worldEvents) {
                new MoonGuid(keyValuePair.Key).Serialize(ar);
                keyValuePair.Value.Serialize(ar);
            }
        } else {
            m_worldEvents.Clear();
            for (var i = 0; i < num; i++) {
                var moonGuid = new MoonGuid(MoonGuid.Empty);
                moonGuid.Serialize(ar);
                var worldEventsRuntime = new WorldEventsRuntime(0);
                worldEventsRuntime.Serialize(ar);
                m_worldEvents[moonGuid] = worldEventsRuntime;
            }
        }
    }

    public WorldEventsRuntime Find(WorldEvents worldEvents) {
        WorldEventsRuntime worldEventsRuntime;
        if (!m_worldEvents.TryGetValue(worldEvents.GetGuid(), out worldEventsRuntime)) {
            var defaultValue = worldEvents.DefaultValue;
            worldEventsRuntime = new WorldEventsRuntime(defaultValue);
            m_worldEvents.Add(worldEvents.GetGuid(), worldEventsRuntime);
            if (worldEvents != null && Application.isPlaying && DebugMenuB.Instance) {
                DebugMenuB.Instance.AddWorldEvent(worldEvents);
            }
        }

        return worldEventsRuntime;
    }

    public static WorldEventsManager Instance = new();

    private readonly Dictionary<MoonGuid, WorldEventsRuntime> m_worldEvents = new();

    public readonly List<WorldEventsRuntime> All = new();
}
