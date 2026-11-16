// PracticeServer's loop on a loopback port with a stand-in main thread, and segments to point it at.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

static class ServerGlue {
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    static TcpListener listener;
    static Thread loop;
    static Thread ticker;
    static volatile bool ticking;
    static int seen;

    static object Field(string name) {
        return typeof(PracticeServer).GetField(name, Private).GetValue(null);
    }

    static void SetField(string name, object value) {
        typeof(PracticeServer).GetField(name, Private).SetValue(null, value);
    }

    public static int Start() {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        SetField("port", port);
        SetField("listener", listener);
        var run = typeof(PracticeServer).GetMethod("Loop", Private);
        loop = new Thread(() => run.Invoke(null, null)) { IsBackground = true };
        loop.Start();
        ticking = true;
        ticker = new Thread(Tick) { IsBackground = true };
        ticker.Start();
        return port;
    }

    // gate is held across Tick (it is reentrant), so every job queued is counted exactly once
    static void Tick() {
        var gate = Field("gate");
        var jobs = (ICollection)Field("jobs");
        while (ticking) {
            lock (gate) {
                seen += jobs.Count;
                PracticeServer.Tick();
            }

            Thread.Sleep(1);
        }
    }

    public static int Jobs() {
        lock (Field("gate")) {
            return seen + ((ICollection)Field("jobs")).Count;
        }
    }

    public static bool Stop() {
        ticking = false;
        SetField("listener", null);
        listener.Stop();
        return loop.Join(3000) & ticker.Join(3000);
    }

    public static void SetFolder(string dir) {
        RandomizerSettings.Practice.Folder = new RandomizerSettings.StringSetting("Practice Folder", dir, "", false);
    }

    // a root without boxes: a save's Reparse puts boxes in force, and that needs the engine
    public static string MakeSegment(string path) {
        var file = BfrpFile.Create(path, JsonValue.Parse("{\"version\":1,\"name\":\"Given\",\"end\":{\"count\":1}}"), new byte[16]);
        file.SetVariantSegment("v", JsonValue.Parse("{\"name\":\"V\"}"));
        file.SetVariantSegment("w", JsonValue.Parse("{\"name\":\"W\"}"));
        file.SetPlacementLines("", new List<string> { "-1560188|SK|0|Swamp" });
        file.Variant = "v";
        file.Save();
        return file.Path;
    }

    public static void Session(string path) {
        var file = BfrpFile.Load(path);
        file.Variant = "v";
        PracticeController.File = file;
    }

    public static string SessionPath() {
        return PracticeController.File == null ? null : PracticeController.File.Path;
    }

    public static void EndSession() {
        PracticeController.File = null;
    }

    public static string Rev() {
        var reply = JsonValue.NewObject();
        typeof(PracticeServer).GetMethod("Current", Private).Invoke(null, new object[] { PracticeController.File, reply });
        return reply["rev"].Str;
    }
}
