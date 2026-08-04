using UnityEngine;

public class BashAttackCritical : Suspendable, IPooled {
    public void OnPoolSpawned() {
        CurrentState = State.Charging;
        stateCurrentTime = 0f;
        suspended = false;
    }

    public void ChangeState(State state) {
        CurrentState = state;
        stateCurrentTime = 0f;
    }

    public void UpdateState() {
        switch (CurrentState) {
            case State.Charging:
                UpdateChargingState();
                break;
            case State.Critical:
                UpdateCriticalState();
                break;
            case State.Failed:
                UpdateFailedState();
                break;
        }

        stateCurrentTime += Time.deltaTime;
    }

    private void UpdateFailedState() {
        transform.localScale = localScale;
        GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_MaskTexture", new Vector2(0.5f, 0f));
        if (stateCurrentTime > FailedDuration) {
            ChangeState(State.Finished);
        }
    }

    private void UpdateCriticalState() {
        transform.localScale = localScale + Vector3.one * Mathf.Sin(stateCurrentTime * 6.2831855f / ShakePeriod) * ShakeAmount;
        GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_MaskTexture", new Vector2(0.5f * (Mathf.RoundToInt(stateCurrentTime * 15f) % 2), 0f));
        var criticalDuration = CriticalDuration;
        if (RandomizerSettings.Controls.LongerBashAimTime) {
            criticalDuration += 3.3f;
        }

        if (stateCurrentTime > criticalDuration) {
            ChangeState(State.Failed);
        }
    }

    private void UpdateChargingState() {
        transform.localScale = localScale;
        var num = stateCurrentTime / ChargingDuration;
        GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_MaskTexture", new Vector2(0.5f - num * 0.5f, 0f));
        if (stateCurrentTime > ChargingDuration) {
            ChangeState(State.Critical);
        }
    }

    public new void Awake() {
        base.Awake();
        localScale = transform.localScale;
    }

    public override bool IsSuspended {
        get => suspended;
        set => suspended = value;
    }

    public void FixedUpdate() {
        if (IsSuspended) {
            return;
        }

        UpdateState();
    }

    public float ChargingDuration;

    public float CriticalDuration;

    public float FailedDuration;

    public float ShakePeriod = 0.2f;

    public float ShakeAmount = 0.5f;

    private Vector3 localScale;

    public State CurrentState;

    private bool suspended;

    private float stateCurrentTime;

    public Texture2D BashAttackArrow;

    public Texture2D RedirectArrow;

    public enum State {
        Charging,
        Critical,
        Failed,
        Finished,
    }
}
