# Type-checks randomizer/, menu/ and bingo/ with the newest SDK's Roslyn, without dnSpy.
#
#   powershell -ExecutionPolicy Bypass -File typecheck.ps1
#
# Errors in typecheck-baseline.txt are expected; -Rebaseline rewrites it, so read them first.
# The built dll is the reference, so modified_classes/ are seen as of the last build.ps1.

param([switch]$Rebaseline)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$sdks = "C:\Program Files\dotnet\sdk"
$csc = Get-ChildItem $sdks -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName "Roslyn\bincore\csc.dll") } |
    Sort-Object { [version]($_.Name -replace "-.*$", "") } | Select-Object -Last 1 |
    ForEach-Object { Join-Path $_.FullName "Roslyn\bincore\csc.dll" }
$baselineFile = Join-Path $repo "typecheck-baseline.txt"

if (-not $csc) {
    Write-Host "Roslyn not found under $sdks" -ForegroundColor Red
    exit 2
}

$managed = Join-Path $repo "Managed"
if (-not (Test-Path (Join-Path $managed "UnityEngine.dll"))) {
    Write-Host "Managed\UnityEngine.dll is missing -- copy Managed\ from the game install." -ForegroundColor Red
    exit 2
}

$built = Join-Path $repo "Assembly-CSharp.dll"
if (-not (Test-Path $built)) {
    Write-Host "Assembly-CSharp.dll is missing -- run build.ps1 once." -ForegroundColor Red
    exit 2
}

# The engine from the install, the game from the last build; the sources' own copies of its
# randomizer types win (CS0436).
$refs = @(Get-ChildItem -Path $managed -Filter *.dll |
    Where-Object { $_.Name -ne "Assembly-CSharp.dll" -and $_.Name -notlike "*.rando.*" } |
    ForEach-Object { "-r:" + $_.FullName }) + @("-r:" + $built)

$sources = @("randomizer", "menu", "bingo" | ForEach-Object {
    Get-ChildItem -Path (Join-Path $repo $_) -Filter *.cs -Recurse | ForEach-Object { $_.FullName } })
# internal to the game and named in randomizer/ signatures, so it has to be ours
$sources += Join-Path $repo "modified_classes\BashAttackGame.cs"

$cmdArgs = @($csc, "-nologo", "-t:library", "-langversion:latest",
             "-nowarn:CS0114,CS0108,CS0162,CS0649,CS0169,CS0436",
             "-out:$env:TEMP\randomizer-typecheck.dll") + $refs + $sources

Write-Host "Type-checking $($sources.Count) sources against $($refs.Count) assemblies..."
$said = & dotnet $cmdArgs 2>&1 | Out-String
$code = $LASTEXITCODE
$raw = $said -split "`r?`n" | Where-Object { $_ -match "error CS" }
if ($code -ne 0 -and -not $raw) {
    Write-Host "csc failed (exit $code) without a compiler error:" -ForegroundColor Red
    Write-Host $said
    exit 2
}

# Baseline key is filename + code + message: directory, line and column shift with edits and
# invocation, and so does the line number of a source location quoted inside a message.
function Key($line) {
    $key = $line -replace "^.*?([^\\/]+\.cs)\(\d+,\d+\):", '$1:'
    return $key -replace "\[[^\]]*[\\/]([^\\/\]]+?)(\(\d+\))?\]", '[$1]'
}

if ($Rebaseline) {
    $keys = @($raw | ForEach-Object { Key $_ } | Sort-Object -Unique)
    $header = @(
        "# Errors typecheck.ps1 expects: artefacts of compiling outside the assembly (types the game keeps",
        "# internal, the built dll's copies of randomizer types colliding with these sources).",
        "# Keyed on filename + error code + message; line, column and directory are stripped."
    )
    Set-Content -Path $baselineFile -Encoding Ascii -Value ($header + $keys)
    Write-Host "Baseline rewritten: $($keys.Count) error(s). Read it." -ForegroundColor Yellow
    exit 0
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
