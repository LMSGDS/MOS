# MOS-KulKul (Windows + macOS)

Đăng nhập trên **Windows** và web `/dang-nhap`: form kiểu GMetrix SMS. Khi mở Office: cụm icon dock 2 hàng. **Luyện tập** mở khung Hướng dẫn trên dock (đề bài, bước đánh số in đậm, nút AAA đổi cỡ chữ, bóng đèn Hiện/Ẩn hướng dẫn); **Thi** ẩn hướng dẫn. Word Objective 1 (1.1–1.4) có đề + chấm Open XML. Không WebView2, không Office Online.

## Bộ cài tự động

CI (Actions → **Build MOS-KulKul**) tạo:

| Nền tảng | File |
| --- | --- |
| Windows | `MOS-KulKul-Setup-Windows.exe` — app PC native (không WebView2) |
| macOS | `MOS-KulKul-Setup-macOS.zip` — giải nén, chuột phải `Cai MOS-KulKul.command` → Mở |
| macOS (pkg) | Bị Gatekeeper chặn nếu chưa notarize Apple |

Copy vào `data/installers/` trên server rồi tải tại `/cai-dat`.

### Windows

```powershell
dotnet publish desktop/MosDock/MosDock.csproj -c Release -o dist-win
# Inno Setup: ISCC desktop/installer/windows/mosdock.iss /DDist=dist-win
```

### macOS

```bash
bash desktop/installer/macos/make-zip.sh
```

Sau khi cài Mac: **System Settings → Privacy & Security → Accessibility** → bật MOS-KulKul.
