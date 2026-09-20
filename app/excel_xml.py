"""Open XML facts for Excel — sheets, names, tables, formulas, charts."""
from __future__ import annotations

import zipfile
from pathlib import Path
from xml.etree import ElementTree as ET

from app.word_xml import MAX_PART, norm

A = "{http://schemas.openxmlformats.org/drawingml/2006/main}"
C = "{http://schemas.openxmlformats.org/drawingml/2006/chart}"
R = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
X = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
XR = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
CP = "{http://schemas.openxmlformats.org/package/2006/metadata/core-properties}"
DC = "{http://purl.org/dc/elements/1.1/}"
X14 = "{http://schemas.microsoft.com/office/spreadsheetml/2009/9/main}"
MC = "{http://schemas.openxmlformats.org/markup-compatibility/2006}"


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
        "sheet_names": [],
        "hidden_sheets": [],
        "defined_names": [],
        "table_names": [],
        "table_styles": [],
        "table_totals": [],
        "formulas": [],
        "formula_funcs": [],
        "unique_values": [],
        "number_formats": [],
        "freeze_cells": [],
        "tab_colors": [],
        "print_orient": [],
        "header_footers": [],
        "autofilter": [],
        "sort_refs": [],
        "merged": 0,
        "chart_count": 0,
        "chart_titles": [],
        "sparkline_count": 0,
        "cf_count": 0,
        "validation_count": 0,
        "hyperlinks": [],
        "comments": [],
        "core_properties": {},
        "workbook_theme": "",
        "show_gridlines": [],
        "page_breaks": 0,
        "protection": False,
        "rel_types": [],
        "keywords": "",
        "print_areas": [],
        "filter_ops": [],
        "alt_texts": [],
        "decorative": False,
        "chart_styles": [],
    }


def _col_row(ref: str) -> tuple[str, int]:
    letters = "".join(ch for ch in ref if ch.isalpha())
    digits = "".join(ch for ch in ref if ch.isdigit())
    return letters, int(digits or 0)


def extract_xlsx_facts(path: Path) -> dict:
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

        shared: list[str] = [""]
        sst = read("xl/sharedStrings.xml")
        if sst is not None:
            shared = []
            for si in sst.findall(f"{X}si"):
                shared.append(_text(si))

        styles = read("xl/styles.xml")
        num_fmts: dict[int, str] = {}
        xf_num: list[int] = []
        if styles is not None:
            for fmt in styles.iter(f"{X}numFmt"):
                num_fmts[int(fmt.attrib.get("numFmtId") or 0)] = fmt.attrib.get("formatCode") or ""
            cell_xfs = styles.find(f"{X}cellXfs")
            if cell_xfs is not None:
                for xf in cell_xfs.findall(f"{X}xf"):
                    xf_num.append(int(xf.attrib.get("numFmtId") or 0))

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
            keys = props.find(f"{CP}keywords")
            if keys is not None and keys.text:
                core["keywords"] = norm(keys.text)

        theme = ""
        theme_xml = read("xl/theme/theme1.xml")
        if theme_xml is not None:
            for el in theme_xml.iter(f"{A}theme"):
                theme = el.attrib.get("name") or theme

        wb = read("xl/workbook.xml")
        sheet_meta: list[tuple[str, str, str]] = []
        defined: list[str] = []
        print_areas: list[str] = []
        protection = False
        if wb is not None:
            protection = wb.find(f"{X}workbookProtection") is not None
            for el in wb.iter(f"{X}definedName"):
                name = el.attrib.get("name") or ""
                if name.startswith("_xlnm.Print_Area"):
                    print_areas.append(norm(el.text or ""))
                elif name and not name.startswith("_xlnm") and not name.startswith("_xlchart"):
                    defined.append(norm(name))
            rels = read("xl/_rels/workbook.xml.rels")
            rid_to: dict[str, str] = {}
            if rels is not None:
                for rel in rels:
                    rid = rel.attrib.get("Id") or ""
                    target = (rel.attrib.get("Target") or "").lstrip("/")
                    if rid and target:
                        if not target.startswith("xl/"):
                            target = "xl/" + target
                        rid_to[rid] = target
            for sh in wb.iter(f"{X}sheet"):
                rid = sh.attrib.get(f"{{{XR}}}id") or sh.attrib.get(f"{R}id") or ""
                sheet_meta.append(
                    (
                        norm(sh.attrib.get("name") or ""),
                        rid_to.get(rid, ""),
                        sh.attrib.get("state") or "visible",
                    )
                )

        sheet_names = [n for n, _p, _s in sheet_meta]
        hidden = [n for n, _p, state in sheet_meta if state != "visible"]
        formulas: list[str] = []
        formula_funcs: list[str] = []
        values: list[str] = []
        used_formats: list[str] = []
        freeze: list[str] = []
        tab_colors: list[str] = []
        orients: list[str] = []
        headers: list[str] = []
        autofilter: list[str] = []
        sort_refs: list[str] = []
        merged = 0
        spark = 0
        cf = 0
        validations = 0
        hyperlinks: list[str] = []
        comments: list[str] = []
        gridlines: list[str] = []
        page_breaks = 0
        table_names: list[str] = []
        table_styles: list[str] = []
        table_totals: list[str] = []
        rel_types: list[str] = []
        charts = 0
        chart_titles: list[str] = []
        filter_ops: list[str] = []
        alt_texts: list[str] = []
        decorative = False
        chart_styles: list[str] = []

        for sheet_name, spath, _state in sheet_meta:
            root = read(spath)
            if root is None:
                continue
            tab = root.find(f"{X}sheetPr/{X}tabColor")
            if tab is None:
                spr = root.find(f"{X}sheetPr")
                tab = spr.find(f"{X}tabColor") if spr is not None else None
            if tab is not None:
                tab_colors.append((tab.attrib.get("rgb") or tab.attrib.get("theme") or "").upper())
            view = root.find(f"{X}sheetViews/{X}sheetView")
            if view is not None:
                pane = view.find(f"{X}pane")
                if pane is not None:
                    freeze.append(pane.attrib.get("topLeftCell") or f"{pane.attrib.get('ySplit')}:{pane.attrib.get('xSplit')}")
                if view.attrib.get("showGridLines") == "0":
                    gridlines.append(sheet_name)
            setup = root.find(f"{X}pageSetup")
            if setup is not None:
                orients.append((setup.attrib.get("orientation") or "").lower())
            hf = root.find(f"{X}headerFooter")
            if hf is not None:
                blob = _text(hf)
                if blob:
                    headers.append(blob)
            af = root.find(f"{X}autoFilter")
            if af is not None:
                autofilter.append(f"{sheet_name}:{af.attrib.get('ref') or ''}")
            for sort in root.iter(f"{X}sortState"):
                sort_refs.append(sort.attrib.get("ref") or "")
            merged += len(list(root.iter(f"{X}mergeCell")))
            cf += len(list(root.iter(f"{X}conditionalFormatting")))
            validations += len(list(root.iter(f"{X}dataValidation")))
            page_breaks += len(list(root.iter(f"{X}brk")))
            spark += sum(1 for el in root.iter() if _local(el.tag) in {"sparklineGroup", "sparkline"})
            for hyper in root.iter(f"{X}hyperlink"):
                display = hyper.attrib.get("display") or hyper.attrib.get("tooltip") or hyper.attrib.get(f"{{{XR}}}id") or ""
                if display:
                    hyperlinks.append(display)
            for cell in root.iter(f"{X}c"):
                ref = cell.attrib.get("r") or ""
                kind = cell.attrib.get("t") or ""
                style = cell.attrib.get("s")
                if style and style.isdigit() and int(style) < len(xf_num):
                    nid = xf_num[int(style)]
                    code = num_fmts.get(nid, str(nid))
                    if code and code not in {"0", "General"}:
                        used_formats.append(code)
                formula = cell.find(f"{X}f")
                if formula is not None:
                    text = (formula.text or "").strip()
                    if text:
                        formulas.append(text)
                        func = text.lstrip("=").split("(", 1)[0].split("!")[-1]
                        if func:
                            formula_funcs.append(func.upper())
                value_el = cell.find(f"{X}v")
                raw = (value_el.text or "").strip() if value_el is not None else ""
                if kind == "s" and raw.isdigit() and int(raw) < len(shared):
                    values.append(shared[int(raw)])
                elif kind == "inlineStr":
                    values.append(_text(cell.find(f"{X}is")))
                elif raw and kind != "e":
                    values.append(raw)
                _ = ref
            srels = read(spath.replace("xl/worksheets/", "xl/worksheets/_rels/") + ".rels")
            if srels is not None:
                for rel in srels:
                    typ = (rel.attrib.get("Type") or "").rsplit("/", 1)[-1]
                    target = rel.attrib.get("Target") or ""
                    rel_types.append(typ)
                    if typ == "table":
                        tpath = "xl/tables/" + Path(target).name
                        table = read(tpath)
                        if table is not None:
                            tname = table.attrib.get("displayName") or table.attrib.get("name") or ""
                            if tname:
                                table_names.append(norm(tname))
                            style = table.find(f"{X}tableStyleInfo")
                            if style is not None:
                                table_styles.append(style.attrib.get("name") or "")
                            if table.attrib.get("totalsRowCount") == "1":
                                table_totals.append(tname)
                            for flt in table.iter():
                                if _local(flt.tag) in {"customFilter", "filter"}:
                                    op = flt.attrib.get("operator") or "eq"
                                    val = flt.attrib.get("val") or ""
                                    if val:
                                        filter_ops.append(f"{op}:{val}")
                            for sort in table.iter():
                                if _local(sort.tag) == "sortState":
                                    sort_refs.append(sort.attrib.get("ref") or "")
                    if typ == "comments":
                        comments.append(Path(target).name)
                    if "hyperlink" in typ.casefold() and target:
                        hyperlinks.append(target)

        for name in sorted(n for n in names if n.startswith("xl/chartsheets/")):
            if name.endswith(".xml") and "/_rels/" not in name:
                sheet_names.append(Path(name).stem)
        for name in sorted(n for n in names if n.startswith("xl/charts/") and n.endswith(".xml")):
            if Path(name).name.startswith("chart"):
                charts += 1
            chart = read(name)
            if chart is None:
                continue
            title = ""
            for el in chart.iter(f"{C}title"):
                title = _text(el) or title
            if title:
                chart_titles.append(norm(title))
            for el in chart.iter():
                if _local(el.tag) == "style" and el.attrib.get("val"):
                    chart_styles.append(el.attrib.get("val") or "")
        for name in sorted(n for n in names if n.startswith("xl/drawings/") and n.endswith(".xml") and "/_rels/" not in name):
            drawing = read(name)
            if drawing is None:
                continue
            for el in drawing.iter():
                descr = el.attrib.get("descr") or ""
                title = el.attrib.get("title") or ""
                if descr:
                    alt_texts.append(norm(descr))
                if title:
                    alt_texts.append(norm(title))
                if _local(el.tag) == "decorative" and el.attrib.get("val") == "1":
                    decorative = True
        for name in sorted(n for n in names if "comments" in n and n.endswith(".xml")):
            root = read(name)
            if root is None:
                continue
            for el in root.iter():
                if _local(el.tag) in {"text", "t"}:
                    blob = _text(el)
                    if blob:
                        comments.append(blob)

        unique = [v for v in dict.fromkeys(values) if v]
        document_text = "\n".join(unique)
        comment_texts = [c for c in dict.fromkeys(comments) if c and not str(c).endswith(".xml")]
        return {
            "ok": True,
            "error": "",
            "document_text": document_text,
            "sheet_names": sheet_names,
            "hidden_sheets": hidden,
            "defined_names": defined,
            "table_names": table_names,
            "table_styles": table_styles,
            "table_totals": table_totals,
            "formulas": formulas,
            "formula_funcs": [f for f in dict.fromkeys(formula_funcs) if f],
            "unique_values": unique,
            "number_formats": [f for f in dict.fromkeys(used_formats) if f],
            "freeze_cells": freeze,
            "tab_colors": [c for c in dict.fromkeys(tab_colors) if c],
            "print_orient": orients,
            "header_footers": headers,
            "autofilter": autofilter,
            "sort_refs": [s for s in dict.fromkeys(sort_refs) if s],
            "merged": merged,
            "chart_count": charts,
            "chart_titles": chart_titles,
            "sparkline_count": spark,
            "cf_count": cf,
            "validation_count": validations,
            "hyperlinks": hyperlinks,
            "comments": comment_texts,
            "comment_count": len(comment_texts),
            "core_properties": core,
            "workbook_theme": theme,
            "theme_name": theme,
            "header_texts": headers,
            "show_gridlines": gridlines,
            "page_breaks": page_breaks,
            "protection": protection,
            "rel_types": rel_types,
            "keywords": (core.get("keywords") or ""),
            "print_areas": print_areas,
            "filter_ops": filter_ops,
            "alt_texts": alt_texts,
            "decorative": decorative,
            "chart_styles": [s for s in dict.fromkeys(chart_styles) if s],
        }


def summarize_facts(facts: dict) -> dict:
    keys = (
        "sheet_names",
        "hidden_sheets",
        "defined_names",
        "table_names",
        "table_styles",
        "table_totals",
        "formula_funcs",
        "number_formats",
        "freeze_cells",
        "tab_colors",
        "print_orient",
        "header_footers",
        "autofilter",
        "sort_refs",
        "merged",
        "chart_count",
        "chart_titles",
        "sparkline_count",
        "cf_count",
        "validation_count",
        "hyperlinks",
        "comments",
        "core_properties",
        "workbook_theme",
        "show_gridlines",
        "page_breaks",
        "protection",
    )
    return {k: facts.get(k) for k in keys}
