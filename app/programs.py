"""Ba chương trình MOS: Word, Excel, PowerPoint — mở trên máy, không Office Online."""
from __future__ import annotations

from typing import Any

DEFAULT = "word"

PROGRAMS: dict[str, dict[str, Any]] = {
    "word": {
        "id": "word",
        "name": "Microsoft Word",
        "short": "Word",
        "protocol": "ms-word:",
        "template": "mau-van-ban.docx",
        "download": "mau-van-ban-mos.docx",
        "mime": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "color": "#2b579a",
        "glyph": "W",
        "open_label": "Mở Word trên máy",
        "new_label": "Tạo văn bản Word (máy)",
        "place_label": "Đặt Word vào chỗ đã chọn",
        "sim_title": "Microsoft Word",
        "sim_hint": "Vùng soạn thảo Word sẽ nằm đúng ô này trên màn hình (không chồng khung MOS)",
        "paper_kind": "document",
    },
    "excel": {
        "id": "excel",
        "name": "Microsoft Excel",
        "short": "Excel",
        "protocol": "ms-excel:",
        "template": "mau-bang-tinh.xlsx",
        "download": "mau-bang-tinh-mos.xlsx",
        "mime": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "color": "#217346",
        "glyph": "X",
        "open_label": "Mở Excel trên máy",
        "new_label": "Tạo bảng tính Excel (máy)",
        "place_label": "Đặt Excel vào chỗ đã chọn",
        "sim_title": "Microsoft Excel",
        "sim_hint": "Lưới Excel sẽ nằm đúng ô này trên màn hình (không chồng khung MOS)",
        "paper_kind": "sheet",
    },
    "powerpoint": {
        "id": "powerpoint",
        "name": "Microsoft PowerPoint",
        "short": "PowerPoint",
        "protocol": "ms-powerpoint:",
        "template": "mau-bai-trinh-bay.pptx",
        "download": "mau-bai-trinh-bay-mos.pptx",
        "mime": "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "color": "#d24726",
        "glyph": "P",
        "open_label": "Mở PowerPoint trên máy",
        "new_label": "Tạo bài trình bày (máy)",
        "place_label": "Đặt PowerPoint vào chỗ đã chọn",
        "sim_title": "Microsoft PowerPoint",
        "sim_hint": "Slide PowerPoint sẽ nằm đúng ô này trên màn hình (không chồng khung MOS)",
        "paper_kind": "slide",
    },
}

MENU = [PROGRAMS["word"], PROGRAMS["excel"], PROGRAMS["powerpoint"]]


def normalize(value: str | None) -> str:
    v = (value or DEFAULT).strip().lower()
    aliases = {
        "word": "word",
        "winword": "word",
        "docx": "word",
        "doc": "word",
        "excel": "excel",
        "xlsx": "excel",
        "xls": "excel",
        "powerpoint": "powerpoint",
        "ppt": "powerpoint",
        "pptx": "powerpoint",
        "powerpnt": "powerpoint",
    }
    return aliases.get(v, DEFAULT)


def resolve(value: str | None) -> dict[str, Any]:
    return PROGRAMS[normalize(value)]
