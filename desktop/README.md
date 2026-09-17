# MOS Dock (hành vi GMetrix)

Cửa sổ **mini-browser TopMost** trên Windows. Mặc định dính **đáy màn hình**. Nút: **Thu nhỏ**, **Đính trái**, **Đính phải**, **Đính đáy**.

Sau khi mở Word (`ms-word:`), MOS Dock **mở đúng ứng dụng**, chờ cửa sổ chính (bỏ splash/maximize), rồi `SetWindowPlacement` + `MoveWindow` tới **ô còn lại** trong ~20 giây — Word / Excel / PowerPoint đều được đặt.

- Agent: `http://127.0.0.1:17331/open?app=word|excel|powerpoint&state=left`
- Protocol: `mosdock:open?app=powerpoint&state=bottom` (đăng ký HKCU lần chạy đầu)
- Nút **Đặt Word** lặp lại thao tác nếu Word mở chậm

Không dùng Office Online. WebView mở `https://mos.gds.edu.vn/dang-nhap?che-do=dock`.

```powershell
dotnet publish desktop/MosDock/MosDock.csproj -c Release
```

Yêu cầu: Windows 10/11, Microsoft Word, WebView2 Runtime (có sẵn trên Windows 11).
