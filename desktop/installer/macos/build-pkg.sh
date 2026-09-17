#!/bin/bash
# Build MOS Dock.app + .pkg on macOS (GitHub Actions macos-latest).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
SRC="$ROOT/desktop/MosDockMac"
OUT="$ROOT/dist-mac"
APP="$OUT/MOS Dock.app"
PKG="$ROOT/dist-installer/MOS-Dock-Setup-macOS.pkg"

rm -rf "$OUT"
mkdir -p "$OUT" "$ROOT/dist-installer"

osacompile -o "$APP" "$SRC/handler.applescript"
mkdir -p "$APP/Contents/Resources"
cp "$SRC/mosdock_mac.py" "$APP/Contents/Resources/mosdock_mac.py"
chmod 755 "$APP/Contents/Resources/mosdock_mac.py"

PLIST="$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleName MOS Dock" "$PLIST" || true
/usr/libexec/PlistBuddy -c "Set :CFBundleIdentifier vn.edu.gds.mosdock" "$PLIST"
/usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes array" "$PLIST" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0 dict" "$PLIST" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLName string MOS Dock" "$PLIST" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes array" "$PLIST" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :CFBundleURLTypes:0:CFBundleURLSchemes:0 string mosdock" "$PLIST" 2>/dev/null || true
/usr/libexec/PlistBuddy -c "Add :NSAppleEventsUsageDescription string MOS Dock cần điều khiển cửa sổ Microsoft Word, Excel và PowerPoint." "$PLIST" 2>/dev/null || true

SCRIPTS="$OUT/scripts"
mkdir -p "$SCRIPTS"
cat > "$SCRIPTS/postinstall" << 'EOF'
#!/bin/bash
open "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility" || true
open -a "MOS Dock" || true
exit 0
EOF
chmod 755 "$SCRIPTS/postinstall"

pkgbuild \
  --identifier vn.edu.gds.mosdock \
  --version 1.0.0 \
  --install-location /Applications \
  --component "$APP" \
  --scripts "$SCRIPTS" \
  "$PKG"

echo "Built $APP"
echo "Built $PKG"
