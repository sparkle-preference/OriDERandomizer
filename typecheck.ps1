# Type-checks randomizer/*.cs against the game's own assemblies, without dnSpy.
#
#   powershell -ExecutionPolicy Bypass -File typecheck.ps1
#
# Errors in typecheck-baseline.txt are expected; anything outside it is real.

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

# A modded Assembly-CSharp beside the vanilla one must be skipped: its randomizer types collide with these sources.
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

# Baseline key is filename + code + message; line, column and directory shift with edits and invocation.
function Key($line) {
    $leaf = $line -replace "^.*[\\/]", ""
    return $leaf -replace "\(\d+,\d+\)", ""
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
