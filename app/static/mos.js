function templateUrl() {
  return `${window.location.origin}/static/mau-van-ban.docx`;
}

function dockState() {
  return sessionStorage.getItem("mos-dock-state") || "bottom";
}

function openProtocol(uri) {
  const hint = document.getElementById("hint");
  if (hint) hint.hidden = false;
  const probe = document.createElement("iframe");
  probe.style.display = "none";
  probe.src = uri;
  document.body.appendChild(probe);
  setTimeout(() => probe.remove(), 4000);
}

function placeWordAfterOpen() {
  const state = dockState();
  const payload = { type: "mos-place-word", state };
  try {
    window.parent.postMessage(payload, window.location.origin);
  } catch {
    /* not in iframe */
  }
  try {
    window.chrome?.webview?.postMessage(JSON.stringify(payload));
  } catch {
    /* not WebView2 */
  }
  fetch(`http://127.0.0.1:17331/place?state=${encodeURIComponent(state)}`, {
    method: "POST",
    mode: "cors",
  }).catch(() => {});
  openProtocol(`mosdock:place?state=${encodeURIComponent(state)}`);
}

document.getElementById("btn-word")?.addEventListener("click", () => {
  openProtocol("ms-word:");
  setTimeout(placeWordAfterOpen, 600);
});

document.getElementById("btn-doc")?.addEventListener("click", () => {
  openProtocol(`ms-word:nft|u|${templateUrl()}`);
  setTimeout(placeWordAfterOpen, 600);
});

document.querySelectorAll("[data-cmd]").forEach((btn) => {
  btn.addEventListener("click", () => {
    document.execCommand(btn.getAttribute("data-cmd"), false, null);
    document.getElementById("page")?.focus();
  });
});

document.getElementById("btn-save-html")?.addEventListener("click", () => {
  const page = document.getElementById("page");
  if (!page) return;
  const html = `<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word"><head><meta charset="utf-8"><title>MOS</title></head><body>${page.innerHTML}</body></html>`;
  const blob = new Blob(["\ufeff", html], { type: "application/msword" });
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = "van-ban-mos.doc";
  a.click();
  URL.revokeObjectURL(a.href);
});
