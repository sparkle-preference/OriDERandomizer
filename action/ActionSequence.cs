using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class ActionSequence : PerformingAction, IPooled, ISuspendable
{
	public bool IsRunning
	{
		get => m_isRunning;
		set => m_isRunning = value;
	}

	public int Index
	{
		get => m_index;
		set => m_index = value;
	}

	public void OnPoolSpawned()
	{
		Stop();
		m_isSuspended = false;
	}

	public override void Awake()
	{
		SuspensionManager.Register(this);
		Game.Checkpoint.Events.OnPostRestore.Add(OnRestoreCheckpoint);
		Events.Scheduler.OnGameReset.Add(OnGameReset);
	}

	public override void OnDestroy()
	{
		SuspensionManager.Unregister(this);
		base.OnDestroy();
		Game.Checkpoint.Events.OnPostRestore.Remove(OnRestoreCheckpoint);
		Events.Scheduler.OnGameReset.Remove(OnGameReset);
	}

	private void OnGameReset()
	{
		if (m_isRunning)
		{
			Stop();
		}
	}

	public void OnRestoreCheckpoint()
	{
		var component = GetComponent<ActionSequenceSerializer>();
		if (component)
		{
			return;
		}
		
		Stop();
	}

	public void FindActions()
	{
		Actions.Clear();
		for (var i = 0; i < transform.childCount; i++)
		{
			var child = transform.GetChild(i);
			foreach (var item in child.GetComponents<ActionMethod>())
			{
				Actions.Add(item);
			}
		}
		Actions.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
	}

	public override void Perform(IContext context)
	{
		Perform(context, false);
	}

	public override void PerformInstantly(IContext context)
	{
		Perform(context, true);
	}

	public void Perform(IContext context, bool instant)
	{
		if (!enabled)
		{
			return;
		}

		if (Actions == null)
		{
			FindActions();
		}

		if (Actions.Count == 0)
		{
			return;
		}

		m_isRunning = true;
		m_isInstant = instant;
		m_index = 0;
		m_context = context;
		RunAction(Actions[m_index]);
		UpdateActions();
	}

	public void RunAction(ActionMethod action)
	{
		if (action)
		{
			if (m_isInstant)
			{
				action.PerformInstantly(m_context);
			}
			else
			{
				action.Perform(m_context);
			}
		}
	}

	public void FixedUpdate()
	{
		if (m_isSuspended)
		{
			return;
		}
		UpdateActions();
	}

	public void UpdateActions()
	{
		if (!m_isRunning)
		{
			return;
		}
		var count = Actions.Count;
		while (m_index < count)
		{
			var actionMethod = Actions[m_index];
			if (actionMethod != null && actionMethod is WaitAction)
			{
				var waitAction = actionMethod as WaitAction;
				if (waitAction.IsPerforming)
				{
					return;
				}
			}
			m_index++;
			if (m_index == count)
			{
				m_isRunning = false;
				return;
			}
			RunAction(Actions[m_index]);
		}
	}

	public static void Rename(List<ActionMethod> actions)
	{
		var num = 0;
		for (var i = 0; i < actions.Count; i++)
		{
			var actionMethod = actions[i];
			num++;
			var niceName = actionMethod.GetNiceName();
			actionMethod.name = FormatName(num, niceName);
		}
	}

	public static string FormatName(int number, string name)
	{
		return string.Format("{0:00}", number) + ". " + name;
	}

	public static string UnformatName(string name)
	{
		return name.Remove(0, 4);
	}

	public void RefreshNames()
	{
		FindActions();
		Rename(Actions);
	}

	public override string GetNiceName()
	{
		return gameObject.name;
	}

	public bool IsSuspended
	{
		get => m_isSuspended;
		set => m_isSuspended = value;
	}

	public override void Stop()
	{
		m_isRunning = false;
		m_isInstant = false;
		m_index = 0;
		m_context = null;
	}

	public override bool IsPerforming => m_isRunning;

	public override void Serialize(Archive ar)
	{
		var component = GetComponent<ActionSequenceSerializer>();
		if (component)
		{
			return;
		}
		if (ar.Reading)
		{
			Stop();
		}
		base.Serialize(ar);
	}

	private bool m_isRunning;

	private int m_index;

	private IContext m_context;

	private bool m_isSuspended;

	public List<ActionMethod> Actions = new List<ActionMethod>();

	private bool m_isInstant;
}
