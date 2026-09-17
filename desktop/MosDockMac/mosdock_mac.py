#!/usr/bin/env python3
"""MOS Dock cho macOS — thanh TopMost + kéo cửa sổ Word/Excel/PowerPoint."""
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

APPS = {
    "word": "Microsoft Word",
    "excel": "Microsoft Excel",
    "powerpoint": "Microsoft PowerPoint",
}


def compute(x: int, y: int, w: int, h: int, state: str, compact: bool):
    state = (state or "bottom").lower()
    compact = compact or state == "minimized"
    if state == "left":
        dw = CONTROLS_W if compact else max(280, int(w * SIDE_RATIO))
        dock = (x, y, dw, h)
        office = (x + dw, y, w - dw, h)
    elif state == "right":
        dw = CONTROLS_W if compact else max(280, int(w * SIDE_RATIO))
        dock = (x + w - dw, y, dw, h)
        office = (x, y, w - dw, h)
    elif state == "minimized":
        dock = (x, y + h - CONTROLS_H, w, CONTROLS_H)
        office = (x, y, w, h - CONTROLS_H)
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
            self._json(200, {"ok": True, "service": "mos-dock", "os": "mac"})
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
    root.title("MOS Dock")
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
    tk.Label(bar, text="MOS Dock · Mac", fg="white", bg="#1e4f73", font=("Lato", 13, "bold")).pack(side="left", padx=(0, 12))
    for label, app in (("Word", "word"), ("Excel", "excel"), ("PowerPoint", "powerpoint")):
        tk.Button(bar, text=label, command=lambda a=app: (setattr(State, "app", a), click(launch=True))).pack(side="left", padx=3)
    for label, st in (("Thu nhỏ", "minimized"), ("Đính trái", "left"), ("Đính phải", "right"), ("Đính đáy", "bottom")):
        tk.Button(bar, text=label, command=lambda s=st: click(state=s)).pack(side="left", padx=3)
    tk.Button(bar, text="Đặt cửa sổ", command=lambda: click(launch=False)).pack(side="left", padx=3)
    tk.Button(bar, text="Mở rộng đề", command=lambda: click(expand=True)).pack(side="left", padx=3)
    State.compact = True
    layout_bar()
    root.mainloop()


def main():
    args = sys.argv[1:]
    url = None
    for i, a in enumerate(args):
        if a in ("--url", "-u") and i + 1 < len(args):
            url = args[i + 1]
        elif a.startswith("mosdock:"):
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
    show_bar()


if __name__ == "__main__":
    main()
