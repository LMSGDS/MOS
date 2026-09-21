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
A = "{http://schemas.openxmlformats.org/drawingml/2006/main}"
WP = "{http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing}"
W14 = "{http://schemas.microsoft.com/office/word/2010/wordml}"
W15 = "{http://schemas.microsoft.com/office/word/2012/wordml}"
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
        **_skill_defaults(),
    }


def _skill_defaults() -> dict:
    return {
        "document_text": "",
        "paragraphs": [],
        "text_effects": [],
        "sections": [],
        "breaks": {},
        "tables": [],
        "numbering_formats": [],
        "numbering_restarts": 0,
        "footnote_count": 0,
        "footnote_texts": [],
        "fields": [],
        "style_counts": {},
        "drawings": [],
        "drawing_kinds": [],
        "drawing_texts": [],
        "artistic_effects": [],
        "picture_effects": [],
        "alt_texts": [],
        "wraps": [],
        "comments": [],
        "resolved_count": 0,
        "reply_count": 0,
        "document_protection": False,
        "document_protection_edit": "",
        "document_protection_enforced": False,
        "personal_info_removed": False,
        "compatibility_mode": "",
        "has_picture": False,
        "has_3d": False,
        "has_smartart": False,
        "has_hdphoto": False,
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
            names: list[str] = []
            if "word/document.xml" not in z.namelist():
                return _empty("not_docx")
            try:
                xml = _read_zip_part(z, "word/document.xml")
                rels_xml = _read_zip_part(z, "word/_rels/document.xml.rels")
                names = list(z.namelist())
                extra_parts = {
                    name: _read_zip_part(z, name)
                    for name in names
                    if name in (
                        "word/settings.xml",
                        "docProps/core.xml",
                        "word/comments.xml",
                        "word/commentsExtended.xml",
                        "word/styles.xml",
                        "word/numbering.xml",
                        "word/footnotes.xml",
                        "word/endnotes.xml",
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
        if not text and not anchor and not external:
            return
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
    field_on = False
    field_instr: list[str] = []
    field_text: list[str] = []

    def flush_field() -> None:
        nonlocal field_on, field_instr, field_text
        raw = "".join(field_instr)
        if "HYPERLINK" in raw.upper():
            add_hyperlink(norm("".join(field_text)), "", "", raw.strip())
        field_on = False
        field_instr = []
        field_text = []

    def walk(el: ET.Element, para: ET.Element | None, style: str) -> None:
        nonlocal field_on, field_instr, field_text
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
        elif tag == "fldChar":
            kind = (el.attrib.get(f"{W}fldCharType") or "").lower()
            if kind == "begin":
                field_on = True
                field_instr = []
                field_text = []
            elif kind == "separate":
                field_text = []
            elif kind == "end" and field_on:
                flush_field()
        elif tag == "instrText" and el.text:
            if field_on:
                field_instr.append(el.text)
            elif "HYPERLINK" in el.text.upper():
                add_hyperlink("", "", "", el.text.strip())
        elif tag == "t":
            collect_text(el)
            if field_on and el.text:
                field_text.append(el.text)
        elif tag == "hyperlink":
            texts = norm("".join((t.text or "") for t in el.iter(f"{W}t")))
            add_hyperlink(texts, el.attrib.get(f"{W}anchor") or "", el.attrib.get(f"{R}id") or "")
        for child in list(el):
            walk(child, current_para, current_style)

    walk(body, None, "")
    if field_on:
        flush_field()

    for bid, name in open_ids.items():
        if name in bookmarks and not bookmarks[name]["text"]:
            bookmarks[name]["text"] = norm("".join(buffers.get(bid) or []))

    for link in hyperlinks:
        anchor = link.get("anchor") or ""
        bm = bookmarks.get(anchor) or {}
        link["target_heading"] = bm.get("heading") or ""
        link["target_text"] = bm.get("text") or ""

    unique: list[dict] = []
    seen_links: set[tuple] = set()
    for link in hyperlinks:
        key = (
            norm(link.get("text")).casefold(),
            (link.get("anchor") or "").casefold(),
            bool(link.get("external")),
        )
        if key in seen_links:
            continue
        seen_links.add(key)
        unique.append(link)
    hyperlinks = unique

    extra = _manage_document_facts(root, extra_parts)
    skills = _skill_facts(root, extra_parts, names)
    return {
        "ok": True,
        "bookmarks": bookmarks,
        "internal_hyperlinks": [h for h in hyperlinks if h.get("anchor") and not h.get("external")],
        "external_hyperlinks": [h for h in hyperlinks if h.get("external")],
        "headings": headings,
        **extra,
        **skills,
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


def _parse_xml(raw: bytes | None) -> ET.Element | None:
    if not raw:
        return None
    try:
        return ET.fromstring(raw)
    except ET.ParseError:
        return None


def _numbering_formats(raw: bytes | None) -> tuple[dict[str, str], list[str]]:
    """Map numId -> ilvl0 numFmt; unique format names."""
    node = _parse_xml(raw)
    if node is None:
        return {}, []
    abstracts: dict[str, str] = {}
    for absn in node.findall(f"{W}abstractNum"):
        aid = _attr(absn, "abstractNumId")
        for lvl in absn.findall(f"{W}lvl"):
            if _attr(lvl, "ilvl") in ("", "0"):
                abstracts[aid] = _attr(lvl.find(f"{W}numFmt"), "val")
                break
    mapping: dict[str, str] = {}
    for num in node.findall(f"{W}num"):
        nid = _attr(num, "numId")
        aid = _attr(num.find(f"{W}abstractNumId"), "val")
        mapping[nid] = abstracts.get(aid) or ""
    formats = sorted({fmt for fmt in mapping.values() if fmt})
    return mapping, formats


def _numbering_restarts(raw: bytes | None) -> int:
    """Số lần đánh số được bắt đầu lại.

    Word ghi "Restart at 1" thành một <w:num> mới mang <w:lvlOverride> với
    <w:startOverride>. Đếm số w:num có startOverride chính là đếm số lần
    restart — đo đúng thao tác, khác hẳn việc đếm số đoạn ListParagraph.
    """
    node = _parse_xml(raw)
    if node is None:
        return 0
    count = 0
    for num in node.findall(f"{W}num"):
        for override in num.findall(f"{W}lvlOverride"):
            if override.find(f"{W}startOverride") is not None:
                count += 1
                break
    return count


def _skill_facts(root: ET.Element, parts: dict[str, bytes | None], names: list[str]) -> dict:
    parts = parts or {}
    names = names or []
    numbering_map, numbering_formats = _numbering_formats(parts.get("word/numbering.xml"))
    numbering_restarts = _numbering_restarts(parts.get("word/numbering.xml"))

    paragraphs: list[dict] = []
    text_effects: list[str] = []
    fields: list[str] = []
    style_counts: dict[str, int] = {}
    breaks: dict[str, int] = {}
    body_chunks: list[str] = []

    for p in root.iter(f"{W}p"):
        ppr = p.find(f"{W}pPr")
        style = ""
        num_id = ""
        if ppr is not None:
            style = _attr(ppr.find(f"{W}pStyle"), "val")
            np = ppr.find(f"{W}numPr")
            if np is not None:
                num_id = _attr(np.find(f"{W}numId"), "val")
        text = norm("".join((t.text or "") for t in p.iter(f"{W}t")))
        if text:
            body_chunks.append(text)
        if style:
            style_counts[style] = style_counts.get(style, 0) + 1
        fmt = numbering_map.get(num_id) or ""
        if text or style or fmt:
            paragraphs.append({"text": text, "style": style, "num_fmt": fmt, "num_id": num_id})
        for instr in p.iter(f"{W}instrText"):
            if instr.text:
                fields.append(instr.text.strip())
        for r in p.findall(f"{W}r"):
            rpr = r.find(f"{W}rPr")
            if rpr is None:
                continue
            if rpr.find(f"{W14}textOutline") is not None or rpr.find(f"{W14}props3d") is not None:
                run_txt = norm("".join((t.text or "") for t in r.findall(f"{W}t")))
                if run_txt:
                    text_effects.append(run_txt)
        for br in p.iter(f"{W}br"):
            kind = _attr(br, "type") or "textWrapping"
            breaks[kind] = breaks.get(kind, 0) + 1

    sections: list[dict] = []
    for sect in root.iter(f"{W}sectPr"):
        cols = sect.find(f"{W}cols")
        pg = sect.find(f"{W}pgSz")
        sections.append(
            {
                "cols": int(_attr(cols, "num") or "1"),
                "orient": _attr(pg, "orient") or "portrait",
                "w": _attr(pg, "w"),
                "h": _attr(pg, "h"),
            }
        )
    breaks["section"] = max(0, len(sections))

    tables: list[dict] = []
    for tbl in root.iter(f"{W}tbl"):
        rows = list(tbl.findall(f"{W}tr"))
        header = False
        merged = False
        header_rows: list[int] = []
        cells: list[list[str]] = []
        for index, tr in enumerate(rows):
            trpr = tr.find(f"{W}trPr")
            if trpr is not None and trpr.find(f"{W}tblHeader") is not None:
                header = True
                header_rows.append(index)
            row: list[str] = []
            for tc in tr.findall(f"{W}tc"):
                row.append(norm("".join((t.text or "") for t in tc.iter(f"{W}t"))))
                tcpr = tc.find(f"{W}tcPr")
                if tcpr is not None and (
                    tcpr.find(f"{W}gridSpan") is not None
                    or tcpr.find(f"{W}vMerge") is not None
                    or tcpr.find(f"{W}hMerge") is not None
                ):
                    merged = True
            cells.append(row)
        tables.append(
            {
                "rows": len(rows),
                "cols": max((len(r) for r in cells), default=0),
                "header": header,
                # Chỉ số các hàng mang w:tblHeader. Không có thì coi hàng đầu là
                # tiêu đề — đủ để phân biệt ô tiêu đề với ô dữ liệu.
                "header_rows": header_rows or ([0] if cells else []),
                "merged": merged,
                "cells": cells,
            }
        )

    footnote_texts: list[str] = []
    fn = _parse_xml(parts.get("word/footnotes.xml"))
    if fn is not None:
        for el in fn.iter(f"{W}footnote"):
            if el.attrib.get(f"{W}type") in ("separator", "continuationSeparator"):
                continue
            txt = norm("".join((t.text or "") for t in el.iter(f"{W}t")))
            if txt:
                footnote_texts.append(txt)

    drawings: list[dict] = []
    drawing_kinds: set[str] = set()
    drawing_texts: list[str] = []
    artistic_effects: list[str] = []
    picture_effects: list[str] = []
    alt_texts: list[str] = []
    wraps: list[str] = []
    for drawing in list(root.iter(f"{W}drawing")) + list(root.iter(f"{W}pict")):
        docpr = None
        for el in drawing.iter():
            if el.tag == f"{WP}docPr":
                docpr = el
                break
        name = (docpr.attrib.get("name") if docpr is not None else "") or ""
        descr = (docpr.attrib.get("descr") if docpr is not None else "") or ""
        if descr:
            alt_texts.append(descr)
        kind = "drawing"
        lname = name.casefold()
        if "3d" in lname or "model" in lname:
            kind = "model3d"
        elif "diagram" in lname or "smart" in lname:
            kind = "smartart"
        elif "text box" in lname or "textbox" in lname:
            kind = "textbox"
        elif "picture" in lname:
            kind = "picture"
        elif name:
            kind = "shape"
        for el in drawing.iter():
            tag = _local(el.tag)
            if tag.startswith("wrap") and tag != "wrapPolygon":
                wraps.append(tag)
            if tag.startswith("artistic"):
                artistic_effects.append(tag)
            if tag in ("innerShdw", "outerShdw", "glow", "softEdge", "reflection", "effectLst"):
                if tag != "effectLst":
                    picture_effects.append(tag)
            if tag == "t" and el.text:
                drawing_texts.append(norm(el.text))
        for txbx in drawing.iter(f"{W}txbxContent"):
            txt = norm("".join((t.text or "") for t in txbx.iter(f"{W}t")))
            if txt:
                drawing_texts.append(txt)
                kind = "textbox" if kind == "drawing" else kind
        drawing_kinds.add(kind)
        drawings.append({"name": name, "descr": descr, "kind": kind})

    comments: list[dict] = []
    resolved_count = 0
    reply_count = 0
    comments_xml = _parse_xml(parts.get("word/comments.xml"))
    ext_xml = _parse_xml(parts.get("word/commentsExtended.xml"))
    ext_by_pid: dict[str, dict] = {}
    if ext_xml is not None:
        for ex in ext_xml:
            pid = ex.attrib.get(f"{W15}paraId") or ""
            ext_by_pid[pid] = {
                "done": (ex.attrib.get(f"{W15}done") or "0") == "1",
                "parent": ex.attrib.get(f"{W15}paraIdParent") or "",
            }
            if ext_by_pid[pid]["done"]:
                resolved_count += 1
            if ext_by_pid[pid]["parent"]:
                reply_count += 1
    if comments_xml is not None:
        for c in comments_xml.iter(f"{W}comment"):
            pid = ""
            for p in c.findall(f"{W}p"):
                pid = p.attrib.get(f"{W14}paraId") or pid
            meta = ext_by_pid.get(pid) or {}
            comments.append(
                {
                    "author": c.attrib.get(f"{W}author") or "",
                    "text": norm("".join((t.text or "") for t in c.iter(f"{W}t"))),
                    "done": bool(meta.get("done")),
                    "reply": bool(meta.get("parent")),
                }
            )

    settings = _parse_xml(parts.get("word/settings.xml"))
    document_protection = False
    protection_edit = ""
    protection_enforced = False
    personal_info_removed = False
    compatibility_mode = ""
    if settings is not None:
        node = settings.find(f"{W}documentProtection")
        document_protection = node is not None
        if node is not None:
            # Kiểu khóa: trackedChanges / comments / readOnly / forms.
            # Chỉ "có phần tử" thì không phân biệt được Lock Tracking với
            # bất kỳ kiểu Restrict Editing nào khác.
            protection_edit = _attr(node, "edit")
            protection_enforced = _attr(node, "enforcement") in ("1", "true", "on")
        # Dấu vết Inspect Document > Remove All (Document Properties and
        # Personal Information): Word ghi w:removePersonalInformation vào
        # settings.xml và xóa dc:creator. Check Compatibility không đổi tệp,
        # nhưng Word 2019 lưu compatibilityMode=15 sau khi tệp được nâng cấp.
        rpi = settings.find(f"{W}removePersonalInformation")
        personal_info_removed = rpi is not None and _attr(rpi, "val") not in ("0", "false", "off")
        for cs in settings.iter(f"{W}compatSetting"):
            if _attr(cs, "name") == "compatibilityMode":
                compatibility_mode = _attr(cs, "val")

    lower_names = [n.casefold() for n in names]
    has_picture = any("word/media/" in n and n.endswith((".png", ".jpeg", ".jpg", ".emf", ".wmf")) for n in lower_names)
    has_3d = any(n.endswith(".glb") or "model3d" in n for n in lower_names)
    has_smartart = any("/diagrams/" in n or "diagram" in n for n in lower_names)
    has_hdphoto = any(n.endswith(".wdp") or "hdphoto" in n for n in lower_names)
    if has_3d:
        drawing_kinds.add("model3d")
    if has_smartart:
        drawing_kinds.add("smartart")

    return {
        "document_text": "\n".join(body_chunks),
        "paragraphs": paragraphs,
        "text_effects": text_effects,
        "sections": sections,
        "breaks": breaks,
        "tables": tables,
        "numbering_formats": numbering_formats,
        "numbering_restarts": numbering_restarts,
        "footnote_count": len(footnote_texts),
        "footnote_texts": footnote_texts,
        "fields": fields,
        "style_counts": style_counts,
        "drawings": drawings,
        "drawing_kinds": sorted(drawing_kinds),
        "drawing_texts": drawing_texts,
        "artistic_effects": artistic_effects,
        "picture_effects": picture_effects,
        "alt_texts": alt_texts,
        "wraps": wraps,
        "comments": comments,
        "resolved_count": resolved_count,
        "reply_count": reply_count,
        "document_protection": document_protection,
        "document_protection_edit": protection_edit,
        "document_protection_enforced": protection_enforced,
        "personal_info_removed": personal_info_removed,
        "compatibility_mode": compatibility_mode,
        "has_picture": has_picture,
        "has_3d": has_3d,
        "has_smartart": has_smartart,
        "has_hdphoto": has_hdphoto,
    }
