function workArea() {
  return {
    x: 0,
    y: 0,
    w: window.innerWidth,
    h: window.innerHeight,
  };
}

function applyRect(el, r) {
  el.style.left = `${r.x}px`;
  el.style.top = `${r.y}px`;
  el.style.width = `${r.w}px`;
  el.style.height = `${r.h}px`;
}

function currentState() {
  return sessionStorage.getItem("mos-dock-state") || "bottom";
}

function isCompact() {
  return sessionStorage.getItem("mos-compact") === "1" || currentState() === "minimized";
}

function setCompact(on) {
  sessionStorage.setItem("mos-compact", on ? "1" : "0");
}

function openHiddenUri(uri) {
  const probe = document.createElement("iframe");
  probe.style.display = "none";
  probe.src = uri;
  document.body.appendChild(probe);
  setTimeout(() => probe.remove(), 4000);
}

function currentApp() {
  return document.body?.dataset?.app || sessionStorage.getItem("mos-app") || "word";
}

function onWindows() {
  return /Windows/i.test(navigator.userAgent) || !!window.chrome?.webview;
}

function placeWordOnPc(state, useProtocol) {
  const s = state || currentState();
  const app = currentApp();
  sessionStorage.setItem("mos-app", app);
  const compact = isCompact() ? 1 : 0;
  fetch(`http://127.0.0.1:17331/place?state=${encodeURIComponent(s)}&app=${encodeURIComponent(app)}&compact=${compact}`, {
    method: "POST",
    mode: "cors",
  }).catch(() => {});
  if (useProtocol || onWindows()) {
    openHiddenUri(`mosdock:place?state=${encodeURIComponent(s)}&app=${encodeURIComponent(app)}&compact=${compact}`);
  }
}

async function applyLayout(state, place, useProtocol) {
  const wa = workArea();
  const compact = isCompact() || state === "minimized";
  const url = `/api/layout?state=${encodeURIComponent(state)}&x=${wa.x}&y=${wa.y}&w=${wa.w}&h=${wa.h}&compact=${compact ? 1 : 0}`;
  const data = await fetch(url).then((r) => r.json());
  const dock = document.getElementById("dock");
  const sim = document.getElementById("word-sim");
  applyRect(dock, data.dock);
  applyRect(sim, data.word);
  dock.classList.toggle("is-compact", compact);
  dock.classList.toggle("is-minimized", compact);
  document.body.classList.toggle("exam-running", compact);
  document.querySelectorAll("[data-dock]").forEach((btn) => {
    btn.setAttribute("aria-pressed", btn.getAttribute("data-dock") === state ? "true" : "false");
  });
  sessionStorage.setItem("mos-dock-state", state);
  const bar = document.getElementById("word-sim-bar");
  if (bar) {
    const title = bar.textContent.split(" — ")[0] || "Microsoft Office";
    bar.textContent = `${title} — vị trí đã chọn (${data.word.x},${data.word.y}) ${data.word.w}×${data.word.h}`;
  }
  if (place) {
    sim.classList.add("is-target");
    placeWordOnPc(state, useProtocol);
  }
}

function enterControlsOnly() {
  setCompact(true);
  const state = currentState() === "minimized" ? "bottom" : currentState();
  applyLayout(state, true, true);
}

document.querySelectorAll("[data-dock]").forEach((btn) => {
  btn.addEventListener("click", () => {
    const next = btn.getAttribute("data-dock");
    if (next === "minimized") setCompact(true);
    applyLayout(next, true, onWindows());
  });
});

document.getElementById("btn-place-word")?.addEventListener("click", () => {
  enterControlsOnly();
});

document.getElementById("btn-expand")?.addEventListener("click", () => {
  setCompact(false);
  const state = currentState() === "minimized" ? "bottom" : currentState();
  applyLayout(state, false, false);
});

window.addEventListener("message", (ev) => {
  if (ev.origin !== window.location.origin) return;
  if (ev.data && ev.data.type === "mos-place-word") {
    if (ev.data.app) {
      document.body.dataset.app = ev.data.app;
      sessionStorage.setItem("mos-app", ev.data.app);
    }
    if (ev.data.open) setCompact(true);
    applyLayout(ev.data.state || currentState(), true, true);
  }
});

window.addEventListener("resize", () => applyLayout(currentState(), false));
if (document.body?.dataset?.app) {
  sessionStorage.setItem("mos-app", document.body.dataset.app);
}
applyLayout(currentState(), false);
