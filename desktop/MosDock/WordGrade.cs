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
}

static class WordXml
{
    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace CP = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    static readonly XNamespace DC = "http://purl.org/dc/elements/1.1/";
    static readonly XNamespace VML = "urn:schemas-microsoft-com:vml";

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
            using var zip = ZipFile.OpenRead(path);
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

            string ParaStyle(XElement p)
            {
                var style = p.Element(W + "pPr")?.Element(W + "pStyle");
                return (string?)style?.Attribute(W + "val") ?? "";
            }

            string ParaText(XElement? p) =>
                p is null ? "" : Norm(string.Concat(p.Descendants(W + "t").Select(t => t.Value)));

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
                else if (el.Name == W + "t")
                {
                    var text = el.Value;
                    if (!string.IsNullOrEmpty(text))
                    {
                        foreach (var id in openIds.Keys)
                        {
                            buffers.GetValueOrDefault(id)?.Add(text);
                        }
                    }
                }
                else if (el.Name == W + "hyperlink")
                {
                    var text = Norm(string.Concat(el.Descendants(W + "t").Select(t => t.Value)));
                    var anchor = (string?)el.Attribute(W + "anchor") ?? "";
                    var rid = (string?)el.Attribute(R + "id") ?? "";
                    rels.TryGetValue(rid, out var rel);
                    if (anchor.Length == 0 && rel.Target.StartsWith("#", StringComparison.Ordinal))
                    {
                        anchor = rel.Target[1..];
                    }

                    var external = rel.Mode == "External" || (rel.Target.Length > 0 && anchor.Length == 0);
                    hyperlinks.Add(new HyperlinkFact { Text = text, Anchor = anchor, External = external });
                }

                foreach (var child in el.Elements())
                {
                    Walk(child, currentPara, currentStyle);
                }
            }

            var body = root.Element(W + "body") ?? root;
            Walk(body, null, "");

            foreach (var kv in openIds)
            {
                if (facts.Bookmarks.TryGetValue(kv.Value, out var bm) && string.IsNullOrEmpty(bm.Text))
                {
                    bm.Text = Norm(string.Concat(buffers.GetValueOrDefault(kv.Key) ?? []));
                }
            }

            foreach (var link in hyperlinks)
            {
                if (facts.Bookmarks.TryGetValue(link.Anchor, out var bm))
                {
                    link.TargetHeading = bm.Heading;
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
}

readonly record struct LocalCriterion(string Id, string Status, double Earned, double Possible, string Message);

static class WordGrade
{
    public static (double Verified, double Pending, IReadOnlyList<LocalCriterion> Criteria) Evaluate(string path, JsonRubric? rubric)
    {
        var facts = WordXml.Extract(path);
        var results = new List<LocalCriterion>();
        if (rubric?.Criteria is null || rubric.Criteria.Count == 0)
        {
            return (0, 0, results);
        }

        foreach (var item in rubric.Criteria)
        {
            var weight = item.Weight;
            if (string.Equals(item.Kind, "action_sequence", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new LocalCriterion(item.Id, "unverified", 0, weight, item.Feedback.Unverified ?? "Chưa xác minh thao tác."));
                continue;
            }

            if (!facts.Ok)
            {
                results.Add(new LocalCriterion(item.Id, "error", 0, weight, item.Feedback.Error ?? "Không đọc được tệp."));
                continue;
            }

            results.Add(GradeArtifact(facts, item));
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

    static LocalCriterion Hyperlink(WordFacts facts, JsonCriterion item)
    {
        var label = item.Predicate.Text ?? item.Selector.TocLabel ?? "";
        var heading = item.Predicate.Heading ?? label;
        var matches = facts.InternalHyperlinks.Where(h => WordXml.Same(h.Text, label)).ToList();
        if (matches.Count == 0)
        {
            return new LocalCriterion(item.Id, "fail", 0, item.Weight, item.Feedback.Fail ?? "Thiếu liên kết mục lục.");
        }

        if (matches.Any(h => WordXml.Same(h.TargetHeading, heading)))
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
}

sealed class JsonRubric
{
    public List<JsonCriterion> Criteria { get; set; } = [];
}

sealed class JsonCriterion
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "artifact";
    public double Weight { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> HelpSteps { get; set; } = [];
    public JsonSelector Selector { get; set; } = new();
    public JsonPredicate Predicate { get; set; } = new();
    public JsonFeedback Feedback { get; set; } = new();
}

sealed class JsonSelector
{
    public string? Bookmark { get; set; }
    public string? TocLabel { get; set; }
}

sealed class JsonPredicate
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Text { get; set; }
    public string? Heading { get; set; }
    public string? Color { get; set; }
    public string? Sz { get; set; }
}

sealed class JsonFeedback
{
    public string? Pass { get; set; }
    public string? Fail { get; set; }
    public string? Unverified { get; set; }
    public string? Error { get; set; }
}
