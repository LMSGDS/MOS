# MOS-KulKul (Windows + macOS)

Đăng nhập trên **Windows** và web `/dang-nhap`: form kiểu GMetrix SMS (logo giữa, ô tài khoản/mật khẩu, hiện/ẩn mật khẩu, nút **Đăng nhập** navy full-width). Màu Canvas LMS. Khi mở Office, thanh icon 48px Trái/Phải/Đáy. Không WebView2, không Office Online.

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
