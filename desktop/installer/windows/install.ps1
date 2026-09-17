# MOS-KulKul silent install for the current Windows user (no Admin).
# Usage: powershell -ExecutionPolicy Bypass -File install.ps1 -PayloadDir <folder with MOS-KulKul.exe>

param(
    [string]$PayloadDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"
$dest = Join-Path $env:LOCALAPPDATA "MOS\KulKul"
$exeSrc = Join-Path $PayloadDir "MOS-KulKul.exe"
if (-not (Test-Path $exeSrc)) {
    $exeSrc = Join-Path $PayloadDir "MosDock.exe"
}
if (-not (Test-Path $exeSrc)) {
    throw "Không thấy MOS-KulKul.exe trong $PayloadDir"
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Get-ChildItem -Path $PayloadDir -File | Where-Object { $_.Name -ne "install.ps1" -and $_.Extension -ne ".pdb" } | ForEach-Object {
    Copy-Item -Force $_.FullName (Join-Path $dest $_.Name)
}
Get-ChildItem -Path $PayloadDir -Directory | ForEach-Object {
    Copy-Item -Force -Recurse $_.FullName (Join-Path $dest $_.Name)
}

$exe = Join-Path $dest "MOS-KulKul.exe"
if (-not (Test-Path $exe)) {
    $exe = Join-Path $dest "MosDock.exe"
}

foreach ($proto in @("mosdock", "mos-kulkul")) {
    $classes = "HKCU:\Software\Classes\$proto"
    New-Item -Force -Path $classes | Out-Null
    Set-ItemProperty -Path $classes -Name "(default)" -Value "URL:MOS-KulKul"
    New-ItemProperty -Path $classes -Name "URL Protocol" -Value "" -PropertyType String -Force | Out-Null
    New-Item -Force -Path "$classes\shell\open\command" | Out-Null
    Set-ItemProperty -Path "$classes\shell\open\command" -Name "(default)" -Value "`"$exe`" `"%1`""
}

$startup = Join-Path ([Environment]::GetFolderPath("Startup")) "MOS-KulKul.lnk"
$w = New-Object -ComObject WScript.Shell
$s = $w.CreateShortcut($startup)
$s.TargetPath = $exe
$s.WorkingDirectory = $dest
$s.Save()

$startMenu = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs\MOS-KulKul"
New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
$sm = $w.CreateShortcut((Join-Path $startMenu "MOS-KulKul.lnk"))
$sm.TargetPath = $exe
$sm.Save()

Start-Process $exe
Write-Host "Đã cài MOS-KulKul vào $dest"
