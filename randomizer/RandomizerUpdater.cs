using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

// Checks for a newer dll on boot and, if the player asks, fetches it and
// restarts into it. Windows keeps the loaded assembly locked, so a throwaway
// script does the swap once the game exits.
//
// Only the blocking transfer runs off the main thread: Unity paths, quitting
// and the log are all touched from Update.
public class RandomizerUpdater : MonoBehaviour {
    public static string LatestVersion { get; private set; }

    public static bool UpdateAvailable { get; private set; }

    // set once the player commits, so the menu item stops responding
    public static bool Busy { get; private set; }

    public static string Status { get; private set; }

    public static void Initialize() {
        Instance = new GameObject("randomizerUpdater").AddComponent<RandomizerUpdater>();
    }

    public void Awake() {
        // extracting and binding the sidecar reads Unity paths, so it happens
        // here rather than on the worker
        s_dataPath = Application.dataPath;
        s_exeDir = string.IsNullOrEmpty(s_dataPath) ? Environment.CurrentDirectory : Path.GetDirectoryName(s_dataPath);

        if (!NativeWebSocket.Load() || !NativeWebSocket.HttpAvailable) {
            Log("updater: sidecar has no http support; update check skipped");
            return;
        }

        Run(CheckThread);
    }

    public void Update() {
        DrainLog();
        RefreshMenuItem();

        if (m_confirmPending) {
            m_confirmPending = false;
            ShowRestartPrompt();
        }

        if (m_restartPending) {
            m_restartPending = false;
            Application.Quit();
        }
    }

    // the version check can land either side of the title screen being built,
    // so the entry is added from Update rather than at bootstrap
    public static void BindMainMenu(CleverMenuItemSelectionManager menu, CleverMenuItemSelectionManager exitScreen) {
        s_mainMenu = menu;
        s_exitScreen = exitScreen;
        s_menuItem = null;
        s_shownStatus = null;
        s_titleBox = null;
        s_okHandler = null;
        s_cancelHandler = null;
    }

    private static void RefreshMenuItem() {
        if (!UpdateAvailable || s_mainMenu == null) {
            return;
        }

        try {
            if (s_menuItem == null) {
                s_shownStatus = $"UPDATE TO {LatestVersion}";
                var template = s_mainMenu.MenuItems[0];
                s_mainMenu.AddMenuItem(s_shownStatus, s_mainMenu.MenuItems.Count - 1, StartUpdate);
                s_menuItem = s_mainMenu.MenuItems[s_mainMenu.MenuItems.Count - 2];

                // AddMenuItem reparents keeping world scale, so the clone comes
                // out divided by the menu's own
                s_menuItem.transform.localScale = template.transform.localScale;

                // PressedCallback only adds to the cloned entry's action, and
                // MenuItems[0] is "start game"
                s_menuItem.Pressed = null;
                return;
            }

            if (Status != null && Status != s_shownStatus) {
                s_shownStatus = Status;
                s_menuItem.GetComponentInChildren<MessageBox>().SetMessage(new MessageDescriptor(Status));
            }
        } catch (Exception e) {
            Log($"updater: could not add menu item: {e.Message}");
            s_mainMenu = null;
        }
    }

    public static void StartUpdate() {
        if (Busy || !UpdateAvailable) {
            return;
        }

        Busy = true;
        Status = "Downloading...";
        Run(Instance.UpdateThread);
    }

    private static void Run(ThreadStart body) {
        // the sidecar's http calls block until the transfer finishes
        var thread = new Thread(body);
        thread.IsBackground = true;
        thread.Start();
    }

    private static string Host {
        get {
            var endpoint = RandomizerSettings.DevSettings.WsEndpoint;
            if (endpoint == null) {
                return "bf.orirando.com";
            }

            // the setting carries a scheme since 4.3; the updater builds its own
            var v = endpoint.Value;
            var at = v.IndexOf("://");
            return (at >= 0 ? v.Substring(at + 3) : v).TrimEnd('/');
        }
    }

    private static void CheckThread() {
        try {
            var scratch = Path.Combine(Path.GetTempPath(), "orirando_version.txt");
            var status = NativeWebSocket.HttpDownload($"https://{Host}/version/latest", scratch);
            if (status != 200) {
                Log($"updater: version check got {status} ({NativeWebSocket.GetLastHttpError()})");
                return;
            }

            var latest = File.ReadAllText(scratch).Trim();
            File.Delete(scratch);
            if (!IsNewer(latest, Randomizer.VERSION)) {
                Log($"updater: up to date (running {Randomizer.VERSION}, latest {latest})");
                return;
            }

            if (IsSymlink(ManagedDll)) {
                // a dev install links the assembly at its build output, and
                // moving a download over that replaces the link
                Log($"updater: {latest} available, but {ManagedDll} is a link; not offering to update");
                return;
            }

            LatestVersion = latest;
            UpdateAvailable = true;
            Log($"updater: {latest} available (running {Randomizer.VERSION})");
        } catch (Exception e) {
            Log($"updater: version check failed: {e}");
        }
    }

    private void UpdateThread() {
        try {
            var staged = ManagedDll + ".new";
            var status = NativeWebSocket.HttpDownload($"https://{Host}/dll", staged);
            if (status != 200) {
                Fail($"download failed ({status}: {NativeWebSocket.GetLastHttpError()})");
                return;
            }

            // a truncated download or an error page must never reach the swap
            var size = new FileInfo(staged).Length;
            if (size < 1000000L) {
                File.Delete(staged);
                Fail($"download was only {size} bytes");
                return;
            }

            // hand back to the main thread: the prompt is UI work
            m_staged = staged;
            Status = "Ready to restart";
            m_confirmPending = true;
        } catch (Exception e) {
            Fail(e.Message);
        }
    }

    // Borrows the quit confirmation: it hides the menu for us, and a live
    // screen's text takes immediately where a fresh clone keeps its prefab
    // text. Its OK action quits, which is what the swap script waits for.
    private void ShowRestartPrompt() {
        if (s_exitScreen == null || s_exitScreen.MenuItems.Count < 2) {
            Log("updater: no quit screen to borrow; restarting directly");
            SwapAndRestart(m_staged);
            return;
        }

        try {
            s_titleBox = FindTitle(s_exitScreen);
            if (s_titleBox != null) {
                s_savedProvider = s_titleBox.MessageProvider;
                s_titleBox.SetMessage(new MessageDescriptor("Restart to apply update?"));
            }

            s_okHandler = delegate { SwapAndRestart(m_staged); };
            s_exitScreen.MenuItems[0].PressedCallback += s_okHandler;

            s_cancelHandler = delegate {
                RestoreExitScreen();
                CancelUpdate();
            };
            s_exitScreen.MenuItems[1].PressedCallback += s_cancelHandler;

            TitleScreenManager.SetScreen(TitleScreenManager.Screen.ExitGame);
        } catch (Exception e) {
            Log($"updater: prompt failed ({e.Message}); restarting directly");
            RestoreExitScreen();
            SwapAndRestart(m_staged);
        }
    }

    // everything borrowed goes back, or the next real quit asks the wrong
    // question and runs our handler
    private static void RestoreExitScreen() {
        if (s_titleBox != null && s_savedProvider != null) {
            s_titleBox.SetMessageProvider(s_savedProvider);
        }

        if (s_exitScreen != null && s_exitScreen.MenuItems.Count >= 2) {
            if (s_okHandler != null) {
                s_exitScreen.MenuItems[0].PressedCallback -= s_okHandler;
            }

            if (s_cancelHandler != null) {
                s_exitScreen.MenuItems[1].PressedCallback -= s_cancelHandler;
            }
        }

        s_titleBox = null;
        s_savedProvider = null;
        s_okHandler = null;
        s_cancelHandler = null;
    }

    // the question, as opposed to the OK/CANCEL labels
    private static MessageBox FindTitle(CleverMenuItemSelectionManager screen) {
        foreach (var box in screen.GetComponentsInChildren<MessageBox>(true)) {
            if (box.GetComponentInParent<CleverMenuItem>() == null) {
                return box;
            }
        }

        return null;
    }

    private void CancelUpdate() {
        try {
            if (File.Exists(m_staged)) {
                File.Delete(m_staged);
            }
        } catch (Exception e) {
            Log($"updater: could not clear staged download: {e.Message}");
        }

        Busy = false;
        Status = $"UPDATE TO {LatestVersion}";
    }

    private static void Fail(string reason) {
        Log($"updater: {reason}");
        Status = "Update failed, see randomizer.log";
        Busy = false;
    }

    private void SwapAndRestart(string staged) {
        var exe = Path.Combine(s_exeDir, "oriDE.exe");
        var script = Path.Combine(Path.GetTempPath(), "orirando_update.bat");

        // %~f0 deletes the script as its own last act; cmd reads batch files
        // line by line, so this is safe once nothing follows it
        var body = "@echo off\r\n"
            + ":wait\r\n"
            + "tasklist /FI \"IMAGENAME eq oriDE.exe\" | find /I \"oriDE.exe\" >nul\r\n"
            + "if not errorlevel 1 (\r\n"
            + "  ping -n 2 127.0.0.1 >nul\r\n"
            + "  goto wait\r\n"
            + ")\r\n"
            + $"move /Y \"{staged}\" \"{ManagedDll}\"\r\n"
            + $"start \"\" /D \"{s_exeDir}\" \"{exe}\"\r\n"
            + "del \"%~f0\"\r\n";

        File.WriteAllText(script, body);

        var info = new ProcessStartInfo("cmd.exe", $"/c \"{script}\"");
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.WorkingDirectory = s_exeDir;
        Process.Start(info);

        Log($"updater: staged {staged}, quitting for restart");
        m_restartPending = true;
    }

    // "4.2.15" is newer than "4.2.14"; anything unparseable is not
    private static bool IsNewer(string candidate, string running) {
        var left = Parse(candidate);
        var right = Parse(running);
        if (left == null || right == null) {
            return false;
        }

        for (var i = 0; i < 3; i++) {
            if (left[i] != right[i]) {
                return left[i] > right[i];
            }
        }

        return false;
    }

    private static int[] Parse(string version) {
        var parts = version.Split('.');
        if (parts.Length != 3) {
            return null;
        }

        var parsed = new int[3];
        for (var i = 0; i < 3; i++) {
            if (!int.TryParse(parts[i], out parsed[i])) {
                return null;
            }
        }

        return parsed;
    }

    private static bool IsSymlink(string path) {
        try {
            return File.Exists(path)
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        } catch (Exception) {
            return false;
        }
    }

    private static string ManagedDll {
        get { return Path.Combine(s_dataPath, Path.Combine("Managed", "Assembly-CSharp.dll")); }
    }

    // Randomizer.log opens the file per call, so the worker queues instead of
    // racing the main thread for it
    private static void Log(string message) {
        lock (s_pending) {
            s_pending.Add(message);
        }
    }

    private static void DrainLog() {
        lock (s_pending) {
            if (s_pending.Count == 0) {
                return;
            }

            foreach (var message in s_pending) {
                Randomizer.log(message);
            }

            s_pending.Clear();
        }
    }

    public static RandomizerUpdater Instance;

    private static readonly List<string> s_pending = new List<string>();

    private static string s_dataPath;

    private static string s_exeDir;

    private static CleverMenuItemSelectionManager s_mainMenu;

    private static CleverMenuItemSelectionManager s_exitScreen;

    private static MessageBox s_titleBox;

    private static MessageProvider s_savedProvider;

    private static Action s_okHandler;

    private static Action s_cancelHandler;

    private static CleverMenuItem s_menuItem;

    private static string s_shownStatus;

    private volatile bool m_restartPending;

    private volatile bool m_confirmPending;

    private string m_staged;
}
