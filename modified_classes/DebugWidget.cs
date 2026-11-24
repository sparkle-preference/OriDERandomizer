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
        var components = obj.GetComponents<Component>();
        var list = new List<OutputEntry> { new TextEntry(obj.name, 0) };
        foreach (var component in components) {
            RenderObject(list, component, verbose);
        }

        output = list;
        LogCallback("Targetted " + obj.name + (verbose ? " (verbose)" : ""), "", LogType.Log);
    }

    private static void RenderObject(List<OutputEntry> list, object obj, bool verbose) {
        if (obj == null) {
            list.Add(new TextEntry("  (null)", 1));
            return;
        }

        list.Add(new TextEntry("  " + obj.GetType().Name + ObjectInfo(obj), 1));
        if (!verbose) {
            return;
        }

        switch (obj) {
            case Transform tf:
                list.Add(new TextEntry("  - Position: " + tf.localPosition, 1));
                list.Add(new TextEntry("  - Rotation: " + tf.localEulerAngles, 1));
                list.Add(new TextEntry("  - Scale: " + tf.localScale, 1));
                break;
            case MeshFilter mf:
                list.Add(new TextEntry("  - Mesh: " + mf.sharedMesh, 1));
                if (mf.sharedMesh != null) {
                    list.Add(new TextEntry("    - Vertices: " + mf.sharedMesh.vertexCount, 2));
                    list.Add(new TextEntry("    - Bounds: " + mf.sharedMesh.bounds, 2));
                }

                break;
            case Renderer mr:
                list.Add(new ListEntry("  - Materials: " + mr.sharedMaterials.Length + " items", 1, mr.sharedMaterials));
                break;
        }

        var fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var fieldInfo in fields) {
            var value = fieldInfo.GetValue(obj);
            var text = value != null ? value.ToString() : "(null)";
            switch (value) {
                case Object o:
                    list.Add(new ObjectEntry($"  - {fieldInfo.Name}: {text}{ObjectInfo(o)}", 1, value));
                    break;
                case List<CleverMenuItemGroup.CleverMenuItemGroupItem> groupItems: {
                    list.Add(new TextEntry($"  - {fieldInfo.Name}:", 1));
                    var num = 0;
                    foreach (var item in groupItems) {
                        list.Add(new TextEntry($"    - Item {num++}", 2));
                        var itemGroup = item.ItemGroup;
                        list.Add(new TextEntry("      - ItemGroup: " + (itemGroup != null ? itemGroup.name : null), 3));
                        var menuItem = item.MenuItem;
                        list.Add(new TextEntry("      - MenuItem: " + (menuItem != null ? menuItem.name : null), 3));
                    }

                    break;
                }
                case IEnumerable enumerable:
                    list.Add(new ListEntry($"  - {fieldInfo.Name}: {enumerable.Cast<object>().Count()} items", 1, enumerable));
                    break;
                default:
                    list.Add(new ObjectEntry($"  - {fieldInfo.Name}: {text}", 1, value));
                    break;
            }
        }
    }

    private static string ObjectInfo(object obj) {
        if (obj is not Object o) {
            return "";
        }

        if (!o) {
            return " (Dead)";
        }

        var name = o is not Component ? " " + o.name : null;
        return $"{name} ({o.GetInstanceID()})";
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

    // TODO: switch "abstract" to "closed" once it's supported by dnSpy
    public abstract class OutputEntry(string text, int indent) {
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
