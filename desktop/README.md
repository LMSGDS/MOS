# MOS-KulKul (Windows + macOS)

Đăng nhập trên **Windows**: giao diện Canvas LMS (nav `#394B58`, nút `#0374B5`, nền `#F5F5F5`). Chữ xếp chồng bằng Dock + đo `TextRenderer` — tiêu đề không đè phụ đề, nút **Đăng xuất** đủ rộng. Trang chủ: **Bài mới** / **Tiếp tục bài** / **Bài đã nộp**. Khung bài thi bên trái, Office bên phải. Luyện tập có **Kiểm tra nhiệm vụ**. Không WebView2, không Office Online.

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
