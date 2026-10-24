using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

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
        if (hidden) {
            return;
        }

        if (output != null) {
            widgetFrame = GUI.Window(0, widgetFrame, WindowFunc, "Debug Widget");
        }

        logsRect = GUI.Window(1, logsRect, LogsWindowFunc, "Logs");
    }

    public void WindowFunc(int id) {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        for (var i = 0; i < output.Count; i++) {
            switch (output[i]) {
                case ListEntry entry: {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(entry.Text);
                    if (GUILayout.Button("Expand")) {
                        var newLines = new List<OutputEntry>();
                        foreach (var item in entry.Items) {
                            RenderObject(newLines, item, true);
                        }

                        foreach (var newLine in newLines) {
                            newLine.Text = new string(' ', 2 * entry.Indent) + newLine.Text;
                            newLine.Indent += entry.Indent;
                        }

                        output[i] = new TextEntry(entry.Text, entry.Indent);
                        output.InsertRange(i + 1, newLines);
                    }

                    GUILayout.EndHorizontal();
                    break;
                }
                case ObjectEntry entry: {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(entry.Text);
                    if (GUILayout.Button("Expand")) {
                        var newLines = new List<OutputEntry>();
                        RenderObject(newLines, entry.Obj, true);
                        newLines.RemoveAt(0);

                        foreach (var newLine in newLines) {
                            newLine.Text = new string(' ', 2 * entry.Indent) + newLine.Text;
                            newLine.Indent += entry.Indent;
                        }

                        output[i] = new TextEntry(entry.Text, entry.Indent);
                        output.InsertRange(i + 1, newLines);
                    }

                    GUILayout.EndHorizontal();
                    break;
                }
                case TextEntry entry:
                    GUILayout.Label(entry.Text);
                    break;
            }
        }

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    public void TargetObject(GameObject obj, bool verbose) {
        hidden = false;
        var components = obj.GetComponents<MonoBehaviour>();
        var list = new List<OutputEntry> { new TextEntry(obj.name, 0) };
        foreach (var monoBehaviour in components) {
            RenderObject(list, monoBehaviour, verbose);
        }

        output = list;
        LogCallback("Targetted " + obj.name + (verbose ? " (verbose)" : ""), "", LogType.Log);
    }

    private static void RenderObject(List<OutputEntry> list, object obj, bool verbose) {
        list.Add(new TextEntry("  " + obj.GetType().Name + (obj is MonoBehaviour mb ? " (" + mb.GetInstanceID() + ")" : null), 1));
        if (!verbose) {
            return;
        }

        var fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var fieldInfo in fields) {
            var value = fieldInfo.GetValue(obj);
            var text = value != null ? value.ToString() : "(null)";
            if (value != null && typeof(MonoBehaviour).IsAssignableFrom(fieldInfo.FieldType)) {
                list.Add(new ObjectEntry("  - " + fieldInfo.Name + ": " + text + " (" + ((MonoBehaviour)value).GetInstanceID() + ")", 1, value));
            } else if (value != null && typeof(List<CleverMenuItemGroup.CleverMenuItemGroupItem>).IsAssignableFrom(fieldInfo.FieldType)) {
                var groupItems = (List<CleverMenuItemGroup.CleverMenuItemGroupItem>)value;
                list.Add(new TextEntry("  - " + fieldInfo.Name + ":", 1));
                var num = 0;
                foreach (var item in groupItems) {
                    list.Add(new TextEntry("    - Item " + num++, 2));
                    var itemGroup = item.ItemGroup;
                    list.Add(new TextEntry("      - ItemGroup: " + (itemGroup != null ? itemGroup.name : null), 3));
                    var menuItem = item.MenuItem;
                    list.Add(new TextEntry("      - MenuItem: " + (menuItem != null ? menuItem.name : null), 3));
                }
            } else if (value is IEnumerable enumerable) {
                list.Add(new ListEntry("  - " + fieldInfo.Name + ": " + enumerable.Cast<object>().Count() + " items", 1, enumerable));
            } else {
                list.Add(new ObjectEntry("  - " + fieldInfo.Name + ": " + text, 1, value));
            }
        }
    }

    public void Update() {
        if (Input.GetKeyDown(KeyCode.KeypadEnter)) {
            hidden = true;
        }
    }

    public void Awake() {
        Application.logMessageReceived += LogCallback;
    }

    public void LogCallback(string condition, string stackTrace, LogType type) {
        if (type == LogType.Exception) {
            logItems.Add(
                new LogItem {
                    text = stackTrace,
                    type = type
                }
            );
        }

        logItems.Add(
            new LogItem {
                text = condition,
                type = type
            }
        );
    }

    public void LogsWindowFunc(int id) {
        GUI.DragWindow();
        logsScrollPosition = GUILayout.BeginScrollView(logsScrollPosition);
        for (var i = logItems.Count - 1; i >= 0; i--) {
            GUILayout.Label(logItems[i].text);
        }

        GUILayout.EndScrollView();
    }

    public Rect widgetFrame = new Rect(10f, 10f, 600f, 1000f);

    public List<OutputEntry> output;

    public static DebugWidget instance;

    public Vector2 scrollPosition;

    public bool hidden = true;

    public List<LogItem> logItems = new List<LogItem>();

    public Rect logsRect = new Rect(10f, 1000f, 1000f, 400f);

    public Vector2 logsScrollPosition;

    public struct LogItem {
        public string text;

        public LogType type;
    }

    public class OutputEntry(string text, int indent) {
        public string Text = text;
        public int Indent = indent;
    };

    public class TextEntry(string text, int indent) : OutputEntry(text, indent);

    public class ObjectEntry(string text, int indent, object obj) : OutputEntry(text, indent) {
        public object Obj = obj;
    }

    public class ListEntry(string text, int indent, IEnumerable items) : OutputEntry(text, indent) {
        public IEnumerable Items = items;
    }
}
