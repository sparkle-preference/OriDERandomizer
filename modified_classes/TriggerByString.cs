using System;
using System.Collections.Generic;
using Game;

public class TriggerByString : Trigger {
    public static void Register(string s) {
        TriggerByString.m_stringTriggersList.Add(s);
    }

    public static void Deregister(string s) {
        TriggerByString.m_stringTriggersList.Remove(s);
    }

    public new void Awake() {
        base.Awake();
        Events.Scheduler.OnSceneRootEnabledAfterSerialize.Add(new Action<SceneRoot>(this.OnSceneRootEnabledAfterSerialize));
        if (this.Data.TriggerEvent == TriggerByString.TriggerEvent.Awake && TriggerByString.m_stringTriggersList.Contains(this.Data.String)) {
            this.DoTrigger();
        }
    }

    public void Start() {
        if (this.Data.TriggerEvent == TriggerByString.TriggerEvent.Start && TriggerByString.m_stringTriggersList.Contains(this.Data.String)) {
            this.DoTrigger();
        }
    }

    public static void OnGameReset() {
        TriggerByString.m_stringTriggersList.Clear();
    }

    public void FixedUpdate() {
        if (this.Data.TriggerEvent == TriggerByString.TriggerEvent.Always && TriggerByString.m_stringTriggersList.Contains(this.Data.String)) {
            this.DoTrigger();
        }
    }

    public void DoTrigger() {
        if (SceneFPSTest.IsRunning()) {
            return;
        }
        TriggerByString.Deregister(this.Data.String);
        base.DoTrigger(true);
    }

    public static void SerializeStringTriggers(Archive ar) {
        if (ar.Writing) {
            ar.Serialize(TriggerByString.m_stringTriggersList.Count);
            foreach (string text in TriggerByString.m_stringTriggersList) {
                ar.Serialize(text);
            }
        } else if (ar.Reading) {
            int num = ar.Serialize(TriggerByString.m_stringTriggersList.Count);
            TriggerByString.m_stringTriggersList = new List<string>();
            for (int i = 0; i < num; i++) {
                TriggerByString.m_stringTriggersList.Add(ar.Serialize(string.Empty));
            }
        }
    }

    public new void OnDestroy() {
        base.OnDestroy();
        Events.Scheduler.OnSceneRootEnabledAfterSerialize.Remove(new Action<SceneRoot>(this.OnSceneRootEnabledAfterSerialize));
        base.OnDestroy();
    }

    public void OnSceneRootEnabledAfterSerialize(SceneRoot sceneRoot) {
        SceneRoot sceneRoot2 = SceneRoot.FindFromTransform(base.transform);
        if (sceneRoot2 == null) {
            throw new Exception("scene root is null, you can't use this action on objects instantiated outside level root");
        }
        if (sceneRoot == sceneRoot2 && this.Data.TriggerEvent == TriggerByString.TriggerEvent.SceneEnabledAfterSerialize && TriggerByString.m_stringTriggersList.Contains(this.Data.String)) {
            this.DoTrigger();
        }
    }

    public TriggerByString.StringTriggerData Data;

    private static List<string> m_stringTriggersList = new List<string>();

    [Serializable]
    public class StringTriggerData {
        public string String = string.Empty;

        public TriggerByString.TriggerEvent TriggerEvent;
    }

    public enum TriggerEvent {
        Awake,
        Start,
        SceneEnabledAfterSerialize,
        Always
    }
}
