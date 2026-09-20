#!/usr/bin/env python3
"""MOS-KulKul cho macOS — đăng nhập + kéo cửa sổ Word/Excel/PowerPoint."""
from __future__ import annotations

import json
import os
import subprocess
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

PORT = 17331
CONTROLS_H = 96
CONTROLS_W = 248
BOTTOM_RATIO = 0.28
SIDE_RATIO = 0.30
MIN_WORD = 400
CERTIPORT_OFFICE = 0.65


def app_version() -> str:
    here = os.path.dirname(os.path.abspath(__file__))
    for candidate in (
        os.path.join(here, "VERSION"),
        os.path.join(here, "..", "Resources", "VERSION"),
    ):
        try:
            text = open(candidate, encoding="utf-8").read().strip()
            if text:
                return text
        except OSError:
            continue
    return os.environ.get("MOS_APP_VERSION") or "1.21.5"


APP_VERSION = app_version()

APPS = {
    "word": "Microsoft Word",
    "excel": "Microsoft Excel",
    "powerpoint": "Microsoft PowerPoint",
}


def compute(x: int, y: int, w: int, h: int, state: str, compact: bool, certiport: bool | None = None):
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    certiport = State.certiport if certiport is None else certiport
    if certiport and state in ("bottom", "top", ""):
        office_h = max(MIN_WORD, int(h * CERTIPORT_OFFICE))
        office_h = min(office_h, h - 72)
        dock_h = max(72, h - office_h)
        if state == "top":
            dock = (x, y, w, dock_h)
            office = (x, y + dock_h, w, office_h)
        else:
            dock = (x, y + office_h, w, dock_h)
            office = (x, y, w, office_h)
        ox, oy, ow, oh = office
        ow = max(MIN_WORD, min(ow, w))
        oh = max(MIN_WORD, min(oh, h))
        return dock, (ox, oy, ow, oh)
    if state == "left":
        dw = CONTROLS_W if compact else max(280, int(w * SIDE_RATIO))
        dock = (x, y, dw, h)
        office = (x + dw, y, w - dw, h)
    elif state == "right":
        dw = CONTROLS_W if compact else max(280, int(w * SIDE_RATIO))
        dock = (x + w - dw, y, dw, h)
        office = (x, y, w - dw, h)
    elif state == "top":
        dh = CONTROLS_H if compact else max(180, int(h * BOTTOM_RATIO))
        dock = (x, y, w, dh)
        office = (x, y + dh, w, h - dh)
    else:
        dh = CONTROLS_H if compact else max(180, int(h * BOTTOM_RATIO))
        dock = (x, y + h - dh, w, dh)
        office = (x, y, w, h - dh)
    ox, oy, ow, oh = office
    ow = max(MIN_WORD, min(ow, w))
    oh = max(MIN_WORD, min(oh, h))
    return dock, (ox, oy, ow, oh)


def osa(script: str) -> str:
    r = subprocess.run(["osascript", "-e", script], capture_output=True, text=True)
    return (r.stdout or "").strip()


def work_area():
    raw = osa("tell application \"Finder\" to get bounds of window of desktop")
    try:
        parts = [int(p.strip()) for p in raw.replace("{", "").replace("}", "").split(",")]
        left, top, right, bottom = parts
        # Finder bounds: left, top, right, bottom — trừ thanh menu ~25px
        top = max(top, 25)
        return left, top, max(800, right - left), max(500, bottom - top)
    except Exception:
        return 0, 25, 1440, 875


def launch_office(app: str, file_url: str | None = None) -> None:
    name = APPS.get(app, APPS["word"])
    if file_url:
        subprocess.Popen(["open", file_url], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        return
    subprocess.Popen(["open", "-a", name], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def place_office(app: str, rect: tuple[int, int, int, int]) -> None:
    name = APPS.get(app, APPS["word"])
    x, y, w, h = rect
    script = f'''
tell application "System Events"
  if not (exists process "{name}") then return
  tell process "{name}"
    set frontmost to true
    delay 0.15
    if (count of windows) is 0 then return
    try
      set value of attribute "AXFullScreen" of window 1 to false
    end try
    set position of window 1 to {{{x}, {y}}}
    set size of window 1 to {{{w}, {h}}}
  end tell
end tell
tell application "{name}"
  try
    set zoomed of window 1 to false
    set bounds of window 1 to {{{x}, {y}, {x + w}, {y + h}}}
  end try
end tell
'''
    osa(script)


class State:
    app = "word"
    dock = "bottom"
    compact = False
    token = ""
    mode = "training"
    attempt_id = ""
    local_path = ""
    version_hash = ""
    certiport = False
    elapsed_only = True
    cut_score = 700


def apply(launch: bool = False, file_url: str | None = None) -> dict:
    wx, wy, ww, wh = work_area()
    dock, office = compute(wx, wy, ww, wh, State.dock, State.compact)
    if launch:
        launch_office(State.app, file_url)
        State.compact = True
        dock, office = compute(wx, wy, ww, wh, State.dock, True)

        def pin():
            deadline = time.time() + 20
            while time.time() < deadline:
                place_office(State.app, office)
                time.sleep(0.35)

        threading.Thread(target=pin, daemon=True).start()
    else:
        threading.Thread(target=lambda: place_office(State.app, office), daemon=True).start()
    return {"ok": True, "app": State.app, "state": State.dock, "compact": State.compact, "dock": dock, "office": office}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        return

    def _cors(self):
        origin = self.headers.get("Origin", "https://mos.gds.edu.vn")
        if origin.startswith("https://mos.gds.edu.vn") or origin.startswith("http://127.0.0.1") or origin.startswith("http://localhost"):
            self.send_header("Access-Control-Allow-Origin", origin)
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Access-Control-Allow-Private-Network", "true")

    def _json(self, code: int, payload: dict):
        body = json.dumps(payload).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self._cors()
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(204)
        self._cors()
        self.end_headers()

    def do_GET(self):
        self.handle_req()

    def do_POST(self):
        self.handle_req()

    def handle_req(self):
        u = urlparse(self.path)
        q = parse_qs(u.query)
        path = u.path.rstrip("/") or "/"
        if path in ("/", "/health"):
            self._json(200, {"ok": True, "service": "mos-kulkul", "os": "mac", "version": APP_VERSION})
            return
        if path == "/from-url":
            raw = (q.get("url") or q.get("u") or [""])[0]
            apply_url(raw)
            self._json(200, {"ok": True})
            return
        if path in ("/open", "/place"):
            if q.get("app"):
                State.app = q["app"][0]
            if q.get("state"):
                State.dock = q["state"][0]
            if (q.get("compact") or [""])[0] in ("1", "true"):
                State.compact = True
            file_url = (q.get("file") or [None])[0]
            result = apply(launch=path == "/open", file_url=file_url)
            self._json(200, result)
            return
        self._json(404, {"ok": False})


def apply_url(raw: str) -> None:
    if not raw:
        return
    u = urlparse(raw)
    q = parse_qs(u.query)
    if q.get("app"):
        State.app = q["app"][0]
    if q.get("state"):
        State.dock = q["state"][0]
    launch = "open" in raw.lower()
    if (q.get("compact") or [""])[0] in ("1", "true"):
        State.compact = True
    apply(launch=launch, file_url=(q.get("file") or [None])[0])


def portal_origin() -> str:
    return (os.environ.get("MOS_PORTAL") or "https://mos.gds.edu.vn").rstrip("/")


def portal_login(username: str, password: str, app: str) -> tuple[bool, str]:
    import urllib.error
    import urllib.request

    body = json.dumps(
        {"username": username, "password": password, "chuong_trinh": app}
    ).encode("utf-8")
    req = urllib.request.Request(
        portal_origin() + "/api/v1/auth/login",
        data=body,
        headers={"Content-Type": "application/json", "User-Agent": f"MOS-KulKul/{APP_VERSION}"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            data = json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as ex:
        if ex.code == 401:
            return False, "Tên đăng nhập hoặc mật khẩu không đúng."
        return False, f"Lỗi máy chủ ({ex.code})."
    except Exception as ex:
        return False, "Không kết nối được MOS-KulKul: " + str(ex)
    if not data.get("ok"):
        return False, "Tên đăng nhập hoặc mật khẩu không đúng."
    pid = data.get("program") or app
    if isinstance(pid, dict):
        pid = pid.get("id") or app
    State.app = str(pid)
    State.token = str(data.get("token") or "")
    return True, (data.get("user") or {}).get("name") or username


def _api(method: str, path: str, data: bytes | None = None, content_type: str | None = None, timeout: int = 20):
    import urllib.error
    import urllib.request

    headers = {"User-Agent": f"MOS-KulKul/{APP_VERSION}"}
    if State.token:
        headers["Authorization"] = "Bearer " + State.token
    if State.version_hash:
        headers["X-MOS-Bank-Hash"] = State.version_hash
    if content_type:
        headers["Content-Type"] = content_type
    req = urllib.request.Request(portal_origin() + path, data=data, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        raw = resp.read()
        ctype = resp.headers.get("Content-Type") or ""
        if "json" in ctype or raw[:1] == b"{":
            return json.loads(raw.decode("utf-8"))
        return raw


def exam_open(root=None) -> None:
    try:
        data = _api("GET", "/api/v1/projects?program=" + State.app)
        projects = data.get("projects") or []
        if not projects:
            return
        project = projects[0]
        pid = project["id"]
        blob = _api("GET", f"/api/v1/projects/{pid}/file")
        dest_dir = os.path.expanduser(f"~/Library/Application Support/MOS/KulKul/projects/{pid}")
        os.makedirs(dest_dir, exist_ok=True)
        dest = os.path.join(dest_dir, project.get("filename") or (pid + ".bin"))
        if isinstance(blob, dict):
            return
        with open(dest, "wb") as f:
            f.write(blob)
        started = _api(
            "POST",
            "/api/v1/attempts",
            json.dumps({"project_id": pid, "mode": State.mode}).encode("utf-8"),
            "application/json",
        )
        bank = started.get("bank") if isinstance(started.get("bank"), dict) else started
        State.attempt_id = started.get("attempt_id") or ""
        State.local_path = dest
        State.version_hash = str((bank or {}).get("version_hash") or "")
        State.certiport = (bank or {}).get("ui") == "certiport_split" or State.mode == "testing"
        State.elapsed_only = bool((bank or {}).get("elapsed_only", State.mode != "testing"))
        State.cut_score = int((bank or {}).get("cut_score") or 700)
        State.compact = True
        apply(launch=True, file_url=dest)
        if root is not None:
            root.after(0, lambda: None)
    except Exception:
        return


def exam_submit(root=None) -> None:
    if not State.attempt_id or not State.local_path or not os.path.isfile(State.local_path):
        return
    try:
        import uuid

        boundary = "----KulKul" + uuid.uuid4().hex
        filename = os.path.basename(State.local_path)
        with open(State.local_path, "rb") as f:
            payload = f.read()
        body = (
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{filename}\"\r\n"
            "Content-Type: application/octet-stream\r\n\r\n"
        ).encode("utf-8") + payload + f"\r\n--{boundary}--\r\n".encode("utf-8")
        data = _api(
            "POST",
            f"/api/v1/attempts/{State.attempt_id}/submit",
            body,
            f"multipart/form-data; boundary={boundary}",
        )
        bank = data.get("bank") if isinstance(data, dict) and isinstance(data.get("bank"), dict) else {}
        score = data.get("score") if isinstance(data, dict) and isinstance(data.get("score"), dict) else {}
        scaled = bank.get("scaled_1000")
        if scaled is None:
            scaled = score.get("scaled_1000")
        passed = bank.get("passed")
        if passed is None:
            passed = score.get("passed")
        if passed is None and isinstance(scaled, int):
            passed = scaled >= State.cut_score
        udl = bank.get("udl_message") or ""
        badge = "PASS" if passed else "FAIL"
        msg = f"{scaled}/1000 {badge}" if scaled is not None else badge
        if udl:
            msg += "\n" + str(udl)
        if root is not None:
            def _show():
                from tkinter import messagebox

                messagebox.showinfo("MOS-KulKul", msg)

            root.after(0, _show)
    except Exception:
        return


def show_login() -> bool:
    try:
        import tkinter as tk
        from tkinter import messagebox
    except Exception:
        return True

    result = {"ok": False}
    root = tk.Tk()
    root.title("MOS-KulKul")
    root.geometry("560x500")
    root.configure(bg="#f4f8fc")
    root.resizable(False, False)

    tk.Label(root, text="MOS-KulKul", bg="#f4f8fc", fg="#0f172a", font=("Helvetica", 22, "bold")).pack(pady=(18, 4))
    tk.Label(
        root,
        text="Chọn Microsoft Word, Excel hoặc PowerPoint rồi đăng nhập trên máy.",
        bg="#f4f8fc",
        fg="#475569",
        wraplength=500,
    ).pack(pady=(0, 12))

    tiles = tk.Frame(root, bg="#f4f8fc")
    tiles.pack()
    colors = {"word": "#2b579a", "excel": "#217346", "powerpoint": "#d24726"}

    def pick(app: str):
        State.app = app
        chosen.set("Chương trình đã chọn: " + APPS.get(app, app))
        for key, btn in tile_btns.items():
            btn.configure(relief="solid" if key == app else "groove", bd=3 if key == app else 1)

    tile_btns = {}
    for key, label in (("word", "Word"), ("excel", "Excel"), ("powerpoint", "PowerPoint")):
        b = tk.Button(
            tiles,
            text=label,
            fg=colors[key],
            font=("Helvetica", 12, "bold"),
            width=12,
            command=lambda a=key: pick(a),
        )
        b.pack(side="left", padx=6)
        tile_btns[key] = b

    chosen = tk.StringVar(value="Chương trình đã chọn: Microsoft Word")
    tk.Label(root, textvariable=chosen, bg="#f4f8fc", fg="#0f172a").pack(pady=10)
    pick("word")

    form = tk.Frame(root, bg="#f4f8fc")
    form.pack(pady=4)
    tk.Label(form, text="Tài khoản", bg="#f4f8fc", font=("Helvetica", 10, "bold")).grid(row=0, column=0, sticky="w")
    user = tk.Entry(form, width=36)
    user.grid(row=1, column=0, pady=(0, 8))
    tk.Label(form, text="Mật khẩu", bg="#f4f8fc", font=("Helvetica", 10, "bold")).grid(row=2, column=0, sticky="w")
    pw = tk.Entry(form, width=36, show="*")
    pw.grid(row=3, column=0)

    mode_var = tk.StringVar(value="training")
    modes = tk.Frame(root, bg="#f4f8fc")
    modes.pack(pady=8)
    tk.Radiobutton(
        modes, text="Luyện tập (Training)", variable=mode_var, value="training", bg="#f4f8fc"
    ).pack(side="left", padx=8)
    tk.Radiobutton(
        modes, text="Thi (Testing)", variable=mode_var, value="testing", bg="#f4f8fc"
    ).pack(side="left", padx=8)

    def submit(_event=None):
        State.mode = mode_var.get() or "training"
        ok, msg = portal_login(user.get().strip(), pw.get(), State.app)
        if not ok:
            messagebox.showerror("MOS-KulKul", msg)
            return
        result["ok"] = True
        root.destroy()

    tk.Button(root, text="Đăng nhập", command=submit, bg="#008EE2", fg="white", font=("Helvetica", 12, "bold"), width=28).pack(pady=16)
    user.focus_set()
    root.bind("<Return>", submit)
    root.mainloop()
    return result["ok"]


def already_running() -> bool:
    try:
        import urllib.request

        urllib.request.urlopen(f"http://127.0.0.1:{PORT}/health", timeout=0.4)
        return True
    except Exception:
        return False


def start_http():
    httpd = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    httpd.serve_forever()


def show_bar():
    try:
        import tkinter as tk
    except Exception:
        threading.Event().wait()
        return

    root = tk.Tk()
    root.title("MOS-KulKul")
    root.attributes("-topmost", True)
    root.configure(bg="#1e4f73")
    root.overrideredirect(True)

    def layout_bar():
        wx, wy, ww, wh = work_area()
        dock, _ = compute(wx, wy, ww, wh, State.dock, State.compact or True)
        x, y, w, h = dock
        root.geometry(f"{max(w, 640)}x{max(h, 72)}+{x}+{y}")

    def click(state=None, launch=False, expand=False):
        if expand:
            State.compact = False
        if state:
            State.dock = state
            if state == "minimized":
                State.compact = True
        if launch:
            State.compact = True
        apply(launch=launch)
        layout_bar()

    bar = tk.Frame(root, bg="#1e4f73")
    bar.pack(fill="both", expand=True, padx=8, pady=8)
    tk.Label(bar, text="MOS-KulKul", fg="white", bg="#1e4f73", font=("Lato", 13, "bold")).pack(side="left", padx=(0, 12))
    for label, app in (("Word", "word"), ("Excel", "excel"), ("PowerPoint", "powerpoint")):
        tk.Button(bar, text=label, command=lambda a=app: (setattr(State, "app", a), click(launch=True))).pack(side="left", padx=3)
    for label, st in (("Thu nhỏ", "minimized"), ("Đính trái", "left"), ("Đính phải", "right"), ("Đính đáy", "bottom")):
        tk.Button(bar, text=label, command=lambda s=st: click(state=s)).pack(side="left", padx=3)
    tk.Button(bar, text="Đặt cửa sổ", command=lambda: click(launch=False)).pack(side="left", padx=3)
    tk.Button(bar, text="Mở rộng đề", command=lambda: click(expand=True)).pack(side="left", padx=3)
    tk.Button(bar, text="Tải đề", command=lambda: threading.Thread(target=lambda: exam_open(root), daemon=True).start()).pack(side="left", padx=3)
    tk.Button(bar, text="Nộp bài", command=lambda: threading.Thread(target=lambda: exam_submit(root), daemon=True).start()).pack(side="left", padx=3)
    State.compact = True
    layout_bar()
    root.mainloop()


def main():
    args = sys.argv[1:]
    url = None
    for i, a in enumerate(args):
        if a in ("--url", "-u") and i + 1 < len(args):
            url = args[i + 1]
        elif a.startswith("mosdock:") or a.startswith("mos-kulkul:"):
            url = a

    if already_running():
        if url:
            import urllib.parse
            import urllib.request

            q = urllib.parse.urlencode({"url": url})
            try:
                urllib.request.urlopen(f"http://127.0.0.1:{PORT}/from-url?{q}", timeout=2)
            except Exception:
                pass
        return

    threading.Thread(target=start_http, daemon=True).start()
    time.sleep(0.15)
    if url:
        apply_url(url)
    if os.environ.get("MOS_DOCK_HEADLESS") == "1":
        threading.Event().wait()
        return
    if not url and not show_login():
        return
    show_bar()


if __name__ == "__main__":
    main()
