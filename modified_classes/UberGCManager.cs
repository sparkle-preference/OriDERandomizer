using System;
using System.Runtime.InteropServices;
using Core;
using Game;
using UnityEngine;

// The unused-asset sweep freezes the game for a moment and the next loads re-read what it freed, so
// it runs behind a fade or the teleport bloom once the process has grown, in the open only at a ceiling.
public static class UberGCManager {
    private const float MinInterval = 10f;
    private const float GrowthMb = 256f;
    private const float CeilingMb = 2560f;

    private static float s_lastUnload;
    private static float s_lastCheck;
    private static float s_sweptAtMb;

    public static int Swept;
    public static int Skipped;

    private static float TimeSinceUnload {
        get { return Time.realtimeSinceStartup - s_lastUnload; }
    }

    public static void OnGameStart() {
        var array = new object[128];
        for (var i = 0; i < 128; i++) {
            array[i] = new byte[1024];
        }

        Events.Scheduler.OnGameFixedUpdateLate.Add(Update);
    }

    public static void CollectProactiveFull() {
        Scenes.Manager.DestroyManager.DestroyAll();
        CollectResourcesIfNeeded();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
    }

    public static void CollectResourcesIfNeeded() {
        if (TimeSinceUnload <= MinInterval) {
            return;
        }

        if (RandomizerPerf.On) {
            var mb = PrivateMb();
            var grown = mb >= s_sweptAtMb + GrowthMb || mb >= CeilingMb;
            if (ScreenCovered() ? !grown : mb < CeilingMb) {
                Skipped++;
                return;
            }
        }

        var op = Resources.UnloadUnusedAssets();
        op.priority = 0;
        s_lastUnload = Time.realtimeSinceStartup;
        s_sweptAtMb = PrivateMb();
        Swept++;
    }

    // a fade to black, or a teleport at full bloom
    private static bool ScreenCovered() {
        var fader = UI.Fader;
        return (fader != null && fader.IsFadingInOrStay()) || TeleporterController.IsBlooming;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters {
        public uint cb;
        public uint PageFaultCount;
        public UIntPtr PeakWorkingSetSize;
        public UIntPtr WorkingSetSize;
        public UIntPtr QuotaPeakPagedPoolUsage;
        public UIntPtr QuotaPagedPoolUsage;
        public UIntPtr QuotaPeakNonPagedPoolUsage;
        public UIntPtr QuotaNonPagedPoolUsage;
        public UIntPtr PagefileUsage;
        public UIntPtr PeakPagefileUsage;
        public UIntPtr PrivateUsage;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetProcessMemoryInfo(IntPtr process, out ProcessMemoryCounters counters, uint size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    // private bytes of this 32-bit process; on failure, large, so the sweep still happens
    public static float PrivateMb() {
        try {
            ProcessMemoryCounters c;
            if (GetProcessMemoryInfo(GetCurrentProcess(), out c, (uint)Marshal.SizeOf(typeof(ProcessMemoryCounters)))) {
                return (float)(c.PrivateUsage.ToUInt64() / 1048576.0);
            }
        } catch (Exception) {
        }

        return float.MaxValue;
    }

    private static void CollectResourcesIfOutOfMem() {
    }

    private static void Update() {
        if (Time.realtimeSinceStartup - s_lastCheck > 2.5f) {
            s_lastCheck = Time.realtimeSinceStartup;
            CollectResourcesIfOutOfMem();
        }
    }
}
