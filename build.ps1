# Builds Assembly-CSharp.dll from this repo's sources via the dnSpy fork.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1 [-DnSpy <path>] [-TimeoutSeconds 180]
#
# dnSpy cannot be trusted to exit: some versions throw while closing themselves
# after a successful save and sit on a modal dialog forever. So the dll's
# timestamp is the signal, not the process, and dnSpy is killed either way.

param(
    [string]$DnSpy = $(if ($env:DNSPY) { $env:DNSPY } else { "E:\dnspy-fork\dnSpy.exe" }),
    [int]$TimeoutSeconds = 180
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
$err = Join-Path $env:TEMP "dnspy-build-err.log"
Remove-Item $log, $err -ErrorAction SilentlyContinue

Write-Host "Building with $DnSpy"
$proc = Start-Process -FilePath $DnSpy -PassThru -RedirectStandardOutput $log -RedirectStandardError $err `
    -ArgumentList "--modfile:dnspy-modfile.json", "--runModfile", "--closeAfterModfile:success,failure"

# The timestamp changes when dnSpy CREATES the file, not when it finishes
# writing it, so killing on that alone truncates the dll to zero bytes. Wait
# until the handle is released and the size has stopped moving.
function Test-Released($path) {
    try {
        $fs = [IO.File]::Open($path, "Open", "Write", "None")
        $fs.Close()
        return $true
    } catch { return $false }
}

# whichever comes first: it exits, or it finishes the dll and then wedges
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

if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    if (-not $rewritten) { Write-Host "dnSpy did not finish within ${TimeoutSeconds}s; killed." -ForegroundColor Yellow }
}
Start-Sleep -Milliseconds 250

$output = @()
foreach ($f in @($log, $err)) {
    if (Test-Path $f) { $output += (Get-Content $f -ErrorAction SilentlyContinue) }
}
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

$item = Get-Item $out
Write-Host ("Built Assembly-CSharp.dll - {0} bytes - {1}" -f $item.Length, $item.LastWriteTime)
exit 0
