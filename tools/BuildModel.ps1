<#
.SYNOPSIS
    Builds a Paniq model from its script with Blender, without opening
    Blender, and puts the result where Unity and the owner can see it.

.DESCRIPTION
    A model is a short script under tools\models\models\ (see
    docs\model-pipeline.md). This runs Blender in the background on that
    script: it builds the mesh, checks it against the model rules, exports
    an FBX file into Assets\Paniq\Content\Models\, renders a preview picture
    into docs\models\previews\ and writes a small report beside it. A model
    whose geometry has not changed since the last build is left alone, so
    rebuilding everything does not touch every file.

    Blender 5.2 LTS is found under Program Files, or through BLENDER_PATH.
    Nothing here needs the Unity editor; the file lands in the project and
    the editor picks it up next time it looks.

.PARAMETER Name
    One or more models to build, by script name without .py.

.PARAMETER All
    Build every script under tools\models\models\.

.PARAMETER Example
    Build a test fixture from tools\models\examples\ instead; its FBX goes
    to Assets\Paniq\Tests\Fixtures\Models\. CalibrationBox is the one that
    ModelsEditModeTests checks.

.PARAMETER KeepBlend
    Also save a .blend file under Temp\PaniqModels\<Name>\ to open and
    look round in Blender. It is a copy for looking, not the source: the
    script is.

.PARAMETER Force
    Draw the preview picture again even when the model has not changed,
    for example after a change to how pictures are lit. The FBX is still
    left alone unless the geometry changed.

.EXAMPLE
    .\tools\BuildModel.ps1 -Name VendingMachine
    .\tools\BuildModel.ps1 -All
    .\tools\BuildModel.ps1 -Example CalibrationBox
#>
[CmdletBinding()]
param(
    [string[]] $Name,
    [switch] $All,
    [string[]] $Example,
    [switch] $KeepBlend,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$scripts = Join-Path $repository 'tools\models'
$scratch = Join-Path $repository 'Temp\PaniqModels'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null

function Find-Blender {
    $candidates = @()
    if ($env:BLENDER_PATH) {
        $candidates += $env:BLENDER_PATH
        $candidates += (Join-Path $env:BLENDER_PATH 'blender.exe')
    }
    $installs = Get-ChildItem "$env:ProgramFiles\Blender Foundation" -Directory -Filter 'Blender 5.*' -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending
    foreach ($install in $installs) { $candidates += (Join-Path $install.FullName 'blender.exe') }
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate -PathType Leaf)) { return $candidate }
    }
    throw 'Cannot find Blender 5. Install Blender 5.2 LTS from blender.org, or set BLENDER_PATH to its blender.exe.'
}

$blender = Find-Blender

$jobs = @()
foreach ($n in $Name) { $jobs += @{ Name = $n; Fixture = $false } }
foreach ($e in $Example) { $jobs += @{ Name = $e; Fixture = $true } }
if ($All) {
    Get-ChildItem (Join-Path $scripts 'models') -Filter '*.py' | ForEach-Object {
        $jobs += @{ Name = $_.BaseName; Fixture = $false }
    }
}
if ($jobs.Count -eq 0) { throw 'Nothing to build. Give -Name <model>, -Example <name> or -All.' }

foreach ($job in $jobs) {
    $arguments = @(
        '--background', '--factory-startup', '--python-exit-code', '1',
        '--python', (Join-Path $scripts 'build.py'), '--',
        '--model', $job.Name, '--repo', $repository, '--scratch', $scratch
    )
    if ($job.Fixture) { $arguments += '--fixture' }
    if ($KeepBlend) { $arguments += '--keep-blend' }
    if ($Force) { $arguments += '--force' }

    Write-Host "Building $($job.Name) with Blender..." -ForegroundColor Cyan
    $output = & $blender @arguments | ForEach-Object { "$_" }
    $exit = $LASTEXITCODE
    $output | Where-Object { $_ -like 'PANIQ *' } | ForEach-Object { Write-Host $_.Substring(6) -ForegroundColor Green }
    if ($exit -ne 0) {
        $output | Select-Object -Last 60 | Out-Host
        throw "$($job.Name) did not build (Blender exit code $exit). The lines above say why."
    }
}
