#!/bin/bash
# Build MOS Dock.app + .pkg on macOS (GitHub Actions macos-latest).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
SRC="$ROOT/desktop/MosDockMac"
OUT="$ROOT/dist-mac"
APP="$OUT/MOS Dock.app"
PKG="$ROOT/dist-installer/MOS-Dock-Setup-macOS.pkg"
PLIST_SRC="$SRC/Info.plist.url.fragment"

rm -rf "$OUT"
mkdir -p "$OUT" "$ROOT/dist-installer"

osacompile -o "$APP" "$SRC/handler.applescript"
mkdir -p "$APP/Contents/Resources"
cp "$SRC/mosdock_mac.py" "$APP/Contents/Resources/mosdock_mac.py"
chmod 755 "$APP/Contents/Resources/mosdock_mac.py"

PLIST="$APP/Contents/Info.plist"

plist_set() {
  local key="$1" type="$2" value="$3"
  if /usr/libexec/PlistBuddy -c "Print :$key" "$PLIST" >/dev/null 2>&1; then
    /usr/libexec/PlistBuddy -c "Set :$key $value" "$PLIST"
  else
    /usr/libexec/PlistBuddy -c "Add :$key $type $value" "$PLIST"
  fi
}

plist_set CFBundleName string "MOS Dock"
plist_set CFBundleDisplayName string "MOS Dock"
plist_set CFBundleIdentifier string vn.edu.gds.mosdock
plist_set CFBundleVersion string 1.0.0
plist_set CFBundleShortVersionString string 1.0
plist_set LSMinimumSystemVersion string 11.0
plist_set NSAppleEventsUsageDescription string "MOS Dock cần điều khiển cửa sổ Microsoft Word, Excel và PowerPoint."

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

if [[ -f "$PLIST_SRC" ]]; then
  /usr/libexec/PlistBuddy -c "Merge $PLIST_SRC" "$PLIST" 2>/dev/null || true
fi

SCRIPTS="$OUT/scripts"
mkdir -p "$SCRIPTS"
cat > "$SCRIPTS/postinstall" << 'EOF'
#!/bin/bash
open "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility" || true
open -a "MOS Dock" || true
exit 0
EOF
chmod 755 "$SCRIPTS/postinstall"

PAYLOAD="$OUT/payload"
rm -rf "$PAYLOAD"
mkdir -p "$PAYLOAD"
cp -R "$APP" "$PAYLOAD/"

pkgbuild \
  --identifier vn.edu.gds.mosdock \
  --version 1.0.0 \
  --install-location /Applications \
  --root "$PAYLOAD" \
  --scripts "$SCRIPTS" \
  "$PKG"

echo "Built $APP"
echo "Built $PKG"
