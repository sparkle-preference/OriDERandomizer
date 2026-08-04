using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class DebugWidget : MonoBehaviour
{
	public static DebugWidget Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new GameObject("DebugWidget").AddComponent<DebugWidget>();
				DontDestroyOnLoad(instance.gameObject);
			}
			return instance;
		}
	}

	public void OnGUI()
	{
		if (hidden)
		{
			return;
		}
		if (output != null)
		{
			widgetFrame = GUI.Window(0, widgetFrame, WindowFunc, "Debug Widget");
		}
		logsRect = GUI.Window(1, logsRect, LogsWindowFunc, "Logs");
	}

	public void WindowFunc(int id)
	{
		GUI.DragWindow();
		scrollPosition = GUILayout.BeginScrollView(scrollPosition);
		for (int i = 0; i < output.Length; i++)
		{
			GUILayout.Label(output[i]);
		}
		GUILayout.EndScrollView();
	}

	public void TargetObject(GameObject obj, bool verbose)
	{
		hidden = false;
		MonoBehaviour[] components = obj.GetComponents<MonoBehaviour>();
		List<string> list = new List<string> { obj.name };
		foreach (MonoBehaviour monoBehaviour in components)
		{
			list.Add("  " + monoBehaviour.GetType().Name + " (" + monoBehaviour.GetInstanceID() + ")");
			if (!verbose)
			{
				continue;
			}
			FieldInfo[] fields = monoBehaviour.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				object value = fieldInfo.GetValue(monoBehaviour);
				string text = value != null ? value.ToString() : "(null)";
				if (value != null && typeof(MonoBehaviour).IsAssignableFrom(fieldInfo.FieldType))
				{
					list.Add("  - " + fieldInfo.Name + ": " + text + " (" + ((MonoBehaviour)value).GetInstanceID() + ")");
				}
				else if (value != null && typeof(List<CleverMenuItemGroup.CleverMenuItemGroupItem>).IsAssignableFrom(fieldInfo.FieldType))
				{
					List<CleverMenuItemGroup.CleverMenuItemGroupItem> groupItems = (List<CleverMenuItemGroup.CleverMenuItemGroupItem>)value;
					list.Add("  - " + fieldInfo.Name + ":");
					int num = 0;
					foreach (CleverMenuItemGroup.CleverMenuItemGroupItem item in groupItems)
					{
						list.Add("    - Item " + num++);
						CleverMenuItemGroupBase itemGroup = item.ItemGroup;
						list.Add("      - ItemGroup: " + (itemGroup != null ? itemGroup.name : null));
						CleverMenuItem menuItem = item.MenuItem;
						list.Add("      - MenuItem: " + (menuItem != null ? menuItem.name : null));
					}
				}
				else if (value != null && typeof(IEnumerable).IsAssignableFrom(fieldInfo.FieldType))
				{
					list.Add("  - " + fieldInfo.Name + ": " + ((IEnumerable)value).Cast<object>().Count() + " items");
				}
				else
				{
					list.Add("  - " + fieldInfo.Name + ": " + text);
				}
			}
		}
		output = list.ToArray();
		LogCallback("Targetted " + obj.name + (verbose ? " (verbose)" : ""), "", LogType.Log);
	}

	public void Update()
	{
		if (Input.GetKeyDown(KeyCode.KeypadEnter))
		{
			hidden = true;
		}
	}

	public void Awake()
	{
		Application.logMessageReceived += LogCallback;
	}

	public void LogCallback(string condition, string stackTrace, LogType type)
	{
		if (type == LogType.Exception)
		{
			logItems.Add(new LogItem
			{
				text = stackTrace,
				type = type
			});
		}
		logItems.Add(new LogItem
		{
			text = condition,
			type = type
		});
	}

	public void LogsWindowFunc(int id)
	{
		GUI.DragWindow();
		logsScrollPosition = GUILayout.BeginScrollView(logsScrollPosition);
		for (int i = logItems.Count - 1; i >= 0; i--)
		{
			GUILayout.Label(logItems[i].text);
		}
		GUILayout.EndScrollView();
	}

	public Rect widgetFrame = new Rect(10f, 10f, 600f, 1000f);

	public string[] output;

	public static DebugWidget instance;

	public Vector2 scrollPosition;

	public bool hidden = true;

	public List<LogItem> logItems = new List<LogItem>();

	public Rect logsRect = new Rect(10f, 1000f, 1000f, 400f);

	public Vector2 logsScrollPosition;

	public struct LogItem
	{
		public string text;

		public LogType type;
	}
}
