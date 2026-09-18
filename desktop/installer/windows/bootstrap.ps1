# MOS-KulKul — cài Windows, không tải .exe qua trình duyệt.
# Chrome/Edge quét virus khi bấm nút Tải; lệnh này tải bằng PowerShell từ máy chủ nhà trường.
#
# Mở PowerShell rồi dán:
#   powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://mos.gds.edu.vn/cai-dat/windows.ps1 | iex"
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
$zip = Join-Path $tmp "MOS-KulKul-Setup-Windows.zip"

$downloaded = $false
foreach ($rel in @("/cai-dat/windows.zip", "/cai-dat/windows-full.zip")) {
    try {
        Invoke-WebRequest -Uri ($Base + $rel) -OutFile $zip -UseBasicParsing
        if ((Get-Item $zip).Length -gt 1000) {
            $downloaded = $true
            break
        }
    }
    catch {
        Write-Host "Chua tai duoc $($Base + $rel), thu link khac..."
    }
}
if (-not $downloaded) {
    throw "Khong tai duoc bo cai. Mo $Base/cai-dat hoac kiem tra mang."
}

Unblock-File -Path $zip -ErrorAction SilentlyContinue
try {
    $meta = Invoke-RestMethod -Uri ($Base + "/cai-dat/checksums")
    $want = @($meta.files | Where-Object { $_.name -match "Windows.*\.zip$" } | Select-Object -First 1).sha256
    if ($want) {
        $got = (Get-FileHash -Path $zip -Algorithm SHA256).Hash
        if ($got.ToLower() -ne $want.ToLower()) {
            throw "SHA-256 khong khop. Xoa $zip roi chay lai lenh."
        }
        Write-Host "SHA-256 khop."
    }
}
catch {
    if ($_.Exception.Message -match "SHA-256") { throw }
}

Expand-Archive -LiteralPath $zip -DestinationPath $tmp -Force
$setup = Get-ChildItem -Path $tmp -Filter "*.exe" -File | Select-Object -First 1
if (-not $setup) {
    throw "Khong thay file cai .exe trong goi ZIP."
}
Unblock-File -Path $setup.FullName -ErrorAction SilentlyContinue
Write-Host "Da tai. Neu Windows SmartScreen: Thong tin them -> Chay anyway."
Start-Process -FilePath $setup.FullName
Write-Host "Mo trinh cai MOS-KulKul."
