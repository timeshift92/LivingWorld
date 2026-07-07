Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceModPath = Join-Path $repoRoot "mod"
$projectPath = Join-Path $repoRoot "src\LivingWorld.RimWorld\LivingWorld.RimWorld.csproj"
$buildOutputPath = Join-Path $repoRoot "src\LivingWorld.RimWorld\bin\Debug\net472"
$compiledDllPath = Join-Path $buildOutputPath "LivingWorld.RimWorld.dll"
$compiledCoreDllPath = Join-Path $buildOutputPath "LivingWorld.Core.dll"
$rimWorldModPath = "C:\Games\RimWorld\Mods\LivingWorld"
$assembliesPath = Join-Path $rimWorldModPath "1.6\Assemblies"

if (-not (Test-Path -LiteralPath $sourceModPath)) {
    throw "Source mod path does not exist: $sourceModPath"
}

dotnet build $projectPath

if (-not (Test-Path -LiteralPath $compiledDllPath)) {
    throw "Compiled RimWorld mod DLL does not exist: $compiledDllPath"
}

if (-not (Test-Path -LiteralPath $compiledCoreDllPath)) {
    throw "Compiled Core DLL does not exist: $compiledCoreDllPath"
}

if (Test-Path -LiteralPath $rimWorldModPath) {
    $resolvedTarget = (Resolve-Path -LiteralPath $rimWorldModPath).Path
    $expectedTarget = "C:\Games\RimWorld\Mods\LivingWorld"

    if ($resolvedTarget -ne $expectedTarget) {
        throw "Refusing to remove unexpected path: $resolvedTarget"
    }

    Remove-Item -LiteralPath $rimWorldModPath -Recurse -Force
}

New-Item -ItemType Directory -Path $rimWorldModPath | Out-Null
Copy-Item -Path (Join-Path $sourceModPath "*") -Destination $rimWorldModPath -Recurse -Force
New-Item -ItemType Directory -Path $assembliesPath -Force | Out-Null
Copy-Item -LiteralPath $compiledDllPath -Destination (Join-Path $assembliesPath "LivingWorld.RimWorld.dll") -Force
Copy-Item -LiteralPath $compiledCoreDllPath -Destination (Join-Path $assembliesPath "LivingWorld.Core.dll") -Force

Write-Host "Living World installed to $rimWorldModPath"
