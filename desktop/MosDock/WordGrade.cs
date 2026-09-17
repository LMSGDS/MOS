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

            var pred = item.Predicate.Type ?? "";
            if (pred == "bookmark_range")
            {
                results.Add(Bookmark(facts, item));
            }
            else if (pred == "internal_hyperlink")
            {
                results.Add(Hyperlink(facts, item));
            }
            else
            {
                results.Add(new LocalCriterion(item.Id, "error", 0, weight, "unknown_predicate"));
            }
        }

        var verified = results.Sum(r => r.Earned);
        var pending = results.Where(r => r.Status == "unverified").Sum(r => r.Possible);
        return (verified, pending, results);
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
}

sealed class JsonFeedback
{
    public string? Pass { get; set; }
    public string? Fail { get; set; }
    public string? Unverified { get; set; }
    public string? Error { get; set; }
}
