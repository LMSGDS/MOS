# MOS

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

Cloud Agent máy này **mở được TUN** (`tun0`). OpenVPN 2.6 đã cài. Chưa kết nối được vì chưa có file `.ovpn` và khóa SSH.

### 1. Thêm secret (Cursor)

Trong [Cloud Agents secrets](https://cursor.com/dashboard/cloud-agents) hoặc panel Environment:

| Secret | Bắt buộc | Nội dung |
| --- | --- | --- |
| `OPENVPN_CONFIG` | Có | Toàn bộ file `.ovpn` |
| `OPENVPN_USERNAME` | Không | User VPN (nếu profile cần) |
| `OPENVPN_PASSWORD` | Không | Mật khẩu VPN |
| `SSH_HOST` | Có | Host/IP server trong mạng VPN |
| `SSH_USER` | Có | User SSH |
| `SSH_PRIVATE_KEY` | Có | Private key (PEM) |
| `SSH_PORT` | Không | Mặc định `22` |

Secret chỉ inject khi agent **khởi động**. Sau khi lưu, gửi tin nhắn mới cho agent này (hoặc mở agent mới).

Cùng tên secret trên GitHub: **Settings → Secrets and variables → Actions**.

### 2. Chạy

```bash
bash scripts/openvpn-up.sh
bash scripts/ssh-connect.sh hostname
bash scripts/openvpn-down.sh
```

Mặc định script **bỏ** `redirect-gateway` để agent vẫn ra GitHub/Cursor. Full tunnel: `MOS_OVPN_FULL_TUNNEL=1 bash scripts/openvpn-up.sh`.

GitHub: **Actions → SSH via OpenVPN → Run workflow**.
