#!/bin/bash
# Build MOS-KulKul.app + .pkg + drag-to-Applications zip (macos-latest).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
# shellcheck source=version.sh
source "$ROOT/desktop/installer/macos/version.sh"
VERSION="$(MOS_ROOT="$ROOT" mos_app_version)"
SRC="$ROOT/desktop/MosDockMac"
OUT="$ROOT/dist-mac"
APP="$OUT/MOS-KulKul.app"
PKG="$ROOT/dist-installer/MOS-KulKul-Setup-macOS.pkg"
ZIP="$ROOT/dist-installer/MOS-KulKul-Setup-macOS.zip"
PLIST_SRC="$SRC/Info.plist"
PNG="$ROOT/app/static/kulkul.png"

rm -rf "$OUT"
mkdir -p "$OUT" "$ROOT/dist-installer"

osacompile -o "$APP" "$SRC/handler.applescript"
mkdir -p "$APP/Contents/Resources"
cp "$SRC/mosdock_mac.py" "$APP/Contents/Resources/mosdock_mac.py"
printf '%s\n' "$VERSION" > "$APP/Contents/Resources/VERSION"
chmod 755 "$APP/Contents/Resources/mosdock_mac.py"

if [[ -f "$PNG" && -x /usr/bin/sips && -x /usr/bin/iconutil ]]; then
  ICONSET="$OUT/kulkul.iconset"
  mkdir -p "$ICONSET"
  for sz in 16 32 64 128 256 512; do
    sips -z "$sz" "$sz" "$PNG" --out "$ICONSET/icon_${sz}x${sz}.png" >/dev/null
    sips -z $((sz * 2)) $((sz * 2)) "$PNG" --out "$ICONSET/icon_${sz}x${sz}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/kulkul.icns"
fi

PLIST="$APP/Contents/Info.plist"
if [[ -f "$PLIST_SRC" && -x /usr/libexec/PlistBuddy ]]; then
  /usr/libexec/PlistBuddy -c "Merge $PLIST_SRC" "$PLIST" 2>/dev/null || true
fi

plist_set() {
  local key="$1" type="$2" value="$3"
  if /usr/libexec/PlistBuddy -c "Print :$key" "$PLIST" >/dev/null 2>&1; then
    /usr/libexec/PlistBuddy -c "Set :$key $value" "$PLIST"
  else
    /usr/libexec/PlistBuddy -c "Add :$key $type $value" "$PLIST"
  fi
}

plist_set CFBundleName string "MOS-KulKul"
plist_set CFBundleDisplayName string "MOS-KulKul"
plist_set CFBundleIdentifier string vn.edu.gds.mosdock
plist_set CFBundleVersion string "$VERSION"
plist_set CFBundleShortVersionString string "$VERSION"
plist_set LSMinimumSystemVersion string 11.0
plist_set CFBundleIconFile string kulkul
plist_set NSAppleEventsUsageDescription string "MOS-KulKul cần điều khiển cửa sổ Microsoft Word, Excel và PowerPoint."

if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes array" "$PLIST"
fi
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes:0" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0 dict" "$PLIST"
fi
plist_set "CFBundleURLTypes:0:CFBundleURLName" string "MOS-KulKul"
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes:0:CFBundleURLSchemes" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes array" "$PLIST"
fi
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes:0:CFBundleURLSchemes:0" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes:0 string mosdock" "$PLIST"
else
  /usr/libexec/PlistBuddy -c "Set :CFBundleURLTypes:0:CFBundleURLSchemes:0 mosdock" "$PLIST"
fi
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes:0:CFBundleURLSchemes:1" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes:1 string mos-kulkul" "$PLIST"
else
  /usr/libexec/PlistBuddy -c "Set :CFBundleURLTypes:0:CFBundleURLSchemes:1 mos-kulkul" "$PLIST"
fi

SCRIPTS="$OUT/scripts"
mkdir -p "$SCRIPTS"
cat > "$SCRIPTS/postinstall" << 'EOF'
#!/bin/bash
open "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility" || true
open -a "MOS-KulKul" || true
exit 0
EOF
chmod 755 "$SCRIPTS/postinstall"

PAYLOAD="$OUT/payload"
rm -rf "$PAYLOAD"
mkdir -p "$PAYLOAD"
cp -R "$APP" "$PAYLOAD/"

pkgbuild \
  --identifier vn.edu.gds.mosdock \
  --version "$VERSION" \
  --install-location /Applications \
  --root "$PAYLOAD" \
  --scripts "$SCRIPTS" \
  "$PKG"

STAGE="$OUT/MOS-KulKul-macOS"
rm -rf "$STAGE"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
cat > "$STAGE/HUONG-DAN.txt" << EOF
MOS-KulKul ${VERSION} cho macOS

1. Giải nén.
2. Kéo MOS-KulKul.app vào thư mục Applications.
3. Chuột phải MOS-KulKul → Mở → Mở (lần đầu, vì bộ cài chưa notarize Apple).
4. Cho phép MOS-KulKul trong Cài đặt hệ thống → Quyền riêng tư & Bảo mật → Accessibility.
5. Mở MOS-KulKul, chọn Word/Excel/PowerPoint rồi đăng nhập.

Hoặc mở Terminal:
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
        mode = 0o755 if ".app/Contents/MacOS" in arc or p.suffix == ".py" else 0o644
        info.external_attr = (mode & 0xFFFF) << 16
        info.compress_type = zipfile.ZIP_DEFLATED
        z.writestr(info, p.read_bytes())
print("Built", zip_path)
PY

(
  cd "$ROOT/dist-installer"
  python3 - << PY
from pathlib import Path
import hashlib
out = Path("SHA256-macOS.txt")
lines = []
for name in ("MOS-KulKul-Setup-macOS.zip", "MOS-KulKul-Setup-macOS.pkg"):
    p = Path(name)
    if not p.is_file():
        continue
    digest = hashlib.sha256(p.read_bytes()).hexdigest()
    lines.append(f"{digest}  {name}")
out.write_text("\n".join(lines) + ("\n" if lines else ""), encoding="ascii")
print(out.read_text(encoding="ascii"), end="")
PY
)

echo "Built $APP ($VERSION)"
echo "Built $PKG"
echo "Built $ZIP"
