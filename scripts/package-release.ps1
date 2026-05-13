param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$fanControlApi = Join-Path $root "lib\FanControl.Plugins.dll"
$project = Join-Path $root "src\FanControl.NPB5ITE\FanControl.NPB5ITE.csproj"
$output = Join-Path $root "src\FanControl.NPB5ITE\bin\Release\net10.0-windows"
$dist = Join-Path $root "dist\release"
$packageDir = Join-Path $dist "FanControl.NPB5ITE-$Version"
$zip = Join-Path $dist "FanControl.NPB5ITE-$Version.zip"

if (-not (Test-Path -LiteralPath $fanControlApi)) {
    throw "Cannot package release without lib\FanControl.Plugins.dll. Copy it from a compatible Fan Control .NET 10 install first."
}

dotnet build $project -c Release /p:UseFanControlApiStub=false

if (Test-Path -LiteralPath $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
Copy-Item -LiteralPath (Join-Path $output "FanControl.NPB5ITE.dll") -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $output "FanControl.NPB5ITE.pdb") -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination $packageDir -Force

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zip -Force

Write-Host "Wrote $zip"
