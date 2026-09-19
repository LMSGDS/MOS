using System.Text.RegularExpressions;

namespace MosDock;

readonly record struct QNodeHit(
    string StepId,
    string SkillType,
    string Status,
    string Label,
    string Detail);

static class QMatrix
{
    public const string Nav = "Navigation";
    public const string Tool = "Tool_Usage";
    public const string Cfg = "Configuration";

    static readonly HashSet<string> NavActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "find", "find_navigate", "results_tab", "goto_page", "goto_bookmark",
        "goto_graphic", "goto", "search_query", "search_navigate",
    };

    public static string SkillLabel(string? skill)
    {
        var key = (skill ?? "").Replace(" ", "_");
        return key switch
        {
            Nav or "locate" => "Định vị",
            Tool or "ToolUsage" => "Công cụ",
            Cfg or "Execution" or "configure" => "Tham số",
            _ => string.IsNullOrWhiteSpace(skill) ? "Kỹ năng" : skill,
        };
    }

    public static IReadOnlyList<JsonQNode> NodesFor(JsonCriterion item)
    {
        if (item.QMatrixNodes is { Count: > 0 })
        {
            return item.QMatrixNodes;
        }

        var steps = item.HelpSteps ?? [];
        var pred = item.Predicate?.Type ?? "";
        return
        [
            new JsonQNode
            {
                StepId = "q1",
                SkillType = Nav,
                ValidationRule = "check_cursor_position",
                SuccessMessage = "Đã định vị đúng vị trí.",
                ErrorFeedback = NavFail(steps, item),
            },
            new JsonQNode
            {
                StepId = "q2",
                SkillType = Tool,
                ValidationRule = "check_tool(" + pred + ")",
                SuccessMessage = "Đã gọi đúng công cụ.",
                ErrorFeedback = ToolFail(steps, item),
            },
            new JsonQNode
            {
                StepId = "q3",
                SkillType = Cfg,
                ValidationRule = "check_config(" + pred + ")",
                SuccessMessage = "Đã cấu hình đúng tham số.",
                ErrorFeedback = CfgFail(steps, item),
            },
        ];
    }

    public static LocalCriterion Attach(
        WordFacts facts,
        JsonCriterion item,
        IReadOnlyList<ActionEvent>? evidence,
        LocalCriterion hit)
    {
        var nodes = Evaluate(facts, item, evidence, hit.Status);
        var first = nodes.FirstOrDefault(n => n.Status == "fail");
        if (hit.Status == "fail" && !string.IsNullOrWhiteSpace(first.Detail))
        {
            return hit with
            {
                Message = first.Detail,
                QTrace = nodes,
                BreakSkill = first.SkillType,
            };
        }

        return hit with { QTrace = nodes };
    }

    public static IReadOnlyList<QNodeHit> Evaluate(
        WordFacts facts,
        JsonCriterion item,
        IReadOnlyList<ActionEvent>? evidence,
        string endStatus)
    {
        var events = evidence ?? [];
        var spec = NodesFor(item);
        var outNodes = new List<QNodeHit>();
        var broken = false;
        foreach (var node in spec)
        {
            string status;
            string detail;
            if (endStatus == "error")
            {
                status = "skip";
                detail = First(node.ErrorFeedback, "Không đọc được tệp để chấm bước này.");
            }
            else if (endStatus == "unverified")
            {
                status = "unverified";
                detail = First(node.ErrorFeedback, "Chưa thu được bằng chứng thao tác.");
            }
            else if (endStatus == "pass")
            {
                status = "pass";
                detail = First(node.SuccessMessage, "Đạt.");
            }
            else if (broken)
            {
                status = "skip";
                detail = "Chưa tới bước này.";
            }
            else if (RuleOk(node, item, facts, events))
            {
                status = "pass";
                detail = First(node.SuccessMessage, "Đạt.");
            }
            else
            {
                status = "fail";
                detail = First(node.ErrorFeedback, CfgFail(item.HelpSteps, item));
                broken = true;
            }

            outNodes.Add(new QNodeHit(
                string.IsNullOrWhiteSpace(node.StepId) ? "q" + (outNodes.Count + 1) : node.StepId,
                string.IsNullOrWhiteSpace(node.SkillType) ? Cfg : node.SkillType,
                status,
                SkillLabel(node.SkillType),
                detail));
        }

        return outNodes;
    }

    static bool RuleOk(JsonQNode node, JsonCriterion item, WordFacts facts, IReadOnlyList<ActionEvent> events)
    {
        var rule = (node.ValidationRule ?? "").ToLowerInvariant();
        var skill = node.SkillType ?? "";
        if (rule.Contains("cursor") || rule.Contains("position") || skill == Nav)
        {
            return HasAnchor(facts, item, events);
        }

        if (rule.Contains("count_footnotes"))
        {
            return facts.FootnoteCount >= (item.Predicate.Min ?? 1);
        }

        if (rule.Contains("ribbon") || rule.Contains("tool") || skill == Tool)
        {
            return HasTool(facts, item, events);
        }

        return false;
    }

    static bool HasAnchor(WordFacts facts, JsonCriterion item, IReadOnlyList<ActionEvent> events)
    {
        if (events.Any(ev => NavActions.Contains(ev.Action ?? "")))
        {
            return true;
        }

        var pred = (item.Predicate.Type ?? "").ToLowerInvariant();
        var text = facts.DocumentText ?? "";
        if (pred is "header_contains" or "header_instruction" or "first_page_header" or "watermark_text")
        {
            return facts.HeaderTexts.Count > 0 || facts.HeaderInstructions.Count > 0 || facts.Watermarks.Count > 0 || text.Trim().Length > 0;
        }

        var style = item.Predicate.Style ?? "";
        if (pred == "paragraph_style" && style.StartsWith("TOC", StringComparison.OrdinalIgnoreCase))
        {
            return facts.Fields.Any(f => (f ?? "").Contains("TOC", StringComparison.OrdinalIgnoreCase))
                || facts.StyleCounts.Keys.Any(k => k.StartsWith("TOC", StringComparison.OrdinalIgnoreCase));
        }

        return text.Trim().Length > 0 || facts.Paragraphs.Count > 0 || facts.Ok;
    }

    static bool HasTool(WordFacts facts, JsonCriterion item, IReadOnlyList<ActionEvent> events)
    {
        var pred = (item.Predicate.Type ?? "").ToLowerInvariant();
        if (string.Equals(item.Kind, "action_sequence", StringComparison.OrdinalIgnoreCase))
        {
            return events.Count > 0;
        }

        if (pred == "footnote_min")
        {
            return facts.FootnoteCount >= 1;
        }

        if (pred == "field_contains")
        {
            return facts.Fields.Count > 0;
        }

        if (pred == "paragraph_style")
        {
            var style = item.Predicate.Style ?? "";
            if (style.StartsWith("TOC", StringComparison.OrdinalIgnoreCase))
            {
                return facts.StyleCounts.Keys.Any(k => k.StartsWith("TOC", StringComparison.OrdinalIgnoreCase))
                    || facts.Fields.Any(f => (f ?? "").Contains("TOC", StringComparison.OrdinalIgnoreCase));
            }

            return facts.StyleCounts.Keys.Any(k => k.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) || k == style)
                || facts.Paragraphs.Any(p => (p.Style ?? "").StartsWith("Heading", StringComparison.OrdinalIgnoreCase));
        }

        if (pred == "contains_text")
        {
            var needle = item.Predicate.Text ?? "";
            var blob = facts.DocumentText ?? "";
            var words = Regex.Matches(needle, @"\w+").Select(m => m.Value).Where(w => w.Length > 3).ToArray();
            return words.Length == 0
                ? blob.Trim().Length > 0
                : words.Any(w => blob.Contains(w, StringComparison.OrdinalIgnoreCase));
        }

        if (pred == "not_contains_text")
        {
            return true;
        }

        if (pred.StartsWith("table", StringComparison.Ordinal))
        {
            return facts.Tables.Count > 0;
        }

        return pred switch
        {
            "page_background" => !string.IsNullOrWhiteSpace(facts.PageBackground),
            "watermark_text" => facts.Watermarks.Count > 0,
            "page_border" => facts.PageBorder,
            "header_contains" or "header_instruction" or "first_page_header" =>
                facts.HeaderTexts.Count > 0 || facts.HeaderInstructions.Count > 0,
            "bookmark_range" => facts.Bookmarks.Count > 0,
            "internal_hyperlink" => facts.InternalHyperlinks.Count > 0 || facts.Fields.Count > 0,
            "comment_text" or "comment_author" or "comment_absent_text" or "comment_resolved" or "comment_reply"
                => facts.Comments.Count > 0 || facts.CommentCount > 0,
            "style_used" or "style_size" => facts.StyleCounts.Count > 0,
            "drawing_kind" or "drawing_text" or "artistic_effect" or "picture_effect" or "alt_text" or "wrap_type" or "hdphoto"
                => facts.DrawingKinds.Count > 0 || facts.DrawingTexts.Count > 0 || facts.AltTexts.Count > 0,
            "section_columns" or "page_orientation" or "break_present" or "section_count"
                => facts.Sections.Count > 0 || facts.Breaks.Count > 0,
            "list_format" => facts.NumberingFormats.Count > 0 || facts.Paragraphs.Any(p => !string.IsNullOrWhiteSpace(p.Fmt)),
            _ => false,
        };
    }

    static string NavFail(IReadOnlyList<string> steps, JsonCriterion item) =>
        steps.Count > 0
            ? "Bạn chưa chọn hoặc click đúng vị trí trước khi thao tác. " + Plain(steps[0])
            : "Bạn chưa đặt con trỏ chuột đúng vị trí cho: " + (item.Prompt ?? "nhiệm vụ này") + ".";

    static string ToolFail(IReadOnlyList<string> steps, JsonCriterion item) =>
        steps.Count >= 2
            ? "Bạn đang sử dụng sai công cụ. Thao tác chuẩn: " + Plain(steps[1])
            : "Sai công cụ. Hãy dùng đúng lệnh trên Ribbon cho: " + (item.Prompt ?? "nhiệm vụ này") + ".";

    static string CfgFail(IReadOnlyList<string> steps, JsonCriterion item)
    {
        var extra = steps.Count >= 3 ? Plain(steps[2]) : (item.Feedback?.Fail ?? "");
        return string.IsNullOrWhiteSpace(extra)
            ? "Bạn đã mở đúng công cụ nhưng chọn sai tham số."
            : "Bạn đã mở đúng công cụ nhưng chọn sai tham số hoặc chưa đủ yêu cầu. " + extra;
    }

    static string Plain(string text) => Regex.Replace(text ?? "", @"\*\*", "").Trim();

    static string First(string? a, string b) => string.IsNullOrWhiteSpace(a) ? b : a;
}
