const names = {
  word: "Microsoft Word",
  excel: "Microsoft Excel",
  powerpoint: "Microsoft PowerPoint",
};

function selectProgram(id) {
  const hidden = document.getElementById("chuong_trinh");
  if (hidden) hidden.value = id;
  const label = document.getElementById("program-chosen");
  if (label) label.textContent = names[id] || id;
  document.querySelectorAll(".program-tile").forEach((btn) => {
    btn.setAttribute("aria-pressed", btn.getAttribute("data-program") === id ? "true" : "false");
  });
}

document.querySelectorAll(".program-tile").forEach((btn) => {
  btn.addEventListener("click", () => selectProgram(btn.getAttribute("data-program")));
});
