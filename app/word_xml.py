"""Open XML facts for Word — bookmarks, headings, internal hyperlinks."""
from __future__ import annotations

import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

W = "{http://schemas.openxmlformats.org/wordprocessingml/2006/main}"
R = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
CP = "{http://schemas.openxmlformats.org/package/2006/metadata/core-properties}"
DC = "{http://purl.org/dc/elements/1.1/}"
VML = "{urn:schemas-microsoft-com:vml}"
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
        "page_background": "",
        "watermarks": [],
        "page_border": False,
        "first_page_header": False,
        "header_texts": [],
        "header_instructions": [],
        "core_properties": {},
        "comment_count": 0,
        "revision_count": 0,
        "track_revisions": False,
        "vanish_count": 0,
        "heading1_sz": "",
        "display_background_shape": False,
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
                extra_parts = {
                    name: _read_zip_part(z, name)
                    for name in z.namelist()
                    if name in (
                        "word/settings.xml",
                        "docProps/core.xml",
                        "word/comments.xml",
                        "word/styles.xml",
                    )
                    or name.startswith("word/header")
                    or name.startswith("word/footer")
                }
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

    extra = _manage_document_facts(root, extra_parts)
    return {
        "ok": True,
        "bookmarks": bookmarks,
        "internal_hyperlinks": [h for h in hyperlinks if h.get("anchor") and not h.get("external")],
        "external_hyperlinks": [h for h in hyperlinks if h.get("external")],
        "headings": headings,
        **extra,
    }


def _attr(el: ET.Element | None, name: str) -> str:
    if el is None:
        return ""
    return el.attrib.get(f"{W}{name}") or el.attrib.get(name) or ""


def _manage_document_facts(root: ET.Element, parts: dict[str, bytes | None]) -> dict:
    parts = parts or {}
    background = root.find(f"{W}background")
    page_background = (_attr(background, "color") or "").upper()
    page_border = False
    first_page_header = False
    for sect in root.iter(f"{W}sectPr"):
        if sect.find(f"{W}pgBorders") is not None:
            page_border = True
        if sect.find(f"{W}titlePg") is not None:
            first_page_header = True
        for ref in list(sect.findall(f"{W}headerReference")) + list(sect.findall(f"{W}footerReference")):
            if _attr(ref, "type") == "first":
                first_page_header = True

    watermarks: list[str] = []
    header_texts: list[str] = []
    header_instructions: list[str] = []
    for name, raw in parts.items():
        if not raw or not (name.startswith("word/header") or name.startswith("word/footer")):
            continue
        try:
            node = ET.fromstring(raw)
        except ET.ParseError:
            continue
        text = norm("".join((t.text or "") for t in node.iter(f"{W}t")))
        if text:
            header_texts.append(text)
        for instr in node.iter(f"{W}instrText"):
            if instr.text:
                header_instructions.append(instr.text.strip())
        for shape in node.iter(f"{VML}textpath"):
            value = shape.attrib.get("string") or ""
            if value:
                watermarks.append(value)

    core_properties: dict[str, str] = {}
    core_raw = parts.get("docProps/core.xml")
    if core_raw:
        try:
            core = ET.fromstring(core_raw)
            mapping = {
                "title": f"{DC}title",
                "subject": f"{DC}subject",
                "creator": f"{DC}creator",
                "keywords": f"{CP}keywords",
                "contentStatus": f"{CP}contentStatus",
                "lastModifiedBy": f"{CP}lastModifiedBy",
            }
            for key, tag in mapping.items():
                el = core.find(tag)
                core_properties[key] = norm(el.text if el is not None else "")
        except ET.ParseError:
            pass

    comment_count = 0
    comments_raw = parts.get("word/comments.xml")
    if comments_raw:
        try:
            comments = ET.fromstring(comments_raw)
            comment_count = len(list(comments.iter(f"{W}comment")))
        except ET.ParseError:
            comment_count = 0
    comment_count += len(list(root.iter(f"{W}commentReference")))

    revision_count = len(list(root.iter(f"{W}ins"))) + len(list(root.iter(f"{W}del")))
    vanish_count = len(list(root.iter(f"{W}vanish")))

    track_revisions = False
    display_background_shape = False
    settings_raw = parts.get("word/settings.xml")
    if settings_raw:
        try:
            settings = ET.fromstring(settings_raw)
            track_revisions = settings.find(f"{W}trackRevisions") is not None
            display_background_shape = settings.find(f"{W}displayBackgroundShape") is not None
        except ET.ParseError:
            pass

    heading1_sz = ""
    styles_raw = parts.get("word/styles.xml")
    if styles_raw:
        try:
            styles = ET.fromstring(styles_raw)
            for style in styles.findall(f"{W}style"):
                if style.attrib.get(f"{W}styleId") == "Heading1":
                    rpr = style.find(f"{W}rPr")
                    sz = rpr.find(f"{W}sz") if rpr is not None else None
                    heading1_sz = _attr(sz, "val")
                    break
        except ET.ParseError:
            pass

    return {
        "page_background": page_background,
        "watermarks": watermarks,
        "page_border": page_border,
        "first_page_header": first_page_header,
        "header_texts": header_texts,
        "header_instructions": header_instructions,
        "core_properties": core_properties,
        "comment_count": comment_count,
        "revision_count": revision_count,
        "track_revisions": track_revisions,
        "vanish_count": vanish_count,
        "heading1_sz": heading1_sz,
        "display_background_shape": display_background_shape,
    }
