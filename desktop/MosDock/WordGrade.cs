using System.IO.Compression;
using System.Xml.Linq;

namespace MosDock;

sealed class WordFacts
{
    public bool Ok { get; init; } = true;
    public string? Error { get; init; }
    public Dictionary<string, BookmarkFact> Bookmarks { get; } = new(StringComparer.Ordinal);
    public List<HyperlinkFact> InternalHyperlinks { get; } = [];
    public List<HyperlinkFact> ExternalHyperlinks { get; } = [];
    public string PageBackground { get; set; } = "";
    public List<string> Watermarks { get; } = [];
    public bool PageBorder { get; set; }
    public bool FirstPageHeader { get; set; }
    public List<string> HeaderTexts { get; } = [];
    public List<string> HeaderInstructions { get; } = [];
    public Dictionary<string, string> Core { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int CommentCount { get; set; }
    public int RevisionCount { get; set; }
    public bool TrackRevisions { get; set; }
    public int VanishCount { get; set; }
    public string Heading1Sz { get; set; } = "";
    public string DocumentText { get; set; } = "";
    public List<ParaFact> Paragraphs { get; } = [];
    public List<string> TextEffects { get; } = [];
    public List<SectionFact> Sections { get; } = [];
    public Dictionary<string, int> Breaks { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<TableFact> Tables { get; } = [];
    public List<string> NumberingFormats { get; } = [];
    public int FootnoteCount { get; set; }
    public List<string> Fields { get; } = [];
    public Dictionary<string, int> StyleCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DrawingKinds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> DrawingTexts { get; } = [];
    public List<string> ArtisticEffects { get; } = [];
    public List<string> PictureEffects { get; } = [];
    public List<string> AltTexts { get; } = [];
    public List<string> Wraps { get; } = [];
    public List<CommentFact> Comments { get; } = [];
    public int ResolvedCount { get; set; }
    public int ReplyCount { get; set; }
    public bool DocumentProtection { get; set; }
    public bool Has3d { get; set; }
    public bool HasSmartArt { get; set; }
    public bool HasHdPhoto { get; set; }
    public string ThemeName { get; set; } = "";
    public int SlideCount { get; set; }
    public int SlideCx { get; set; }
    public int SlideCy { get; set; }
    public List<string> LayoutNames { get; } = [];
    public List<string> NotesTexts { get; } = [];
    public List<string> Transitions { get; } = [];
    public int AnimationCount { get; set; }
    public int PptTableCount { get; set; }
    public int ChartCount { get; set; }
    public int PictureCount { get; set; }
    public int SmartArtCount { get; set; }
    public bool HasMedia { get; set; }
    public List<string> SectionNames { get; } = [];
    public List<string> CustomShows { get; } = [];
    public List<string> SchemeColors { get; } = [];
    public List<int> HiddenSlides { get; } = [];
    public List<string> SheetNames { get; } = [];
    public List<string> HiddenSheets { get; } = [];
    public List<string> DefinedNames { get; } = [];
    public List<string> TableNames { get; } = [];
    public List<string> TableStyles { get; } = [];
    public List<string> TableTotals { get; } = [];
    public List<string> Formulas { get; } = [];
    public List<string> FormulaFuncs { get; } = [];
    public List<string> FreezeCells { get; } = [];
    public List<string> PrintOrients { get; } = [];
    public List<string> PrintAreas { get; } = [];
    public List<string> FilterOps { get; } = [];
    public List<string> ChartTitles { get; } = [];
    public List<string> AltTextsXlsx { get; } = [];
    public int SparklineCount { get; set; }
    public int CfCount { get; set; }
    public int MergedCount { get; set; }
    public bool Decorative { get; set; }
}

sealed class ParaFact
{
    public string Text { get; init; } = "";
    public string Style { get; init; } = "";
    public string Fmt { get; init; } = "";
}

sealed class SectionFact
{
    public int Cols { get; init; } = 1;
    public string Orient { get; init; } = "portrait";
}

sealed class TableFact
{
    public int Rows { get; init; }
    public int Cols { get; init; }
    public bool Header { get; init; }
    public bool Merged { get; init; }
    public List<List<string>> Cells { get; init; } = [];
}

sealed class CommentFact
{
    public string Author { get; init; } = "";
    public string Text { get; init; } = "";
    public bool Done { get; init; }
    public bool Reply { get; init; }
}

sealed class BookmarkFact
{
    public string Name { get; init; } = "";
    public string Text { get; set; } = "";
    public string Heading { get; set; } = "";
}

sealed class HyperlinkFact
{
    public string Text { get; init; } = "";
    public string Anchor { get; init; } = "";
    public bool External { get; init; }
    public string TargetHeading { get; set; } = "";
    public string TargetText { get; set; } = "";
}

static class WordXml
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace CP = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    static readonly XNamespace DC = "http://purl.org/dc/elements/1.1/";
    static readonly XNamespace VML = "urn:schemas-microsoft-com:vml";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    static readonly XNamespace W15 = "http://schemas.microsoft.com/office/word/2012/wordml";

    public static string Norm(string? text) =>
        string.Join(" ", (text ?? "").Replace('\u00a0', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static bool Same(string? a, string? b) =>
        string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);

    public static WordFacts Extract(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new WordFacts { Ok = false, Error = "missing_file" };
        }

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var docEntry = zip.GetEntry("word/document.xml");
            if (docEntry is null)
            {
                return new WordFacts { Ok = false, Error = "not_docx" };
            }

            using var docStream = docEntry.Open();
            var root = XDocument.Load(docStream).Root;
            if (root is null)
            {
                return new WordFacts { Ok = false, Error = "bad_xml" };
            }

            var rels = new Dictionary<string, (string Target, string Mode)>(StringComparer.Ordinal);
            var relEntry = zip.GetEntry("word/_rels/document.xml.rels");
            if (relEntry is not null)
            {
                using var relStream = relEntry.Open();
                var relRoot = XDocument.Load(relStream).Root;
                if (relRoot is not null)
                {
                    foreach (var rel in relRoot.Elements())
                    {
                        var id = (string?)rel.Attribute("Id");
                        if (!string.IsNullOrEmpty(id))
                        {
                            rels[id] = ((string?)rel.Attribute("Target") ?? "", (string?)rel.Attribute("TargetMode") ?? "");
                        }
                    }
                }
            }

            var facts = new WordFacts();
            var openIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var buffers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var hyperlinks = new List<HyperlinkFact>();
            var fieldOn = false;
            var fieldInstr = new List<string>();
            var fieldText = new List<string>();

            string ParaStyle(XElement p)
            {
                var style = p.Element(W + "pPr")?.Element(W + "pStyle");
                return (string?)style?.Attribute(W + "val") ?? "";
            }

            string ParaText(XElement? p) =>
                p is null ? "" : Norm(string.Concat(p.Descendants(W + "t").Select(t => t.Value)));

            static (string Anchor, bool External) FieldAnchor(string raw)
            {
                var parts = raw.Replace("\"", " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var anchor = "";
                for (var i = 0; i < parts.Length - 1; i++)
                {
                    if (string.Equals(parts[i], "\\l", StringComparison.OrdinalIgnoreCase))
                    {
                        anchor = parts[i + 1];
                        break;
                    }
                }

                var lower = raw.ToLowerInvariant();
                var external = lower.Contains("http://") || lower.Contains("https://") || lower.Contains("mailto:");
                return (anchor, external);
            }

            void AddHyperlink(string text, string anchor, string rid, string fieldRaw = "")
            {
                // TOC / HYPERLINK fields often have no rId — missing rel must not NRE.
                rels.TryGetValue(rid ?? "", out var rel);
                var target = rel.Target ?? "";
                var mode = rel.Mode ?? "";
                if (anchor.Length == 0 && target.StartsWith("#", StringComparison.Ordinal))
                {
                    anchor = target[1..];
                }

                if (anchor.Length == 0 && fieldRaw.Length > 0)
                {
                    (anchor, _) = FieldAnchor(fieldRaw);
                }

                var external = mode == "External" || (target.Length > 0 && anchor.Length == 0);
                if (fieldRaw.Length > 0)
                {
                    var lower = fieldRaw.ToLowerInvariant();
                    if (lower.Contains("http://") || lower.Contains("https://") || lower.Contains("mailto:"))
                    {
                        external = true;
                    }
                }

                if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(anchor) && !external)
                {
                    return;
                }

                hyperlinks.Add(new HyperlinkFact { Text = text, Anchor = anchor, External = external });
            }

            void FlushField()
            {
                if (!fieldOn)
                {
                    return;
                }

                var raw = string.Concat(fieldInstr);
                if (raw.IndexOf("HYPERLINK", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AddHyperlink(Norm(string.Concat(fieldText)), "", "", raw.Trim());
                }

                fieldOn = false;
                fieldInstr.Clear();
                fieldText.Clear();
            }

            void Walk(XElement el, XElement? para, string style)
            {
                var currentPara = para;
                var currentStyle = style;
                if (el.Name == W + "p")
                {
                    currentPara = el;
                    currentStyle = ParaStyle(el);
                }

                if (el.Name == W + "bookmarkStart")
                {
                    var id = (string?)el.Attribute(W + "id") ?? "";
                    var name = (string?)el.Attribute(W + "name") ?? "";
                    if (id.Length > 0 && name.Length > 0)
                    {
                        openIds[id] = name;
                        buffers[id] = [];
                        facts.Bookmarks[name] = new BookmarkFact
                        {
                            Name = name,
                            Heading = ParaText(currentPara),
                        };
                    }
                }
                else if (el.Name == W + "bookmarkEnd")
                {
                    var id = (string?)el.Attribute(W + "id") ?? "";
                    if (openIds.Remove(id, out var name) && facts.Bookmarks.TryGetValue(name, out var bm))
                    {
                        bm.Text = Norm(string.Concat(buffers.GetValueOrDefault(id) ?? []));
                    }
                }
                else if (el.Name == W + "fldChar")
                {
                    var kind = ((string?)el.Attribute(W + "fldCharType") ?? "").ToLowerInvariant();
                    if (kind == "begin")
                    {
                        fieldOn = true;
                        fieldInstr.Clear();
                        fieldText.Clear();
                    }
                    else if (kind == "separate")
                    {
                        fieldText.Clear();
                    }
                    else if (kind == "end" && fieldOn)
                    {
                        FlushField();
                    }
                }
                else if (el.Name == W + "instrText")
                {
                    var instr = el.Value ?? "";
                    if (fieldOn)
                    {
                        fieldInstr.Add(instr);
                    }
                    else if (instr.IndexOf("HYPERLINK", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AddHyperlink("", "", "", instr.Trim());
                    }
                }
                else if (el.Name == W + "t")
                {
                    var text = el.Value;
                    if (!string.IsNullOrEmpty(text))
                    {
                        foreach (var id in openIds.Keys)
                        {
                            buffers.GetValueOrDefault(id)?.Add(text);
                        }

                        if (fieldOn)
                        {
                            fieldText.Add(text);
                        }
                    }
                }
                else if (el.Name == W + "hyperlink")
                {
                    var text = Norm(string.Concat(el.Descendants(W + "t").Select(t => t.Value)));
                    var anchor = (string?)el.Attribute(W + "anchor") ?? "";
                    var rid = (string?)el.Attribute(R + "id") ?? "";
                    AddHyperlink(text, anchor, rid);
                }

                foreach (var child in el.Elements())
                {
                    Walk(child, currentPara, currentStyle);
                }
            }

            var body = root.Element(W + "body") ?? root;
            Walk(body, null, "");
            FlushField();

            foreach (var kv in openIds)
            {
                if (facts.Bookmarks.TryGetValue(kv.Value, out var bm) && string.IsNullOrEmpty(bm.Text))
                {
                    bm.Text = Norm(string.Concat(buffers.GetValueOrDefault(kv.Key) ?? []));
                }
            }

            var unique = new List<HyperlinkFact>();
            var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var link in hyperlinks)
            {
                var key = $"{Norm(link.Text)}\0{link.Anchor}\0{link.External}";
                if (!seenLinks.Add(key))
                {
                    continue;
                }

                unique.Add(link);
            }

            foreach (var link in unique)
            {
                if (facts.Bookmarks.TryGetValue(link.Anchor, out var bm))
                {
                    link.TargetHeading = bm.Heading;
                    link.TargetText = bm.Text;
                }

                if (link.External)
                {
                    facts.ExternalHyperlinks.Add(link);
                }
                else if (!string.IsNullOrEmpty(link.Anchor))
                {
                    facts.InternalHyperlinks.Add(link);
                }
            }

            FillManage(facts, zip, root);
            FillSkills(facts, zip, root);
            return facts;
        }
        catch (InvalidDataException)
        {
            return new WordFacts { Ok = false, Error = "bad_zip" };
        }
        catch (System.Xml.XmlException)
        {
            return new WordFacts { Ok = false, Error = "bad_xml" };
        }
        catch (Exception)
        {
            return new WordFacts { Ok = false, Error = "extract_failed" };
        }
    }

    static XElement? LoadPart(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream).Root;
    }

    static void FillManage(WordFacts facts, ZipArchive zip, XElement root)
    {
        var bg = root.Element(W + "background");
        facts.PageBackground = ((string?)bg?.Attribute(W + "color") ?? "").ToUpperInvariant();
        foreach (var sect in root.Descendants(W + "sectPr"))
        {
            if (sect.Element(W + "pgBorders") is not null)
            {
                facts.PageBorder = true;
            }

            if (sect.Element(W + "titlePg") is not null)
            {
                facts.FirstPageHeader = true;
            }

            foreach (var refEl in sect.Elements(W + "headerReference").Concat(sect.Elements(W + "footerReference")))
            {
                if ((string?)refEl.Attribute(W + "type") == "first")
                {
                    facts.FirstPageHeader = true;
                }
            }
        }

        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith("word/header", StringComparison.Ordinal) && !name.StartsWith("word/footer", StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = entry.Open();
            var node = XDocument.Load(stream).Root;
            if (node is null)
            {
                continue;
            }

            var text = Norm(string.Concat(node.Descendants(W + "t").Select(t => t.Value)));
            if (text.Length > 0)
            {
                facts.HeaderTexts.Add(text);
            }

            foreach (var instr in node.Descendants(W + "instrText"))
            {
                var value = (instr.Value ?? "").Trim();
                if (value.Length > 0)
                {
                    facts.HeaderInstructions.Add(value);
                }
            }

            foreach (var shape in node.Descendants(VML + "textpath"))
            {
                var mark = (string?)shape.Attribute("string") ?? "";
                if (mark.Length > 0)
                {
                    facts.Watermarks.Add(mark);
                }
            }
        }

        var core = LoadPart(zip, "docProps/core.xml");
        if (core is not null)
        {
            facts.Core["title"] = Norm(core.Element(DC + "title")?.Value);
            facts.Core["keywords"] = Norm(core.Element(CP + "keywords")?.Value);
            facts.Core["contentStatus"] = Norm(core.Element(CP + "contentStatus")?.Value);
            facts.Core["creator"] = Norm(core.Element(DC + "creator")?.Value);
        }

        var comments = LoadPart(zip, "word/comments.xml");
        facts.CommentCount = comments?.Descendants(W + "comment").Count() ?? 0;
        facts.CommentCount += root.Descendants(W + "commentReference").Count();
        facts.RevisionCount = root.Descendants(W + "ins").Count() + root.Descendants(W + "del").Count();
        facts.VanishCount = root.Descendants(W + "vanish").Count();

        var settings = LoadPart(zip, "word/settings.xml");
        facts.TrackRevisions = settings?.Element(W + "trackRevisions") is not null;

        var styles = LoadPart(zip, "word/styles.xml");
        var heading1 = styles?.Elements(W + "style").FirstOrDefault(s => (string?)s.Attribute(W + "styleId") == "Heading1");
        facts.Heading1Sz = (string?)heading1?.Element(W + "rPr")?.Element(W + "sz")?.Attribute(W + "val") ?? "";
    }

    static string LocalName(XName name) => name.LocalName;

    static void FillSkills(WordFacts facts, ZipArchive zip, XElement root)
    {
        var numberingMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var numbering = LoadPart(zip, "word/numbering.xml");
        if (numbering is not null)
        {
            var abstracts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var abs in numbering.Elements(W + "abstractNum"))
            {
                var aid = (string?)abs.Attribute(W + "abstractNumId") ?? "";
                var lvl0 = abs.Elements(W + "lvl").FirstOrDefault(l => ((string?)l.Attribute(W + "ilvl") ?? "0") == "0");
                abstracts[aid] = (string?)lvl0?.Element(W + "numFmt")?.Attribute(W + "val") ?? "";
            }

            foreach (var num in numbering.Elements(W + "num"))
            {
                var nid = (string?)num.Attribute(W + "numId") ?? "";
                var aid = (string?)num.Element(W + "abstractNumId")?.Attribute(W + "val") ?? "";
                numberingMap[nid] = abstracts.GetValueOrDefault(aid) ?? "";
            }

            facts.NumberingFormats.AddRange(numberingMap.Values.Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        var chunks = new List<string>();
        foreach (var p in root.Descendants(W + "p"))
        {
            var style = (string?)p.Element(W + "pPr")?.Element(W + "pStyle")?.Attribute(W + "val") ?? "";
            var numId = (string?)p.Element(W + "pPr")?.Element(W + "numPr")?.Element(W + "numId")?.Attribute(W + "val") ?? "";
            var text = Norm(string.Concat(p.Descendants(W + "t").Select(t => t.Value)));
            if (text.Length > 0)
            {
                chunks.Add(text);
            }

            if (style.Length > 0)
            {
                facts.StyleCounts[style] = facts.StyleCounts.GetValueOrDefault(style) + 1;
            }

            var fmt = numberingMap.GetValueOrDefault(numId) ?? "";
            if (text.Length > 0 || style.Length > 0 || fmt.Length > 0)
            {
                facts.Paragraphs.Add(new ParaFact { Text = text, Style = style, Fmt = fmt });
            }

            foreach (var instr in p.Descendants(W + "instrText"))
            {
                var value = (instr.Value ?? "").Trim();
                if (value.Length > 0)
                {
                    facts.Fields.Add(value);
                }
            }

            foreach (var r in p.Elements(W + "r"))
            {
                var rpr = r.Element(W + "rPr");
                if (rpr is not null && (rpr.Element(W14 + "textOutline") is not null || rpr.Element(W14 + "props3d") is not null))
                {
                    var run = Norm(string.Concat(r.Elements(W + "t").Select(t => t.Value)));
                    if (run.Length > 0)
                    {
                        facts.TextEffects.Add(run);
                    }
                }
            }

            foreach (var br in p.Descendants(W + "br"))
            {
                var kind = (string?)br.Attribute(W + "type") ?? "textWrapping";
                facts.Breaks[kind] = facts.Breaks.GetValueOrDefault(kind) + 1;
            }
        }

        facts.DocumentText = string.Join("\n", chunks);
        foreach (var sect in root.Descendants(W + "sectPr"))
        {
            var cols = int.TryParse((string?)sect.Element(W + "cols")?.Attribute(W + "num"), out var n) ? n : 1;
            var orient = (string?)sect.Element(W + "pgSz")?.Attribute(W + "orient") ?? "portrait";
            facts.Sections.Add(new SectionFact { Cols = cols, Orient = orient });
        }

        facts.Breaks["section"] = facts.Sections.Count;
        foreach (var tbl in root.Descendants(W + "tbl"))
        {
            var rows = tbl.Elements(W + "tr").ToList();
            var header = false;
            var merged = false;
            var cells = new List<List<string>>();
            foreach (var tr in rows)
            {
                if (tr.Element(W + "trPr")?.Element(W + "tblHeader") is not null)
                {
                    header = true;
                }

                var row = new List<string>();
                foreach (var tc in tr.Elements(W + "tc"))
                {
                    row.Add(Norm(string.Concat(tc.Descendants(W + "t").Select(t => t.Value))));
                    var tcpr = tc.Element(W + "tcPr");
                    if (tcpr?.Element(W + "gridSpan") is not null || tcpr?.Element(W + "vMerge") is not null || tcpr?.Element(W + "hMerge") is not null)
                    {
                        merged = true;
                    }
                }

                cells.Add(row);
            }

            facts.Tables.Add(new TableFact
            {
                Rows = rows.Count,
                Cols = cells.Count == 0 ? 0 : cells.Max(r => r.Count),
                Header = header,
                Merged = merged,
                Cells = cells,
            });
        }

        var footnotes = LoadPart(zip, "word/footnotes.xml");
        if (footnotes is not null)
        {
            facts.FootnoteCount = footnotes.Elements(W + "footnote")
                .Count(el => (string?)el.Attribute(W + "type") is not ("separator" or "continuationSeparator"));
        }

        foreach (var drawing in root.Descendants(W + "drawing").Concat(root.Descendants(W + "pict")))
        {
            var docpr = drawing.Descendants(WP + "docPr").FirstOrDefault();
            var name = (string?)docpr?.Attribute("name") ?? "";
            var descr = (string?)docpr?.Attribute("descr") ?? "";
            if (descr.Length > 0)
            {
                facts.AltTexts.Add(descr);
            }

            var kind = "drawing";
            var lower = name.ToLowerInvariant();
            if (lower.Contains("3d") || lower.Contains("model"))
            {
                kind = "model3d";
            }
            else if (lower.Contains("diagram") || lower.Contains("smart"))
            {
                kind = "smartart";
            }
            else if (lower.Contains("text box") || lower.Contains("textbox"))
            {
                kind = "textbox";
            }
            else if (lower.Contains("picture"))
            {
                kind = "picture";
            }
            else if (name.Length > 0)
            {
                kind = "shape";
            }

            foreach (var el in drawing.Descendants())
            {
                var tag = LocalName(el.Name);
                if (tag.StartsWith("wrap", StringComparison.Ordinal) && tag != "wrapPolygon")
                {
                    facts.Wraps.Add(tag);
                }

                if (tag.StartsWith("artistic", StringComparison.Ordinal))
                {
                    facts.ArtisticEffects.Add(tag);
                }

                if (tag is "innerShdw" or "outerShdw" or "glow" or "softEdge" or "reflection")
                {
                    facts.PictureEffects.Add(tag);
                }

                if ((el.Name == A + "t" || el.Name == W + "t") && !string.IsNullOrWhiteSpace(el.Value))
                {
                    facts.DrawingTexts.Add(Norm(el.Value));
                }
            }

            foreach (var txbx in drawing.Descendants(W + "txbxContent"))
            {
                var txt = Norm(string.Concat(txbx.Descendants(W + "t").Select(t => t.Value)));
                if (txt.Length > 0)
                {
                    facts.DrawingTexts.Add(txt);
                    if (kind == "drawing")
                    {
                        kind = "textbox";
                    }
                }
            }

            facts.DrawingKinds.Add(kind);
        }

        var ext = LoadPart(zip, "word/commentsExtended.xml");
        var extByPid = new Dictionary<string, (bool Done, bool Reply)>(StringComparer.OrdinalIgnoreCase);
        if (ext is not null)
        {
            foreach (var ex in ext.Elements())
            {
                var pid = (string?)ex.Attribute(W15 + "paraId") ?? "";
                var done = (string?)ex.Attribute(W15 + "done") == "1";
                var parent = (string?)ex.Attribute(W15 + "paraIdParent") ?? "";
                extByPid[pid] = (done, parent.Length > 0);
                if (done)
                {
                    facts.ResolvedCount++;
                }

                if (parent.Length > 0)
                {
                    facts.ReplyCount++;
                }
            }
        }

        var comments = LoadPart(zip, "word/comments.xml");
        if (comments is not null)
        {
            foreach (var c in comments.Descendants(W + "comment"))
            {
                var pid = (string?)c.Elements(W + "p").FirstOrDefault()?.Attribute(W14 + "paraId") ?? "";
                extByPid.TryGetValue(pid, out var meta);
                facts.Comments.Add(new CommentFact
                {
                    Author = (string?)c.Attribute(W + "author") ?? "",
                    Text = Norm(string.Concat(c.Descendants(W + "t").Select(t => t.Value))),
                    Done = meta.Done,
                    Reply = meta.Reply,
                });
            }
        }

        var settings = LoadPart(zip, "word/settings.xml");
        facts.DocumentProtection = settings?.Element(W + "documentProtection") is not null;
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/').ToLowerInvariant();
            if (name.EndsWith(".glb", StringComparison.Ordinal) || name.Contains("model3d"))
            {
                facts.Has3d = true;
                facts.DrawingKinds.Add("model3d");
            }

            if (name.Contains("/diagrams/") || name.Contains("diagram"))
            {
                facts.HasSmartArt = true;
                facts.DrawingKinds.Add("smartart");
            }

            if (name.EndsWith(".wdp", StringComparison.Ordinal) || name.Contains("hdphoto"))
            {
                facts.HasHdPhoto = true;
            }
        }
    }
}

readonly record struct LocalCriterion(string Id, string Status, double Earned, double Possible, string Message)
{
    public IReadOnlyList<QNodeHit> QTrace { get; init; } = [];
    public string BreakSkill { get; init; } = "";
}

static class WordGrade
{
    public static (double Verified, double Pending, IReadOnlyList<LocalCriterion> Criteria) Evaluate(string path, JsonRubric? rubric) =>
        Evaluate(path, rubric, ActionEvidence.Events);

    public static (double Verified, double Pending, IReadOnlyList<LocalCriterion> Criteria) Evaluate(
        string path,
        JsonRubric? rubric,
        IReadOnlyList<ActionEvent>? evidence)
    {
        var facts = path.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)
            ? PptXml.Extract(path)
            : path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase)
                ? ExcelXml.Extract(path)
                : WordXml.Extract(path);
        var results = new List<LocalCriterion>();
        if (rubric?.Criteria is null || rubric.Criteria.Count == 0)
        {
            return (0, 0, results);
        }

        foreach (var item in rubric.Criteria)
        {
            item.Predicate ??= new JsonPredicate();
            item.Selector ??= new JsonSelector();
            item.Feedback ??= new JsonFeedback();
            var weight = item.Weight;
            try
            {
                LocalCriterion hit;
                if (string.Equals(item.Kind, "action_sequence", StringComparison.OrdinalIgnoreCase))
                {
                    hit = ActionEvidence.Grade(item, evidence);
                }
                else if (!facts.Ok)
                {
                    hit = new LocalCriterion(item.Id, "error", 0, weight, item.Feedback.Error ?? "Không đọc được tệp.");
                }
                else
                {
                    hit = GradeArtifact(facts, item);
                }

                results.Add(QMatrix.Attach(facts, item, evidence, hit));
            }
            catch (Exception ex)
            {
                var hit = new LocalCriterion(item.Id, "error", 0, weight, "Không chấm được mục này: " + ex.Message);
                results.Add(QMatrix.Attach(facts, item, evidence, hit));
            }
        }

        var verified = results.Sum(r => r.Earned);
        var pending = results.Where(r => r.Status == "unverified").Sum(r => r.Possible);
        return (verified, pending, results);
    }

    static LocalCriterion GradeArtifact(WordFacts facts, JsonCriterion item)
    {
        return (item.Predicate.Type ?? "") switch
        {
            "bookmark_range" => Bookmark(facts, item),
            "internal_hyperlink" => Hyperlink(facts, item),
            "page_background" => HexMatch(facts.PageBackground, item.Predicate.Color, item, "Đã đặt màu nền trang.", "Chưa đúng màu nền trang."),
            "watermark_text" => ContainsList(facts.Watermarks, item.Predicate.Text, item, "Watermark đúng.", "Chưa thấy watermark."),
            "page_border" => Flag(facts.PageBorder, item, "Đã có Page Borders.", "Chưa có Page Borders."),
            "header_contains" => ContainsList(facts.HeaderTexts, item.Predicate.Text, item, "Header đúng.", "Header chưa có nội dung yêu cầu."),
            "header_instruction" => Instr(facts.HeaderInstructions, item.Predicate.Text ?? "PAGE", item),
            "first_page_header" => Flag(
                facts.FirstPageHeader && (facts.HeaderTexts.Count > 0 || facts.Watermarks.Count > 0 || facts.HeaderInstructions.Count > 0),
                item,
                "Đã bật Different First Page.",
                "Chưa bật Different First Page."),
            "style_size" => string.Equals(facts.Heading1Sz, item.Predicate.Sz, StringComparison.Ordinal)
                ? Pass(item, "Heading 1 đúng cỡ.")
                : Fail(item, "Heading 1 chưa đúng cỡ style set."),
            "core_property" => Core(facts, item),
            "comments_absent" => Flag(facts.CommentCount == 0, item, "Đã xóa comment.", "Vẫn còn comment."),
            "revisions_cleared" => Flag(facts.RevisionCount == 0 && !facts.TrackRevisions, item, "Đã chấp nhận thay đổi.", "Vẫn còn Track Changes."),
            "hidden_text_absent" => Flag(facts.VanishCount == 0, item, "Đã bỏ Hidden text.", "Vẫn còn Hidden text."),
            "contains_text" => Phrase(facts.DocumentText, item, true),
            "not_contains_text" => Phrase(facts.DocumentText, item, false),
            "paragraph_style" => ParaStyle(facts, item),
            "text_effect" => ContainsList(facts.TextEffects, item.Predicate.Text, item, "Đã có Text Effects.", "Chưa thấy Text Effects."),
            "section_columns" => Flag(facts.Sections.Any(s => s.Cols >= (item.Predicate.Min ?? 2)), item, "Đã có nhiều cột.", "Chưa thấy section cột."),
            "page_orientation" => Flag(facts.Sections.Any(s => string.Equals(s.Orient, item.Predicate.Orient ?? "landscape", StringComparison.OrdinalIgnoreCase)), item, "Đúng hướng trang.", "Chưa đúng hướng trang."),
            "break_present" => Flag(facts.Breaks.GetValueOrDefault(item.Predicate.Name ?? "page") >= (item.Predicate.Min ?? 1), item, "Đã có break.", "Chưa thấy break."),
            "section_count" => Flag(facts.Sections.Count >= (item.Predicate.Min ?? 2), item, "Đủ section.", "Chưa đủ section."),
            "table_count" => Flag(facts.Tables.Count >= (item.Predicate.Min ?? 1), item, "Đủ bảng.", "Chưa đủ bảng."),
            "table_has_text" => TableHas(facts, item, true),
            "table_lacks_text" => TableHas(facts, item, false),
            "table_dim" => TableDim(facts, item),
            "table_repeat_header" => Flag(facts.Tables.Any(t => t.Header), item, "Repeat Header Rows.", "Chưa Repeat Header."),
            "table_merged" => Flag(facts.Tables.Any(t => t.Merged), item, "Đã gộp ô.", "Chưa gộp ô."),
            "table_data_starts" => TableStarts(facts, item),
            "list_format" => ListFmt(facts, item),
            "footnote_min" => Flag(facts.FootnoteCount >= (item.Predicate.Min ?? 1), item, "Đã có footnote.", "Chưa đủ footnote."),
            "field_contains" => Flag(facts.Fields.Any(f => (f ?? "").Contains(item.Predicate.Text ?? "", StringComparison.OrdinalIgnoreCase)), item, "Đã có trường.", "Chưa thấy trường."),
            "style_used" => Flag(facts.StyleCounts.GetValueOrDefault(item.Predicate.Style ?? item.Predicate.Name ?? "") >= (item.Predicate.Min ?? 1), item, "Đã dùng style.", "Chưa dùng style."),
            "drawing_kind" => DrawingKind(facts, item),
            "drawing_text" => DrawingText(facts, item),
            "artistic_effect" => Flag(facts.ArtisticEffects.Any(e => e.Contains(item.Predicate.Name ?? "", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(item.Predicate.Name)), item, "Artistic effect.", "Chưa artistic effect."),
            "picture_effect" => Flag(facts.PictureEffects.Any(e => e.Contains(item.Predicate.Name ?? "", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(item.Predicate.Name)), item, "Picture effect.", "Chưa picture effect."),
            "alt_text" => ContainsList(facts.AltTexts, item.Predicate.Text, item, "Alt text đúng.", "Chưa đúng alt text."),
            "wrap_type" => Flag(facts.Wraps.Any(w => w.Contains(item.Predicate.Wrap ?? "", StringComparison.OrdinalIgnoreCase)), item, "Wrap đúng.", "Chưa đúng wrap."),
            "comment_text" => Flag(facts.Comments.Any(c => WordXml.Norm(c.Text).Contains(WordXml.Norm(item.Predicate.Text), StringComparison.OrdinalIgnoreCase)), item, "Đã có comment.", "Thiếu comment."),
            "comment_author" => Flag(facts.Comments.Any(c => WordXml.Same(c.Author, item.Predicate.Author)), item, "Đúng tác giả comment.", "Thiếu tác giả comment."),
            "comment_absent_text" => Flag(!facts.Comments.Any(c => WordXml.Norm(c.Text).Contains(WordXml.Norm(item.Predicate.Text), StringComparison.OrdinalIgnoreCase)), item, "Đã xóa comment.", "Comment vẫn còn."),
            "comment_resolved" => Flag(facts.ResolvedCount >= (item.Predicate.Min ?? 1), item, "Đã resolve.", "Chưa resolve."),
            "comment_reply" => Flag(facts.ReplyCount >= (item.Predicate.Min ?? 1), item, "Đã reply.", "Chưa reply."),
            "revision_max" => Flag(facts.RevisionCount <= (item.Predicate.Max ?? 0), item, "Revision trong hạn.", "Còn nhiều revision."),
            "document_protection" => Flag(facts.DocumentProtection, item, "Đã Lock Tracking.", "Chưa Lock Tracking."),
            "hdphoto" => Flag(facts.HasHdPhoto, item, "Đã remove background.", "Chưa remove background."),
            "theme_name" => Flag(WordXml.Norm(facts.ThemeName).Contains(WordXml.Norm(item.Predicate.Name), StringComparison.OrdinalIgnoreCase), item, "Theme đúng.", "Chưa đúng theme."),
            "core_empty" => Flag(string.IsNullOrWhiteSpace(facts.Core.GetValueOrDefault(item.Predicate.Name ?? "title")), item, "Đã xóa thuộc tính.", "Thuộc tính vẫn còn."),
            "slide_count" => SlideCount(facts, item),
            "slide_size" => SlideSize(facts, item),
            "layout_named" => ContainsList(facts.LayoutNames, item.Predicate.Name, item, "Đã có layout.", "Chưa thấy layout."),
            "layout_absent" => Flag(!facts.LayoutNames.Any(n => WordXml.Norm(n).Contains(WordXml.Norm(item.Predicate.Name), StringComparison.OrdinalIgnoreCase)), item, "Đã bỏ layout.", "Layout vẫn còn."),
            "ppt_table_min" => Flag(facts.PptTableCount >= (item.Predicate.Min ?? 1), item, "Đã có bảng.", "Chưa đủ bảng."),
            "ppt_chart_min" => Flag(facts.ChartCount >= (item.Predicate.Min ?? 1), item, "Đã có biểu đồ.", "Chưa đủ biểu đồ."),
            "ppt_picture_min" => Flag(facts.PictureCount >= (item.Predicate.Min ?? 1), item, "Đã có ảnh.", "Chưa đủ ảnh."),
            "ppt_smartart_min" => Flag(facts.SmartArtCount >= (item.Predicate.Min ?? 1), item, "Đã có SmartArt.", "Chưa đủ SmartArt."),
            "ppt_anim_min" => Flag(facts.AnimationCount >= (item.Predicate.Min ?? 1), item, "Đã có animation.", "Chưa đủ animation."),
            "has_3d" => Flag(facts.Has3d, item, "Đã có 3D.", "Chưa có 3D."),
            "has_media" => Flag(facts.HasMedia, item, "Đã có media.", "Chưa có media."),
            "transition_named" => Flag(facts.Transitions.Any(t => t.Contains(item.Predicate.Name ?? "", StringComparison.OrdinalIgnoreCase)), item, "Đã có transition.", "Chưa thấy transition."),
            "notes_contains" => ContainsList(facts.NotesTexts, item.Predicate.Text, item, "Notes đúng.", "Chưa thấy notes."),
            "custom_show" => ContainsList(facts.CustomShows, item.Predicate.Name, item, "Đã có custom show.", "Chưa có custom show."),
            "section_named" => ContainsList(facts.SectionNames, item.Predicate.Name, item, "Đã có section.", "Chưa có section."),
            "scheme_color" => Flag(facts.SchemeColors.Any(c => string.Equals(c, item.Predicate.Name, StringComparison.OrdinalIgnoreCase)), item, "Đúng màu scheme.", "Chưa đúng màu."),
            "hidden_slide" => Flag(item.Predicate.Index is int idx ? facts.HiddenSlides.Contains(idx) : facts.HiddenSlides.Count > 0, item, "Đã ẩn slide.", "Chưa ẩn slide."),
            "hyperlink_contains" => Flag(
                facts.ExternalHyperlinks.Any(h => (h.Text + h.TargetText + h.Anchor).Contains(item.Predicate.Text ?? "", StringComparison.OrdinalIgnoreCase))
                    || (facts.DocumentText ?? "").Contains(item.Predicate.Text ?? "", StringComparison.OrdinalIgnoreCase),
                item,
                "Đã có hyperlink.",
                "Chưa thấy hyperlink."),
            "sheet_named" => ContainsList(facts.SheetNames, item.Predicate.Name, item, "Đã có sheet.", "Chưa thấy sheet."),
            "sheet_count" => Flag(facts.SheetNames.Count >= (item.Predicate.Min ?? 1), item, "Đủ sheet.", "Chưa đủ sheet."),
            "table_named" => ContainsList(facts.TableNames, item.Predicate.Name, item, "Đã có bảng.", "Chưa thấy bảng."),
            "table_style" => ContainsList(facts.TableStyles, item.Predicate.Name, item, "Đúng style bảng.", "Chưa đúng style."),
            "table_has_total" => Flag(facts.TableTotals.Count > 0, item, "Đã có Total row.", "Chưa có Total row."),
            "defined_name" => ContainsList(facts.DefinedNames, item.Predicate.Name, item, "Đã có named range.", "Chưa có named range."),
            "formula_func" => Flag(facts.FormulaFuncs.Any(f => f.Contains(item.Predicate.Name ?? "", StringComparison.OrdinalIgnoreCase)), item, "Đã có hàm.", "Chưa thấy hàm."),
            "formula_contains" => Flag(facts.Formulas.Any(f => f.Contains(item.Predicate.Text ?? "", StringComparison.OrdinalIgnoreCase)), item, "Đã có công thức.", "Chưa thấy công thức."),
            "freeze_named" => ContainsList(facts.FreezeCells, item.Predicate.Name, item, "Đã freeze.", "Chưa freeze."),
            "print_orient" => Flag(facts.PrintOrients.Any(o => o.Contains(item.Predicate.Name ?? "landscape", StringComparison.OrdinalIgnoreCase)), item, "Đúng hướng in.", "Chưa đúng hướng in."),
            "hidden_sheet" => ContainsList(facts.HiddenSheets, item.Predicate.Name, item, "Đã ẩn sheet.", "Sheet vẫn hiện."),
            "xlsx_chart_min" => Flag(facts.ChartCount >= (item.Predicate.Min ?? 1), item, "Đã có chart.", "Chưa đủ chart."),
            "xlsx_spark_min" => Flag(facts.SparklineCount >= (item.Predicate.Min ?? 1), item, "Đã có sparkline.", "Chưa đủ sparkline."),
            "xlsx_cf_min" => Flag(facts.CfCount >= (item.Predicate.Min ?? 1), item, "Đã có conditional format.", "Chưa có CF."),
            "xlsx_merged_min" => Flag(facts.MergedCount >= (item.Predicate.Min ?? 1), item, "Đã merge.", "Chưa merge."),
            "chart_title" => ContainsList(facts.ChartTitles, item.Predicate.Name ?? item.Predicate.Text, item, "Đúng tiêu đề chart.", "Chưa đúng tiêu đề."),
            "filter_contains" => ContainsList(facts.FilterOps, item.Predicate.Text, item, "Đã lọc.", "Chưa lọc."),
            "print_area" => Flag(facts.PrintAreas.Count > 0, item, "Đã đặt Print Area.", "Chưa đặt Print Area."),
            "xlsx_decorative" => Flag(facts.Decorative, item, "Đã đánh decorative.", "Chưa decorative."),
            "xlsx_alt_text" => ContainsList(facts.AltTextsXlsx, item.Predicate.Text, item, "Đã có alt text.", "Chưa có alt text."),
            _ => new LocalCriterion(item.Id, "error", 0, item.Weight, "unknown_predicate"),
        };
    }

    static LocalCriterion Bookmark(WordFacts facts, JsonCriterion item)
    {
        var name = item.Predicate.Name ?? item.Selector.Bookmark ?? "";
        if (!facts.Bookmarks.TryGetValue(name, out var found))
        {
            return new LocalCriterion(item.Id, "fail", 0, item.Weight, item.Feedback.Fail ?? "Thiếu bookmark.");
        }

        if (WordXml.Same(found.Text, item.Predicate.Text))
        {
            return new LocalCriterion(item.Id, "pass", item.Weight, item.Weight, item.Feedback.Pass ?? "Đạt.");
        }

        return new LocalCriterion(item.Id, "fail", 0, item.Weight, item.Feedback.Fail ?? "Bookmark sai phạm vi.");
    }

    static bool LinkHitsHeading(HyperlinkFact link, string heading)
    {
        if (WordXml.Same(link.TargetHeading, heading) || WordXml.Same(link.TargetText, heading))
        {
            return true;
        }

        var slug = (link.Anchor ?? "").Replace("_", " ").Trim(' ', '_');
        return slug.Length > 0 && WordXml.Same(slug, heading);
    }

    static LocalCriterion Hyperlink(WordFacts facts, JsonCriterion item)
    {
        var label = item.Predicate.Text ?? item.Selector.TocLabel ?? "";
        var heading = item.Predicate.Heading ?? label;
        var matches = facts.InternalHyperlinks.Where(h => WordXml.Same(h.Text, label)).ToList();
        if (matches.Count == 0)
        {
            return new LocalCriterion(item.Id, "fail", 0, item.Weight, item.Feedback.Fail ?? "Thiếu liên kết mục lục.");
        }

        if (matches.Any(h => LinkHitsHeading(h, heading)))
        {
            return new LocalCriterion(item.Id, "pass", item.Weight, item.Weight, item.Feedback.Pass ?? "Đạt.");
        }

        return new LocalCriterion(item.Id, "fail", 0, item.Weight, item.Feedback.Fail ?? "Liên kết sai đích.");
    }

    static LocalCriterion Pass(JsonCriterion item, string fallback) =>
        new(item.Id, "pass", item.Weight, item.Weight, item.Feedback.Pass ?? fallback);

    static LocalCriterion Fail(JsonCriterion item, string fallback) =>
        new(item.Id, "fail", 0, item.Weight, item.Feedback.Fail ?? fallback);

    static LocalCriterion Flag(bool ok, JsonCriterion item, string pass, string fail) =>
        ok ? Pass(item, pass) : Fail(item, fail);

    static LocalCriterion HexMatch(string got, string? expected, JsonCriterion item, string pass, string fail)
    {
        static string Hex(string? value) =>
            string.Concat((value ?? "").ToUpperInvariant().Where(Uri.IsHexDigit));
        return string.Equals(Hex(got), Hex(expected), StringComparison.Ordinal)
            ? Pass(item, pass)
            : Fail(item, fail);
    }

    static LocalCriterion ContainsList(IEnumerable<string> items, string? needle, JsonCriterion item, string pass, string fail)
    {
        needle = WordXml.Norm(needle);
        if (needle.Length == 0)
        {
            return Fail(item, fail);
        }

        return items.Any(v => WordXml.Same(v, needle) || WordXml.Norm(v).Contains(needle, StringComparison.OrdinalIgnoreCase))
            ? Pass(item, pass)
            : Fail(item, fail);
    }

    static LocalCriterion Instr(IEnumerable<string> items, string needle, JsonCriterion item) =>
        items.Any(v => (v ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase))
            ? Pass(item, "Header có trường PAGE.")
            : Fail(item, "Chưa thấy trường PAGE.");

    static LocalCriterion Core(WordFacts facts, JsonCriterion item)
    {
        var name = item.Predicate.Name ?? "";
        facts.Core.TryGetValue(name, out var got);
        return WordXml.Same(got, item.Predicate.Text)
            ? Pass(item, "Thuộc tính tài liệu đúng.")
            : Fail(item, "Thuộc tính tài liệu chưa đúng.");
    }

    static LocalCriterion SlideCount(WordFacts facts, JsonCriterion item)
    {
        if (item.Predicate.Count is int count)
        {
            return Flag(facts.SlideCount == count, item, "Đúng số slide.", "Chưa đúng số slide.");
        }

        if (item.Predicate.Min is int min)
        {
            return Flag(facts.SlideCount >= min, item, "Đủ số slide.", "Chưa đủ slide.");
        }

        return Fail(item, "Chưa đúng số slide.");
    }

    static LocalCriterion SlideSize(WordFacts facts, JsonCriterion item)
    {
        if (item.Predicate.Cx is int cx && Math.Abs(facts.SlideCx - cx) > 20000)
        {
            return Fail(item, "Chưa đúng khổ slide.");
        }

        if (item.Predicate.Cy is int cy && Math.Abs(facts.SlideCy - cy) > 20000)
        {
            return Fail(item, "Chưa đúng khổ slide.");
        }

        return item.Predicate.Cx is not null || item.Predicate.Cy is not null
            ? Pass(item, "Khổ slide đúng.")
            : Fail(item, "Chưa đúng khổ slide.");
    }

    static bool PhraseIn(string blob, string? needle)
    {
        needle = WordXml.Norm(needle);
        if (needle.Length == 0)
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(
            blob ?? "",
            @"(?<!\w)" + System.Text.RegularExpressions.Regex.Escape(needle) + @"(?!\w)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    static LocalCriterion Phrase(string blob, JsonCriterion item, bool wantPresent) =>
        PhraseIn(blob, item.Predicate.Text) == wantPresent
            ? Pass(item, wantPresent ? "Đã có nội dung." : "Đã bỏ nội dung.")
            : Fail(item, wantPresent ? "Thiếu nội dung." : "Nội dung vẫn còn.");

    static LocalCriterion ParaStyle(WordFacts facts, JsonCriterion item)
    {
        var needle = WordXml.Norm(item.Predicate.Text);
        var style = item.Predicate.Style ?? "";
        foreach (var para in facts.Paragraphs)
        {
            if (!WordXml.Same(para.Style, style))
            {
                continue;
            }

            var text = WordXml.Norm(para.Text);
            if (WordXml.Same(text, needle) || text.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            {
                return Pass(item, "Style đúng.");
            }
        }

        return Fail(item, "Style chưa đúng.");
    }

    static LocalCriterion TableHas(WordFacts facts, JsonCriterion item, bool wantPresent)
    {
        var needle = WordXml.Norm(item.Predicate.Text);
        var exact = item.Predicate.Exact;
        var found = facts.Tables.SelectMany(t => t.Cells).SelectMany(r => r).Any(cell =>
            exact ? WordXml.Same(cell, needle) : WordXml.Norm(cell).Contains(needle, StringComparison.OrdinalIgnoreCase));
        if (wantPresent)
        {
            return found ? Pass(item, "Có trong bảng.") : Fail(item, "Chưa thấy trong bảng.");
        }

        return found ? Fail(item, "Vẫn còn trong bảng.") : Pass(item, "Đã bỏ khỏi bảng.");
    }

    static LocalCriterion TableDim(WordFacts facts, JsonCriterion item)
    {
        var rows = item.Predicate.Rows ?? 0;
        var cols = item.Predicate.Cols ?? 0;
        foreach (var tbl in facts.Tables)
        {
            if ((rows == 0 || tbl.Rows == rows) && (cols == 0 || tbl.Cols == cols))
            {
                return Pass(item, "Kích thước bảng đúng.");
            }
        }

        return Fail(item, "Chưa đúng hàng/cột.");
    }

    static LocalCriterion TableStarts(WordFacts facts, JsonCriterion item)
    {
        var needle = WordXml.Norm(item.Predicate.Text);
        var headers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id", "customer", "appointment", "lastname", "firstname", "address", "city", "state", "date", "time",
        };
        foreach (var tbl in facts.Tables.Where(t => t.Rows >= 4))
        {
            foreach (var row in tbl.Cells)
            {
                var first = WordXml.Norm(row.FirstOrDefault());
                if (first.Length == 0 || headers.Contains(first))
                {
                    continue;
                }

                return WordXml.Same(first, needle) ? Pass(item, "Đã sort.") : Fail(item, "Chưa sort đúng.");
            }
        }

        return Fail(item, "Chưa sort đúng.");
    }

    static LocalCriterion ListFmt(WordFacts facts, JsonCriterion item)
    {
        var needle = WordXml.Norm(item.Predicate.Text);
        var fmt = (item.Predicate.Fmt ?? "").ToLowerInvariant();
        foreach (var para in facts.Paragraphs)
        {
            var text = WordXml.Norm(para.Text);
            if (needle.Length > 0 && !WordXml.Same(text, needle) && !text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (fmt.Length > 0 && string.Equals(para.Fmt, fmt, StringComparison.OrdinalIgnoreCase))
            {
                return Pass(item, "List đúng.");
            }

            if (fmt.Length == 0 && para.Fmt.Length > 0)
            {
                return Pass(item, "Đã là list.");
            }
        }

        if (needle.Length == 0 && fmt.Length > 0 && facts.NumberingFormats.Any(f => string.Equals(f, fmt, StringComparison.OrdinalIgnoreCase)))
        {
            return Pass(item, "Có numbering.");
        }

        return Fail(item, "Chưa đúng list.");
    }

    static LocalCriterion DrawingKind(WordFacts facts, JsonCriterion item)
    {
        var kind = (item.Predicate.Kind ?? "").ToLowerInvariant();
        if (kind == "model3d" && (facts.Has3d || facts.DrawingKinds.Contains(kind)))
        {
            return Pass(item, "Đã có 3D.");
        }

        if (kind == "smartart" && (facts.HasSmartArt || facts.DrawingKinds.Contains(kind)))
        {
            return Pass(item, "Đã có SmartArt.");
        }

        return facts.DrawingKinds.Contains(kind) ? Pass(item, "Đã chèn đối tượng.") : Fail(item, "Chưa chèn đối tượng.");
    }

    static LocalCriterion DrawingText(WordFacts facts, JsonCriterion item)
    {
        var needle = WordXml.Norm(item.Predicate.Text);
        var min = item.Predicate.Min ?? 1;
        var hits = facts.DrawingTexts.Count(t => WordXml.Norm(t).Contains(needle, StringComparison.OrdinalIgnoreCase));
        return hits >= min ? Pass(item, "Đã gõ chữ lên graphic.") : Fail(item, "Chưa gõ chữ lên graphic.");
    }
}

sealed class JsonRubric
{
    public string Title { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Scenario { get; set; } = "";
    public List<JsonCriterion> Criteria { get; set; } = [];
}

sealed class JsonCriterion
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "artifact";
    public double Weight { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> HelpSteps { get; set; } = [];
    public List<JsonQNode> QMatrixNodes { get; set; } = [];
    public JsonSelector Selector { get; set; } = new();
    public JsonPredicate Predicate { get; set; } = new();
    public JsonFeedback Feedback { get; set; } = new();
}

sealed class JsonSelector
{
    public string? Bookmark { get; set; }
    public string? TocLabel { get; set; }
    public string? Action { get; set; }
    public string? Query { get; set; }
    public string? Source { get; set; }
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public string? Style { get; set; }
    public int? Page { get; set; }
}

sealed class JsonPredicate
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Text { get; set; }
    public string? Heading { get; set; }
    public string? Color { get; set; }
    public string? Sz { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int? Rows { get; set; }
    public int? Cols { get; set; }
    public bool Exact { get; set; }
    public string? Style { get; set; }
    public string? Wrap { get; set; }
    public string? Kind { get; set; }
    public string? Orient { get; set; }
    public string? Fmt { get; set; }
    public string? Author { get; set; }
    public string? Query { get; set; }
    public bool MatchCase { get; set; }
    public bool WholeWord { get; set; }
    public int? MinHits { get; set; }
    public int? Page { get; set; }
    public int? Count { get; set; }
    public int? Cx { get; set; }
    public int? Cy { get; set; }
    public int? Index { get; set; }
}

sealed class JsonFeedback
{
    public string? Pass { get; set; }
    public string? Fail { get; set; }
    public string? Unverified { get; set; }
    public string? Error { get; set; }
}

sealed class JsonQNode
{
    public string StepId { get; set; } = "";
    public string SkillType { get; set; } = "";
    public string ValidationRule { get; set; } = "";
    public string SuccessMessage { get; set; } = "";
    public string ErrorFeedback { get; set; } = "";
}
