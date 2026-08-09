using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Managed face of the native websocket sidecar. The dll is built from
// github.com/timoschwarzer/dotnet-native-websocket (our changes are PR'd
// upstream; the built binary lives in resource_files/ — see the README
// there for the build recipe). The native dll and the CA bundle mbedtls
// needs ride inside Assembly-CSharp.dll as embedded resources (named
// exactly DllResource / CaResource); Load() extracts both next to
// oriDE.exe.
//
// No [DllImport("NativeWebSocket.dll")] anywhere: this Mono can't resolve a
// native dll that appeared on disk after process start (kernel32 LoadLibrary
// by full path succeeded while the managed-to-native wrapper threw
// DllNotFound; a pre-existing file worked on relaunch). Every export is
// bound by hand off the module handle instead — GetProcAddress +
// GetDelegateForFunctionPointer — which cannot be affected by Mono's
// search behavior.
//
// Logging rule for this file: Randomizer.log ONLY. Load() runs during
// line-0 seed parsing, before the UI exists, and Randomizer.LogError
// renders on-screen — it writes its line and then NREs at that phase.
public static class NativeWebSocket {
    public enum SocketState {
        Connecting = 0,
        Open = 1,
        Closing = 2,
        Closed = 3,
    }

    public const string DllResource = "NativeWebSocket.dll";
    public const string CaResource = "cacert.pem";

    public static bool Loaded { get; private set; }
    public static string CaPath { get; private set; }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibrary(string path);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VoidFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StrFn(string s);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void IntFn(int v);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ByteFn(byte v);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RetIntFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte RetByteFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void BytesFn(byte[] data, int len);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr PtrLenFn(out int len);

    private static VoidFn initialize_network;
    private static VoidFn finalize_network;
    private static StrFn set_url;
    private static StrFn set_ca_file;
    private static IntFn set_ping_interval;
    private static ByteFn set_auto_reconnect;
    private static VoidFn start_fn;
    private static VoidFn stop_fn;
    private static StrFn send_text;
    private static BytesFn send_binary;
    private static RetIntFn get_state;
    private static RetIntFn get_open_count;
    private static RetIntFn get_error_count;
    private static RetIntFn get_close_count;
    private static PtrLenFn get_last_error;
    private static RetByteFn has_pending_message;
    private static PtrLenFn get_pending_message;
    private static VoidFn pop_pending_message;

    public static bool Load() {
        if (Loaded)
            return true;
        try {
            var dir = ExeDir();
            Randomizer.log($"ws diag: extracting to {dir}");
            var dllPath = Extract(DllResource, Path.Combine(dir, DllResource));
            CaPath = Extract(CaResource, Path.Combine(dir, CaResource));
            if (dllPath == null)
                return false;
            var module = LoadLibrary(dllPath);
            if (module == IntPtr.Zero) {
                Randomizer.log($"ws diag: LoadLibrary({dllPath}) failed, Win32 error {Marshal.GetLastWin32Error()}");
                return false;
            }

            initialize_network = (VoidFn)Bind(module, "initialize_network", typeof(VoidFn));
            finalize_network = (VoidFn)Bind(module, "finalize_network", typeof(VoidFn));
            set_url = (StrFn)Bind(module, "set_url", typeof(StrFn));
            set_ca_file = (StrFn)Bind(module, "set_ca_file", typeof(StrFn));
            set_ping_interval = (IntFn)Bind(module, "set_ping_interval", typeof(IntFn));
            set_auto_reconnect = (ByteFn)Bind(module, "set_auto_reconnect", typeof(ByteFn));
            start_fn = (VoidFn)Bind(module, "start", typeof(VoidFn));
            stop_fn = (VoidFn)Bind(module, "stop", typeof(VoidFn));
            send_text = (StrFn)Bind(module, "send_text", typeof(StrFn));
            send_binary = (BytesFn)Bind(module, "send_binary", typeof(BytesFn));
            get_state = (RetIntFn)Bind(module, "get_state", typeof(RetIntFn));
            get_open_count = (RetIntFn)Bind(module, "get_open_count", typeof(RetIntFn));
            get_error_count = (RetIntFn)Bind(module, "get_error_count", typeof(RetIntFn));
            get_close_count = (RetIntFn)Bind(module, "get_close_count", typeof(RetIntFn));
            get_last_error = (PtrLenFn)Bind(module, "get_last_error", typeof(PtrLenFn));
            has_pending_message = (RetByteFn)Bind(module, "has_pending_message", typeof(RetByteFn));
            get_pending_message = (PtrLenFn)Bind(module, "get_pending_message", typeof(PtrLenFn));
            pop_pending_message = (VoidFn)Bind(module, "pop_pending_message", typeof(VoidFn));
            initialize_network();
            Randomizer.log("ws diag: exports bound, initialize_network ok");
            Loaded = true;
            return true;
        } catch (Exception e) {
            Randomizer.log($"NativeWebSocket.Load: {e}");
            return false;
        }
    }

    private static Delegate Bind(IntPtr module, string name, Type t) {
        var fn = GetProcAddress(module, name);
        if (fn == IntPtr.Zero)
            throw new MissingMethodException($"export {name} missing from {DllResource}");
        return Marshal.GetDelegateForFunctionPointer(fn, t);
    }

    // Application.dataPath is <install root>/oriDE_Data and never flakes;
    // Process.MainModule on this Mono is not so dependable.
    private static string ExeDir() {
        try {
            var dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
                return Path.GetDirectoryName(dataPath);
        } catch (Exception e) {
            Randomizer.log($"ws diag: Application.dataPath unavailable ({e.GetType().Name}); using cwd");
        }

        // the game's cwd is its install root (randomizer.log lives there)
        return Environment.CurrentDirectory;
    }

    // Writes the resource to disk if missing or stale. A locked stale file
    // (second game instance) is used as-is — versions only drift across
    // dll updates, and both instances then hold the same Assembly-CSharp.
    private static string Extract(string resource, string target) {
        var bytes = RandomizerResources.ReadResource(resource);
        if (bytes == null) {
            Randomizer.log($"ws diag: failed to load embedded resource '{resource}'. See previous log for more details.");
            return null;
        }

        try {
            if (!File.Exists(target) || new FileInfo(target).Length != bytes.Length) {
                File.WriteAllBytes(target, bytes);
                Randomizer.log($"ws diag: wrote {resource} ({bytes.Length} bytes) to {target}");
            } else
                Randomizer.log($"ws diag: {target} already current ({bytes.Length} bytes)");
        } catch (IOException e) {
            Randomizer.log($"ws diag: can't write {target} ({e.Message}); {(File.Exists(target) ? "using existing file" : "giving up")}");
            if (!File.Exists(target))
                return null;
        }

        return target;
    }

    public static void FinalizeNetwork() {
        finalize_network();
    }

    public static void SetUrl(string url) {
        set_url(url);
    }

    public static void SetCaFile(string path) {
        set_ca_file(path);
    }

    public static void SetPingInterval(int seconds) {
        set_ping_interval(seconds);
    }

    public static void SetAutoReconnect(bool enabled) {
        set_auto_reconnect(enabled ? (byte)1 : (byte)0);
    }

    public static void Start() {
        start_fn();
    }

    public static void Stop() {
        stop_fn();
    }

    public static void SendText(string data) {
        send_text(data);
    }

    public static void SendBinary(byte[] data) {
        send_binary(data, data.Length);
    }

    public static SocketState GetState() {
        return (SocketState)get_state();
    }

    public static int GetOpenCount() {
        return get_open_count();
    }

    public static int GetErrorCount() {
        return get_error_count();
    }

    public static int GetCloseCount() {
        return get_close_count();
    }

    public static string GetLastError() {
        int length;
        var ptr = get_last_error(out length);
        if (ptr == IntPtr.Zero || length == 0)
            return "";
        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    public static bool HasPendingMessage() {
        return has_pending_message() != 0;
    }

    // Returns null when the queue is empty.
    public static string GetPendingMessage() {
        int length;
        var ptr = get_pending_message(out length);
        if (ptr == IntPtr.Zero)
            return null;
        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        pop_pending_message();
        return Encoding.UTF8.GetString(bytes);
    }
}
