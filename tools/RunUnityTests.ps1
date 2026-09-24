<#
.SYNOPSIS
    Runs Paniq's tests inside the Unity editor that is already open.

.DESCRIPTION
    Unity's batch-mode runner cannot open the project while the editor has it
    open, and the physics tests need Unity itself, so this script hands the
    request to the editor instead: Assets/Paniq/Editor/TestBridge/TestBridge.cs
    watches Temp/PaniqTestBridge/request.txt, compiles any changed code, runs
    the tests with Unity's own runner and writes the results back.

    The editor must be open on this project; a playtest left running is
    stopped by the bridge before the tests start. The first
    time the bridge itself is added, click into the editor once so Unity
    compiles it; after that it picks up code changes by itself.

.PARAMETER Filter
    Only run tests whose full name contains this text.

.PARAMETER Category
    Only run tests in this NUnit category, for example UnityPhysics.

.PARAMETER PlayMode
    Run the play-mode tests instead of the edit-mode tests.

.PARAMETER All
    Run both halves, edit mode then play mode, and fail if either does. This
    is what to use before committing. Without it the script runs edit mode
    only, which is half the suite: a play-mode test once sat broken for a day
    because every check that day had been an edit-mode one.

.PARAMETER TimeoutSeconds
    How long to wait for the editor before giving up.

.PARAMETER Menu
    Run one of the editor's menu commands instead of tests, for example
    "Paniq/Profiling/Build Stress Profile Player".

.PARAMETER Reset
    Tell the bridge to forget a run that will never report back (Unity can
    drop a run when a dialog interrupts it), then stop. Run the tests again
    afterwards.

.EXAMPLE
    .\tools\RunUnityTests.ps1 -All
    .\tools\RunUnityTests.ps1
    .\tools\RunUnityTests.ps1 -Category UnityPhysics
    .\tools\RunUnityTests.ps1 -Filter ReplayFingerprint
    .\tools\RunUnityTests.ps1 -PlayMode
#>
[CmdletBinding()]
param(
    [string] $Filter,
    [string] $Category,
    [switch] $PlayMode,
    [switch] $All,
    [int] $TimeoutSeconds = 900,
    [switch] $ShowPassed,
    [switch] $Reset,
    [string] $Menu
)

$ErrorActionPreference = 'Stop'

if ($All) {
    if ($Menu)     { throw '-All runs the tests; it cannot be combined with -Menu.' }
    if ($PlayMode) { throw '-All already runs the play-mode tests.' }
    if ($Reset)    { throw '-All runs the tests; it cannot be combined with -Reset.' }

    $worst = 0
    foreach ($half in @($false, $true)) {
        Write-Host ''
        Write-Host "===== $(if ($half) { 'play' } else { 'edit' }) mode =====" -ForegroundColor Cyan
        $arguments = @{ TimeoutSeconds = $TimeoutSeconds }
        if ($Filter)     { $arguments['Filter'] = $Filter }
        if ($Category)   { $arguments['Category'] = $Category }
        if ($ShowPassed) { $arguments['ShowPassed'] = $true }
        if ($half)       { $arguments['PlayMode'] = $true }

        & $PSCommandPath @arguments
        if ($LASTEXITCODE -ne 0) { $worst = $LASTEXITCODE }
    }

    Write-Host ''
    if ($worst -eq 0) {
        Write-Host 'Both halves passed.' -ForegroundColor Green
    } else {
        Write-Host 'Something failed -- look above for which half.' -ForegroundColor Red
    }

    exit $worst
}

$repository = Split-Path -Parent $PSScriptRoot
$folder = Join-Path $repository 'Temp\PaniqTestBridge'
$request = Join-Path $folder 'request.txt'
$result = Join-Path $folder 'result.txt'
$status = Join-Path $folder 'status.txt'

if (-not (Test-Path (Join-Path $repository 'Temp\UnityLockfile'))) {
    throw 'The Unity editor does not have this project open. Open it: every test that builds a run needs the physics engine inside the editor.'
}

New-Item -ItemType Directory -Force -Path $folder | Out-Null

if ($Reset) {
    Set-Content -Path (Join-Path $folder 'reset.txt') -Value 'reset' -Encoding ascii
    Write-Host 'Asked the bridge to forget any run in progress.' -ForegroundColor Cyan
    exit 0
}

$id = [guid]::NewGuid().ToString('N')
$lines = @("id=$id", "mode=$(if ($Menu) { 'Menu' } elseif ($PlayMode) { 'PlayMode' } else { 'EditMode' })")
if ($Menu)     { $lines += "menu=$Menu" }
if ($Filter)   { $lines += "filter=$Filter" }
if ($Category) { $lines += "category=$Category" }

# Write to a side file and rename, so the editor never reads half a request.
$staging = "$request.tmp"
Set-Content -Path $staging -Value $lines -Encoding ascii
Move-Item -Force $staging $request

if ($Menu) {
    Write-Host "Asked the Unity editor to run the menu command $Menu..." -ForegroundColor Cyan
} else {
    Write-Host "Asked the Unity editor to run the $(if ($PlayMode) { 'play' } else { 'edit' })-mode tests..." -ForegroundColor Cyan
}

$started = Get-Date
$lastStatus = ''
while ($true) {
    if (Test-Path $result) {
        $text = Get-Content $result -Raw -ErrorAction SilentlyContinue
        if ($text -and $text -match "(?m)^done=$id\s*$") { break }
    }

    if (Test-Path $status) {
        $current = (Get-Content $status -Raw -ErrorAction SilentlyContinue)
        if ($current) { $current = $current.Trim() }
        if ($current -and $current -ne $lastStatus) {
            Write-Host "  editor: $current" -ForegroundColor DarkGray
            $lastStatus = $current
        }
    }

    if (((Get-Date) - $started).TotalSeconds -gt $TimeoutSeconds) {
        if (Test-Path $request) {
            throw "The editor never picked up the request. Click into the Unity editor once so it compiles the test bridge, then try again."
        }
        throw "Timed out after $TimeoutSeconds s waiting for the editor."
    }

    Start-Sleep -Milliseconds 500
}

$exitCode = 0
$passed = 0
foreach ($line in ($text -split "`n")) {
    $line = $line.TrimEnd("`r")
    if ($line -match '^menu=ran') { Write-Host 'The menu command ran.' -ForegroundColor Green; $passed++; continue }
    if ($line -match '^compile=failed') { Write-Host 'COMPILE FAILED' -ForegroundColor Red; $exitCode = 1; continue }
    if ($line -match '^error=(.*)$') { Write-Host "  $($Matches[1])" -ForegroundColor Red; $exitCode = 1; continue }
    if ($line -match '^PASSED\t') { $passed++; if ($ShowPassed) { Write-Host $line -ForegroundColor Green }; continue }
    if ($line -match '^FAILED\t') { Write-Host $line -ForegroundColor Red; $exitCode = 1; continue }
    if ($line -match '^(SKIPPED|INCONCLUSIVE)\t') { Write-Host $line -ForegroundColor Yellow; continue }
    if ($line -match '^  \| ') { if ($ShowPassed -or $exitCode -ne 0) { Write-Host $line -ForegroundColor DarkGray }; continue }
    if ($line -match '^(passed|failed|skipped|inconclusive)=(\d+)$') {
        Write-Host ("{0,-13}{1}" -f $Matches[1], $Matches[2])
        if ($Matches[1] -eq 'failed' -and [int]$Matches[2] -gt 0) { $exitCode = 1 }
    }
}

if ($passed -eq 0 -and $exitCode -eq 0) {
    Write-Host 'No tests ran. Check the filter or category.' -ForegroundColor Yellow
    $exitCode = 1
}

exit $exitCode
