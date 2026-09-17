# MOS

## Cổng web mos.gds.edu.vn

Ứng dụng đăng nhập tài khoản nhà trường, chọn **Microsoft Word / Excel / PowerPoint** (menu ngoài khung đăng nhập) và **mở ứng dụng trên máy tính cá nhân** (protocol `ms-word:` / `ms-excel:` / `ms-powerpoint:`).

```bash
bash scripts/run-mos-web.sh
# http://127.0.0.1:8088
```

Tài khoản mặc định (đổi ngay khi đưa lên trường): `admin`, `giaovien`, `hocsinh` / `Mos@Gds2026`.

## PostgreSQL

Cổng và client MOS-KulKul dùng **PostgreSQL** (không SQLite): trường → lớp → học sinh, đề, lần làm bài, telemetry JSONB, chấm OpenXML.

```bash
docker compose -f deploy/docker-compose.yml up -d
export DATABASE_URL=postgresql://mos:mos@127.0.0.1:5432/mos
bash scripts/run-mos-web.sh
```

- Schema: [`app/schema.sql`](app/schema.sql)
- JWT client: `/api/v1/auth/login`, `/api/v1/projects`, `/api/v1/attempts`
- Quản trị web: `/quan-tri` (admin / giáo viên / BGH)

systemd `deploy/mos.service` đọc `DATABASE_URL`.

Nút **Mở … trên máy** gọi protocol Office tương ứng. Nút tạo tệp mẫu gọi `ms-*:nft|u|<url file mẫu>`. Máy người dùng cần cài Microsoft 365/Office.

Chứng chỉ Cloudflare Origin CA **không** nằm trong git. Đặt tại `/etc/ssl/cloudflare/mos.gds.edu.vn.pem` và `.key`, rồi dùng `deploy/nginx-mos.gds.edu.vn.conf`.

## MOS-KulKul

Ứng dụng trên máy: **đăng nhập**, **chọn Word / Excel / PowerPoint**, **luyện tập / thi**, tải đề PostgreSQL và mở Office trên PC (không WebView2, không Office Online).

- Windows: [`MOS-KulKul-Setup-Windows.exe`](/cai-dat/windows)
- macOS: [`MOS-KulKul-Setup-macOS.zip`](/cai-dat/macos)

| Môi trường | Việc làm |
| --- | --- |
| Trình duyệt | `/` mô phỏng layout; `/cai-dat` tải bộ cài |
| Windows | MOS-KulKul.exe — đăng nhập + dock + `SetWindowPos` |
| macOS | .zip → `Cai MOS-KulKul.command` hoặc `curl …/cai-dat/macos.sh \| bash` |

Không dùng Office Online. Chi tiết: [`desktop/README.md`](desktop/README.md).



Repo GitHub private `LMSGDS/MOS`. Agent Cursor đã kết nối GitHub, có quyền đọc/ghi mã nguồn, và commit/push tự động trên nhánh làm việc.



## Kết nối GitHub



Cursor dùng **GitHub App** (không dùng mật khẩu cá nhân) để clone repo, tạo nhánh, commit có chữ ký, và mở pull request.



1. Mở [Integrations](https://cursor.com/dashboard?tab=integrations).

2. Chọn **Connect** (hoặc **Manage Connections**) cạnh GitHub.

3. Cấp quyền cho tài khoản `LMSGDS` và chọn repo **MOS** (hoặc All repositories).

4. Mỗi người dùng Cursor cần kết nối GitHub của chính mình; agent chỉ vào được repo mà bạn đã có quyền.



Cài đặt lại app nếu mất quyền: [github.com/apps/cursor](https://github.com/apps/cursor).



## Cấp quyền repo



### Cursor GitHub App



Quyền cần cho Cloud Agent trên repo này:



| Quyền | Mục đích |

| --- | --- |

| Contents (read/write) | Clone, commit, push |

| Pull requests (read/write) | Tạo PR và comment |

| Metadata | Định danh repo |

| Actions / Checks (read) | Xem CI |

| Issues (read/write) | Tùy chọn; token sandbox hiện có thể thiếu scope này |



Repo MOS đã clone được và token agent có quyền **push** mã nguồn.



### GitHub Actions (`GITHUB_TOKEN`)



Workflow auto-commit yêu cầu:



```yaml

permissions:

  contents: write

  pull-requests: write

```



Trên GitHub: **Settings → Actions → General → Workflow permissions**



- Chọn **Read and write permissions**, hoặc

- Giữ Read rồi cho phép workflow tự xin write qua khóa `permissions`.

- Bật *Allow GitHub Actions to create and approve pull requests* nếu muốn workflow mở PR.



Token Cloud Agent **không** đổi được setting này hộ bạn.



## Commit tự động



Có hai lớp:



1. **Cursor Cloud Agent** — mỗi lần agent sửa code thì commit, push nhánh `cursor/...`, và mở PR. Commit được ký (Verified).

2. **GitHub Actions** — workflow [`.github/workflows/auto-commit.yml`](.github/workflows/auto-commit.yml) commit/push file phát sinh trong CI.



Chạy tay trên GitHub: **Actions → Auto commit → Run workflow**.



Gọi từ workflow khác:



```yaml

jobs:

  generate:

    # ... bước tạo file ...

  commit:

    needs: generate

    uses: ./.github/workflows/auto-commit.yml

    with:

      commit_message: "chore: regenerate outputs"

      file_pattern: "."

```



Script dùng chung: [`scripts/auto-commit.sh`](scripts/auto-commit.sh)



```bash

COMMIT_MESSAGE="chore: auto-commit" bash scripts/auto-commit.sh

```



## Kiểm tra



- Actions → **CI** phải chạy trên pull request này.

- Actions → **Check GitHub permissions** phải hiện `push: true`. Nếu `push: false`, hãy cấp Workflow permissions như trên rồi chạy lại.





## Kết nối SSH qua OpenVPN client



Đã xác minh trên Cloud Agent: OpenVPN lên `tun0` (`10.10.11.18`), SSH vào `plhien@160.191.49.65` (`gpu-160-191-49-65`). Profile lấy từ [OpenVPN Client trên Drive](https://drive.google.com/drive/folders/1Z9lFTUnB60SdiHmTDZqahVZzBniCXjz3) — **không commit** `.ovpn` hay mật khẩu.



Cổng 22 của máy GPU bị chặn từ Internet; phải đi qua VPN rồi `ip route` host SSH vào `tun0`.



### 1. Secret (Cursor / GitHub Actions)



| Secret | Bắt buộc | Nội dung |

| --- | --- | --- |

| `OPENVPN_CONFIG` | Có | File `openvpn_plhien.ovpn` |

| `OPENVPN_USERNAME` | Có với profile này | User VPN |

| `OPENVPN_PASSWORD` | Có với profile này | Mật khẩu VPN (trong doc Drive, cặp `Username: plhien`) |

| `SSH_HOST` | Có | `160.191.49.65` |

| `SSH_USER` | Có | `plhien` |

| `SSH_PASSWORD` hoặc `SSH_PRIVATE_KEY` | Một trong hai | Mật khẩu SSH là dòng `password:` riêng trong doc Drive (không dùng `plhien@123`) |

| `SSH_PORT` | Không | Mặc định `22` |



Secret Cursor chỉ có khi agent **khởi động**. Agent hiện tại đã nối bằng file Drive, không qua secret môi trường.



### 2. Chạy



```bash

bash scripts/openvpn-up.sh

bash scripts/ssh-connect.sh hostname

bash scripts/openvpn-down.sh

```



Mặc định bỏ `redirect-gateway` để agent vẫn ra GitHub. GitHub: **Actions → SSH via OpenVPN**.
