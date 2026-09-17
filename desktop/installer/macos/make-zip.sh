#!/bin/bash
# Build a double-click macOS installer zip on Linux or macOS.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
SRC="$ROOT/desktop/MosDockMac"
STAGE="$ROOT/dist-mac/MOS-Dock-macOS"
ZIP="$ROOT/dist-installer/MOS-Dock-Setup-macOS.zip"
INSTALL="$ROOT/desktop/installer/macos/install.sh"

rm -rf "$STAGE"
mkdir -p "$STAGE/Files" "$ROOT/dist-installer"
cp "$SRC/mosdock_mac.py" "$STAGE/Files/mosdock_mac.py"
cp "$SRC/handler.applescript" "$STAGE/Files/handler.applescript"
cp "$SRC/Info.plist.url.fragment" "$STAGE/Files/Info.plist"
cp "$INSTALL" "$STAGE/Cai MOS Dock.command"
chmod 755 "$STAGE/Cai MOS Dock.command"

cat > "$STAGE/HUONG-DAN.txt" << 'EOF'
MOS Dock / KulKul cho macOS

macOS sẽ chặn gói .pkg vì chưa được Apple ký. Dùng zip này:

1. Giải nén.
2. Chuột phải "Cai MOS Dock.command" → Mở → Mở
   (đừng nhấp đúp nếu Mac báo Move to Trash).
3. Cho phép MOS Dock trong Cài đặt hệ thống → Quyền riêng tư → Accessibility.
4. Vào https://mos.gds.edu.vn chọn Word / Excel / PowerPoint.

Hoặc mở Terminal và chạy:
curl -fsSL https://mos.gds.edu.vn/cai-dat/macos.sh | bash
EOF

python3 - "$STAGE" "$ZIP" << 'PY'
import pathlib, sys, zipfile
stage = pathlib.Path(sys.argv[1])
zip_path = pathlib.Path(sys.argv[2])
zip_path.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
    for p in stage.rglob("*"):
        if not p.is_file():
            continue
        arc = p.relative_to(stage.parent).as_posix()
        info = zipfile.ZipInfo(arc)
        mode = 0o755 if p.name.endswith(".command") or p.suffix == ".py" else 0o644
        info.external_attr = (mode & 0xFFFF) << 16
        info.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(info, p.read_bytes())
print("Built", zip_path)
PY
