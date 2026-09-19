"""Windows + macOS installer packaging stays in sync with 1.21.0."""
from pathlib import Path
import zipfile

from app.main import app_version

ROOT = Path(__file__).resolve().parent.parent


def test_mac_installer_scripts_use_kulkul_names_and_version():
    version = app_version()
    assert version == "1.21.0"
    plist = (ROOT / "desktop" / "MosDockMac" / "Info.plist").read_text(encoding="utf-8")
    assert f"<string>{version}</string>" in plist
    assert "mos-kulkul" in plist
    assert "vn.edu.gds.mosdock" in plist
    pkg = (ROOT / "desktop" / "installer" / "macos" / "build-pkg.sh").read_text(encoding="utf-8")
    assert "MOS-KulKul-Setup-macOS.pkg" in pkg
    assert "MOS-KulKul-Setup-macOS.zip" in pkg
    assert "MOS-Dock-Setup-macOS.pkg" not in pkg
    assert "mos_app_version" in pkg
    install = (ROOT / "desktop" / "installer" / "macos" / "install.sh").read_text(encoding="utf-8")
    assert "MOS-KulKul.app" in install
    assert "macos-files/VERSION" in install
    assert "mos-kulkul" in install
    sync = (ROOT / "scripts" / "sync-installers.sh").read_text(encoding="utf-8")
    assert '"MOS-KulKul-Setup-macOS"' in sync
    assert "MOS-KulKul-Setup-macOS.pkg" in sync
    workflow = (ROOT / ".github" / "workflows" / "build-mos-dock.yml").read_text(encoding="utf-8")
    assert "MOS-KulKul-Setup-macOS" in workflow
    assert "name: MOS-Dock-Setup-macOS" not in workflow
    assert "workflow_dispatch" in workflow


def test_make_zip_includes_command_version_and_guide(tmp_path, monkeypatch):
    import subprocess

    dest_root = tmp_path / "repo"
    # Run against the real repo; script writes dist-installer under ROOT.
    zip_path = ROOT / "dist-installer" / "MOS-KulKul-Setup-macOS.zip"
    before = zip_path.stat().st_mtime if zip_path.is_file() else 0
    subprocess.check_call(["bash", str(ROOT / "desktop" / "installer" / "macos" / "make-zip.sh")])
    assert zip_path.is_file()
    assert zip_path.stat().st_mtime >= before
    with zipfile.ZipFile(zip_path) as zf:
        names = set(zf.namelist())
    assert any(n.endswith("Cai MOS-KulKul.command") for n in names)
    assert any(n.endswith("Files/mosdock_mac.py") for n in names)
    assert any(n.endswith("Files/Info.plist") for n in names)
    assert any(n.endswith("Files/VERSION") for n in names)
    assert any(n.endswith("HUONG-DAN.txt") for n in names)
    version_name = next(n for n in names if n.endswith("Files/VERSION"))
    with zipfile.ZipFile(zip_path) as zf:
        assert zf.read(version_name).decode().strip() == app_version()
    mac = (ROOT / "desktop" / "MosDockMac" / "mosdock_mac.py").read_text(encoding="utf-8")
    assert "APP_VERSION" in mac
    assert 'service": "mos-kulkul"' in mac or "mos-kulkul" in mac
