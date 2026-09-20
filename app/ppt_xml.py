"""Open XML facts for PowerPoint — slides, theme, media, transitions."""
from __future__ import annotations

import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from app.word_xml import MAX_PART, norm

A = "{http://schemas.openxmlformats.org/drawingml/2006/main}"
P = "{http://schemas.openxmlformats.org/presentationml/2006/main}"
R = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
CP = "{http://schemas.openxmlformats.org/package/2006/metadata/core-properties}"
DC = "{http://purl.org/dc/elements/1.1/}"
CP_EXT = "{http://schemas.openxmlformats.org/officeDocument/2006/extended-properties}"
REL_NS = "{http://schemas.openxmlformats.org/package/2006/relationships}"


def _local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _text(el: ET.Element | None) -> str:
    if el is None:
        return ""
    return norm(" ".join((t or "").strip() for t in el.itertext() if (t or "").strip()))


def _empty(error: str) -> dict:
    return {
        "ok": False,
        "error": error,
        "document_text": "",
        "slide_count": 0,
        "slide_texts": [],
        "hidden_slides": [],
        "layout_names": [],
        "theme_name": "",
        "slide_cx": 0,
        "slide_cy": 0,
        "core_properties": {},
        "notes_texts": [],
        "transitions": [],
        "animation_count": 0,
        "table_count": 0,
        "chart_count": 0,
        "picture_count": 0,
        "smartart_count": 0,
        "has_3d": False,
        "has_media": False,
        "hyperlinks": [],
        "section_names": [],
        "custom_shows": [],
        "footers": [],
        "hide_bg": [],
        "rel_types": [],
        "scheme_colors": [],
    }


def extract_ppt_facts(path: Path) -> dict:
    path = Path(path)
    if not path.is_file():
        return _empty("missing")
    try:
        zf = zipfile.ZipFile(path)
    except zipfile.BadZipFile:
        return _empty("bad_zip")
    with zf:
        names = set(zf.namelist())

        def read(name: str) -> ET.Element | None:
            if name not in names:
                return None
            raw = zf.read(name)
            if len(raw) > MAX_PART:
                return None
            try:
                return ET.fromstring(raw)
            except ET.ParseError:
                return None

        theme = ""
        for name in sorted(n for n in names if n.startswith("ppt/theme/") and n.endswith(".xml")):
            root = read(name)
            if root is None:
                continue
            for el in root.iter(f"{A}theme"):
                theme = el.attrib.get("name") or theme
        core: dict[str, str] = {}
        props = read("docProps/core.xml")
        if props is not None:
            for tag in ("title", "subject", "creator", "description"):
                el = props.find(f"{DC}{tag}")
                if el is not None and el.text:
                    core[tag] = norm(el.text)
            status = props.find(f"{CP}contentStatus")
            if status is not None and status.text:
                core["status"] = norm(status.text)
        cx = cy = 0
        pres = read("ppt/presentation.xml")
        hidden: list[int] = []
        sections: list[str] = []
        shows: list[str] = []
        sld_ids: list[str] = []
        if pres is not None:
            sld_sz = pres.find(f"{P}sldSz")
            if sld_sz is not None:
                cx = int(sld_sz.attrib.get("cx") or 0)
                cy = int(sld_sz.attrib.get("cy") or 0)
            for el in pres.iter(f"{P}sldId"):
                sld_ids.append(el.attrib.get("id") or "")
            for el in pres.iter():
                if _local(el.tag) == "sldLst":
                    continue
            for el in pres.iter():
                if _local(el.tag) == "sldId" and el.attrib.get("show") == "0":
                    hidden.append(len(hidden) + 1)
            for el in pres.iter():
                if _local(el.tag) in {"sldId"} and el.attrib.get(f"{{{R}}}id"):
                    pass
            for el in pres.iter():
                if _local(el.tag) == "sldSection" or _local(el.tag) == "section":
                    name = el.attrib.get("name")
                    if name:
                        sections.append(norm(name))
            for el in pres.iter():
                if _local(el.tag) == "custShow":
                    name = el.attrib.get("name")
                    if name:
                        shows.append(norm(name))

        rels = read("ppt/_rels/presentation.xml.rels")
        rid_to_target: dict[str, str] = {}
        if rels is not None:
            for rel in rels:
                rid = rel.attrib.get("Id") or ""
                target = rel.attrib.get("Target") or ""
                if rid and target:
                    rid_to_target[rid] = target.lstrip("/")

        slide_files: list[str] = []
        if pres is not None:
            for el in pres.iter(f"{P}sldId"):
                rid = el.attrib.get(f"{{{R}}}id") or ""
                target = rid_to_target.get(rid, "")
                if target:
                    if not target.startswith("ppt/"):
                        target = "ppt/" + target
                    slide_files.append(target)
                if el.attrib.get("show") == "0":
                    hidden.append(len(slide_files))
        if not slide_files:
            slide_files = sorted(n for n in names if n.startswith("ppt/slides/slide") and n.endswith(".xml"))

        slide_texts: list[str] = []
        layouts: list[str] = []
        notes: list[str] = []
        transitions: list[str] = []
        animations = 0
        tables = charts = pictures = smartart = 0
        has_3d = False
        has_media = False
        hyperlinks: list[str] = []
        hide_bg: list[int] = []
        rel_types: list[str] = []
        footers: list[str] = []
        scheme_colors: list[str] = []

        for i, sname in enumerate(slide_files, start=1):
            root = read(sname)
            if root is None:
                slide_texts.append("")
                continue
            blob = _text(root)
            slide_texts.append(blob)
            for el in root.iter(f"{A}schemeClr"):
                val = el.attrib.get("val") or ""
                if val:
                    scheme_colors.append(val)
            if root.attrib.get("showMasterSp") == "0":
                hide_bg.append(i)
            for el in root.iter(f"{P}transition"):
                kind = next((_local(child.tag) for child in list(el) if _local(child.tag) != "sndAc"), "")
                dur = el.attrib.get("advTm") or el.attrib.get("spd") or ""
                transitions.append(f"{kind}:{dur}".strip(":"))
            timing = root.find(f"{P}timing")
            if timing is not None:
                animations += sum(1 for el in timing.iter() if _local(el.tag) in {"anim", "animEffect", "animMotion", "set", "animScale"})
            tables += sum(1 for el in root.iter(f"{A}tbl"))
            for hf in root.iter(f"{P}hf"):
                pass
            for el in root.iter(f"{P}ftr"):
                t = _text(el)
                if t:
                    footers.append(t)
            srels_name = sname.replace("ppt/slides/", "ppt/slides/_rels/") + ".rels"
            srels = read(srels_name)
            if srels is not None:
                for rel in srels:
                    typ = (rel.attrib.get("Type") or "").rsplit("/", 1)[-1]
                    target = rel.attrib.get("Target") or ""
                    rel_types.append(typ)
                    if typ == "slideLayout":
                        layout_path = "ppt/slideLayouts/" + Path(target).name
                        layout = read(layout_path)
                        c_sld = layout.find(f"{P}cSld") if layout is not None else None
                        layouts.append((c_sld.attrib.get("name") if c_sld is not None else "") or Path(target).stem)
                    if typ == "notesSlide":
                        npath = "ppt/notesSlides/" + Path(target).name
                        notes.append(_text(read(npath)))
                    if typ == "chart":
                        charts += 1
                    if typ in {"image", "video", "audio", "media"}:
                        if typ == "image":
                            pictures += 1
                        else:
                            has_media = True
                    if typ == "diagram":
                        smartart += 1
                    if "hyperlink" in typ.casefold() or rel.attrib.get("TargetMode") == "External":
                        if target:
                            hyperlinks.append(target)
            if any("model3d" in n.casefold() or n.endswith(".glb") or n.endswith(".3mf") for n in names):
                has_3d = True
            for el in root.iter():
                if "model3d" in _local(el.tag).casefold():
                    has_3d = True
                if _local(el.tag) in {"videoFile", "audioFile"}:
                    has_media = True
                if _local(el.tag) == "hlinkClick":
                    href = el.attrib.get(f"{{{R}}}id") or ""
                    if href:
                        hyperlinks.append(href)

        document_text = "\n".join(t for t in slide_texts if t)
        return {
            "ok": True,
            "error": "",
            "document_text": document_text,
            "slide_count": len(slide_files),
            "slide_texts": slide_texts,
            "hidden_slides": sorted(set(hidden)),
            "layout_names": layouts,
            "theme_name": theme,
            "slide_cx": cx,
            "slide_cy": cy,
            "core_properties": core,
            "notes_texts": [t for t in notes if t],
            "transitions": transitions,
            "animation_count": animations,
            "table_count": tables,
            "chart_count": charts,
            "picture_count": pictures,
            "smartart_count": smartart,
            "has_3d": has_3d,
            "has_media": has_media,
            "hyperlinks": hyperlinks,
            "section_names": sections,
            "custom_shows": shows,
            "footers": footers,
            "hide_bg": hide_bg,
            "rel_types": rel_types,
            "scheme_colors": scheme_colors,
        }
