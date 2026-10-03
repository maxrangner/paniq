<#
.SYNOPSIS
    Compiles Paniq's runtime, editor and test code against the real Unity
    libraries, without opening Unity, to catch compile errors in seconds.

.DESCRIPTION
    Uses the Roslyn compiler bundled with the installed editor and the same
    UnityEngine and UnityEditor reference assemblies Unity compiles against.
    It only compiles; nothing runs. Run the tests with
    tools\RunUnityTests.ps1, inside the open editor: every test that builds
    a run needs the editor's physics engine.

    The assemblies mirror the project's: Paniq.Runtime, then the edit-mode
    tests and the editor code, each referencing the one before.

.EXAMPLE
    .\tools\CompileAgainstUnity.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repository 'Temp\PaniqCompileCheck'
New-Item -ItemType Directory -Force -Path $output | Out-Null

$versionFile = Join-Path $repository 'ProjectSettings\ProjectVersion.txt'
$editorVersion = ((Get-Content $versionFile | Select-String '^m_EditorVersion:') -split ':\s*')[1].Trim()
$editorData = $null
foreach ($root in @($env:UNITY_EDITOR_PATH, "$env:ProgramFiles\Unity\Hub\Editor\$editorVersion\Editor\Data")) {
    if ($root -and (Test-Path $root)) { $editorData = $root; break }
}
if (-not $editorData) { throw "Cannot find Unity $editorVersion. Set UNITY_EDITOR_PATH to its Editor\Data folder." }

$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($dotnet) { $dotnet.Source } else { Join-Path $editorData 'NetCoreRuntime\dotnet.exe' }

$scriptAssemblies = Join-Path $repository 'Library\ScriptAssemblies'
$unityReferences = @()
$unityReferences += Join-Path $editorData 'NetStandard\ref\2.1.0\netstandard.dll'
# NUnit is built against the .NET Framework; this facade forwards its types.
$unityReferences += Join-Path $editorData 'NetStandard\compat\2.1.0\shims\netfx\mscorlib.dll'
$unityReferences += Get-ChildItem (Join-Path $editorData 'Managed\UnityEngine') -Filter '*.dll' | ForEach-Object FullName
$unityReferences += Join-Path $scriptAssemblies 'Unity.InputSystem.dll'

$nunit = Get-ChildItem (Join-Path $repository 'Library\PackageCache') -Recurse -Filter 'nunit.framework.dll' -ErrorAction SilentlyContinue |
    Select-Object -First 1
$testRunner = @(
    (Join-Path $scriptAssemblies 'UnityEngine.TestRunner.dll'),
    (Join-Path $scriptAssemblies 'UnityEditor.TestRunner.dll')
)

# The version define follows the installed editor (6000.3.x -> UNITY_6000_3_OR_NEWER)
# rather than being written down here, so an editor upgrade does not leave it stale.
$versionParts = $editorVersion -split '\.'
$playerDefines = "UNITY_$($versionParts[0])_$($versionParts[1])_OR_NEWER;UNITY_STANDALONE_WIN;ENABLE_INPUT_SYSTEM"
$defines = "UNITY_EDITOR;UNITY_INCLUDE_TESTS;UNITY_TESTS_FRAMEWORK;$playerDefines"

# Unity.InputSystem and the test runner only exist once the editor has
# opened the project and filled Library\. Without them the compile below
# would print hundreds of "type not found" errors that say nothing useful.
$missing = @($unityReferences + $testRunner | Where-Object { $_ -and -not (Test-Path $_) })
if (-not $nunit) { $missing += 'nunit.framework.dll (Library\PackageCache)' }
if ($missing) {
    throw "Open the project in the Unity editor once first, so it fills Library\. Missing: $($missing -join ', ')"
}

function Invoke-Compile([string] $name, [string[]] $sources, [string[]] $references, [string] $assemblyDefines = $defines) {
    $assembly = Join-Path $output "$name.dll"
    $response = @(
        '-nologo', '-target:library', '-nostdlib+', '-langversion:9.0', '-nowarn:CS1591,CS0618,CS0649',
        "-define:$assemblyDefines", "-out:`"$assembly`""
    )
    $response += $references | Where-Object { $_ } | ForEach-Object { "-reference:`"$_`"" }
    $response += $sources | ForEach-Object { "`"$_`"" }
    $rsp = Join-Path $output "$name.rsp"
    Set-Content -Path $rsp -Value $response -Encoding utf8
    Write-Host "Compiling $name ($($sources.Count) files)..." -ForegroundColor Cyan
    & $dotnetExe $compiler "@$rsp" | Where-Object { $_ -match 'error' } | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$name failed to compile." }
    return $assembly
}

$runtimeSources = Get-ChildItem (Join-Path $repository 'Assets\Paniq\Runtime') -Recurse -Filter '*.cs' | ForEach-Object FullName
$runtime = Invoke-Compile 'Paniq.Runtime' $runtimeSources $unityReferences

$testSources = Get-ChildItem (Join-Path $repository 'Assets\Paniq\Tests\EditMode') -Recurse -Filter '*.cs' | ForEach-Object FullName
$null = Invoke-Compile 'Paniq.Tests.EditMode' $testSources ($unityReferences + $runtime + $nunit.FullName + $testRunner)

$playSources = Get-ChildItem (Join-Path $repository 'Assets\Paniq\Tests\PlayMode') -Recurse -Filter '*.cs' | ForEach-Object FullName
if ($playSources) {
    $null = Invoke-Compile 'Paniq.Tests.PlayMode' $playSources ($unityReferences + $runtime + $nunit.FullName + $testRunner)
}

# Authoring has no assembly definition, so Unity puts it in the player's
# Assembly-CSharp: compile it the same way, without UNITY_EDITOR, so code
# that would break the player build breaks here too.
$authoringSources = Get-ChildItem (Join-Path $repository 'Assets\Paniq\Authoring') -Recurse -Filter '*.cs' | ForEach-Object FullName
$authoring = Invoke-Compile 'Paniq.Authoring' $authoringSources ($unityReferences + $runtime) $playerDefines

$bridgeFolder = Join-Path $repository 'Assets\Paniq\Editor\TestBridge'
$editorSources = Get-ChildItem (Join-Path $repository 'Assets\Paniq\Editor') -Recurse -Filter '*.cs' |
    Where-Object { -not $_.FullName.StartsWith($bridgeFolder) } | ForEach-Object FullName
$null = Invoke-Compile 'Paniq.EditorCode' $editorSources ($unityReferences + $runtime + $authoring)

$bridgeSources = Get-ChildItem $bridgeFolder -Recurse -Filter '*.cs' | ForEach-Object FullName
$null = Invoke-Compile 'Paniq.Editor.TestBridge' $bridgeSources ($unityReferences + $nunit.FullName + $testRunner)

Write-Host 'Everything compiles against Unity.' -ForegroundColor Green
