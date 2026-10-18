# Builds the release files into dist\:
#   TaskbarPet-<version>-setup.exe   installer (Inno Setup)
#   TaskbarPet-<version>-portable.exe  the app as a single exe, no install needed
#
# Requirements: .NET 10 SDK, Inno Setup 6 (winget install JRSoftware.InnoSetup).
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# 1. Self-contained single-file exe -> publish\TaskbarPet.exe
dotnet publish "$root\src\TaskbarPet" -p:PublishProfile=win-x64
if ($LASTEXITCODE) { throw "dotnet publish failed" }
$exe = "$root\publish\TaskbarPet.exe"
$version = (Get-Item $exe).VersionInfo.ProductVersion

# 2. Installer -> dist\TaskbarPet-<version>-setup.exe
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }
& $iscc /Q "$root\installer\TaskbarPet.iss"
if ($LASTEXITCODE) { throw "Inno Setup failed" }

# 3. Portable exe next to the installer
Copy-Item $exe "$root\dist\TaskbarPet-$version-portable.exe" -Force

Get-ChildItem "$root\dist" | Format-Table Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
