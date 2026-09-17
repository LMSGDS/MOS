#!/bin/bash
# Build a double-click macOS installer zip on Linux or macOS.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
SRC="$ROOT/desktop/MosDockMac"
STAGE="$ROOT/dist-mac/MOS-Dock-macOS"
ZIP="$ROOT/dist-installer/MOS-Dock-Setup-macOS.zip"

rm -rf "$STAGE"
mkdir -p "$STAGE/Files" "$ROOT/dist-installer"
cp "$SRC/mosdock_mac.py" "$STAGE/Files/mosdock_mac.py"
cp "$SRC/handler.applescript" "$STAGE/Files/handler.applescript"
cp "$SRC/Info.plist.url.fragment" "$STAGE/Files/Info.plist"

cat > "$STAGE/Cai MOS Dock.command" << 'EOF'
#!/bin/bash
# Cài MOS Dock vào /Applications và đăng ký protocol mosdock:
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
APP="/Applications/MOS Dock.app"

if ! command -v osacompile >/dev/null 2>&1; then
  osascript -e 'display dialog "Cần công cụ dòng lệnh Xcode (osacompile) để cài MOS Dock." buttons {"OK"} default button 1' || true
  exit 1
fi

rm -rf "$APP"
osacompile -o "$APP" "$DIR/Files/handler.applescript"
mkdir -p "$APP/Contents/Resources"
cp "$DIR/Files/mosdock_mac.py" "$APP/Contents/Resources/mosdock_mac.py"
chmod 755 "$APP/Contents/Resources/mosdock_mac.py"

PLIST="$APP/Contents/Info.plist"
if [[ -f "$DIR/Files/Info.plist" ]] && [[ -x /usr/libexec/PlistBuddy ]]; then
  /usr/libexec/PlistBuddy -c "Merge $DIR/Files/Info.plist" "$PLIST" 2>/dev/null || true
fi

plist_set() {
  local key="$1" type="$2" value="$3"
  if /usr/libexec/PlistBuddy -c "Print :$key" "$PLIST" >/dev/null 2>&1; then
    /usr/libexec/PlistBuddy -c "Set :$key $value" "$PLIST"
  else
    /usr/libexec/PlistBuddy -c "Add :$key $type $value" "$PLIST"
  fi
}
plist_set CFBundleName string "MOS Dock"
plist_set CFBundleIdentifier string vn.edu.gds.mosdock
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes array" "$PLIST"
fi
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes:0" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0 dict" "$PLIST"
fi
plist_set "CFBundleURLTypes:0:CFBundleURLName" string "MOS Dock"
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes:0:CFBundleURLSchemes" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes array" "$PLIST"
fi
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleURLTypes:0:CFBundleURLSchemes:0" "$PLIST" >/dev/null 2>&1; then
  /usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes:0 string mosdock" "$PLIST"
else
  /usr/libexec/PlistBuddy -c "Set :CFBundleURLTypes:0:CFBundleURLSchemes:0 mosdock" "$PLIST"
fi

open "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility" || true
open -a "MOS Dock" || true
osascript -e 'display dialog "Đã cài MOS Dock vào Applications. Cho phép Accessibility, rồi vào mos.gds.edu.vn." buttons {"OK"} default button 1' || true
EOF
chmod 755 "$STAGE/Cai MOS Dock.command"

cat > "$STAGE/HUONG-DAN.txt" << 'EOF'
MOS Dock cho macOS
1. Giải nén zip này.
2. Nhấp đúp "Cai MOS Dock.command" (nếu macOS chặn: chuột phải → Mở).
3. Cho phép MOS Dock trong Cài đặt hệ thống → Quyền riêng tư → Accessibility.
4. Vào https://mos.gds.edu.vn chọn Word / Excel / PowerPoint.
EOF

python3 - "$STAGE" "$ZIP" << 'PY'
import pathlib, sys, zipfile
stage = pathlib.Path(sys.argv[1])
zip_path = pathlib.Path(sys.argv[2])
zip_path.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
    for p in stage.rglob("*"):
        if p.is_file():
            z.write(p, p.relative_to(stage.parent).as_posix())
print("Built", zip_path)
PY
