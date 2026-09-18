#!/usr/bin/env bash
# MOS không chuyển mã nguồn hay bài học sinh qua SSH/scp/sshpass.
# Đi SSH từ Cloud Agent / CI sẽ lộ máy chủ. Chỉ HTTPS: GitHub + mos.gds.edu.vn.
set -euo pipefail
echo "Từ chối chuyển dữ liệu qua SSH — mất bảo mật server." >&2
echo "Mã nguồn: git pull HTTPS (scripts/git-sync.sh) hoặc webhook GitHub." >&2
echo "Bài học sinh: MOS-KulKul → https://mos.gds.edu.vn/api/v1/" >&2
exit 2
