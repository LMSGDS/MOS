"""Open XML facts for Word — bookmarks, headings, internal hyperlinks."""
from __future__ import annotations

import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
R = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
MAX_PART = 8 * 1024 * 1024


def norm(text: str | None) -> str:
    return " ".join((text or "").replace("\u00a0", " ").split())


def same(a: str | None, b: str | None) -> bool:
    return norm(a).casefold() == norm(b).casefold()


def _local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _empty(error: str) -> dict:
    return {
        "ok": False,
        "error": error,
        "bookmarks": {},
        "internal_hyperlinks": [],
        "external_hyperlinks": [],
        "headings": [],
    }


def _read_zip_part(z: zipfile.ZipFile, name: str) -> bytes | None:
    if name not in z.namelist():
        return None
    info = z.getinfo(name)
    if info.file_size > MAX_PART:
        raise ValueError("xml_too_large")
    return z.read(name)


def extract_word_facts(path: Path) -> dict:
    if not path or not Path(path).is_file():
        return _empty("missing_file")
    path = Path(path)
    try:
        with zipfile.ZipFile(path) as z:
            if "word/document.xml" not in z.namelist():
                return _empty("not_docx")
            try:
                xml = _read_zip_part(z, "word/document.xml")
                rels_xml = _read_zip_part(z, "word/_rels/document.xml.rels")
            except ValueError:
                return _empty("xml_too_large")
            root = ET.fromstring(xml or b"<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'/>")
            rels: dict[str, dict] = {}
            if rels_xml:
                rroot = ET.fromstring(rels_xml)
                for rel in rroot:
                    rid = rel.attrib.get("Id")
                    if rid:
                        rels[rid] = {
                            "target": rel.attrib.get("Target") or "",
                            "mode": rel.attrib.get("TargetMode") or "",
                            "type": rel.attrib.get("Type") or "",
                        }
    except zipfile.BadZipFile:
        return _empty("bad_zip")
    except ET.ParseError:
        return _empty("bad_xml")

    bookmarks: dict[str, dict] = {}
    open_ids: dict[str, str] = {}
    buffers: dict[str, list[str]] = {}
    headings: list[dict] = []
    hyperlinks: list[dict] = []

    def para_style(p: ET.Element) -> str:
        ppr = p.find(f"{W}pPr")
        if ppr is None:
            return ""
        ps = ppr.find(f"{W}pStyle")
        if ps is None:
            return ""
        return ps.attrib.get(f"{W}val") or ""

    def para_text(p: ET.Element) -> str:
        return norm("".join((t.text or "") for t in p.iter(f"{W}t")))

    def start_bookmark(el: ET.Element, para: ET.Element | None, style: str) -> None:
        bid = el.attrib.get(f"{W}id") or ""
        name = el.attrib.get(f"{W}name") or ""
        if not bid or not name:
            return
        open_ids[bid] = name
        buffers.setdefault(bid, [])
        heading = para_text(para) if para is not None else ""
        bookmarks[name] = {
            "id": bid,
            "name": name,
            "text": "",
            "heading": heading,
            "style": style,
        }

    def end_bookmark(el: ET.Element) -> None:
        bid = el.attrib.get(f"{W}id") or ""
        name = open_ids.pop(bid, None)
        if name and name in bookmarks:
            bookmarks[name]["text"] = norm("".join(buffers.get(bid) or []))

    def collect_text(el: ET.Element) -> None:
        if not el.text:
            return
        for bid in list(open_ids):
            buffers.setdefault(bid, []).append(el.text)

    def field_anchor(raw: str) -> tuple[str, bool]:
        parts = raw.replace('"', " ").split()
        lower = [p.lower() for p in parts]
        anchor = ""
        if "\\l" in lower:
            i = lower.index("\\l")
            if i + 1 < len(parts):
                anchor = parts[i + 1]
        external = "http://" in raw.lower() or "https://" in raw.lower() or "mailto:" in raw.lower()
        return anchor, external

    def add_hyperlink(text: str, anchor: str, rid: str, field_raw: str = "") -> None:
        rel = rels.get(rid) or {}
        target = rel.get("target") or ""
        if not anchor and target.startswith("#"):
            anchor = target[1:]
        if not anchor and field_raw:
            anchor, _ = field_anchor(field_raw)
        external = (rel.get("mode") == "External") or bool(target and not anchor)
        if field_raw and ("http://" in field_raw.lower() or "https://" in field_raw.lower() or "mailto:" in field_raw.lower()):
            external = True
        hyperlinks.append(
            {
                "text": text,
                "anchor": anchor,
                "external": external,
                "target": target or field_raw,
            }
        )

    body = root.find(f"{W}body")
    if body is None:
        body = root

    def walk(el: ET.Element, para: ET.Element | None, style: str) -> None:
        tag = _local(el.tag)
        current_para = para
        current_style = style
        if tag == "p":
            current_para = el
            current_style = para_style(el)
            text = para_text(el)
            if current_style.lower().startswith("heading") and text:
                headings.append({"style": current_style, "text": text})
        if tag == "bookmarkStart":
            start_bookmark(el, current_para, current_style)
        elif tag == "bookmarkEnd":
            end_bookmark(el)
        elif tag == "t":
            collect_text(el)
        elif tag == "hyperlink":
            texts = norm("".join((t.text or "") for t in el.iter(f"{W}t")))
            add_hyperlink(texts, el.attrib.get(f"{W}anchor") or "", el.attrib.get(f"{R}id") or "")
        elif tag == "instrText" and el.text and "HYPERLINK" in el.text.upper():
            add_hyperlink("", "", "", el.text.strip())
        for child in list(el):
            walk(child, current_para, current_style)

    walk(body, None, "")

    for bid, name in open_ids.items():
        if name in bookmarks and not bookmarks[name]["text"]:
            bookmarks[name]["text"] = norm("".join(buffers.get(bid) or []))

    for link in hyperlinks:
        anchor = link.get("anchor") or ""
        bm = bookmarks.get(anchor) or {}
        link["target_heading"] = bm.get("heading") or ""
        link["target_text"] = bm.get("text") or ""

    return {
        "ok": True,
        "bookmarks": bookmarks,
        "internal_hyperlinks": [h for h in hyperlinks if h.get("anchor") and not h.get("external")],
        "external_hyperlinks": [h for h in hyperlinks if h.get("external")],
        "headings": headings,
    }
