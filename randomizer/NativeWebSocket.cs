using System;
using System.IO;
using System.Runtime.InteropServices;

public enum SocketState : int
{
	Connecting = 0,
	Open = 1,
	Closing = 2,
	Closed = 3,
}

// P/Invoke surface of the native websocket sidecar (native/websocket/).
// The native dll and the CA bundle mbedtls needs ride inside
// Assembly-CSharp.dll as embedded resources (named exactly DllResource /
// CaResource below); Load() extracts both next to oriDE.exe and
// LoadLibrary's the dll by full path so the DllImports resolve without
// touching the search path.
//
// Diagnostic note: on this Mono a DllNotFoundException's Message is just
// the dll name, so "ws diag" breadcrumbs log every step — one run of a
// broken merge should pinpoint the failing layer.
public static class NativeWebSocket
{
	public const string DllResource = "NativeWebSocket.dll";
	public const string CaResource = "cacert.pem";

	public static bool Loaded { get; private set; }
	public static string CaPath { get; private set; }

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
	private static extern IntPtr LoadLibrary(string path);

	public static bool Load()
	{
		if (Loaded)
			return true;
		try
		{
			string dir = ExeDir();
			Randomizer.log($"ws diag: extracting to {dir}");
			string dllPath = Extract(DllResource, Path.Combine(dir, DllResource));
			CaPath = Extract(CaResource, Path.Combine(dir, CaResource));
			if (dllPath == null)
				return false;
			IntPtr handle = LoadLibrary(dllPath);
			if (handle == IntPtr.Zero)
			{
				Randomizer.LogError($"ws diag: LoadLibrary({dllPath}) failed, Win32 error {Marshal.GetLastWin32Error()}");
				return false;
			}
			Randomizer.log($"ws diag: LoadLibrary ok ({handle})");
			InitializeNetwork();
			Randomizer.log("ws diag: initialize_network ok");
			Loaded = true;
			return true;
		}
		catch (Exception e)
		{
			Randomizer.LogError($"NativeWebSocket.Load: {e}");
			return false;
		}
	}

	// Application.dataPath is <install root>/oriDE_Data and never flakes;
	// Process.MainModule on this Mono is not so dependable.
	private static string ExeDir()
	{
		try
		{
			string dataPath = UnityEngine.Application.dataPath;
			if (!string.IsNullOrEmpty(dataPath))
				return Path.GetDirectoryName(dataPath);
		}
		catch (Exception e)
		{
			Randomizer.log($"ws diag: Application.dataPath unavailable ({e.GetType().Name}); using cwd");
		}
		// the game's cwd is its install root (randomizer.log lives there)
		return Environment.CurrentDirectory;
	}

	// Writes the resource to disk if missing or stale. A locked stale file
	// (second game instance) is used as-is — versions only drift across
	// dll updates, and both instances then hold the same Assembly-CSharp.
	private static string Extract(string resource, string target)
	{
		byte[] bytes = RandomizerResources.ReadResource(resource);
		if (bytes == null)
		{
			string have = string.Join(", ", typeof(NativeWebSocket).Assembly.GetManifestResourceNames());
			Randomizer.LogError($"ws diag: embedded resource '{resource}' not found; assembly has: [{have}]");
			return null;
		}
		try
		{
			if (!File.Exists(target) || new FileInfo(target).Length != bytes.Length)
			{
				File.WriteAllBytes(target, bytes);
				Randomizer.log($"ws diag: wrote {resource} ({bytes.Length} bytes) to {target}");
			}
			else
				Randomizer.log($"ws diag: {target} already current ({bytes.Length} bytes)");
		}
		catch (IOException e)
		{
			Randomizer.log($"ws diag: can't write {target} ({e.Message}); {(File.Exists(target) ? "using existing file" : "giving up")}");
			if (!File.Exists(target))
				return null;
		}
		return target;
	}

	[DllImport("NativeWebSocket.dll", EntryPoint = "initialize_network", CallingConvention = CallingConvention.Cdecl)]
	private static extern void InitializeNetwork();

	[DllImport("NativeWebSocket.dll", EntryPoint = "finalize_network", CallingConvention = CallingConvention.Cdecl)]
	public static extern void FinalizeNetwork();

	[DllImport("NativeWebSocket.dll", EntryPoint = "set_url", CallingConvention = CallingConvention.Cdecl)]
	public static extern void SetUrl([MarshalAs(UnmanagedType.LPStr)] string url);

	[DllImport("NativeWebSocket.dll", EntryPoint = "set_ca_file", CallingConvention = CallingConvention.Cdecl)]
	public static extern void SetCaFile([MarshalAs(UnmanagedType.LPStr)] string path);

	[DllImport("NativeWebSocket.dll", EntryPoint = "set_ping_interval", CallingConvention = CallingConvention.Cdecl)]
	public static extern void SetPingInterval(int seconds);

	[DllImport("NativeWebSocket.dll", EntryPoint = "set_auto_reconnect", CallingConvention = CallingConvention.Cdecl)]
	public static extern void SetAutoReconnect([MarshalAs(UnmanagedType.I1)] bool enabled);

	[DllImport("NativeWebSocket.dll", EntryPoint = "start", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Start();

	[DllImport("NativeWebSocket.dll", EntryPoint = "stop", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Stop();

	[DllImport("NativeWebSocket.dll", EntryPoint = "send_text", CallingConvention = CallingConvention.Cdecl)]
	public static extern void SendText([MarshalAs(UnmanagedType.LPStr)] string data);

	[DllImport("NativeWebSocket.dll", EntryPoint = "send_binary", CallingConvention = CallingConvention.Cdecl)]
	private static extern void SendBinaryRaw(byte[] data, int length);

	public static void SendBinary(byte[] data)
	{
		SendBinaryRaw(data, data.Length);
	}

	[DllImport("NativeWebSocket.dll", EntryPoint = "get_state", CallingConvention = CallingConvention.Cdecl)]
	public static extern SocketState GetState();

	[DllImport("NativeWebSocket.dll", EntryPoint = "get_open_count", CallingConvention = CallingConvention.Cdecl)]
	public static extern int GetOpenCount();

	[DllImport("NativeWebSocket.dll", EntryPoint = "get_error_count", CallingConvention = CallingConvention.Cdecl)]
	public static extern int GetErrorCount();

	[DllImport("NativeWebSocket.dll", EntryPoint = "get_close_count", CallingConvention = CallingConvention.Cdecl)]
	public static extern int GetCloseCount();

	[DllImport("NativeWebSocket.dll", EntryPoint = "get_last_error", CallingConvention = CallingConvention.Cdecl)]
	private static extern int GetLastErrorRaw(byte[] buf, int buflen);

	public static string GetLastError()
	{
		byte[] buf = new byte[512];
		int n = GetLastErrorRaw(buf, buf.Length);
		return System.Text.Encoding.UTF8.GetString(buf, 0, Math.Min(n, buf.Length - 1));
	}

	[DllImport("NativeWebSocket.dll", EntryPoint = "has_pending_message", CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.I1)]
	public static extern bool HasPendingMessage();

	[DllImport("NativeWebSocket.dll", EntryPoint = "get_pending_message", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr GetPendingMessageRaw(out int length);

	[DllImport("NativeWebSocket.dll", EntryPoint = "pop_pending_message", CallingConvention = CallingConvention.Cdecl)]
	private static extern void PopPendingMessage();

	// Returns null when the queue is empty.
	public static string GetPendingMessage()
	{
		int length;
		IntPtr ptr = GetPendingMessageRaw(out length);
		if (ptr == IntPtr.Zero)
			return null;
		byte[] bytes = new byte[length];
		Marshal.Copy(ptr, bytes, 0, length);
		PopPendingMessage();
		return System.Text.Encoding.UTF8.GetString(bytes);
	}
}
