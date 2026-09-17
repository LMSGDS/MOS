#!/bin/bash
# Cài MOS-KulKul vào /Applications
# - Chuột phải file này → Mở (khi nằm trong zip)
# - Hoặc: curl -fsSL https://mos.gds.edu.vn/cai-dat/macos.sh | bash
set -euo pipefail
BASE="${MOS_BASE_URL:-https://mos.gds.edu.vn}"
APP="/Applications/MOS-KulKul.app"
SELF="${BASH_SOURCE[0]:-}"
DIR=""
if [[ -n "$SELF" && -f "$SELF" ]]; then
  DIR="$(cd "$(dirname "$SELF")" && pwd)"
fi

need() {
  if ! command -v "$1" >/dev/null 2>&1; then
    osascript -e "display dialog \"Thiếu lệnh $1. Mở Terminal và chạy: xcode-select --install\" buttons {\"OK\"} default button 1" >/dev/null 2>&1 || true
    echo "Thiếu $1" >&2
    exit 1
  fi
}

need osacompile
need python3
need curl

SRC=""
if [[ -n "$DIR" && -f "$DIR/Files/mosdock_mac.py" ]]; then
  SRC="$DIR/Files"
else
  TMP="$(mktemp -d "${TMPDIR:-/tmp}/mosdock.XXXXXX")"
  trap 'rm -rf "$TMP"' EXIT
  mkdir -p "$TMP/Files"
  curl -fsSL "$BASE/cai-dat/macos-files/mosdock_mac.py" -o "$TMP/Files/mosdock_mac.py"
  curl -fsSL "$BASE/cai-dat/macos-files/handler.applescript" -o "$TMP/Files/handler.applescript"
  curl -fsSL "$BASE/cai-dat/macos-files/Info.plist" -o "$TMP/Files/Info.plist"
  SRC="$TMP/Files"
fi

if [[ -n "$DIR" ]]; then
  xattr -cr "$DIR" >/dev/null 2>&1 || true
fi
rm -rf "/Applications/MOS Dock.app" "$APP"
osacompile -o "$APP" "$SRC/handler.applescript"
mkdir -p "$APP/Contents/Resources"
cp "$SRC/mosdock_mac.py" "$APP/Contents/Resources/mosdock_mac.py"
chmod 755 "$APP/Contents/Resources/mosdock_mac.py"
xattr -cr "$APP" >/dev/null 2>&1 || true

PLIST="$APP/Contents/Info.plist"
if [[ -f "$SRC/Info.plist" && -x /usr/libexec/PlistBuddy ]]; then
  /usr/libexec/PlistBuddy -c "Merge $SRC/Info.plist" "$PLIST" 2>/dev/null || true
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
plist_set CFBundleIdentifier string vn.edu.gds.mosdock
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

open "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility" || true
open -a "MOS-KulKul" || true
osascript -e 'display dialog "Đã cài MOS-KulKul vào Applications. Cho phép Accessibility, rồi mở MOS-KulKul để đăng nhập." buttons {"OK"} default button 1' >/dev/null 2>&1 || true
echo "Installed $APP"
