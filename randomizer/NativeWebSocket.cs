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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DownloadFn(string url, string caPath, string outPath);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BeginFn(string method, string url, string caPath, string body, string contentType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int HandleRetIntFn(int handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr HandlePtrLenFn(int handle, out int len);

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
    private static DownloadFn http_download;
    private static PtrLenFn get_last_http_error;
    private static BeginFn http_begin;
    private static HandleRetIntFn http_status;
    private static HandlePtrLenFn http_response;
    private static HandlePtrLenFn http_response_error;
    private static IntFn http_release;

    // false when the extracted sidecar predates the updater; netcode is
    // unaffected, the update option just stays hidden
    public static bool HttpAvailable => http_download != null;

    // false when it predates the async request api; callers fall back to
    // System.Net rather than losing the request
    public static bool AsyncHttpAvailable => http_begin != null;

    public const int HttpPending = -1;

    public static bool Load() {
        if (Loaded) {
            return true;
        }

        try {
            var dir = ExeDir();
            Randomizer.log($"ws diag: extracting to {dir}");
            var dllPath = Extract(DllResource, Path.Combine(dir, DllResource));
            CaPath = Extract(CaResource, Path.Combine(dir, CaResource));
            if (dllPath == null) {
                return false;
            }

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
            // optional, so a wrapper newer than the extracted dll keeps its socket
            http_download = (DownloadFn)BindOptional(module, "http_download", typeof(DownloadFn));
            get_last_http_error = (PtrLenFn)BindOptional(module, "get_last_http_error", typeof(PtrLenFn));
            http_begin = (BeginFn)BindOptional(module, "http_begin", typeof(BeginFn));
            http_status = (HandleRetIntFn)BindOptional(module, "http_status", typeof(HandleRetIntFn));
            http_response = (HandlePtrLenFn)BindOptional(module, "http_response", typeof(HandlePtrLenFn));
            http_response_error = (HandlePtrLenFn)BindOptional(module, "http_response_error", typeof(HandlePtrLenFn));
            http_release = (IntFn)BindOptional(module, "http_release", typeof(IntFn));
            if (http_download == null) {
                Randomizer.log("ws diag: sidecar has no http_download; updater disabled");
            }

            if (http_begin == null) {
                Randomizer.log("ws diag: sidecar has no async http; falling back to System.Net");
            }

            initialize_network();
            Randomizer.log("ws diag: exports bound, initialize_network ok");
            Loaded = true;
            return true;
        } catch (Exception e) {
            Randomizer.log($"NativeWebSocket.Load: {e}");
            return false;
        }
    }

    private static Delegate BindOptional(IntPtr module, string name, Type t) {
        var fn = GetProcAddress(module, name);
        return fn == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer(fn, t);
    }

    private static Delegate Bind(IntPtr module, string name, Type t) {
        var fn = GetProcAddress(module, name);
        if (fn == IntPtr.Zero) {
            throw new MissingMethodException($"export {name} missing from {DllResource}");
        }

        return Marshal.GetDelegateForFunctionPointer(fn, t);
    }

    // Application.dataPath is <install root>/oriDE_Data and never flakes;
    // Process.MainModule on this Mono is not so dependable.
    private static string ExeDir() {
        try {
            var dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath)) {
                return Path.GetDirectoryName(dataPath);
            }
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
            } else {
                Randomizer.log($"ws diag: {target} already current ({bytes.Length} bytes)");
            }
        } catch (IOException e) {
            Randomizer.log($"ws diag: can't write {target} ({e.Message}); {(File.Exists(target) ? "using existing file" : "giving up")}");
            if (!File.Exists(target)) {
                return null;
            }
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
        var ptr = get_last_error(out var length);
        if (ptr == IntPtr.Zero || length == 0) {
            return "";
        }

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    // Blocking, and the body lands in outPath rather than crossing interop.
    // Returns the HTTP status, or negative if it never got that far.
    public static int HttpDownload(string url, string outPath) {
        if (http_download == null) {
            return -1;
        }

        return http_download(url, CaPath ?? "", outPath);
    }

    // Async request handles. Returns 0 if the request could not be started;
    // poll HttpStatus until it stops returning HttpPending, then read the body
    // and always HttpRelease.
    public static int HttpBegin(string method, string url, string body, string contentType) {
        if (http_begin == null) {
            return 0;
        }

        return http_begin(method, url, CaPath ?? "", body ?? "", contentType ?? "");
    }

    public static int HttpStatus(int handle) {
        return http_status == null ? 0 : http_status(handle);
    }

    public static string HttpResponse(int handle) {
        return ReadHandleString(http_response, handle);
    }

    public static string HttpResponseError(int handle) {
        return ReadHandleString(http_response_error, handle);
    }

    public static void HttpRelease(int handle) {
        if (http_release != null) {
            http_release(handle);
        }
    }

    private static string ReadHandleString(HandlePtrLenFn fn, int handle) {
        if (fn == null) {
            return "";
        }

        var ptr = fn(handle, out var length);
        if (ptr == IntPtr.Zero || length == 0) {
            return "";
        }

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    public static string GetLastHttpError() {
        if (get_last_http_error == null) {
            return "sidecar has no http support";
        }

        var ptr = get_last_http_error(out var length);
        if (ptr == IntPtr.Zero || length == 0) {
            return "";
        }

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    public static bool HasPendingMessage() {
        return has_pending_message() != 0;
    }

    // Returns null when the queue is empty.
    public static string GetPendingMessage() {
        var ptr = get_pending_message(out var length);
        if (ptr == IntPtr.Zero) {
            return null;
        }

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        pop_pending_message();
        return Encoding.UTF8.GetString(bytes);
    }
}
