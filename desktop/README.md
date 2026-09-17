# MOS-KulKul (Windows + macOS)

Đăng nhập và chọn **Word / Excel / PowerPoint** ngay trong app, rồi kéo cửa sổ Office đúng ô dock.

## Bộ cài tự động

CI (Actions → **Build MOS-KulKul**) tạo:

| Nền tảng | File |
| --- | --- |
| Windows | `MOS-KulKul-Setup-Windows.exe` — đăng nhập trong app, kèm WebView2Loader.dll |
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
