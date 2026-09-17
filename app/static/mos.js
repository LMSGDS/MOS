function templateUrl() {
  return `${window.location.origin}/static/mau-van-ban.docx`;
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

document.getElementById("btn-word")?.addEventListener("click", () => {
  openProtocol("ms-word:");
});

document.getElementById("btn-doc")?.addEventListener("click", () => {
  openProtocol(`ms-word:nft|u|${templateUrl()}`);
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
