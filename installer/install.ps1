Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$destinationDir = Join-Path $env:LOCALAPPDATA "Programs\BnsLauncher"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Bn's Launcher.lnk"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$startMenuShortcut = Join-Path $startMenuDir "Bn's Launcher.lnk"
$exePath = Join-Path $destinationDir "Bn's Launcher.exe"

New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
New-Item -ItemType Directory -Force -Path $startMenuDir | Out-Null

Get-ChildItem -LiteralPath $sourceDir -File |
    Where-Object { $_.Name -notin @("install.cmd", "install.ps1") } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $destinationDir $_.Name) -Force
    }

$shell = New-Object -ComObject WScript.Shell

$desktopLink = $shell.CreateShortcut($desktopShortcut)
$desktopLink.TargetPath = $exePath
$desktopLink.WorkingDirectory = $destinationDir
$desktopLink.IconLocation = $exePath
$desktopLink.Save()

$startMenuLink = $shell.CreateShortcut($startMenuShortcut)
$startMenuLink.TargetPath = $exePath
$startMenuLink.WorkingDirectory = $destinationDir
$startMenuLink.IconLocation = $exePath
$startMenuLink.Save()

$uninstallPath = Join-Path $destinationDir "Uninstall Bn's Launcher.cmd"
$uninstallScript = @"
@echo off
setlocal
taskkill /IM "Bn's Launcher.exe" /F >nul 2>nul
rmdir /S /Q "%LOCALAPPDATA%\Programs\BnsLauncher"
del "%USERPROFILE%\Desktop\Bn's Launcher.lnk" >nul 2>nul
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Bn's Launcher.lnk" >nul 2>nul
echo Bn's Launcher removido.
pause
"@
Set-Content -LiteralPath $uninstallPath -Value $uninstallScript -Encoding ASCII

Start-Process -FilePath $exePath
