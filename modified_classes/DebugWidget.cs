using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

public class DebugWidget : MonoBehaviour {
    public static DebugWidget Instance {
        get {
            if (instance == null) {
                instance = new GameObject("DebugWidget").AddComponent<DebugWidget>();
                DontDestroyOnLoad(instance.gameObject);
            }

            return instance;
        }
    }

    public void OnGUI() {
        if (Hidden) {
            return;
        }

        if (Output != null) {
            WidgetFrame = GUI.Window(0, WidgetFrame, WindowFunc, "Debug Widget");
        }

        LogsRect = GUI.Window(1, LogsRect, LogsWindowFunc, "Logs");
    }

    public void WindowFunc(int id) {
        GUI.DragWindow();
        ScrollPosition = GUILayout.BeginScrollView(ScrollPosition);
        for (var i = 0; i < Output.Length; i++) {
            GUILayout.Label(Output[i]);
        }

        GUILayout.EndScrollView();
    }

    public void TargetObject(GameObject obj, bool verbose) {
        Hidden = false;
        var components = obj.GetComponents<MonoBehaviour>();
        var list = new List<string> { obj.name };
        foreach (var monoBehaviour in components) {
            list.Add("  " + monoBehaviour.GetType().Name + " (" + monoBehaviour.GetInstanceID() + ")");
            if (!verbose) {
                continue;
            }

            var fields = monoBehaviour.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var fieldInfo in fields) {
                var value = fieldInfo.GetValue(monoBehaviour);
                var text = value != null ? value.ToString() : "(null)";
                if (value != null && typeof(MonoBehaviour).IsAssignableFrom(fieldInfo.FieldType)) {
                    list.Add("  - " + fieldInfo.Name + ": " + text + " (" + ((MonoBehaviour)value).GetInstanceID() + ")");
                } else if (value != null && typeof(List<CleverMenuItemGroup.CleverMenuItemGroupItem>).IsAssignableFrom(fieldInfo.FieldType)) {
                    var groupItems = (List<CleverMenuItemGroup.CleverMenuItemGroupItem>)value;
                    list.Add("  - " + fieldInfo.Name + ":");
                    var num = 0;
                    foreach (var item in groupItems) {
                        list.Add("    - Item " + num++);
                        var itemGroup = item.ItemGroup;
                        list.Add("      - ItemGroup: " + (itemGroup != null ? itemGroup.name : null));
                        var menuItem = item.MenuItem;
                        list.Add("      - MenuItem: " + (menuItem != null ? menuItem.name : null));
                    }
                } else if (value != null && typeof(IEnumerable).IsAssignableFrom(fieldInfo.FieldType)) {
                    list.Add("  - " + fieldInfo.Name + ": " + ((IEnumerable)value).Cast<object>().Count() + " items");
                } else {
                    list.Add("  - " + fieldInfo.Name + ": " + text);
                }
            }
        }

        Output = list.ToArray();
        LogCallback("Targetted " + obj.name + (verbose ? " (verbose)" : ""), "", LogType.Log);
    }

    public void Update() {
        if (Input.GetKeyDown(KeyCode.KeypadEnter)) {
            Hidden = true;
        }
    }

    public void Awake() {
        Application.logMessageReceived += LogCallback;
    }

    public void LogCallback(string condition, string stackTrace, LogType type) {
        if (type == LogType.Exception) {
            LogItems.Add(
                new LogItem {
                    Text = stackTrace,
                    Type = type,
                }
            );
        }

        LogItems.Add(
            new LogItem {
                Text = condition,
                Type = type,
            }
        );
    }

    public void LogsWindowFunc(int id) {
        GUI.DragWindow();
        LogsScrollPosition = GUILayout.BeginScrollView(LogsScrollPosition);
        for (var i = LogItems.Count - 1; i >= 0; i--) {
            GUILayout.Label(LogItems[i].Text);
        }

        GUILayout.EndScrollView();
    }

    [FormerlySerializedAs("widgetFrame")] public Rect WidgetFrame = new Rect(10f, 10f, 600f, 1000f);

    [FormerlySerializedAs("output")] public string[] Output;

    public static DebugWidget instance;

    [FormerlySerializedAs("scrollPosition")] public Vector2 ScrollPosition;

    [FormerlySerializedAs("hidden")] public bool Hidden = true;

    public List<LogItem> LogItems = new List<LogItem>();

    [FormerlySerializedAs("logsRect")] public Rect LogsRect = new Rect(10f, 1000f, 1000f, 400f);

    [FormerlySerializedAs("logsScrollPosition")] public Vector2 LogsScrollPosition;

    public struct LogItem {
        public string Text;

        public LogType Type;
    }
}
