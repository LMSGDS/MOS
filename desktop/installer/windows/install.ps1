# MOS Dock silent install for the current Windows user (no Admin).
# Usage: powershell -ExecutionPolicy Bypass -File install.ps1 -PayloadDir <folder with MosDock.exe>

param(
    [string]$PayloadDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"
$dest = Join-Path $env:LOCALAPPDATA "MOS\MosDock"
$exeSrc = Join-Path $PayloadDir "MosDock.exe"
if (-not (Test-Path $exeSrc)) {
    throw "Không thấy MosDock.exe trong $PayloadDir"
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item -Force $exeSrc (Join-Path $dest "MosDock.exe")

$exe = Join-Path $dest "MosDock.exe"
$classes = "HKCU:\Software\Classes\mosdock"
New-Item -Force -Path $classes | Out-Null
Set-ItemProperty -Path $classes -Name "(default)" -Value "URL:MOS Dock"
New-ItemProperty -Path $classes -Name "URL Protocol" -Value "" -PropertyType String -Force | Out-Null
New-Item -Force -Path "$classes\shell\open\command" | Out-Null
Set-ItemProperty -Path "$classes\shell\open\command" -Name "(default)" -Value "`"$exe`" `"%1`""

$startup = Join-Path ([Environment]::GetFolderPath("Startup")) "MOS Dock.lnk"
$w = New-Object -ComObject WScript.Shell
$s = $w.CreateShortcut($startup)
$s.TargetPath = $exe
$s.WorkingDirectory = $dest
$s.Save()

$startMenu = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\MOS GDS"
New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
$sm = $w.CreateShortcut((Join-Path $startMenu "MOS Dock.lnk"))
$sm.TargetPath = $exe
$sm.Save()

Start-Process $exe
Write-Host "Đã cài MOS Dock vào $dest và đăng ký protocol mosdock:"
