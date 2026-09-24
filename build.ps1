# Builds Assembly-CSharp.dll from this repo's sources via the dnSpy fork.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1 [-DnSpy <path>] [-TimeoutSeconds 180] [-Visible]
#
# A new dll timestamp is the completion signal once the file settles or dnSpy exits 0:
# dnSpy has wedged on a modal dialog rather than closing, in both directions, across versions.

param(
    [string]$DnSpy = $(if ($env:DNSPY) { $env:DNSPY } else { "E:\dnspy-fork\dnSpy.exe" }),
    [int]$TimeoutSeconds = 180,
    [switch]$Visible
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repo

if (-not (Test-Path $DnSpy)) {
    Write-Host "ERROR: dnSpy not found at $DnSpy" -ForegroundColor Red
    Write-Host "Get it from https://github.com/AsmPrgmC3/dnSpy/releases/latest,"
    Write-Host "then pass -DnSpy <path> or set the DNSPY environment variable."
    exit 2
}

$source = Join-Path $repo "Managed\Assembly-CSharp.dll"
if (-not (Test-Path $source)) {
    Write-Host "ERROR: Managed\Assembly-CSharp.dll is missing." -ForegroundColor Red
    Write-Host "Copy the Managed folder from a clean game install (Ori DE\oriDE_Data\Managed) here."
    exit 2
}

# a vanilla Assembly-CSharp is ~2 MB; building on top of an already-modded one
# silently duplicates the embedded resources
$sourceKb = [int]((Get-Item $source).Length / 1KB)
if ($sourceKb -gt 2600) {
    Write-Host "ERROR: Managed\Assembly-CSharp.dll is $sourceKb KB, which looks modded rather" -ForegroundColor Red
    Write-Host "than vanilla. Restore it with Steam's 'Verify integrity of game files'."
    exit 2
}

$out = Join-Path $repo "Assembly-CSharp.dll"
$before = if (Test-Path $out) { (Get-Item $out).LastWriteTime.Ticks } else { 0 }

$log = Join-Path $env:TEMP "dnspy-build-out.log"
Remove-Item $log -ErrorAction SilentlyContinue

# dnSpy runs on a desktop nobody looks at, so its windows never pop up or take focus
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public sealed class DeskProcess {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO {
        public int cb; public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateDesktop(string name, IntPtr dev, IntPtr mode, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags,
        IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] static extern bool SetHandleInformation(IntPtr h, int mask, int flags);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr h, out int code);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    IntPtr process, desktop;

    public DeskProcess(string exe, string args, string cwd, string logPath, bool hidden) {
        string name = null;
        if (hidden) {
            name = "OriBuild";
            desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000u, IntPtr.Zero);
            if (desktop == IntPtr.Zero) name = null;
        }
        using (var log = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
            IntPtr h = log.SafeFileHandle.DangerousGetHandle();
            SetHandleInformation(h, 1, 1);
            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.lpDesktop = name;
            si.dwFlags = 0x100;
            si.hStdOutput = h;
            si.hStdError = h;
            PROCESS_INFORMATION pi;
            if (!CreateProcess(exe, "\"" + exe + "\" " + args, IntPtr.Zero, IntPtr.Zero, true, 0, IntPtr.Zero, cwd, ref si, out pi))
                throw new Exception("CreateProcess failed: " + Marshal.GetLastWin32Error());
            CloseHandle(pi.hThread);
            process = pi.hProcess;
        }
    }

    public bool OnHiddenDesktop { get { return desktop != IntPtr.Zero; } }
    public bool HasExited { get { return WaitForSingleObject(process, 0) == 0; } }
    public int ExitCode { get { int c; GetExitCodeProcess(process, out c); return c; } }
    public void Kill() { TerminateProcess(process, 1); WaitForSingleObject(process, 5000); }
    public void Close() {
        CloseHandle(process);
        if (desktop != IntPtr.Zero) CloseDesktop(desktop);
    }
}
'@

Write-Host "Building with $DnSpy"
$proc = New-Object DeskProcess($DnSpy, "--modfile:dnspy-modfile.json --runModfile --closeAfterModfile:success,failure",
    $repo, $log, (-not $Visible))
if (-not $Visible -and -not $proc.OnHiddenDesktop) { Write-Host "(no hidden desktop; dnSpy will show)" -ForegroundColor Yellow }

# dnSpy stamps the dll on create, not on finish, so a bare timestamp check catches it half-written.
function Test-Released($path) {
    try {
        $fs = [IO.File]::Open($path, "Open", "Write", "None")
        $fs.Close()
        return $true
    } catch { return $false }
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$rewritten = $false
$lastSize = -1
while ((Get-Date) -lt $deadline) {
    if ($proc.HasExited) { break }
    if ((Test-Path $out) -and (Get-Item $out).LastWriteTime.Ticks -ne $before) {
        $size = (Get-Item $out).Length
        if ($size -gt 0 -and $size -eq $lastSize -and (Test-Released $out)) {
            $rewritten = $true
            break
        }
        $lastSize = $size
    }
    Start-Sleep -Milliseconds 400
}

$killed = $false
if (-not $proc.HasExited) {
    $proc.Kill()
    $killed = $true
    if (-not $rewritten) { Write-Host "dnSpy did not finish within ${TimeoutSeconds}s; killed." -ForegroundColor Yellow }
}
Start-Sleep -Milliseconds 250

$exitCode = $proc.ExitCode
$proc.Close()

$output = @()
if (Test-Path $log) { $output = @(Get-Content $log -ErrorAction SilentlyContinue) }
$diagnostics = $output | Where-Object { $_ -match "^\[(Error|Warning)\]" -or $_ -match "Exception|Invalid --" }
$errors = $diagnostics | Where-Object { $_ -match "^\[Error\]" -or $_ -match "Exception|Invalid --" }

if ($errors) {
    Write-Host ""
    $errors | Select-Object -First 25 | ForEach-Object { Write-Host "  $_" }
}

$after = if (Test-Path $out) { (Get-Item $out).LastWriteTime.Ticks } else { 0 }
if ($after -eq $before) {
    Write-Host ""
    Write-Host "BUILD FAILED: Assembly-CSharp.dll was not rewritten." -ForegroundColor Red
    if (-not $errors) { Write-Host "  No diagnostics captured; try running dnSpy by hand with the same arguments." }
    exit 1
}

$complete = (Test-Path $out) -and ($rewritten -or
    (-not $killed -and $exitCode -eq 0 -and (Get-Item $out).Length -gt 0 -and (Test-Released $out)))
if (-not $complete) {
    $why = if ($killed) { "dnSpy was killed" } else { "dnSpy exited with code $exitCode" }
    Write-Host ""
    Write-Host "BUILD FAILED: Assembly-CSharp.dll was rewritten but may be half-written ($why)." -ForegroundColor Red
    Write-Host "  Rebuild before launching the game."
    exit 1
}

$item = Get-Item $out
Write-Host ("Built Assembly-CSharp.dll - {0} bytes - {1}" -f $item.Length, $item.LastWriteTime)
exit 0
