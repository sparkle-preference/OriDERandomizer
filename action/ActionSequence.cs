using System;
using System.Collections.Generic;
using Game;

public class ActionSequence : PerformingAction, IPooled, ISuspendable {
    public bool IsRunning {
        get => isRunning;
        set => isRunning = value;
    }

    public int Index {
        get => index;
        set => index = value;
    }

    public void OnPoolSpawned() {
        Stop();
        isSuspended = false;
    }

    public override void Awake() {
        SuspensionManager.Register(this);
        Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
        Events.Scheduler.OnGameReset.Add(OnGameReset);
    }

    public override void OnDestroy() {
        SuspensionManager.Unregister(this);
        base.OnDestroy();
        Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
        Events.Scheduler.OnGameReset.Remove(OnGameReset);
    }

    private void OnGameReset() {
        if (isRunning) {
            Stop();
        }
    }

    public void OnRestoreCheckpoint() {
        var component = GetComponent<ActionSequenceSerializer>();
        if (component) {
            return;
        }

        Stop();
    }

    public void FindActions() {
        Actions.Clear();
        for (var i = 0; i < transform.childCount; i++) {
            var child = transform.GetChild(i);
            foreach (var item in child.GetComponents<ActionMethod>()) {
                Actions.Add(item);
            }
        }

        Actions.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
    }

    public override void Perform(IContext context) {
        Perform(context, false);
    }

    public override void PerformInstantly(IContext context) {
        Perform(context, true);
    }

    public void Perform(IContext context, bool instant) {
        if (!enabled) {
            return;
        }

        if (Actions == null) {
            FindActions();
        }

        if (Actions.Count == 0) {
            return;
        }

        isRunning = true;
        isInstant = instant;
        index = 0;
        this.context = context;
        RunAction(Actions[index]);
        UpdateActions();
    }

    public void RunAction(ActionMethod action) {
        if (action) {
            if (isInstant) {
                action.PerformInstantly(context);
            } else {
                action.Perform(context);
            }
        }
    }

    public void FixedUpdate() {
        if (isSuspended) {
            return;
        }

        UpdateActions();
    }

    public void UpdateActions() {
        if (!isRunning) {
            return;
        }

        var count = Actions.Count;
        while (index < count) {
            var actionMethod = Actions[index];
            if (actionMethod != null && actionMethod is WaitAction) {
                var waitAction = actionMethod as WaitAction;
                if (waitAction.IsPerforming) {
                    return;
                }
            }

            index++;
            if (index == count) {
                isRunning = false;
                return;
            }

            RunAction(Actions[index]);
        }
    }

    public static void Rename(List<ActionMethod> actions) {
        var num = 0;
        for (var i = 0; i < actions.Count; i++) {
            var actionMethod = actions[i];
            num++;
            var niceName = actionMethod.GetNiceName();
            actionMethod.name = FormatName(num, niceName);
        }
    }

    public static string FormatName(int number, string name) {
        return string.Format("{0:00}", number) + ". " + name;
    }

    public static string UnformatName(string name) {
        return name.Remove(0, 4);
    }

    public void RefreshNames() {
        FindActions();
        Rename(Actions);
    }

    public override string GetNiceName() {
        return gameObject.name;
    }

    public bool IsSuspended {
        get => isSuspended;
        set => isSuspended = value;
    }

    public override void Stop() {
        isRunning = false;
        isInstant = false;
        index = 0;
        context = null;
    }

    public override bool IsPerforming => isRunning;

    public override void Serialize(Archive ar) {
        var component = GetComponent<ActionSequenceSerializer>();
        if (component) {
            return;
        }

        if (ar.Reading) {
            Stop();
        }

        base.Serialize(ar);
    }

    private bool isRunning;

    private int index;

    private IContext context;

    private bool isSuspended;

    public List<ActionMethod> Actions = new List<ActionMethod>();

    private bool isInstant;
}
