# MOS-KulKul — cài Windows, không tải file qua trình duyệt.
# Ưu tiên bản đầy đủ để chỉ hiện một cửa sổ Inno Setup (stub + full = cài hai lần).
#
# Đã mở PowerShell thì dán khối lệnh trên trang /cai-dat (không gõ thêm powershell -Command).
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Base = $env:MOS_BASE_URL
if ([string]::IsNullOrWhiteSpace($Base)) {
    $Base = "https://mos.gds.edu.vn"
}
$Base = $Base.TrimEnd("/")
$origin = [Uri]$Base
$allowed = ($origin.Scheme -eq "https" -and $origin.Host -eq "mos.gds.edu.vn") -or
    ($origin.Host -eq "127.0.0.1") -or
    ($origin.Host -eq "localhost")
if (-not $allowed) {
    throw "Chi cai MOS-KulKul tu https://mos.gds.edu.vn (dang: $Base)"
}

Write-Host "Dang tai MOS-KulKul tu $Base (khong qua Chrome/Edge)..."
$tmp = Join-Path $env:TEMP ("MOS-KulKul-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$blob = Join-Path $tmp "download.bin"

$downloaded = $false
foreach ($rel in @("/cai-dat/windows-full", "/cai-dat/windows-full.zip", "/cai-dat/windows.zip")) {
    try {
        Invoke-WebRequest -Uri ($Base + $rel) -OutFile $blob -UseBasicParsing
        if ((Get-Item $blob).Length -lt 5000000) {
            Write-Host "Bo qua $rel (bo cai nho se mo them mot cua so cai)."
            continue
        }
        Write-Host "Tai xong: $rel"
        $downloaded = $true
        break
    }
    catch {
        Write-Host "Chua tai duoc $($Base + $rel), thu link khac..."
    }
}
if (-not $downloaded) {
    throw "Khong tai duoc bo cai. Mo $Base/cai-dat hoac kiem tra mang."
}

Unblock-File -Path $blob -ErrorAction SilentlyContinue
$header = Get-Content -LiteralPath $blob -Encoding Byte -TotalCount 2
$isZip = ($header.Count -ge 2 -and $header[0] -eq 80 -and $header[1] -eq 75)
$setup = $null
if ($isZip) {
    $zip = Join-Path $tmp "MOS-KulKul-Setup-Windows.zip"
    Move-Item -Force $blob $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
    $setup = Get-ChildItem -Path $tmp -Filter "*.exe" -File | Select-Object -First 1
}
else {
    $setup = Get-Item $blob
    $renamed = Join-Path $tmp "MOS-KulKul-Setup.exe"
    Move-Item -Force $setup.FullName $renamed
    $setup = Get-Item $renamed
}
if (-not $setup) {
    throw "Khong thay file cai .exe."
}
Unblock-File -Path $setup.FullName -ErrorAction SilentlyContinue
Write-Host "Da tai. Neu Windows SmartScreen: Thong tin them -> Chay anyway."
Start-Process -FilePath $setup.FullName
Write-Host "Mo trinh cai MOS-KulKul."
