using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class DebugWidget : MonoBehaviour {
    public static DebugWidget Instance {
        get {
            if (DebugWidget.instance == null) {
                DebugWidget.instance = new GameObject("DebugWidget").AddComponent<DebugWidget>();
                UnityEngine.Object.DontDestroyOnLoad(DebugWidget.instance.gameObject);
            }

            return DebugWidget.instance;
        }
    }

    public void OnGUI() {
        if (this.hidden) {
            return;
        }

        if (this.output != null) {
            this.widgetFrame = GUI.Window(0, this.widgetFrame, new GUI.WindowFunction(this.WindowFunc), "Debug Widget");
        }

        this.logsRect = GUI.Window(1, this.logsRect, new GUI.WindowFunction(this.LogsWindowFunc), "Logs");
    }

    public void WindowFunc(int id) {
        GUI.DragWindow();
        this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUILayoutOption[0]);
        for (int i = 0; i < this.output.Length; i++) {
            GUILayout.Label(this.output[i], new GUILayoutOption[0]);
        }

        GUILayout.EndScrollView();
    }

    public void TargetObject(GameObject obj, bool verbose) {
        this.hidden = false;
        MonoBehaviour[] components = obj.GetComponents<MonoBehaviour>();
        List<string> list = new List<string> { obj.name };
        foreach (MonoBehaviour monoBehaviour in components) {
            list.Add("  " + monoBehaviour.GetType().Name + " (" + monoBehaviour.GetInstanceID() + ")");
            if (!verbose) {
                continue;
            }

            FieldInfo[] fields = monoBehaviour.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo fieldInfo in fields) {
                object value = fieldInfo.GetValue(monoBehaviour);
                string text = (value != null) ? value.ToString() : "(null)";
                if (value != null && typeof(MonoBehaviour).IsAssignableFrom(fieldInfo.FieldType)) {
                    list.Add("  - " + fieldInfo.Name + ": " + text + " (" + ((MonoBehaviour)value).GetInstanceID() + ")");
                } else if (value != null && typeof(List<CleverMenuItemGroup.CleverMenuItemGroupItem>).IsAssignableFrom(fieldInfo.FieldType)) {
                    List<CleverMenuItemGroup.CleverMenuItemGroupItem> groupItems = (List<CleverMenuItemGroup.CleverMenuItemGroupItem>)value;
                    list.Add("  - " + fieldInfo.Name + ":");
                    int num = 0;
                    foreach (CleverMenuItemGroup.CleverMenuItemGroupItem item in groupItems) {
                        list.Add("    - Item " + num++);
                        CleverMenuItemGroupBase itemGroup = item.ItemGroup;
                        list.Add("      - ItemGroup: " + ((itemGroup != null) ? itemGroup.name : null));
                        CleverMenuItem menuItem = item.MenuItem;
                        list.Add("      - MenuItem: " + ((menuItem != null) ? menuItem.name : null));
                    }
                } else if (value != null && typeof(IEnumerable).IsAssignableFrom(fieldInfo.FieldType)) {
                    list.Add("  - " + fieldInfo.Name + ": " + ((IEnumerable)value).Cast<object>().Count() + " items");
                } else {
                    list.Add("  - " + fieldInfo.Name + ": " + text);
                }
            }
        }

        this.output = list.ToArray();
        this.LogCallback("Targetted " + obj.name + (verbose ? " (verbose)" : ""), "", LogType.Log);
    }

    public void Update() {
        if (UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) {
            this.hidden = true;
        }
    }

    public void Awake() {
        Application.logMessageReceived += this.LogCallback;
    }

    public void LogCallback(string condition, string stackTrace, LogType type) {
        if (type == LogType.Exception) {
            this.logItems.Add(
                new DebugWidget.LogItem {
                    text = stackTrace,
                    type = type
                }
            );
        }

        this.logItems.Add(
            new DebugWidget.LogItem {
                text = condition,
                type = type
            }
        );
    }

    public void LogsWindowFunc(int id) {
        GUI.DragWindow();
        this.logsScrollPosition = GUILayout.BeginScrollView(this.logsScrollPosition, new GUILayoutOption[0]);
        for (int i = this.logItems.Count - 1; i >= 0; i--) {
            GUILayout.Label(this.logItems[i].text, new GUILayoutOption[0]);
        }

        GUILayout.EndScrollView();
    }

    public Rect widgetFrame = new Rect(10f, 10f, 600f, 1000f);

    public string[] output;

    public static DebugWidget instance;

    public Vector2 scrollPosition;

    public bool hidden = true;

    public List<DebugWidget.LogItem> logItems = new List<DebugWidget.LogItem>();

    public Rect logsRect = new Rect(10f, 1000f, 1000f, 400f);

    public Vector2 logsScrollPosition;

    public struct LogItem {
        public string text;

        public LogType type;
    }
}
