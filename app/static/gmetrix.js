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

async function layout(state) {
  const wa = workArea();
  const url = `/api/layout?state=${encodeURIComponent(state)}&x=${wa.x}&y=${wa.y}&w=${wa.w}&h=${wa.h}`;
  const data = await fetch(url).then((r) => r.json());
  const dock = document.getElementById("dock");
  applyRect(dock, data.dock);
  applyRect(document.getElementById("word-sim"), data.word);
  dock.classList.toggle("is-minimized", state === "minimized");
  document.querySelectorAll("[data-dock]").forEach((btn) => {
    btn.setAttribute("aria-pressed", btn.getAttribute("data-dock") === state ? "true" : "false");
  });
  sessionStorage.setItem("mos-dock-state", state);
}

function currentState() {
  return sessionStorage.getItem("mos-dock-state") || "bottom";
}

document.querySelectorAll("[data-dock]").forEach((btn) => {
  btn.addEventListener("click", () => layout(btn.getAttribute("data-dock")));
});

window.addEventListener("resize", () => layout(currentState()));
layout(currentState());
