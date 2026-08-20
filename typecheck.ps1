# Type-checks randomizer/*.cs against the game's own assemblies.
#
# build.bat cannot report a compile error: dnSpy raises a GUI dialog that
# --closeAfterModfile never closes, so the build hangs instead of failing and
# the errors are only ever visible in that dialog. Run this first.
#
#   powershell -ExecutionPolicy Bypass -File typecheck.ps1
#
# Five errors are expected and listed in typecheck-baseline.txt: types that
# exist only in the modded assembly the modfile builds, which this compile has
# no reference to. Anything outside that baseline is a real error.

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Program Files\dotnet\sdk\7.0.306\Roslyn\bincore\csc.dll"
$baselineFile = Join-Path $repo "typecheck-baseline.txt"

if (-not (Test-Path $csc)) {
    Write-Host "Roslyn not found at $csc" -ForegroundColor Red
    exit 2
}

$managed = Join-Path $repo "Managed"
if (-not (Test-Path (Join-Path $managed "Assembly-CSharp.dll"))) {
    Write-Host "Managed\Assembly-CSharp.dll is missing -- copy Managed\ from the game install." -ForegroundColor Red
    exit 2
}

# Vanilla Assembly-CSharp supplies the game types these sources call into. An
# already-modded copy beside it must be skipped: it carries the randomizer
# classes too, and each one then collides with its own source.
$refs = Get-ChildItem -Path $managed -Filter *.dll |
    Where-Object { $_.Name -notlike "*.rando.*" } |
    ForEach-Object { "-r:" + $_.FullName }

$sources = Get-ChildItem -Path (Join-Path $repo "randomizer") -Filter *.cs -Recurse |
    ForEach-Object { $_.FullName }

$cmdArgs = @($csc, "-nologo", "-t:library", "-langversion:latest",
             "-nowarn:CS0114,CS0108,CS0162,CS0649,CS0169",
             "-out:$env:TEMP\randomizer-typecheck.dll") + $refs + $sources

Write-Host "Type-checking $($sources.Count) sources against $($refs.Count) assemblies..."
$raw = (& dotnet $cmdArgs 2>&1 | Out-String) -split "`r?`n" | Where-Object { $_ -match "error CS" }

# key on file + code + message, so an edit that shifts line numbers does not
# invalidate the baseline
function Key($line) {
    $stripped = $line -replace [regex]::Escape($repo + "\"), ""
    return $stripped -replace "\(\d+,\d+\)", ""
}

$baseline = @{}
if (Test-Path $baselineFile) {
    Get-Content $baselineFile | Where-Object { $_ -and -not $_.StartsWith("#") } | ForEach-Object { $baseline[$_] = $true }
}

$new = @()
foreach ($line in $raw) {
    if (-not $baseline.ContainsKey((Key $line))) { $new += $line }
}

if ($new.Count -eq 0) {
    Write-Host "No new errors ($($raw.Count) baselined). Safe to run build.bat." -ForegroundColor Green
    exit 0
}

Write-Host "$($new.Count) NEW error(s):" -ForegroundColor Red
$new | ForEach-Object { Write-Host "  $($_ -replace [regex]::Escape($repo + '\'), '')" }
exit 1
