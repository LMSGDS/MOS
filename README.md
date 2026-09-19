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
- JWT client: `/api/v1/auth/login`, `/api/v1/projects`, `/api/v1/attempts`, checkpoint `/api/v1/attempts/{id}/checkpoints`
- App PC: đăng nhập → trang chủ (Bài mới / Tiếp tục / Đã nộp) → chọn chương trình → Luyện tập hoặc Thi
- Chấm Word 1.1 (bookmark/hyperlink Open XML): 62 điểm artifact, 38 điểm Find/Go To còn `unverified` khi chưa có bộ ghi nhận thao tác
- Quản trị web: `/quan-tri` (admin / giáo viên)

systemd `deploy/mos.service` đọc `DATABASE_URL`.

Nút **Mở … trên máy** gọi protocol Office tương ứng. Nút tạo tệp mẫu gọi `ms-*:nft|u|<url file mẫu>`. Máy người dùng cần cài Microsoft 365/Office.

Chứng chỉ Cloudflare Origin CA **không** nằm trong git. Đặt tại `/etc/ssl/cloudflare/mos.gds.edu.vn.pem` và `.key`, rồi dùng `deploy/nginx-mos.gds.edu.vn.conf`.

## MOS-KulKul

Ứng dụng trên máy: **đăng nhập**, **chọn Word / Excel / PowerPoint**, **luyện tập / thi**, tải đề PostgreSQL và mở Office trên PC (không WebView2, không Office Online).

- Windows **nên dán lệnh PowerShell** (đã mở PowerShell, không gói `powershell -Command`):

```powershell
$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; $d=Join-Path $env:TEMP 'MOS-KulKul'; New-Item -ItemType Directory -Force $d|Out-Null; $f=Join-Path $d 'MOS-KulKul-Setup.exe'; Invoke-WebRequest 'https://mos.gds.edu.vn/cai-dat/windows-full' -OutFile $f -UseBasicParsing; Unblock-File $f; Start-Process $f
```
- Windows gói ZIP (Chrome vẫn có thể quét vì trong ZIP có .exe): [`MOS-KulKul-Setup-Windows.zip`](/cai-dat/windows.zip)
- Windows .exe trực tiếp: [`MOS-KulKul-Setup-Windows.exe`](/cai-dat/windows) (Chrome/Edge sẽ quét — chọn Giữ lại)
- Windows offline ZIP: [`MOS-KulKul-Setup-Windows-Full.zip`](/cai-dat/windows-full.zip)
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

## Chuyển dữ liệu — chỉ HTTPS

Không SSH, không scp, không sshpass. Đưa mã hoặc bài học sinh đi SSH sẽ lộ máy chủ.

| Dữ liệu | Đường đi |
| --- | --- |
| Mã nguồn | GitHub HTTPS (`https://github.com/LMSGDS/MOS.git`) |
| Bài làm, điểm, bằng chứng | MOS-KulKul → `https://mos.gds.edu.vn/api/v1/` (JWT) |
| Cập nhật máy chủ | `scripts/git-sync.sh` trên chính server (kéo mã + bộ cài CI), timer systemd, hoặc webhook GitHub `POST /api/v1/hooks/github` |

### MOS_GITHUB_TOKEN (máy chủ mos.gds.edu.vn)

Token này **không** cài trong Cursor. Tạo trên GitHub, rồi ghi vào file trên **console máy chủ** (không SSH từ Cloud Agent).

1. GitHub (tài khoản đọc được repo private `LMSGDS/MOS`): **Settings → Developer settings → Personal access tokens → Fine-grained tokens → Generate new token**.
2. Đặt tên `MOS-server-git-sync`. **Repository access:** Only select repositories → `MOS`. **Permissions:** Contents = Read, Actions = Read (Metadata tự có). Generate, copy chuỗi `github_pat_…`.
3. Trên máy chủ:

```bash
cd /home/plhien/MOS
git remote set-url origin https://github.com/LMSGDS/MOS.git
install -d -m 700 data
cp -n data/git-sync.env.example data/git-sync.env
nano data/git-sync.env   # dán MOS_GITHUB_TOKEN=github_pat_…
chmod 600 data/git-sync.env
set -a; source data/git-sync.env; set +a
bash scripts/git-sync.sh
sudo cp deploy/mos-git-sync.service deploy/mos-git-sync.timer /etc/systemd/system/
sudo systemctl enable --now mos-git-sync.timer
```

`git-sync.sh` dùng token để `git pull` HTTPS và tải bộ cài CI vào `data/installers/`. Không đưa token vào git, chat, hay issue.

Đối chiếu máy chủ với GitHub (không cần SSH):

```bash
curl -sS https://mos.gds.edu.vn/healthz
# git.sha phải trùng `git rev-parse --short origin/main`
```

Webhook GitHub (HTTPS, chữ ký HMAC): Settings → Webhooks → `https://mos.gds.edu.vn/api/v1/hooks/github` (push). Secret = `MOS_GITHUB_WEBHOOK_SECRET`.

`scripts/ssh-connect.sh` chủ động từ chối. Workflow SSH qua OpenVPN đã gỡ.
