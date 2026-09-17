function templateUrl() {
  return `${window.location.origin}/static/mau-van-ban.docx`;
}

function openProtocol(uri) {
  const hint = document.getElementById("hint");
  if (hint) hint.hidden = false;
  window.location.href = uri;
}

document.getElementById("btn-word")?.addEventListener("click", () => {
  // Protocol handler registered by Microsoft Office on the personal computer.
  openProtocol("ms-word:");
});

document.getElementById("btn-doc")?.addEventListener("click", () => {
  const url = templateUrl();
  openProtocol(`ms-word:nft|u|${url}`);
});
