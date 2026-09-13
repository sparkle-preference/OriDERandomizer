# Type-checks randomizer/*.cs with Roslyn, without dnSpy.
#
#   powershell -ExecutionPolicy Bypass -File typecheck.ps1
#
# Errors in typecheck-baseline.txt are expected; anything outside it is real. -Rebaseline
# rewrites that file from the current errors, so read them before you run it.
# Roslyn skips every method body while any declaration fails to resolve, and only the built
# dll has all the types randomizer/ names, so that is the reference: modified_classes/ are
# seen as of the last build.ps1, and a member added there since fails here until the next.

param([switch]$Rebaseline)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Program Files\dotnet\sdk\7.0.306\Roslyn\bincore\csc.dll"
$baselineFile = Join-Path $repo "typecheck-baseline.txt"

if (-not (Test-Path $csc)) {
    Write-Host "Roslyn not found at $csc" -ForegroundColor Red
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

$sources = @(Get-ChildItem -Path (Join-Path $repo "randomizer") -Filter *.cs -Recurse |
    ForEach-Object { $_.FullName })
# internal to the game and named in randomizer/ signatures, so it has to be ours
$sources += Join-Path $repo "modified_classes\BashAttackGame.cs"

$cmdArgs = @($csc, "-nologo", "-t:library", "-langversion:latest",
             "-nowarn:CS0114,CS0108,CS0162,CS0649,CS0169,CS0436",
             "-out:$env:TEMP\randomizer-typecheck.dll") + $refs + $sources

Write-Host "Type-checking $($sources.Count) sources against $($refs.Count) assemblies..."
$raw = (& dotnet $cmdArgs 2>&1 | Out-String) -split "`r?`n" | Where-Object { $_ -match "error CS" }

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
