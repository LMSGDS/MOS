# MOS Dock (Windows + macOS)

Plugin kéo cửa sổ **Word / Excel / PowerPoint** đúng ô dock. Trình duyệt không làm được việc này.

## Bộ cài tự động

CI (Actions → **Build MOS Dock**) tạo:

| Nền tảng | File |
| --- | --- |
| Windows | `MOS-Dock-Setup-Windows.exe` — cài per-user, đăng ký `mosdock:`, chạy cùng Windows |
| macOS | `MOS-Dock-Setup-macOS.zip` — giải nén, chuột phải `Cai MOS Dock.command` → Mở |
| macOS (pkg) | Bị Gatekeeper chặn nếu chưa notarize Apple; không dùng làm mặc định |

Copy vào `data/installers/` trên server rồi học sinh tải tại `/cai-dat`.

### Windows (máy build)

```powershell
dotnet publish desktop/MosDock/MosDock.csproj -c Release -o dist-win
# Inno Setup: ISCC desktop/installer/windows/mosdock.iss /DDist=dist-win
# Hoặc: powershell -ExecutionPolicy Bypass -File desktop/installer/windows/install.ps1 -PayloadDir dist-win
```

### macOS (máy Mac hoặc runner macos-latest)

```bash
bash desktop/installer/macos/build-pkg.sh
# hoặc trên Linux: bash desktop/installer/macos/make-zip.sh
```

Sau khi cài Mac: **System Settings → Privacy & Security → Accessibility** → bật MOS Dock.

Chạy tay không cần pkg: `python3 desktop/MosDockMac/mosdock_mac.py`
