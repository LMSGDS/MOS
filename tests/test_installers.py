"""Windows + macOS installer packaging stays in sync with 1.21.3."""
from pathlib import Path
import zipfile

from app.main import app_version

ROOT = Path(__file__).resolve().parent.parent


def test_mac_installer_scripts_use_kulkul_names_and_version():
    version = app_version()
    assert version == "1.21.3"
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
    assert "MOS_INSTALLER_BRANCH" in sync
    assert 'os.environ.get("MOS_INSTALLER_BRANCH", "main")' in sync
    workflow = (ROOT / ".github" / "workflows" / "build-mos-dock.yml").read_text(encoding="utf-8")
    assert "MOS-KulKul-Setup-macOS" in workflow
    assert "name: MOS-Dock-Setup-macOS" not in workflow
    assert "workflow_dispatch" in workflow
    on_block = workflow.split("jobs:", 1)[0]
    assert "push:" in on_block
    assert "branches: [main]" in on_block
    assert "pull_request:" not in on_block
    release_job = workflow.split("\n  release:", 1)[1]
    assert "needs: [windows, macos, macos-zip]" in release_job
    assert "contents: write" in release_job
    assert "gh release create" in release_job
    assert "gh release upload" in release_job and "--clobber" in release_job
    assert "MOS-KulKul-Setup-Windows-Full.exe" in release_job
    assert "MOS-KulKul-Setup-macOS.pkg" in release_job
    assert "SHA256-release.txt" in release_job


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
    assert "X-MOS-Bank-Hash" in mac
    assert "CERTIPORT_OFFICE" in mac
    assert "PASS" in mac
    assert "scaled_1000" in mac


def test_mac_certiport_split_65_35():
    import importlib.util

    path = ROOT / "desktop" / "MosDockMac" / "mosdock_mac.py"
    spec = importlib.util.spec_from_file_location("mosdock_mac_layout", path)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    dock, office = mod.compute(0, 0, 1440, 900, "bottom", True, certiport=True)
    assert office[3] == 585
    assert dock[3] == 315
    assert office[1] == 0
    assert dock[1] == 585
