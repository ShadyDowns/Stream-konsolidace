[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $workspaceRoot 'StreamKartoteka.sln'
$testProject = Join-Path $workspaceRoot 'StreamKartoteka.Tests\StreamKartoteka.Tests.csproj'
$appProject = Join-Path $workspaceRoot 'StreamKartoteka\StreamKartoteka.csproj'
$publishDirectory = Join-Path $workspaceRoot 'publish\win-x64'
$iconSource = Join-Path $workspaceRoot 'StreamKartoteka\Assets\ZombieLilcin-icon-source.png'
$iconOutput = Join-Path $workspaceRoot 'StreamKartoteka\Assets\ZombieLilcin.ico'
$iconBuilder = Join-Path $workspaceRoot 'tools\build-icon.ps1'
$executableName = 'ZombieLilčin konsolidátor odpovědí.exe'
$legacyExecutable = Join-Path $publishDirectory 'StreamKartoteka.exe'

& $iconBuilder -InputPath $iconSource -OutputPath $iconOutput
dotnet build $solutionPath --configuration Release
dotnet run --project $testProject --configuration Release --no-build
if (Test-Path -LiteralPath $legacyExecutable) {
    Remove-Item -LiteralPath $legacyExecutable
}
dotnet publish $appProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $publishDirectory

Write-Host ""
Write-Host "Hotovo: $publishDirectory\$executableName"
