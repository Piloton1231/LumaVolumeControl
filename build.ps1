param([string]$Output = "$PSScriptRoot\publish")
$ErrorActionPreference = "Stop"
dotnet publish "$PSScriptRoot\src\LumaVolumeControl\LumaVolumeControl.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$setup = Join-Path (Split-Path $Output -Parent) "LumaVolumeControl-1.5.3-Setup.exe"
Copy-Item -LiteralPath (Join-Path $Output "LumaVolumeControl.exe") -Destination $setup -Force
Write-Host "Installer: $setup"
