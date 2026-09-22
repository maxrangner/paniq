<#
.SYNOPSIS
    Compiles Paniq's runtime, editor and test code against the real Unity
    libraries, without opening Unity, to catch compile errors in seconds.

.DESCRIPTION
    Uses the Roslyn compiler bundled with the installed editor and the same
    UnityEngine and UnityEditor reference assemblies Unity compiles against.
    It only compiles; nothing runs. Run the tests with
    tools\RunUnityTests.ps1 (inside the open editor) or, for the tests that
    do not need Unity, tools\RunEditModeTests.ps1.

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

$defines = 'UNITY_EDITOR;UNITY_6000_3_OR_NEWER;UNITY_INCLUDE_TESTS;UNITY_TESTS_FRAMEWORK;UNITY_STANDALONE_WIN;ENABLE_INPUT_SYSTEM'

function Invoke-Compile([string] $name, [string[]] $sources, [string[]] $references) {
    $assembly = Join-Path $output "$name.dll"
    $response = @(
        '-nologo', '-target:library', '-nostdlib+', '-langversion:9.0', '-nowarn:CS1591,CS0618,CS0649',
        "-define:$defines", "-out:`"$assembly`""
    )
    $response += $references | Where-Object { $_ -and (Test-Path $_) } | ForEach-Object { "-reference:`"$_`"" }
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

$editorSources = Get-ChildItem (Join-Path $repository 'Assets\Paniq\Editor') -Filter '*.cs' | ForEach-Object FullName
$editorSources += Get-ChildItem (Join-Path $repository 'Assets\Paniq\Authoring') -Recurse -Filter '*.cs' | ForEach-Object FullName
$null = Invoke-Compile 'Paniq.EditorCode' $editorSources ($unityReferences + $runtime)

$bridgeSources = Get-ChildItem (Join-Path $repository 'Assets\Paniq\Editor\TestBridge') -Filter '*.cs' | ForEach-Object FullName
$null = Invoke-Compile 'Paniq.Editor.TestBridge' $bridgeSources ($unityReferences + $nunit.FullName + $testRunner)

Write-Host 'Everything compiles against Unity.' -ForegroundColor Green
