# MOS-KulKul (Windows + macOS)

Đăng nhập (JWT) và chọn **Word / Excel / PowerPoint** ngay trong app PC, chọn **Luyện tập / Thi**, xem danh sách đề MOS (PostgreSQL), mở Office trên máy, nộp bài (COM lưu tệp + store-and-forward), rồi kéo cửa sổ Office đúng ô dock. Không nhúng website, không dùng WebView2 / Office Online.

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
