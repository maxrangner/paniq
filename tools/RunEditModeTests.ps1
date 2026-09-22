<#
.SYNOPSIS
    Compiles and runs Paniq's edit-mode tests without opening Unity.

.DESCRIPTION
    Unity's batch-mode test runner cannot open this project while the editor
    has it open, which is the normal state while working. The simulation is
    plain C# -- it touches one Unity type, and only as a serialization marker
    -- so this script compiles it, the edit-mode tests, a few small Unity
    stand-ins and a reflection-driven runner into one assembly with Unity's
    bundled Roslyn compiler, then runs it on the installed .NET 8 runtime.

    It is a fast check, not a substitute for Unity's own EditMode and PlayMode
    runners. Three tests genuinely need the editor and are reported as skipped.

.PARAMETER Filter
    Only run tests whose name contains this text.

.PARAMETER FingerprintsOnly
    Only run the ten recorded replay fingerprints.

.PARAMETER Record
    Run the fingerprints and print them as [TestCase] lines ready to paste.
    Use only for a deliberate behaviour change.

.PARAMETER List
    List the tests that would run, and exit.

.PARAMETER Measure
    Also run the tests marked [Explicit], which report measurements rather
    than checking anything. Pair it with -Filter to run just one.

.EXAMPLE
    .\tools\RunEditModeTests.ps1
    .\tools\RunEditModeTests.ps1 -FingerprintsOnly
    .\tools\RunEditModeTests.ps1 -Filter Doors
#>
[CmdletBinding()]
param(
    [string] $Filter,
    [switch] $FingerprintsOnly,
    [switch] $Record,
    [switch] $List,
    [switch] $Measure
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$outputDirectory = Join-Path $repository 'Temp\PaniqTestHarness'
$assembly = Join-Path $outputDirectory 'Paniq.Tests.Headless.dll'

# ---------------------------------------------------------------- toolchain

$versionFile = Join-Path $repository 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $versionFile)) {
    throw "Not a Unity project: $versionFile is missing."
}

$editorVersion = ((Get-Content $versionFile | Select-String '^m_EditorVersion:') -split ':\s*')[1].Trim()

$editorData = $null
foreach ($root in @($env:UNITY_EDITOR_PATH, "$env:ProgramFiles\Unity\Hub\Editor\$editorVersion\Editor\Data")) {
    if ($root -and (Test-Path $root)) { $editorData = $root; break }
}

if (-not $editorData) {
    throw "Cannot find Unity $editorVersion. Set UNITY_EDITOR_PATH to its Editor\Data folder."
}

$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
if (-not (Test-Path $compiler)) { throw "Missing Roslyn compiler: $compiler" }

# Unity ships a .NET 6 runtime; prefer the machine's .NET 8, which the tests
# are compiled against. Fall back to Unity's own so a machine without the
# standalone runtime still works.
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($dotnet) { $dotnet.Source } else { Join-Path $editorData 'NetCoreRuntime\dotnet.exe' }
if (-not (Test-Path $dotnetExe)) { throw "Cannot find dotnet.exe." }

$runtimeRoot = Join-Path (Split-Path -Parent $dotnetExe) 'shared\Microsoft.NETCore.App'
$runtime = Get-ChildItem $runtimeRoot -Directory |
    Sort-Object { [version]($_.Name -replace '-.*$') } |
    Select-Object -Last 1
if (-not $runtime) { throw "No .NET runtime found under $runtimeRoot." }

$nunit = Get-ChildItem (Join-Path $repository 'Library\PackageCache') -Recurse -Filter 'nunit.framework.dll' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $nunit) {
    throw "Cannot find nunit.framework.dll under Library\PackageCache. Open the project in Unity once so packages resolve."
}

# ---------------------------------------------------------------- sources

# Excluded because they need the editor itself, not because they are
# inconvenient. The runner reports them as skipped; keep the two lists in step.
$excludedTestFiles = @(
    'BootstrapperEditModeTests.cs',          # loads Unity scenes through Paniq.App
    'SimulationContractEditModeTests.cs'     # reads Time.fixedDeltaTime (checked below instead)
)

# The physics engine only exists inside Unity: tools\Stubs holds a stand-in
# that refuses to run, so tests that need it are reported as skipped.
$excludedSimulationFiles = @(
    'PhysicsWorld.cs'
)

$sources = @()
$sources += Get-ChildItem (Join-Path $repository 'Assets\Paniq\Runtime\Simulation') -Filter '*.cs' |
    Where-Object { $excludedSimulationFiles -notcontains $_.Name } | ForEach-Object FullName
$sources += Join-Path $repository 'Assets\Paniq\Runtime\Gameplay\FireReactionScenario.cs'
$sources += Join-Path $repository 'Assets\Paniq\Runtime\Diagnostics\StressBuilding.cs'
$sources += Get-ChildItem (Join-Path $repository 'Assets\Paniq\Tests\EditMode') -Filter '*.cs' |
    Where-Object { $excludedTestFiles -notcontains $_.Name } | ForEach-Object FullName
$sources += Get-ChildItem (Join-Path $PSScriptRoot 'Stubs') -Filter '*.cs' | ForEach-Object FullName
$sources += Get-ChildItem (Join-Path $PSScriptRoot 'TestRunner') -Filter '*.cs' | ForEach-Object FullName

foreach ($source in $sources) {
    if (-not (Test-Path $source)) { throw "Missing source file: $source" }
}

# ---------------------------------------------------------------- compile

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$references = @()
$references += Get-ChildItem $runtime.FullName -Filter 'System*.dll' |
    Where-Object { $_.Name -notlike '*.Native.dll' } | ForEach-Object FullName
$references += Join-Path $runtime.FullName 'netstandard.dll'
# Unity ships NUnit built against the .NET Framework, so its attributes are
# typed against mscorlib. .NET 8's mscorlib facade forwards those to the real
# types and makes the same binary usable here.
$references += Join-Path $runtime.FullName 'mscorlib.dll'
$references += $nunit.FullName
$references = $references | Where-Object { Test-Path $_ } | Sort-Object -Unique

$responseFile = Join-Path $outputDirectory 'compile.rsp'
$response = @(
    '-nologo'
    '-target:exe'
    '-nostdlib+'
    '-langversion:9.0'
    '-optimize+'
    '-warnaserror-'
    '-nowarn:CS1591'
    "-out:`"$assembly`""
)
$response += $references | ForEach-Object { "-reference:`"$_`"" }
$response += $sources | ForEach-Object { "`"$_`"" }
Set-Content -Path $responseFile -Value $response -Encoding utf8

Write-Host "Compiling $($sources.Count) files with Unity $editorVersion's Roslyn..." -ForegroundColor Cyan
& $dotnetExe $compiler "@$responseFile"
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed."
}

# The runtime needs to be told which framework to load the assembly on.
$runtimeConfig = @{
    runtimeOptions = @{
        tfm       = 'net8.0'
        framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name }
    }
} | ConvertTo-Json -Depth 5
Set-Content -Path (Join-Path $outputDirectory 'Paniq.Tests.Headless.runtimeconfig.json') -Value $runtimeConfig -Encoding utf8
Copy-Item $nunit.FullName $outputDirectory -Force

# ---------------------------------------------------------------- run

$arguments = @("--repo=$repository")
if ($Filter)           { $arguments += "--filter=$Filter" }
if ($FingerprintsOnly) { $arguments += '--fingerprints-only' }
if ($Record)           { $arguments += '--record' }
if ($List)             { $arguments += '--list' }
if ($Measure)          { $arguments += '--include-explicit' }

& $dotnetExe $assembly @arguments
$testsExitCode = $LASTEXITCODE

# ---------------------------------------------------------------- settings check

# Stands in for SimulationContractEditModeTests, which cannot run here: the
# fixed timestep is replay-compatible configuration and must stay at 0.02 s.
if (-not $List) {
    # Unity 6 stores the fixed timestep as a rational -- a tick count over a
    # rate -- rather than a float, so read all three numbers and divide.
    $timeManager = Get-Content (Join-Path $repository 'ProjectSettings\TimeManager.asset') -Raw
    $rational = [regex]::Match(
        $timeManager,
        'Fixed Timestep:\s*\r?\n\s*m_Count:\s*(\d+)\s*\r?\n\s*m_Rate:\s*\r?\n\s*m_Denominator:\s*(\d+)\s*\r?\n\s*m_Numerator:\s*(\d+)')

    if (-not $rational.Success) {
        Write-Host "FAILED  Could not read Fixed Timestep from TimeManager.asset." -ForegroundColor Red
        $testsExitCode = 1
    } else {
        $step = ([double]$rational.Groups[1].Value * [double]$rational.Groups[2].Value) / [double]$rational.Groups[3].Value
        if ([math]::Abs($step - 0.02) -gt 0.000001) {
            Write-Host "FAILED  Fixed Timestep is $step s; the simulation contract requires 0.02." -ForegroundColor Red
            $testsExitCode = 1
        } else {
            Write-Host "Fixed Timestep is 0.02 s, as the simulation contract requires." -ForegroundColor DarkGray
        }
    }
}

exit $testsExitCode
