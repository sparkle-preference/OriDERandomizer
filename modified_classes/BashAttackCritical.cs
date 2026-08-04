using UnityEngine;

public class BashAttackCritical : Suspendable, IPooled
{
	public void OnPoolSpawned()
	{
		CurrentState = State.Charging;
		m_stateCurrentTime = 0f;
		m_suspended = false;
	}

	public void ChangeState(State state)
	{
		CurrentState = state;
		m_stateCurrentTime = 0f;
	}

	public void UpdateState()
	{
		switch (CurrentState)
		{
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
		m_stateCurrentTime += Time.deltaTime;
	}

	private void UpdateFailedState()
	{
		transform.localScale = m_localScale;
		GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_MaskTexture", new Vector2(0.5f, 0f));
		if (m_stateCurrentTime > FailedDuration)
		{
			ChangeState(State.Finished);
		}
	}

	private void UpdateCriticalState()
	{
		transform.localScale = m_localScale + Vector3.one * Mathf.Sin(m_stateCurrentTime * 6.2831855f / ShakePeriod) * ShakeAmount;
		GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_MaskTexture", new Vector2(0.5f * (Mathf.RoundToInt(m_stateCurrentTime * 15f) % 2), 0f));
		float criticalDuration = CriticalDuration;
		if (RandomizerSettings.Controls.LongerBashAimTime)
		{
			criticalDuration += 3.3f;
		}
		if (m_stateCurrentTime > criticalDuration)
		{
			ChangeState(State.Failed);
		}
	}

	private void UpdateChargingState()
	{
		transform.localScale = m_localScale;
		float num = m_stateCurrentTime / ChargingDuration;
		GetComponent<Renderer>().sharedMaterial.SetTextureOffset("_MaskTexture", new Vector2(0.5f - num * 0.5f, 0f));
		if (m_stateCurrentTime > ChargingDuration)
		{
			ChangeState(State.Critical);
		}
	}

	public new void Awake()
	{
		base.Awake();
		m_localScale = transform.localScale;
	}

	public override bool IsSuspended
	{
		get
		{
			return m_suspended;
		}
		set
		{
			m_suspended = value;
		}
	}

	public void FixedUpdate()
	{
		if (IsSuspended)
		{
			return;
		}
		UpdateState();
	}

	public float ChargingDuration;

	public float CriticalDuration;

	public float FailedDuration;

	public float ShakePeriod = 0.2f;

	public float ShakeAmount = 0.5f;

	private Vector3 m_localScale;

	public State CurrentState;

	private bool m_suspended;

	private float m_stateCurrentTime;

	public Texture2D BashAttackArrow;

	public Texture2D RedirectArrow;

	public enum State
	{
		Charging,
		Critical,
		Failed,
		Finished
	}
}
