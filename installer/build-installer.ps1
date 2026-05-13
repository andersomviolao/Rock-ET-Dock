param(
    [string]$Version = "0.2.2"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $repoRoot "artifacts\publish\Rock-ET-Dock-$Version-win-x64"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$scriptPath = Join-Path $PSScriptRoot "RockETDock.iss"

$iscc = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    $isccPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($isccPath)) {
        throw "ISCC.exe nao encontrado. Instale Inno Setup 6 antes de gerar o instalador."
    }
} else {
    $isccPath = $iscc.Source
}

Set-Location $repoRoot

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

dotnet publish "src\Dock.App\Dock.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir

Copy-Item "LICENSE", "README.md", "CHANGELOG.md", "documentation.md" -Destination $publishDir -Force

New-Item -ItemType Directory -Force $installerDir | Out-Null
& $isccPath "/DAppVersion=$Version" $scriptPath

Get-Item (Join-Path $installerDir "Rock-ET-Dock-Setup-$Version-win-x64.exe")
