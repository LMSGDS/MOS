# MOS Dock (hành vi GMetrix)

Cửa sổ **mini-browser TopMost** trên Windows. Mặc định dính **đáy màn hình**. Nút: **Thu nhỏ**, **Đính trái**, **Đính phải**, **Đính đáy**.

Sau khi mở Word (`ms-word:`), MOS Dock **chờ cửa sổ `OpusApp` hiện ra**, restore nếu đang maximize, rồi `SetWindowPos` tới **ô còn lại** của vị trí đã chọn — hai cửa sổ không chồng, vùng soạn thảo không bị che.

- Agent local: `http://127.0.0.1:17331/place?state=left|right|bottom|minimized`
- Protocol: `mosdock:place?state=left` (đăng ký HKCU lần chạy đầu)
- Nút **Đặt Word** lặp lại thao tác nếu Word mở chậm

Không dùng Office Online. WebView mở `https://mos.gds.edu.vn/dang-nhap?che-do=dock`.

```powershell
dotnet publish desktop/MosDock/MosDock.csproj -c Release
```

Yêu cầu: Windows 10/11, Microsoft Word, WebView2 Runtime (có sẵn trên Windows 11).
