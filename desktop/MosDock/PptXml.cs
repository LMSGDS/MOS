using System.IO.Compression;
using System.Xml.Linq;

namespace MosDock;

static class PptXml
{
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace CP = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    static readonly XNamespace DC = "http://purl.org/dc/elements/1.1/";

    public static WordFacts Extract(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new WordFacts { Ok = false, Error = "missing_file" };
        }

        try
        {
            using var zip = LockedFile.OpenZip(path);
            var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var facts = new WordFacts();

            foreach (var name in names.Where(n => n.StartsWith("ppt/theme/", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
            {
                var theme = Read(zip, name);
                var label = theme?.Descendants(A + "theme").Select(el => (string?)el.Attribute("name")).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    facts.ThemeName = WordXml.Norm(label);
                }
            }

            var core = Read(zip, "docProps/core.xml");
            if (core is not null)
            {
                SetCore(facts, core, DC + "title", "title");
                SetCore(facts, core, DC + "subject", "subject");
                SetCore(facts, core, DC + "creator", "creator");
                SetCore(facts, core, DC + "description", "description");
                SetCore(facts, core, CP + "contentStatus", "status");
            }

            var pres = Read(zip, "ppt/presentation.xml");
            var sldSz = pres?.Element(P + "sldSz");
            facts.SlideCx = ParseInt((string?)sldSz?.Attribute("cx"));
            facts.SlideCy = ParseInt((string?)sldSz?.Attribute("cy"));
            foreach (var el in pres?.Descendants() ?? Enumerable.Empty<XElement>())
            {
                var local = Local(el.Name);
                if (local is "sldSection" or "section")
                {
                    var name = (string?)el.Attribute("name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        facts.SectionNames.Add(WordXml.Norm(name));
                    }
                }

                if (local == "custShow")
                {
                    var name = (string?)el.Attribute("name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        facts.CustomShows.Add(WordXml.Norm(name));
                    }
                }
            }

            var ridToTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rels = Read(zip, "ppt/_rels/presentation.xml.rels");
            foreach (var rel in rels?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var rid = (string?)rel.Attribute("Id");
                var target = ((string?)rel.Attribute("Target") ?? "").TrimStart('/');
                if (!string.IsNullOrWhiteSpace(rid) && target.Length > 0)
                {
                    ridToTarget[rid] = target;
                }
            }

            var slideFiles = new List<string>();
            foreach (var el in pres?.Descendants(P + "sldId") ?? Enumerable.Empty<XElement>())
            {
                var rid = (string?)el.Attribute(R + "id") ?? "";
                if (ridToTarget.TryGetValue(rid, out var target) && target.Length > 0)
                {
                    if (!target.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase))
                    {
                        target = "ppt/" + target;
                    }

                    slideFiles.Add(target);
                }

                if ((string?)el.Attribute("show") == "0")
                {
                    facts.HiddenSlides.Add(slideFiles.Count);
                }
            }

            if (slideFiles.Count == 0)
            {
                slideFiles.AddRange(names
                    .Where(n => n.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            }

            var slideTexts = new List<string>();
            foreach (var sname in slideFiles)
            {
                var root = Read(zip, sname);
                if (root is null)
                {
                    slideTexts.Add("");
                    continue;
                }

                var blob = TextOf(root);
                slideTexts.Add(blob);
                foreach (var color in root.Descendants(A + "schemeClr"))
                {
                    var val = (string?)color.Attribute("val");
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        facts.SchemeColors.Add(val);
                    }
                }

                foreach (var trans in root.Descendants(P + "transition"))
                {
                    var child = trans.Elements().Select(el => Local(el.Name)).FirstOrDefault(n => n != "sndAc") ?? "";
                    var dur = (string?)trans.Attribute("advTm") ?? (string?)trans.Attribute("spd") ?? "";
                    facts.Transitions.Add((child + ":" + dur).Trim(':'));
                }

                var timing = root.Element(P + "timing");
                if (timing is not null)
                {
                    facts.AnimationCount += timing.Descendants().Count(el => Local(el.Name) is "anim" or "animEffect" or "animMotion" or "set" or "animScale");
                }

                facts.PptTableCount += root.Descendants(A + "tbl").Count();

                var srelsName = sname.Replace("ppt/slides/", "ppt/slides/_rels/", StringComparison.OrdinalIgnoreCase) + ".rels";
                var srels = Read(zip, srelsName);
                foreach (var rel in srels?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    var typ = ((string?)rel.Attribute("Type") ?? "").Split('/').Last();
                    var target = (string?)rel.Attribute("Target") ?? "";
                    if (typ == "slideLayout")
                    {
                        var layoutPath = "ppt/slideLayouts/" + Path.GetFileName(target);
                        var layout = Read(zip, layoutPath);
                        var cSld = layout?.Element(P + "cSld");
                        facts.LayoutNames.Add((string?)cSld?.Attribute("name") ?? Path.GetFileNameWithoutExtension(target));
                    }

                    if (typ == "notesSlide")
                    {
                        var notes = TextOf(Read(zip, "ppt/notesSlides/" + Path.GetFileName(target)));
                        if (notes.Length > 0)
                        {
                            facts.NotesTexts.Add(notes);
                        }
                    }

                    if (typ == "chart")
                    {
                        facts.ChartCount++;
                    }

                    if (typ == "image")
                    {
                        facts.PictureCount++;
                    }

                    if (typ is "video" or "audio" or "media")
                    {
                        facts.HasMedia = true;
                    }

                    if (typ == "diagram")
                    {
                        facts.SmartArtCount++;
                    }

                }

                if (names.Any(n => n.Contains("model3d", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || n.EndsWith(".3mf", StringComparison.OrdinalIgnoreCase)))
                {
                    facts.Has3d = true;
                }

                foreach (var el in root.Descendants())
                {
                    var local = Local(el.Name);
                    if (local.Contains("model3d", StringComparison.OrdinalIgnoreCase))
                    {
                        facts.Has3d = true;
                    }

                    if (local is "videoFile" or "audioFile")
                    {
                        facts.HasMedia = true;
                    }
                }
            }

            facts.SlideCount = slideFiles.Count;
            facts.DocumentText = string.Join("\n", slideTexts.Where(t => t.Length > 0));
            return facts;
        }
        catch
        {
            return new WordFacts { Ok = false, Error = "extract_failed" };
        }
    }

    static void SetCore(WordFacts facts, XElement root, XName tag, string key)
    {
        var text = WordXml.Norm(root.Descendants(tag).Select(el => el.Value).FirstOrDefault());
        if (text.Length > 0)
        {
            facts.Core[key] = text;
        }
    }

    static XElement? Read(ZipArchive zip, string name)
    {
        var entry = zip.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), name, StringComparison.OrdinalIgnoreCase));
        if (entry is null || entry.Length > 8 * 1024 * 1024)
        {
            return null;
        }

        try
        {
            using var stream = entry.Open();
            return XElement.Load(stream);
        }
        catch
        {
            return null;
        }
    }

    static string TextOf(XElement? root) =>
        root is null ? "" : WordXml.Norm(string.Join(" ", root.DescendantNodes().OfType<XText>().Select(t => t.Value)));

    static string Local(XName name) => name.LocalName;

    static int ParseInt(string? raw) => int.TryParse(raw, out var n) ? n : 0;
}
