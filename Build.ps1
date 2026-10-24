# Build from the root of your existing 7D2D-UnlockOpenDoors Git repository.
# Uses YOUR installed 7 Days to Die game assemblies, not a bundled DLL.
[CmdletBinding()]
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die',
    [string]$RepoRoot = $PSScriptRoot,
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $RepoRoot 'UnlockOpenDoors.sln'
if (-not (Test-Path -LiteralPath $solution -PathType Leaf)) {
    throw "Can't find UnlockOpenDoors.sln at '$RepoRoot'. Extract this kit over the root of your existing GitHub checkout (not into a subfolder), or pass -RepoRoot."
}

$gameAssembly = Join-Path $GameDir '7DaysToDie_Data\Managed\Assembly-CSharp.dll'
$harmonyDll = Join-Path $GameDir 'Mods\0_TFP_Harmony\0Harmony.dll'
if (-not (Test-Path -LiteralPath $gameAssembly -PathType Leaf)) {
    throw "Cannot find game assembly: $gameAssembly. Specify -GameDir with your Steam library path."
}
if (-not (Test-Path -LiteralPath $harmonyDll -PathType Leaf)) {
    throw "Cannot find Harmony: $harmonyDll"
}

$msbuild = $null
$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
    $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $msbuild) {
    $found = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($found) { $msbuild = $found.Source }
}

Write-Host "Game assemblies: $GameDir"
Write-Host "Project: $solution"
if ($msbuild) {
    Write-Host "MSBuild: $msbuild"
    & $msbuild $solution '/t:Build' '/p:Configuration=Release' "/p:GameDir=$GameDir" '/v:minimal' '/nologo'
} elseif (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Write-Host 'Using dotnet msbuild (requires the .NET Framework 4.8 targeting pack).'
    & dotnet msbuild $solution '/t:Build' '/p:Configuration=Release' "/p:GameDir=$GameDir" '/v:minimal' '/nologo'
} else {
    throw 'Neither Visual Studio MSBuild nor dotnet was found. Install Visual Studio Build Tools with .NET desktop build tools.'
}
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

$dllCandidates = @(
    (Join-Path $RepoRoot 'Releases\UnlockOpenDoors\UnlockOpenDoors.dll'),
    (Join-Path $RepoRoot 'UnlockOpenDoors\bin\Release\UnlockOpenDoors.dll')
)
$dll = $dllCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $dll) { throw 'Build reported success but UnlockOpenDoors.dll could not be located.' }
Write-Host "Built DLL: $dll" -ForegroundColor Green

if ($Install) {
    $locations = @(
        (Join-Path $env:APPDATA '7DaysToDie\Mods\UnlockOpenDoors'),
        (Join-Path $GameDir 'Mods\UnlockOpenDoors')
    )
    $existing = @($locations | Where-Object { Test-Path -LiteralPath (Join-Path $_ 'ModInfo.xml') -PathType Leaf })
    if ($existing.Count -eq 0) {
        throw "No EXISTING UnlockOpenDoors installation with ModInfo.xml was found. Install the repository's Releases\UnlockOpenDoors mod folder first."
    }
    foreach ($dest in $existing) {
        Copy-Item -LiteralPath $dll -Destination (Join-Path $dest 'UnlockOpenDoors.dll') -Force
        Write-Host "Installed: $dest" -ForegroundColor Green
    }
}
Write-Host 'Now launch V3.4 with EAC disabled and search output_log_client for [UnlockOpenDoors].'
