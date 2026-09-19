# MOS app version — single source: desktop/MosDock/MosDock.csproj
mos_app_version() {
  python3 -c '
from pathlib import Path
import sys
root = Path(sys.argv[1])
text = (root / "desktop" / "MosDock" / "MosDock.csproj").read_text(encoding="utf-8")
start = text.find("<Version>")
end = text.find("</Version>", start)
if start < 0 or end < 0:
    raise SystemExit("missing Version in MosDock.csproj")
print(text[start + 9:end].strip())
' "${MOS_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)}"
}
