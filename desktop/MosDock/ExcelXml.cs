using System.IO.Compression;
using System.Xml.Linq;

namespace MosDock;

static class ExcelXml
{
    static readonly XNamespace X = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
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
            var shared = new List<string>();
            var sst = Read(zip, "xl/sharedStrings.xml");
            foreach (var si in sst?.Elements(X + "si") ?? Enumerable.Empty<XElement>())
            {
                shared.Add(TextOf(si));
            }

            var core = Read(zip, "docProps/core.xml");
            SetCore(facts, core, DC + "title", "title");
            SetCore(facts, core, DC + "subject", "subject");
            SetCore(facts, core, DC + "creator", "creator");
            SetCore(facts, core, CP + "contentStatus", "status");
            SetCore(facts, core, CP + "keywords", "keywords");

            var theme = Read(zip, "xl/theme/theme1.xml");
            facts.ThemeName = WordXml.Norm(theme?.Descendants(A + "theme").Select(el => (string?)el.Attribute("name")).FirstOrDefault());

            var wb = Read(zip, "xl/workbook.xml");
            var ridTo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rels = Read(zip, "xl/_rels/workbook.xml.rels");
            foreach (var rel in rels?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var rid = (string?)rel.Attribute("Id") ?? "";
                var target = ((string?)rel.Attribute("Target") ?? "").TrimStart('/');
                if (rid.Length == 0 || target.Length == 0)
                {
                    continue;
                }

                if (!target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                {
                    target = "xl/" + target;
                }

                ridTo[rid] = target;
            }

            var sheets = new List<(string Name, string Path, string State)>();
            foreach (var sh in wb?.Descendants(X + "sheet") ?? Enumerable.Empty<XElement>())
            {
                var rid = (string?)sh.Attribute(R + "id") ?? "";
                ridTo.TryGetValue(rid, out var target);
                var name = WordXml.Norm((string?)sh.Attribute("name"));
                var state = (string?)sh.Attribute("state") ?? "visible";
                sheets.Add((name, target ?? "", state));
                facts.SheetNames.Add(name);
                if (state != "visible")
                {
                    facts.HiddenSheets.Add(name);
                }
            }

            foreach (var el in wb?.Descendants(X + "definedName") ?? Enumerable.Empty<XElement>())
            {
                var name = (string?)el.Attribute("name") ?? "";
                if (name.StartsWith("_xlnm.Print_Area", StringComparison.Ordinal))
                {
                    facts.PrintAreas.Add(WordXml.Norm(el.Value));
                }
                else if (name.Length > 0 && !name.StartsWith("_xlnm", StringComparison.Ordinal) && !name.StartsWith("_xlchart", StringComparison.Ordinal))
                {
                    facts.DefinedNames.Add(WordXml.Norm(name));
                }
            }

            var values = new List<string>();
            foreach (var (sheetName, spath, _) in sheets)
            {
                var root = Read(zip, spath);
                if (root is null)
                {
                    continue;
                }

                var pane = root.Descendants(X + "pane").FirstOrDefault();
                if (pane is not null)
                {
                    facts.FreezeCells.Add((string?)pane.Attribute("topLeftCell") ?? "");
                }

                var setup = root.Element(X + "pageSetup");
                if (setup is not null)
                {
                    facts.PrintOrients.Add(((string?)setup.Attribute("orientation") ?? "").ToLowerInvariant());
                }

                var hf = root.Element(X + "headerFooter");
                if (hf is not null)
                {
                    facts.HeaderTexts.Add(TextOf(hf));
                }

                facts.MergedCount += root.Descendants(X + "mergeCell").Count();
                facts.CfCount += root.Descendants(X + "conditionalFormatting").Count();
                facts.SparklineCount += root.Descendants().Count(el => Local(el.Name) is "sparklineGroup" or "sparkline");
                foreach (var hyper in root.Descendants(X + "hyperlink"))
                {
                    var display = (string?)hyper.Attribute("display") ?? (string?)hyper.Attribute("tooltip") ?? "";
                    if (display.Length > 0)
                    {
                        facts.ExternalHyperlinks.Add(new HyperlinkFact { Text = display, External = true });
                    }
                }

                foreach (var cell in root.Descendants(X + "c"))
                {
                    var kind = (string?)cell.Attribute("t") ?? "";
                    var formula = cell.Element(X + "f");
                    if (formula is not null && !string.IsNullOrWhiteSpace(formula.Value))
                    {
                        facts.Formulas.Add(formula.Value.Trim());
                        var func = formula.Value.Trim().TrimStart('=').Split('(')[0].Split('!').Last();
                        if (func.Length > 0 && !facts.FormulaFuncs.Contains(func.ToUpperInvariant()))
                        {
                            facts.FormulaFuncs.Add(func.ToUpperInvariant());
                        }
                    }

                    var raw = cell.Element(X + "v")?.Value?.Trim() ?? "";
                    if (kind == "s" && int.TryParse(raw, out var idx) && idx >= 0 && idx < shared.Count)
                    {
                        values.Add(shared[idx]);
                    }
                    else if (kind == "inlineStr")
                    {
                        values.Add(TextOf(cell.Element(X + "is")));
                    }
                    else if (raw.Length > 0 && kind != "e")
                    {
                        values.Add(raw);
                    }
                }

                var srels = Read(zip, spath.Replace("xl/worksheets/", "xl/worksheets/_rels/", StringComparison.OrdinalIgnoreCase) + ".rels");
                foreach (var rel in srels?.Elements() ?? Enumerable.Empty<XElement>())
                {
                    var typ = ((string?)rel.Attribute("Type") ?? "").Split('/').Last();
                    var target = (string?)rel.Attribute("Target") ?? "";
                    if (typ == "table")
                    {
                        var table = Read(zip, "xl/tables/" + Path.GetFileName(target));
                        if (table is null)
                        {
                            continue;
                        }

                        var tname = WordXml.Norm((string?)table.Attribute("displayName") ?? (string?)table.Attribute("name"));
                        if (tname.Length > 0)
                        {
                            facts.TableNames.Add(tname);
                        }

                        var style = table.Element(X + "tableStyleInfo");
                        if (style is not null)
                        {
                            facts.TableStyles.Add((string?)style.Attribute("name") ?? "");
                        }

                        if ((string?)table.Attribute("totalsRowCount") == "1")
                        {
                            facts.TableTotals.Add(tname);
                        }

                        foreach (var flt in table.Descendants())
                        {
                            if (Local(flt.Name) is "customFilter" or "filter")
                            {
                                var op = (string?)flt.Attribute("operator") ?? "eq";
                                var val = (string?)flt.Attribute("val") ?? "";
                                if (val.Length > 0)
                                {
                                    facts.FilterOps.Add(op + ":" + val);
                                }
                            }
                        }
                    }

                    if (typ.Contains("hyperlink", StringComparison.OrdinalIgnoreCase) && target.Length > 0)
                    {
                        facts.ExternalHyperlinks.Add(new HyperlinkFact { Text = target, External = true, TargetText = target });
                    }
                }

                _ = sheetName;
            }

            foreach (var name in names.Where(n => n.StartsWith("xl/chartsheets/", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !n.Contains("/_rels/")))
            {
                facts.SheetNames.Add(Path.GetFileNameWithoutExtension(name));
            }

            foreach (var name in names.Where(n => n.StartsWith("xl/charts/", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                if (Path.GetFileName(name).StartsWith("chart", StringComparison.OrdinalIgnoreCase))
                {
                    facts.ChartCount++;
                }

                var chart = Read(zip, name);
                var title = chart?.Descendants(C + "title").Select(TextOf).FirstOrDefault(t => t.Length > 0);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    facts.ChartTitles.Add(WordXml.Norm(title));
                }
            }

            foreach (var name in names.Where(n => n.StartsWith("xl/drawings/", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) && !n.Contains("/_rels/")))
            {
                var drawing = Read(zip, name);
                foreach (var el in drawing?.Descendants() ?? Enumerable.Empty<XElement>())
                {
                    var descr = (string?)el.Attribute("descr");
                    if (!string.IsNullOrWhiteSpace(descr))
                    {
                        facts.AltTextsXlsx.Add(WordXml.Norm(descr));
                    }

                    if (Local(el.Name) == "decorative" && (string?)el.Attribute("val") == "1")
                    {
                        facts.Decorative = true;
                    }
                }
            }

            foreach (var name in names.Where(n => n.Contains("comments", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            {
                var root = Read(zip, name);
                foreach (var el in root?.Descendants() ?? Enumerable.Empty<XElement>())
                {
                    if (Local(el.Name) is "text" or "t")
                    {
                        var blob = TextOf(el);
                        if (blob.Length > 0)
                        {
                            facts.Comments.Add(new CommentFact { Text = blob });
                        }
                    }
                }
            }

            facts.CommentCount = facts.Comments.Count;
            facts.DocumentText = string.Join("\n", values.Where(v => v.Length > 0).Distinct());
            return facts;
        }
        catch
        {
            return new WordFacts { Ok = false, Error = "extract_failed" };
        }
    }

    static void SetCore(WordFacts facts, XElement? root, XName tag, string key)
    {
        var text = WordXml.Norm(root?.Descendants(tag).Select(el => el.Value).FirstOrDefault());
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
}
