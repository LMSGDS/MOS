# MOS Dock (hành vi GMetrix)

Cửa sổ **mini-browser TopMost** trên Windows. Mặc định dính **đáy màn hình**. Nút: **Thu nhỏ**, **Đính trái**, **Đính phải**, **Đính đáy**.

Khi đổi trạng thái, MOS Dock **restore nếu Word đang maximize**, rồi `SetWindowPos` để cửa sổ `WINWORD.EXE` lấp phần còn lại — hai cửa sổ không chồng, vùng soạn thảo Word không bị che.

Không dùng Office Online. WebView mở `https://mos.gds.edu.vn/dang-nhap?che-do=dock`.

```powershell
dotnet publish desktop/MosDock/MosDock.csproj -c Release
```

Yêu cầu: Windows 10/11, Microsoft Word, WebView2 Runtime (có sẵn trên Windows 11).
