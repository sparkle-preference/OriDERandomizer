using System;
using System.Collections.Generic;
using Game;

public class AllEnemiesKilledTrigger : Trigger {
    public override void Serialize(Archive ar) {
        ar.Serialize(ref m_counter);
        base.Serialize(ar);
        if (ActionOnAwakeTrigger && m_counter >= TriggerOnCounter) {
            ActionOnAwakeTrigger.Perform(null);
        }
    }

    public void Increment() {
        m_counter++;
        if (m_counter == TriggerOnCounter) {
            BingoController.OnPurpleDoor(MoonGuid);
            DoTrigger();
        }
    }

    public new void Awake() {
        base.Awake();
        RegisterEvent();
    }

    public new void OnDestroy() {
        base.OnDestroy();
        DeregisterEvent();
    }

    public void Init() {
        RespawningPlaceholders.Clear();
        for (var i = 0; i < GetComponentsInChildren<RespawningPlaceholder>().Length; i++) {
            var item = GetComponentsInChildren<RespawningPlaceholder>()[i];
            RespawningPlaceholders.Add(item);
        }

        Entities.Clear();
        for (var j = 0; j < GetComponentsInChildren<Entity>().Length; j++) {
            var item2 = GetComponentsInChildren<Entity>()[j];
            Entities.Add(item2);
        }

        TriggerOnCounter = RespawningPlaceholders.Count + Entities.Count;
    }

    private void RegisterEvent() {
        Action<Damage> action = EntityKilled;
        for (var i = 0; i < RespawningPlaceholders.Count; i++) {
            var respawningPlaceholder = RespawningPlaceholders[i];
            respawningPlaceholder.OnCurrentInstanceDeath = (Action<Damage>)Delegate.Combine(respawningPlaceholder.OnCurrentInstanceDeath, action);
        }

        for (var j = 0; j < Entities.Count; j++) {
            Entities[j].DamageReciever.OnDeathEvent.Add(action);
        }
    }

    private void DeregisterEvent() {
        Action<Damage> action = EntityKilled;
        for (var i = 0; i < RespawningPlaceholders.Count; i++) {
            var respawningPlaceholder = RespawningPlaceholders[i];
            respawningPlaceholder.OnCurrentInstanceDeath = (Action<Damage>)Delegate.Remove(respawningPlaceholder.OnCurrentInstanceDeath, action);
        }

        for (var j = 0; j < Entities.Count; j++) {
            Entities[j].DamageReciever.OnDeathEvent.Remove(action);
        }
    }

    private void EntityKilled(Damage damage) {
        EnemyKilled();
    }

    private void EnemyKilled() {
        if (Active) {
            Increment();
            if (m_lastMessageBox) {
                m_lastMessageBox.HideMessageScreen();
            }

            if (ShowMessages) {
                var num = TriggerOnCounter - m_counter - 1;
                if (num >= Messages.Count) {
                    num = Messages.Count - 1;
                }

                if (num > 0) {
                    m_lastMessageBox = UI.Hints.Show(Messages[num], HintLayer.Gameplay, 1f);
                }
            }
        }
    }

    public List<RespawningPlaceholder> RespawningPlaceholders = new List<RespawningPlaceholder>();

    public List<Entity> Entities = new List<Entity>();

    public List<MessageProvider> Messages = new List<MessageProvider>();

    public bool ShowMessages = true;

    public int TriggerOnCounter;

    private int m_counter;

    private MessageBox m_lastMessageBox;

    public ActionMethod ActionOnAwakeTrigger;
}
