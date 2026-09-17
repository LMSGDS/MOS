# MOS-KulKul (Windows + macOS)

Đăng nhập trên **Windows**: trang chủ giống luồng SMS (Bài mới → chương trình → Luyện tập/Thi), **Tiếp tục bài**, **Bài đã nộp**. Khi vào bài, khung nhiệm vụ nằm bên trái, Word/Excel/PowerPoint chiếm phần còn lại. Luyện tập có **Kiểm tra nhiệm vụ**. Không WebView2, không Office Online. macOS để sau.

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
